using System;
using System.Collections.Generic;
using System.Text;
using System2.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

using Discord;
using Discord.Audio;
using Discord.WebSocket;

using NAudio.Wave;

using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;

using FFmpegAudioPipeline;

namespace NanoBot.Audio
{
    class AudioManager
    {
        class AudioPipelineLogger : ILogger
        {
            public void Log(string message)
            {
                Console.WriteLine(message);
            }
        }

        static AudioManager()
        {
            AudioPipeline.SetGlobalLogger(new AudioPipelineLogger());
        }

        public SocketGuild Guild { get; }
        public IVoiceChannel VoiceChannel => Guild.CurrentUser.VoiceChannel;
        public IAudioClient AudioClient => Guild.AudioClient;

        public bool IsRunning => runningTask != null;

        public float Volume
        {
            get
            {
                return volume;
            }
            set
            {
                volume = value;
                if (audioStreamPipeline != null)
                {
                    audioStreamPipeline.Volume = value;
                }
            }
        }
        private float volume;

        private AudioStreamPipeline audioStreamPipeline;

        private Task runningTask;
        private CancellationTokenSource cancellationTokenSource;


        public AudioManager(SocketGuild guild)
        {
            Guild = guild;
            volume = 1.0f;
        }

        public async Task ConnectAsync(IVoiceChannel voiceChannel)
        {
            if (AudioClient == null || AudioClient.ConnectionState == ConnectionState.Disconnected)
            {
                await voiceChannel.ConnectAsync();
            }
        }

        public async Task DisconnectAsync()
        {
            if (AudioClient != null)
            {
                await StopRunningTaskAsync();
                await VoiceChannel.DisconnectAsync();
            }
        }

        public Task StopAsync()
        {
            return StopRunningTaskAsync();
        }

        public async Task PlayAudioClip(string fileName)
        {
            if (AudioClient == null)
            {
                return;
            }

            using (AudioOutStream outStream = AudioClient.CreatePCMStream(AudioApplication.Mixed))
            {
                WaveFileReader waveFileReader = new WaveFileReader($@"C:\Users\ErikHa\Desktop\Audio clips\{fileName}.wav");

                IWaveProvider waveProvider = waveFileReader;

                if (waveProvider.WaveFormat.SampleRate != 48000)
                {
                    WaveFormat targetFormat = new WaveFormat(48000, waveFileReader.WaveFormat.Channels);
                    waveProvider = new WaveFormatConversionProvider(targetFormat, waveProvider);
                }
                if (waveProvider.WaveFormat.Channels != 2)
                {
                    waveProvider = new MonoToStereoProvider16(waveProvider);
                }

                WaveProviderPlayer waveProviderPlayer = new WaveProviderPlayer(waveProvider);
                await waveProviderPlayer.PlayToStreamAsync(outStream);
            }
        }

        public Task StreamAudio(string url)
        {
            cancellationTokenSource = new CancellationTokenSource();
            runningTask = Task.Run(() => StreamAudioAsyncInternal(url, cancellationTokenSource.Token));

            return Task.CompletedTask;
        }


        private async Task StreamAudioAsyncInternal(string url, CancellationToken cancellationToken)
        {
            if (AudioClient == null)
            {
                return;
            }

            #region Old implementation
            //using (AudioOutStream outStream = AudioClient.CreatePCMStream(AudioApplication.Music))
            //{
            //    ProcessStartInfo processStartInfo = new ProcessStartInfo
            //    {
            //        FileName = "cmd",
            //        Arguments = $"/C \"youtube-dl \"{url}\" --format \"bestaudio\" --no-playlist  -o - | ffmpeg -i - -f s16le -ac 2 -ar 48000 -hide_banner -\"",
            //        UseShellExecute = false,
            //        CreateNewConsole = true,
            //        RedirectStandardOutput = true,
            //        InheritStandardInput = false,
            //        InheritStandardError = false
            //    };

            //    using (Process process = Process.Start(processStartInfo))
            //    {
            //        audioStreamPipeline = new AudioStreamPipeline(process.StandardOutput.BaseStream, new WaveFormat(48000, 2));
            //        audioStreamPipeline.Volume = volume;
            //        try
            //        {
            //            await audioStreamPipeline.CopyToStreamAsync(outStream, cancellationToken);
            //        }
            //        catch (OperationCanceledException)
            //        {
            //        }
            //        finally
            //        {
            //            process.StandardOutput.Close();
            //            await outStream.FlushAsync();

            //            if (!process.WaitForExit(1000))
            //            {
            //                ProcessStartInfo stopProcessStartInfo = new ProcessStartInfo
            //                {
            //                    FileName = "SendSignal.exe",
            //                    Arguments = $"{process.Id}",
            //                    CreateNoWindow = false
            //                };

            //                Process processStop = Process.Start(stopProcessStartInfo);

            //                process.WaitForExit(5000);
            //                if (!process.HasExited)
            //                {
            //                    process.Kill(true);
            //                }
            //            }
            //        }
            //    }
            //}
            #endregion
            #region New implementation
            string videoID = YoutubeClient.ParseVideoId(url);

            YoutubeClient youtubeClient = new YoutubeClient();
            MediaStreamInfoSet streamInfoSet = await youtubeClient.GetVideoMediaStreamInfosAsync(videoID);
            AudioStreamInfo audioStreamInfo = streamInfoSet.Audio.WithHighestBitrate();

            using (MediaStream mediaStream = await youtubeClient.GetMediaStreamAsync(audioStreamInfo))
            {
                using (AudioOutStream outStream = AudioClient.CreateOpusStream())
                {
                    using (AudioPipeline audioPipeline = new AudioPipeline(mediaStream, outStream))
                    {
                        audioPipeline.Init();
                        audioPipeline.Process(cancellationToken);
                    }
                }
            }
            #endregion
            //using (System.IO.FileStream fileStream = new System.IO.FileStream(@"C:\Users\ErikHa\Desktop\Test\Tools\a.raw", System.IO.FileMode.Open))
            //{
            //    using (AudioOutStream outStream = AudioClient.CreatePCMStream(AudioApplication.Music))
            //    {
            //        fileStream.CopyTo(outStream);
            //    }
            //}

            Console.WriteLine("Done streaming audio.");
        }

        private async Task StopRunningTaskAsync()
        {
            if (runningTask != null)
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();

                await runningTask;

                cancellationTokenSource = null;
                audioStreamPipeline = null;
                runningTask = null;
            }
        }

    }
}
