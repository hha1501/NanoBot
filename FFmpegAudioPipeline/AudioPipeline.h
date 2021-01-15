#pragma once
#include "FFmpegEngine.h"

using namespace System::Threading;
using namespace System::IO;

namespace FFmpegAudioPipeline
{
	public ref class AudioPipeline
	{
	public:
		AudioPipeline(Stream^ inStream, Stream^ outStream)
		{
			pFFmpegEngine = new FFmpegEngine(inStream, outStream);
		}
		~AudioPipeline()
		{
			delete pFFmpegEngine;
		}
	public:
		static void SetGlobalLogger(ILogger^ logger);
		int Init();
		int Process(CancellationToken cancellationToken);

	private:
		void Stop();

	private:
		FFmpegEngine* pFFmpegEngine;
	};
}