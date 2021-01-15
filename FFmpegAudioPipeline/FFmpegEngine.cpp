#include "FFmpegEngine.h"
extern "C"
{
#include <libavutil/opt.h>
#include <libavfilter/buffersrc.h>
#include <libavfilter/buffersink.h>
}

using namespace System;
using namespace System::Buffers;
using namespace System::Runtime::InteropServices;

namespace FFmpegAudioPipeline
{
    gcroot<ILogger^> FFmpegEngine::s_globalLogger;

    FFmpegEngine::FFmpegEngine(Stream^ inStream, Stream^ outStream) : m_inStream(inStream), m_outStream(outStream),
        m_pIOContextBuffer(nullptr), m_pIOContext(nullptr), m_pFormatContext(nullptr),
        m_pInputAudioCodec(nullptr), m_pDecodeContext(nullptr), m_pOutputAudioCodec(nullptr), m_pEncodeContext(nullptr),
        m_pPacket(nullptr), m_pFrame(nullptr),
        m_pFilterGraph(nullptr), m_pSourceFilterContext(nullptr), m_pVolumeFilterContext(nullptr), m_pSinkFilterContext(nullptr)
    {
        m_State = State::Uninitialized;
        m_Running = false;
    }

    FFmpegEngine::~FFmpegEngine()
    {
        avfilter_graph_free(&m_pFilterGraph);
        av_frame_free(&m_pFrame);
        av_packet_free(&m_pPacket);
        avcodec_free_context(&m_pEncodeContext);
        avcodec_free_context(&m_pDecodeContext);
        avformat_close_input(&m_pFormatContext);
        if (m_pIOContext != nullptr)
        {
            av_freep(&m_pIOContext->buffer);
        }
        avio_context_free(&m_pIOContext);

        m_pSourceFilterContext = nullptr;
        m_pVolumeFilterContext = nullptr;
        m_pSinkFilterContext = nullptr;
    }

    void FFmpegEngine::setGlobalLogger(ILogger^ logger)
    {
        s_globalLogger = logger;
    }

    int FFmpegEngine::init(AVCodecID outputCodec, int outputSampleRate, int outputChannelCount)
    {
        if (m_State != State::Uninitialized)
        {
            return -1;
        }

        int result = initInternal(outputCodec, outputSampleRate, outputChannelCount);
        if (result < 0)
        {
            m_State = State::Error;
        }
        else
        {
            m_State = State::Initialized;
        }

        return result;
    }

