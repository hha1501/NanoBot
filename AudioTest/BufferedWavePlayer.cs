using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using NAudio;
using NAudio.Wave;

namespace AudioTest
{
    enum StreamingPlaybackState
    {
        Stopped,
        Playing,
        Buffering,
        Paused
    }

    class BufferedWavePlayer
    {
        public Stream BaseStream { get; private set; }

        private BufferedWaveProvider bufferedWaveProvider;

        private byte[] buffer;

        private StreamingPlaybackState playbackState;
        private bool endOfStream;

        private WaveOutEvent waveOutEvent;

        public BufferedWavePlayer(Stream stream)
        {
            BaseStream = stream;
            buffer = new byte[4096];

            bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(48000, 2));
            bufferedWaveProvider.BufferDuration = TimeSpan.FromSeconds(20);

            waveOutEvent = new WaveOutEvent();
            waveOutEvent.Init(bufferedWaveProvider);

            playbackState = StreamingPlaybackState.Stopped;
            endOfStream = false;
        }

        public async Task PlayAsync()
        {
            playbackState = StreamingPlaybackState.Buffering;

            while (true)
            {
                if (IsBufferNearlyFull())
                {
                    await Task.Delay(500);
                }
                else
                {
                    if (!endOfStream)
                    {
                        int read = await BaseStream.ReadAsync(buffer, 0, buffer.Length);

                        if (read == 0)
                        {
                            endOfStream = true;
                        }
                        else
                        {
                            bufferedWaveProvider.AddSamples(buffer, 0, read);

                            double bufferedSeconds = bufferedWaveProvider.BufferedDuration.TotalSeconds;

                            if (bufferedSeconds < 0.5 && playbackState == StreamingPlaybackState.Playing)
                            {
                                PausePlayback();
                            }
                            else if (bufferedSeconds > 4 && playbackState == StreamingPlaybackState.Buffering)
                            {
                                ResumePlayback();
                            }
                        }
                    }
                    else
                    {
                        if (waveOutEvent.PlaybackState != PlaybackState.Stopped)
                        {
                            break;
                        }
                    }
                }
            }
        }

        public void Play()
        {
            playbackState = StreamingPlaybackState.Buffering;
        }

        public void Pause()
        {
            waveOutEvent.Pause();
            playbackState = StreamingPlaybackState.Paused;
        }

        public void Stop()
        {
            waveOutEvent.Stop();
            endOfStream = true;
        }

        private void ResumePlayback()
        {
            waveOutEvent.Play();
            playbackState = StreamingPlaybackState.Playing;
        }

        private void PausePlayback()
        {
            waveOutEvent.Pause();
            playbackState = StreamingPlaybackState.Buffering;
        }



        private bool IsBufferNearlyFull()
        {
            return (bufferedWaveProvider.BufferLength - bufferedWaveProvider.BufferedBytes) < bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;
        }
    }
}
