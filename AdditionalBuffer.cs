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
        public MixingSampleProvider Mixer { get; set; }
        public int Offset { get; set; }

        public AdditionalBuffer(BufferedWaveProvider buffer, MixingSampleProvider mixer, int offset)
        {
            Buffer = buffer;
            Mixer = mixer;
            Offset = offset;
        }
    }
}
