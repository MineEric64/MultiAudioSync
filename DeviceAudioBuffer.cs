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
        public long Timestamp { get; set; }
        public BufferedWaveProvider Buffered { get; set; }
        public byte[] Buffer { get; set; }

        public DeviceAudioBuffer(int deviceNumber, long timestamp, BufferedWaveProvider buffered, byte[] buffer)
        {
            DeviceNumber = deviceNumber;
            Timestamp = timestamp;
            Buffered = buffered;
            Buffer = buffer;
        }
    }
}