    int FFmpegEngine::initInternal(AVCodecID outputCodecID, int outputSampleRate, int outputChannelCount)
    {
        int result;

        // IO context.
        m_pIOContextBuffer = (uint8_t*)av_malloc(IOContextBufferSize);
        if (m_pIOContextBuffer == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }

        m_pIOContext = avio_alloc_context(m_pIOContextBuffer, IOContextBufferSize, 0, this, &readPacketForwarder, NULL, NULL);
        if (m_pIOContext == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }

        // Format context.
        m_pFormatContext = avformat_alloc_context();
        if (m_pFormatContext == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }
        m_pFormatContext->pb = m_pIOContext;

        result = avformat_open_input(&m_pFormatContext, NULL, NULL, NULL);
        if (result < 0)
        {
            // Error opening input.
            return result;
        }

        result = avformat_find_stream_info(m_pFormatContext, NULL);
        if (result < 0)
        {
            // Error finding stream info.
            return result;
        }

        m_audioStreamIndex = av_find_best_stream(m_pFormatContext, AVMediaType::AVMEDIA_TYPE_AUDIO, -1, -1, &m_pInputAudioCodec, 0);
        if (m_audioStreamIndex < 0)
        {
            // No audio stream found.
            return -1;
        }

        logFormat(m_pFormatContext, m_audioStreamIndex);

        // Decode context.
        m_pDecodeContext = avcodec_alloc_context3(m_pInputAudioCodec);
        if (m_pDecodeContext == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }

        result = avcodec_parameters_to_context(m_pDecodeContext, m_pFormatContext->streams[m_audioStreamIndex]->codecpar);
        if (result < 0)
        {
            // Error copying parameters to codec context.
            return result;
        }

        result = avcodec_open2(m_pDecodeContext, m_pInputAudioCodec, NULL);
        if (result < 0)
        {
            // Error opening codec context.
            return result;
        }

        // Packet.
        m_pPacket = av_packet_alloc();
        if (m_pPacket == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }

        // Frame.
        m_pFrame = av_frame_alloc();
        if (m_pFrame == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }

        // Encode context.
        m_pOutputAudioCodec = avcodec_find_encoder(outputCodecID);
        if (m_pOutputAudioCodec == nullptr)
        {
            // Cannot find codec.
            return -1;
        }
        m_pEncodeContext = avcodec_alloc_context3(m_pOutputAudioCodec);
        if (m_pEncodeContext == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }
        // Set encoding parameters.
        if (setChannelLayoutForEncoder(m_pOutputAudioCodec, m_pEncodeContext, av_get_default_channel_layout(outputChannelCount), outputChannelCount) < 0)
        {
            // Invalid channel layout.
            return -1;
        }
        if (setSampleFormatForEncoder(m_pOutputAudioCodec, m_pEncodeContext, m_pDecodeContext->sample_fmt) < 0)
        {
            // Invalid sample format.
            return -1;
        }
        if (setSampleRateForEncoder(m_pOutputAudioCodec, m_pEncodeContext, outputSampleRate) < 0)
        {
            // Invalid sample rate.
            return -1;
        }

        result = avcodec_open2(m_pEncodeContext, m_pOutputAudioCodec, NULL);
        if (result < 0)
        {
            // Error opening encoder context.
            return result;
        }

        // Filter graph.
        m_pFilterGraph = avfilter_graph_alloc();
        if (m_pFilterGraph == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }

        // Buffer filter (source).
        const AVFilter* pSourceFilter = avfilter_get_by_name("abuffer");
        if (pSourceFilter == nullptr)
        {
            // Buffer filter not found.
            return -1;
        }

        m_pSourceFilterContext = avfilter_graph_alloc_filter(m_pFilterGraph, pSourceFilter, "source");
        if (m_pSourceFilterContext == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }

        char graphChannelLayoutString[64];
        av_get_channel_layout_string(graphChannelLayoutString, sizeof(graphChannelLayoutString), 0, m_pDecodeContext->channel_layout);
        av_opt_set(m_pSourceFilterContext, "channel_layout", graphChannelLayoutString, AV_OPT_SEARCH_CHILDREN);
        av_opt_set_sample_fmt(m_pSourceFilterContext, "sample_fmt", m_pDecodeContext->sample_fmt, AV_OPT_SEARCH_CHILDREN);
        av_opt_set_q(m_pSourceFilterContext, "time_base", m_pFormatContext->streams[m_audioStreamIndex]->time_base, AV_OPT_SEARCH_CHILDREN);
        av_opt_set_int(m_pSourceFilterContext, "sample_rate", m_pDecodeContext->sample_rate, AV_OPT_SEARCH_CHILDREN);

        result = avfilter_init_str(m_pSourceFilterContext, NULL);
        if (result < 0)
        {
            // Error initializing abuffer filter.
            return result;
        }

        // Volume filter.
        const AVFilter* pVolumeFilter = avfilter_get_by_name("volume");
        if (pVolumeFilter == nullptr)
        {
            // Buffer filter not found.
            return -1;
        }

        m_pVolumeFilterContext = avfilter_graph_alloc_filter(m_pFilterGraph, pVolumeFilter, "volume");
        if (m_pVolumeFilterContext == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }

        av_opt_set(m_pVolumeFilterContext, "volume", "1.0", AV_OPT_SEARCH_CHILDREN);

        result = avfilter_init_str(m_pVolumeFilterContext, NULL);
        if (result < 0)
        {
            // Error initializing volume filter.
            return result;
        }

        // Audio format filter.
        const AVFilter* pFormatFilter = avfilter_get_by_name("aformat");
        if (pFormatFilter == nullptr)
        {
            // Format filter not found.
            return -1;
        }

        AVFilterContext* pFormatFilterContext = avfilter_graph_alloc_filter(m_pFilterGraph, pFormatFilter, "format");
        if (pFormatFilterContext == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }

        std::string sampleRatesString = fmt::to_string(m_pEncodeContext->sample_rate);
        av_opt_set(pFormatFilterContext, "sample_rates", sampleRatesString.c_str(), AV_OPT_SEARCH_CHILDREN);
        av_opt_set(pFormatFilterContext, "sample_fmts", av_get_sample_fmt_name(m_pEncodeContext->sample_fmt), AV_OPT_SEARCH_CHILDREN);
        av_opt_set(pFormatFilterContext, "channel_layouts", graphChannelLayoutString, AV_OPT_SEARCH_CHILDREN);

        result = avfilter_init_str(pFormatFilterContext, NULL);
        if (result < 0)
        {
            // Error initializing volume filter.
            return result;
        }

        // Buffer filter (sink).
        const AVFilter* pSinkFilter = avfilter_get_by_name("abuffersink");
        if (pSinkFilter == nullptr)
        {
            // Buffer filter not found.
            return -1;
        }

        m_pSinkFilterContext = avfilter_graph_alloc_filter(m_pFilterGraph, pSinkFilter, "sink");
        if (m_pSinkFilterContext == nullptr)
        {
            // No memory.
            return AVERROR(ENOMEM);
        }

        result = avfilter_init_str(m_pSinkFilterContext, NULL);
        if (result < 0)
        {
            // Error initializing abuffersink filter.
            return result;
        }

        // Link filters.
        result = avfilter_link(m_pSourceFilterContext, 0, m_pVolumeFilterContext, 0);
        if (result < 0)
        {
            // Error linking filters.
            return result;
        }
        result = avfilter_link(m_pVolumeFilterContext, 0, pFormatFilterContext, 0);
        if (result < 0)
        {
            // Error linking filters.
            return result;
        }
        result = avfilter_link(pFormatFilterContext, 0, m_pSinkFilterContext, 0);
        if (result < 0)
        {
            // Error linking filters.
            return result;
        }

        // Configure graph.
        result = avfilter_graph_config(m_pFilterGraph, NULL);
        if (result < 0)
        {
            // Error configuring filter graph.
            return result;
        }
        char* filterGraphStringDump = avfilter_graph_dump(m_pFilterGraph, NULL);
        log(filterGraphStringDump);
        av_freep(&filterGraphStringDump);

        return 0;
    }

