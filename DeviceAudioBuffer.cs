using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;

namespace MultiAudioSync
{
    public class DeviceAudioBuffer
    {
        public int DeviceNumber { get; set; }
        public DateTime Date { get; set; }
        public BufferedWaveProvider Buffered { get; set; }
        public byte[] Buffer { get; set; }

        public DeviceAudioBuffer(int deviceNumber, DateTime date, BufferedWaveProvider buffered, byte[] buffer)
        {
            DeviceNumber = deviceNumber;
            Date = date;
            Buffered = buffered;
            Buffer = buffer;
        }
    }
}
