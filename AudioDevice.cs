using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MultiAudioSync
{
    public class AudioDevice
    {
        public static MMDevice DefaultDevice => WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice();

        public string Name { get; private set; }
        public string Id { get; private set; }
        public MMDevice Device { get; private set; }
        public WasapiOut Playback { get; private set; }
        public int Offset { get; set; }
        public bool IsDefault { get; set; }

        public AudioDevice(MMDevice device)
        {
            Name = device.FriendlyName;
            Id = device.ID;
            Device = device;
            Playback = null;
            Offset = 0;
            IsDefault = false;
        }

        public void InitPlayback()
        {
            Playback = new WasapiOut(Device, AudioClientShareMode.Shared, true, 10);
        }
    }
}
