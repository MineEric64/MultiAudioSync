using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAudioSync
{
    public class SilenceDetector
    {
        public enum SilenceDetectionMethod
        {
            /// <summary>
            /// No Silence Detection Method
            /// </summary>
            None,

            /// <summary>
            /// e.BytesRecorded
            /// </summary>
            Count,

            /// <summary>
            /// check if all buffer's value is zero
            /// </summary>
            Zero,

            /// <summary>
            /// check if volume is extremely low
            /// </summary>
            DB,

            /// <summary>
            /// count + zero + db
            /// </summary>
            Hybrid
        }

        private static Stopwatch _sw = new Stopwatch(); //because of detecting silent

        public static bool IsAudioPlaying(byte[] buffer, SilenceDetectionMethod method)
        {
            bool none = method == SilenceDetectionMethod.None;
            bool count = method == SilenceDetectionMethod.Count;
            bool zero = method == SilenceDetectionMethod.Zero;
            bool db = method == SilenceDetectionMethod.DB;
            bool hybrid = method == SilenceDetectionMethod.Hybrid;
            bool run = true;

            if (!none)
            {
                if (count || hybrid) run = buffer.Length > 0;
                if (run && (zero || hybrid)) run = buffer.Any(x => x != 0);
                if (run && (db || hybrid))
                {
                    if (!hybrid) run = buffer.Length > 0;
                    if (run)
                    {
                        double sample16Bit = BitConverter.ToSingle(buffer, 0);
                        double volume = Math.Abs(sample16Bit / 32768.0);
                        double decibels = 20 * Math.Log10(volume);

                        run = decibels > -200;

                        if (!run)
                        {
                            if (!_sw.IsRunning) _sw.Start();
                            else run = _sw.ElapsedMilliseconds < 1000;
                        }
                        else if (_sw.IsRunning)
                        {
                            _sw.Stop();
                            _sw.Reset();
                        }
                    }
                }
            }

            return run;
        }
    }
}
