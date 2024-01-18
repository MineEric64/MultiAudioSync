using NAudio.Wave;
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
        public int Offset { get; set; }

        public AdditionalBuffer(BufferedWaveProvider buffer, int offset)
        {
            Buffer = buffer;
            Offset = offset;
        }
    }
}
