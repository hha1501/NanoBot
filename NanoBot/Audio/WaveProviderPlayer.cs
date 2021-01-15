using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;

namespace NanoBot.Audio
{
    class WaveProviderPlayer
    {
        private readonly IWaveProvider source;

        public WaveProviderPlayer(IWaveProvider source)
        {
            this.source = source;
        }

        public async Task PlayToStreamAsync(Stream stream)
        {
            byte[] buffer = new byte[1920];
            int read = 0;

            while (true)
            {
                read = source.Read(buffer, 0, buffer.Length);

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
