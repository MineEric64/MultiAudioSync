using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MultiAudioSync
{
    public class WasapiCapture
    {
        //Brought the source code from https://github.com/MineEric64/BetterLiveScreen/blob/main/Recording/Audio/Wasapi/WasapiCapture.cs (Better Live Screen)

        internal static MMDevice DefaultMMDevice //Speaker
        {
            get
            {
                try
                {
                    return WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice();
                }
                catch
                {
                    Debug.WriteLine("[Error] Can't get DefaultMMDevice");
                    return null;
                }
            }
        }
        internal static WaveFormat DeviceMixFormat => DefaultMMDevice?.AudioClient?.MixFormat;
        internal static WaveFormat DeviceWaveFormat => DeviceMixFormat?.AsStandardWaveFormat();
        public static WaveFormat WaveFormat => _capture?.WaveFormat;
        public static WaveFormat StandardFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        public const bool INVOKE_WHEN_SILENCE = false;

        /// <summary>
        /// for capturing audio (Speaker)
        /// </summary>
        private static WasapiLoopbackCapture _capture;
        private static string _prevDeviceId = string.Empty;
        /// <summary>
        /// uses when audio data isn't available in DataAvailable
        /// </summary>
        private static Stopwatch _sw = new Stopwatch();

        public static event EventHandler<byte[]> DataAvailable;

        public static bool IsInitialized { get; private set; } = false;

        /// <summary>
        /// Initializes the output capture device.
        /// </summary>
        public static void Initialize()
        {
            MMDevice device = DefaultMMDevice;

            if (device != null)
            {
                _capture = new WasapiLoopbackCapture(device);
                _prevDeviceId = device.ID;

                IsInitialized = true;
            }
        }

        public static bool Record()
        {
            MMDevice device = DefaultMMDevice;

            if (!IsInitialized || device == null)
            {
                return false;
            }

            if (_prevDeviceId != device.ID)
            {
                Initialize();
            }

            _capture.DataAvailable += WhenDataAvailable;
            _capture.StartRecording();
            _sw.Start();

            return true;
        }

        public static void Stop()
        {
            if (!IsInitialized) return;

            _capture.StopRecording();
            _capture.DataAvailable -= WhenDataAvailable;
            _capture.Dispose();

            IsInitialized = false;

            _sw.Stop();
            _sw.Reset();
        }

        private static void WhenDataAvailable(object sender, WaveInEventArgs e)
        {
            _sw.Stop();

            byte[] buffer = new byte[e.BytesRecorded];

            if (INVOKE_WHEN_SILENCE || e.BytesRecorded > 0)
            {
                if (e.BytesRecorded == 0)
                {
                    int bytesPerMillisecond = WaveFormat.AverageBytesPerSecond / 1000;
                    int bytesRecorded = (int)_sw.ElapsedMilliseconds * bytesPerMillisecond;

                    buffer = new byte[bytesRecorded];
                }
                else
                {
                    Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
                }
                DataAvailable?.Invoke(sender, buffer);
            }
            _sw.Restart();
        }
    }
}