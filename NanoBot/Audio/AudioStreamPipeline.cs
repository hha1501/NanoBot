using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NAudio;
using NAudio.Wave;

namespace NanoBot.Audio
{
    class AudioStreamPipeline
    {
        public float Volume
        {
            get
            {
                return volumeWaveProvider.Volume;
            }
            set
            {
                volumeWaveProvider.Volume = value;
            }
        }

        private readonly IWaveProvider inputWaveProvider;
        private readonly WaveFormat inputWaveFormat;

        private VolumeWaveProvider16 volumeWaveProvider;
        private IWaveProvider outputWaveProvider;


        public AudioStreamPipeline(IWaveProvider inputWaveProvider)
        {
            this.inputWaveProvider = inputWaveProvider;
            inputWaveFormat = inputWaveProvider.WaveFormat;
            volumeWaveProvider = new VolumeWaveProvider16(inputWaveProvider);
            outputWaveProvider = volumeWaveProvider;
        }

        public AudioStreamPipeline(Stream inputStream, WaveFormat waveFormat) : this(new RawSourceWaveStream(inputStream, waveFormat))
        {
        }

        public async Task CopyToStreamAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[3840];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int read = outputWaveProvider.Read(buffer, 0, buffer.Length);

                if (read > 0)
                {
                    await stream.WriteAsync(buffer, 0, read);
                }
                else
                {
                    break;
                }
            }
        }
    }
}