    int FFmpegEngine::process()
    {
        if (m_State != State::Initialized)
        {
            return -1;
        }

        m_Running = true;
        int result = processInternal();
        if (result < 0)
        {
            m_State = State::Error;
        }
        else
        {
            log(L"Done processing audio.\n");
        }

        return result;
    }

    int FFmpegEngine::stop()
    {
        m_Running = false;
        return 0;
    }

    int FFmpegEngine::processInternal()
    {
        int result;

        while (m_Running && av_read_frame(m_pFormatContext, m_pPacket) >= 0)
        {
            result = decode(m_pDecodeContext, m_pPacket, m_pFrame);
            if (result < 0)
            {
                return result;
            }
        }

        // Flush decoder.
        result = decode(m_pDecodeContext, NULL, m_pFrame);

        return result;
    }

    int FFmpegEngine::decode(AVCodecContext* pDecodeContext, AVPacket* pPacket, AVFrame* pFrame)
    {
        int result;

        result = avcodec_send_packet(pDecodeContext, pPacket);
        if (pPacket != nullptr)
        {
            av_packet_unref(pPacket);
        }

        if (result < 0)
        {
            // Error sending packet to decoder.
            return result;
        }

        while (true)
        {
            result = avcodec_receive_frame(pDecodeContext, pFrame);
            if (result == 0)
            {
                // Filtering process consumes the frame so no need to unref after calling.
                result = filter(m_pSourceFilterContext, m_pSinkFilterContext, pFrame);
                if (result < 0)
                {
                    return result;
                }
            }
            else if (result == AVERROR_EOF)
            {
                // Flush filter graph.
                return filter(m_pSourceFilterContext, m_pSinkFilterContext, NULL);
            }
            else if (result == AVERROR(EAGAIN))
            {
                return 0;
            }
            else if (result < 0)
            {
                // Error decoding.
                return result;
            }
        }
    }

