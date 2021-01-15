using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using NAudio;
using NAudio.Wave;

using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;

using FFmpegAudioPipeline;
using System.Threading;

namespace AudioTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string url = "https://www.youtube.com/watch?v=hyCK8QBOh_I";
            string outputFileName = @"C:\Users\ErikHa\Desktop\d.opusraw";
            bool outputToFile = true;

            if (args.Length >= 1)
            {
                url = args[0];
            }
            if (args.Length >= 2)
            {
                string output = args[1];
                if (output == "-")
                {
                    outputToFile = false;
                }
                else
                {
                    outputFileName = output;
                }
            }

            using (Stream mediaStream = await GetInputStream(url, @"C:\Users\ErikHa\Desktop\Test\Tools\strangers again.webm", usingMock: true))
            {
                Stream outStream = null;
                if (outputToFile)
                {
                    outStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write);
                }
                else
                {
                    outStream = Console.OpenStandardOutput();
                }
                using (outStream)
                {
                    AudioPipeline.SetGlobalLogger(new Logger());
                    using (AudioPipeline audioPipeline = new AudioPipeline(mediaStream, outStream))
                    {
                        int result = audioPipeline.Init();
                        audioPipeline.Process(default(CancellationToken));
                    }
                }
            }

            Console.Error.WriteLine("Done");
        }

        static async Task<Stream> GetInputStream(string url, string mockUrl, bool usingMock)
        {
            if (usingMock)
            {
                return new FileStream(mockUrl, FileMode.Open, FileAccess.Read);
            }
            else
            {
                string videoID = YoutubeClient.ParseVideoId(url);

                YoutubeClient youtubeClient = new YoutubeClient();
                MediaStreamInfoSet streamInfoSet = await youtubeClient.GetVideoMediaStreamInfosAsync(videoID);
                AudioStreamInfo audioStreamInfo = streamInfoSet.Audio.WithHighestBitrate();
                return await youtubeClient.GetMediaStreamAsync(audioStreamInfo);
            }
        }
    }

    class Logger : ILogger
    {
        public void Log(string message)
        {
            Console.Error.Write(message);
        }
    }
}
