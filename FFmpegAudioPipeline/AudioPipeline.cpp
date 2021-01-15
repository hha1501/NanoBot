#include "AudioPipeline.h"

namespace FFmpegAudioPipeline
{
	void AudioPipeline::SetGlobalLogger(ILogger^ logger)
	{
		FFmpegEngine::setGlobalLogger(logger);
	}

	int AudioPipeline::Init()
	{
		return pFFmpegEngine->init(AVCodecID::AV_CODEC_ID_OPUS, 48000, 2);
	}

	int AudioPipeline::Process(CancellationToken cancellationToken)
	{
		cancellationToken.Register(gcnew System::Action(this, &AudioPipeline::Stop));
		return pFFmpegEngine->process();
	}

	void AudioPipeline::Stop()
	{
		pFFmpegEngine->stop();
	}
}