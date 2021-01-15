#pragma once
using namespace System;

namespace FFmpegAudioPipeline
{
	public interface class ILogger
	{
		void Log(String^ message);
	};
}