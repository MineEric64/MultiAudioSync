using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.SqlServer.Server;
using Microsoft.Win32;

using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MultiAudioSync
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public const int MAX_DEVICE_LENGTH = 2;

        public int AudioMechanism { get; set; } = 0;
        /* 0: from Audio Capture Device (Default)
         * 1: from Audio File (Old)
         */

        public List<AudioDevice> AudioDevices { get; set; } = new List<AudioDevice>();
        public List<AudioDevice> CurrentAudioDevices { get; set; } = new List<AudioDevice>();

        public List<AdditionalBuffer> Buffers { get; private set; } = new List<AdditionalBuffer>();
        public SynchronizedCollection<DeviceAudioBuffer> DeviceBuffers = new SynchronizedCollection<DeviceAudioBuffer>();

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GetAudioDevices();
            AddAudioDevicesToComboBoxes();

            Task.Run(ProcessBufferBackgroundAsync);
        }

        private void buttonPath_Click(object sender, RoutedEventArgs e)
        {
            buttonApply_Click(null, null);
            AudioMechanism = 1;

            var ofd = new OpenFileDialog();
            ofd.Filter = "|All Files|*.*|Mp3 File|*.mp3|Flac File|*.flac|Wav File|*.wav";

            var dialog = ofd.ShowDialog();

            if (dialog.HasValue && dialog.Value)
            {
                foreach (var device in CurrentAudioDevices)
                {
                    var audioData = GetSoundData(ofd.FileName);

                    device.InitPlayback();
                    PassAudioToDevice(audioData, device);
                }
            }
        }

        public void GetAudioDevices()
        {
            AudioDevices.Clear();

            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var audioDevice = new AudioDevice(device);

                if (AudioDevice.DefaultDevice.ID == device.ID) audioDevice.IsDefault = true;
                AudioDevices.Add(audioDevice);
            }
        }

        public ISampleProvider GetSoundData(string path)
        {
            var audio = new AudioFileReader(path);
            return audio;
        }

        public void PassAudioToDevice(ISampleProvider audio, AudioDevice device)
        {
            device.Playback.Init(audio);
        }

        public void AddAudioDevicesToComboBoxes()
        {
            var comboBoxes = new ComboBox[MAX_DEVICE_LENGTH] { comboDevice1, comboDevice2 };

            foreach (var device in AudioDevices)
            {
                for (int i = 0; i < MAX_DEVICE_LENGTH; i++)
                {
                    var comboBox = comboBoxes[i];
                    var item = new ComboBoxItem();
                    item.Content = device.Name;
                    item.Tag = device.Id;

                    comboBox.Items.Add(item);
                }
            }
        }

        private void play_Click(object sender, RoutedEventArgs e)
        {
            foreach (var device in CurrentAudioDevices)
            {
                _ = Task.Run(async () =>
                {
                    var start = DateTime.Now;

                    if (device.Offset > 0) await Task.Delay(device.Offset);
                    device.Playback.Play();

                    var end = DateTime.Now;
                    int elapsed = (int)(end - start).TotalMilliseconds;

                    Debug.WriteLine($"{device.Name} : {elapsed}ms Elapsed.");
                });
            }
        }

        private void pause_Click(object sender, RoutedEventArgs e)
        {
            foreach (var device in CurrentAudioDevices)
            {
                device.Playback.Pause();
            }
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            foreach (var device in CurrentAudioDevices)
            {
                device.Playback.Stop();
            }
        }

        private void buttonApply_Click(object sender, RoutedEventArgs e)
        {
            CurrentAudioDevices.Clear();
            var checkBoxes = new CheckBox[MAX_DEVICE_LENGTH] { checkEnabled1, checkEnabled2 };
            var comboBoxes = new ComboBox[MAX_DEVICE_LENGTH] { comboDevice1, comboDevice2 };
            var textBoxes = new TextBox[MAX_DEVICE_LENGTH] { textOffset1, textOffset2 };

            for (int i = 0; i < MAX_DEVICE_LENGTH; i++)
            {
                var checkBox = checkBoxes[i];
                var comboBox = comboBoxes[i];
                var textBox = textBoxes[i];

                if (checkBox.IsChecked.HasValue && checkBox.IsChecked.Value)
                {
                    var item = comboBox.SelectedItem as ComboBoxItem;

                    if (item != null)
                    {
                        string id = (string)item.Tag;
                        int offset = int.Parse(textBox.Text);
                        var device = AudioDevices.Where(x => x.Id == id).First();

                        device.Offset = offset;
                        CurrentAudioDevices.Add(device);
                    }
                }
            }
        }

        private void buttonaddoffsetapply_Click(object sender, RoutedEventArgs e)
        {
            AudioMechanism = 0;

            int[] additionalOffsets = new int[MAX_DEVICE_LENGTH] { int.Parse(textaddoffset1.Text), int.Parse(textaddoffset2.Text) };
            bool[] isMuted = new bool[MAX_DEVICE_LENGTH] { checkMute1.IsChecked.Value, checkMute2.IsChecked.Value };

            if (!WasapiCapture.IsInitialized)
            {
                WasapiCapture.Initialize();
                WasapiCapture.DataAvailable += WasapiCapture_DataAvailable;

                for (int i = 0; i < CurrentAudioDevices.Count; i++)
                {
                    var device = CurrentAudioDevices[i];
                    var buffered = new BufferedWaveProvider(WasapiCapture.WaveFormat)
                    {
                        DiscardOnBufferOverflow = true
                    };
                    var converted = new WdlResamplingSampleProvider(buffered.ToSampleProvider(), 44100).ToStereo();
                    var volumeProvider = new VolumeSampleProvider(converted) { Volume = 1.0f };
                    int offset = additionalOffsets[i];
                    
                    Buffers.Add(new AdditionalBuffer(buffered, volumeProvider, offset));

                    device.InitPlayback();
                    PassAudioToDevice(volumeProvider, device);
                }
                
                WasapiCapture.Record();
                play_Click(null, null);
            }

            for (int i = 0; i < Buffers.Count; i++)
            {
                int currentOffset = additionalOffsets[i];
                var buffered = Buffers[i];

                if (buffered.Offset != currentOffset) buffered.Offset = currentOffset;

                if (isMuted[i] && buffered.VolumeProvider.Volume == 1.0f) buffered.VolumeProvider.Volume = 0.0f;
                else if (!isMuted[i] && buffered.VolumeProvider.Volume == 0.0f) buffered.VolumeProvider.Volume = 1.0f;
            }
        }

        private void WasapiCapture_DataAvailable(object sender, byte[] buffer) //called every 50ms
        {
            for (int i = 0; i < Buffers.Count; i++)
            {
                AdditionalBuffer buffered = Buffers[i];
                DateTime date = DateTime.Now;

                if (buffered.Offset > 0) date = date.AddMilliseconds(buffered.Offset);
 
                DeviceAudioBuffer bufferInfo = new DeviceAudioBuffer(i + 1, date, buffered.Buffer, buffer);
                DeviceBuffers.Add(bufferInfo);
            }
        }

        public async Task ProcessBufferBackgroundAsync()
        {
            var removes = new List<DeviceAudioBuffer>();

            while (true)
            {
                for (int i = 0; i < DeviceBuffers.Count; i++)
                {
                    var bufferInfo = DeviceBuffers[i];

                    if (DateTime.Now >= bufferInfo.Date)
                    {
                        bufferInfo.Buffered.AddSamples(bufferInfo.Buffer, 0, bufferInfo.Buffer.Length);
                        removes.Add(bufferInfo);
                    }
                }
                for (int i = 0; i < removes.Count; i++) DeviceBuffers.Remove(removes[i]);
                if (removes.Count > 0) removes.Clear();

                await Task.Delay(5);
            }
        }

        private void checkEnabled1_Checked(object sender, RoutedEventArgs e)
        {
            EnableDevice(1, true);
        }

        private void checkEnabled2_Checked(object sender, RoutedEventArgs e)
        {
            EnableDevice(2, true);
        }

        private void checkEnabled1_Unchecked(object sender, RoutedEventArgs e)
        {
            EnableDevice(1, false);
        }

        private void checkEnabled2_Unchecked(object sender, RoutedEventArgs e)
        {
            EnableDevice(2, false);
        }

        private void EnableDevice(int deviceNumber, bool enabled)
        {
            switch (deviceNumber)
            {
                case 1:
                    comboDevice1.IsEnabled = enabled;
                    textOffset1.IsEnabled = enabled;
                    textaddoffset1.IsEnabled = enabled;
                    break;

                case 2:
                    comboDevice2.IsEnabled = enabled;
                    textOffset2.IsEnabled = enabled;
                    textaddoffset2.IsEnabled = enabled;
                    break;
            }
        }    }
}