    int FFmpegEngine::filter(AVFilterContext* pSourceContext, AVFilterContext* pSinkContext, AVFrame* pFrame)
    {
        int result;

        result = av_buffersrc_add_frame(pSourceContext, pFrame);
        if (result < 0)
        {
            if (pFrame != nullptr)
            {
                av_frame_unref(pFrame);
            }
            return result;
        }

        while (true)
        {
            result = av_buffersink_get_frame(pSinkContext, pFrame);
            if (result == 0)
            {
                // Send frame to encoder.
                result = encode(m_pEncodeContext, pFrame, m_pPacket);
                if (result < 0)
                {
                    return result;
                }
            }
            else if (result == AVERROR_EOF)
            {
                // Flush encoder.
                return encode(m_pEncodeContext, NULL, m_pPacket);
            }
            else if (result == AVERROR(EAGAIN))
            {
                return 0;
            }
            else if (result < 0)
            {
                // Error decoding.
                return result;
            }
        }
    }

    int FFmpegEngine::encode(AVCodecContext* pEncodeContext, AVFrame* pFrame, AVPacket* pPacket)
    {
        int result;

        result = avcodec_send_frame(pEncodeContext, pFrame);
        if (pFrame != nullptr)
        {
            av_frame_unref(pFrame);
        }

        if (result < 0)
        {
            // Error sending frame to decoder.
            return result;
        }

        while (true)
        {
            result = avcodec_receive_packet(pEncodeContext, pPacket);
            if (result == 0)
            {
                writeRawDataToOutput(pPacket->data, pPacket->size);
                av_packet_unref(pPacket);
            }
            else if (result == AVERROR(EAGAIN) || result == AVERROR_EOF)
            {
                return 0;
            }
            else if (result < 0)
            {
                // Error decoding.
                return result;
            }
        }
    }

    int FFmpegEngine::setSampleFormatForEncoder(const AVCodec* pEncoder, AVCodecContext* pEncoderContext, AVSampleFormat suggestedSampleFormat)
    {
        suggestedSampleFormat = av_get_packed_sample_fmt(suggestedSampleFormat);
        AVSampleFormat validSampleFormat = AV_SAMPLE_FMT_NONE;

        const AVSampleFormat* p = pEncoder->sample_fmts;
        if (p != nullptr)
        {
            while (true)
            {	
                if (*p!=AV_SAMPLE_FMT_NONE)
                {
                    log("{0}\n", av_get_sample_fmt_name(*p));
                }
                if (*p == suggestedSampleFormat)
                {
                    break;
                }
                else if (*p == AV_SAMPLE_FMT_NONE)
                {
                    if (validSampleFormat != AV_SAMPLE_FMT_NONE)
                    {
                        suggestedSampleFormat = validSampleFormat;
                        break;
                    }
                    else
                    {
                        return -1;
                    }
                }
                else
                {
                    validSampleFormat = *p;
                }
                p++;
            }
        }

        pEncoderContext->sample_fmt = suggestedSampleFormat;
        return 0;
    }
    int FFmpegEngine::setSampleRateForEncoder(const AVCodec* pEncoder, AVCodecContext* pEncoderContext, int suggestedSampleRate)
    {
        const int* p = pEncoder->supported_samplerates;
        if (p != nullptr)
        {
            while (true)
            {
                if (*p == suggestedSampleRate)
                {
                    break;
                }
                else if (*p == 0)
                {
                    return -1;
                }
                p++;
            }
        }

        pEncoderContext->sample_rate = suggestedSampleRate;
        return 0;
    }
    int FFmpegEngine::setChannelLayoutForEncoder(const AVCodec* pEncoder, AVCodecContext* pEncoderContext, uint64_t suggestedChannelLayout, int suggestedChannelCount)
    {
        const uint64_t* p = pEncoder->channel_layouts;
        if (p != nullptr)
        {
            while (true)
            {
                if (*p == suggestedChannelLayout)
                {
                    break;
                }
                else if (*p == 0)
                {
                    return -1;
                }
                p++;
            }
        }

        pEncoderContext->channel_layout = suggestedChannelLayout;
        pEncoderContext->channels = suggestedChannelCount;
        return 0;
    }

