using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using NAudio;
using NAudio.Wave;

namespace AudioTest
{
    class AudioManager
    {
        private WaveInEvent waveIn;
        private WaveFileWriter waveFileWriter;

        public AudioManager()
        {
            waveIn = new WaveInEvent();
            waveIn.DeviceNumber = 0;
            waveIn.DataAvailable += WaveInDataAvailable;
            waveIn.RecordingStopped += WaveInRecordingStopped;

            waveIn.WaveFormat = new WaveFormat();

            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "recorded.wav");
            waveFileWriter = new WaveFileWriter(filePath, waveIn.WaveFormat);
        }

        public void Start()
        {
            waveIn.StartRecording();
            
        }

        public void Stop()
        {
            waveIn.StopRecording();
        }

        private void WaveInDataAvailable(object sender, WaveInEventArgs e)
        {
            waveFileWriter.Write(e.Buffer, 0, e.BytesRecorded);
        }

        private void WaveInRecordingStopped(object sender, StoppedEventArgs e)
        {
            waveFileWriter.Dispose();
        }

    }
}
