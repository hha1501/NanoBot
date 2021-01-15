#pragma once
#include <gcroot.h>
#include <stdint.h>
#include <string>
#include <atomic>

#include <fmt/format.h>

#include "ILogger.h"

#define __STDC_CONSTANT_MACROS
extern "C"
{
#include <libavutil/frame.h>
#include <libavutil/mem.h>
#include <libavformat/avformat.h>
#include <libavcodec/avcodec.h>
#include <libavutil/avutil.h>
#include <libavfilter/avfilter.h>
#include <libswresample/swresample.h>
}



using namespace System::IO;

namespace FFmpegAudioPipeline
{
	private class FFmpegEngine
	{
	public:
		enum class State
		{
			Error = -1,
			Uninitialized = 0,
			Initialized = 1
		};

	public:
		FFmpegEngine(Stream^ inStream, Stream^ outStream);
		~FFmpegEngine();

		static void setGlobalLogger(ILogger^ logger);
		int init(AVCodecID outputCodec, int outputSampleRate, int outputChannelCount);
		int process();
		int stop();

	private:
		int initInternal(AVCodecID outputCodecID, int outputSampleRate, int outputChannelCount);
		int processInternal();

		int decode(AVCodecContext* pDecodeContext, AVPacket* pPacket, AVFrame* pFrame);
		int filter(AVFilterContext* pSourceContext, AVFilterContext* pSinkContext, AVFrame* pFrame);
		int encode(AVCodecContext* pEncodeContext, AVFrame* pFrame, AVPacket* pPacket);

		int setSampleFormatForEncoder(const AVCodec* pEncoder, AVCodecContext* pEncoderContext, AVSampleFormat suggestedSampleFormat);
		int setSampleRateForEncoder(const AVCodec* pEncoder, AVCodecContext* pEncoderContext, int suggestedSampleRate);
		int setChannelLayoutForEncoder(const AVCodec* pEncoder, AVCodecContext* pEncoderContext, uint64_t suggestedChannelLayout, int suggestedChannelCount);

		static int readPacketForwarder(void* opaque, uint8_t* buf, int buf_size);
		int readPacket(uint8_t* buf, int buf_size);
		void writeRawDataToOutput(uint8_t* buf, int buf_size);

		void log(const char* message);
		void log(const wchar_t* message);
		void log(const std::string& message);
		void log(const std::wstring& message);

		template <typename... Args>
		void log(const char* format, const Args& ... args)
		{
			std::string message = fmt::format(format, args...);
			log(message);
		}

		template <typename... Args>
		void log(const wchar_t* format, const Args& ... args)
		{
			std::wstring message = fmt::format(format, args...);
			log(message);
		}
		void logFormat(AVFormatContext* pFormatContext, int streamIndex);

	private:
		static gcroot<ILogger^> s_globalLogger;

		gcroot<Stream^> m_inStream;
		gcroot<Stream^> m_outStream;

	private:
		const int IOContextBufferSize = 4096;
		uint8_t* m_pIOContextBuffer;
		AVIOContext* m_pIOContext;

		AVFormatContext* m_pFormatContext;

		AVCodec* m_pInputAudioCodec;
		AVCodecContext* m_pDecodeContext;
		int m_audioStreamIndex;

		AVCodec* m_pOutputAudioCodec;
		AVCodecContext* m_pEncodeContext;

		AVPacket* m_pPacket;
		AVFrame* m_pFrame;

		AVFilterGraph* m_pFilterGraph;
		AVFilterContext* m_pSourceFilterContext;
		AVFilterContext* m_pVolumeFilterContext;
		AVFilterContext* m_pSinkFilterContext;

	private:
		State m_State;
		bool m_Running;
	};
}