    int FFmpegEngine::readPacketForwarder(void* opaque, uint8_t* buf, int buf_size)
    {
        return static_cast<FFmpegEngine*>(opaque)->readPacket(buf, buf_size);
    }
    int FFmpegEngine::readPacket(uint8_t* buf, int buf_size)
    {
        int result = AVERROR_EOF;
        array<Byte>^ buffer = ArrayPool<Byte>::Shared->Rent(buf_size);

        try
        {
            int read = m_inStream->Read(buffer, 0, buf_size);
            Marshal::Copy(buffer, 0, IntPtr(buf), read);

            if (read > 0)
            {
                result = read;
            }
        }
        finally
        {
            ArrayPool<Byte>::Shared->Return(buffer, false);
        }

        return result;
    }

    void FFmpegEngine::writeRawDataToOutput(uint8_t* buf, int buf_size)
    {
        array<Byte>^ buffer = ArrayPool<Byte>::Shared->Rent(buf_size);

        try
        {
            Marshal::Copy(IntPtr(buf), buffer, 0, buf_size);
            m_outStream->Write(buffer, 0, buf_size);
        }
        finally
        {
            ArrayPool<Byte>::Shared->Return(buffer, false);
        }
    }

    void FFmpegEngine::log(const char* message)
    {
        if (s_globalLogger.operator->() == nullptr)
        {
            return;
        }
        String^ string = gcnew String(message);
        s_globalLogger->Log(string);
    }
    void FFmpegEngine::log(const std::string& message)
    {
        if (s_globalLogger.operator->() == nullptr)
        {
            return;
        }
        String^ string = gcnew String(message.c_str());
        s_globalLogger->Log(string);
    }
    void FFmpegEngine::log(const wchar_t* message)
    {
        if (s_globalLogger.operator->() == nullptr)
        {
            return;
        }
        String^ string = gcnew String(message);
        s_globalLogger->Log(string);
    }
    void FFmpegEngine::log(const std::wstring& message)
    {
        if (s_globalLogger.operator->() == nullptr)
        {
            return;
        }
        String^ string = gcnew String(message.c_str());
        s_globalLogger->Log(string);
    }

    void FFmpegEngine::logFormat(AVFormatContext* pFormatContext, int streamIndex)
    {
        AVCodecParameters* pCodecParams = pFormatContext->streams[streamIndex]->codecpar;

        log("Container: {0}, Codec: {1} ({2} Hz, {3} channels)\n", pFormatContext->iformat->name,
            avcodec_get_name(pCodecParams->codec_id),
            pCodecParams->sample_rate,
            pCodecParams->channels);
    }

    // TODO:
    //
    // Implement processing on demand? Expose methods for reading into a byte buffer.
    //
    // Extend logging to support multiple verbosities.
    //
    // Refactor error checking into an ensure function.
    //
    // TODO:
    //
    //
    // CancellationToken support. std::atomic_bool maybe ?
    // Provide mechanism to wait on process() stop.
    // 
    // Consider buffering input/output.
    //
    // TODO:
    // With the arrival of .NET 5, consider:
    // 1. Refactor ffmpeg pipeline to ultilize filter graph more.
    // 2. C++/CLI stack should be replaced with normal pinvoke stack.
    // 3. Experiment with .NET pipeline: 
    //    3.1. The producer should constantly read from input stream (network stream/audio clip stream, ...),
    //         call into native audio pipeline and then write raw audio frames (pcm, opus, ...) into the pipeline.
    //    3.2. The consumer could push all data into discord output stream.
    //    
    //    3.3. Another approach for using .NET pipeline is reimplement Discord.Audio, replacing internal message queue with producer/consumer pattern.
    //         This has several benefits:  + remove dependency on libopus
    //                                     + (hopefully) simplify existing message queueing mechanism (with .NET pipeline backpressure)
    //                          
    //         but with huge cost       :  + Need a deep understand of discord voice api (packet structure, heartbeat)
    //                                  :  + Thread safety, task synchronization are hard
    //
    //

}