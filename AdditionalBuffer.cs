using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAudioSync
{
    public class AdditionalBuffer
    {
        public BufferedWaveProvider Buffer { get; set; }
        public VolumeSampleProvider VolumeProvider { get; set; }
        public int Offset { get; set; }

        public AdditionalBuffer(BufferedWaveProvider buffer, VolumeSampleProvider volumeProvider, int offset)
        {
            Buffer = buffer;
            VolumeProvider = volumeProvider;
            Offset = offset;
        }
    }
}
