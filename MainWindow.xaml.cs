﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
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
using System.Windows.Media.Animation;
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
        public const int MAX_DEVICE_LENGTH = 4;

        public int AudioMechanism { get; set; } = 0;
        /* 0: from Audio Capture Device (Default)
         * 1: from Audio File (Old)
         */

        public List<AudioDevice> AudioDevices { get; set; } = new List<AudioDevice>(); //스피커 및 헤드폰과 같은 전체 오디오 장치
        public AudioDevice[] CurrentAudioDevices { get; set; } = new AudioDevice[MAX_DEVICE_LENGTH] { null, null, null, null }; //프로그램에 연결해서 동기화할 장치

        public AdditionalBuffer[] Buffers { get; private set; } = new AdditionalBuffer[MAX_DEVICE_LENGTH] { null, null, null, null };
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

            Task.Run(ProcessBufferBackground);

            for (int i = 1; i <= MAX_DEVICE_LENGTH; i++) EnableDevice(i, false);
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
                textPath.Text = ofd.FileName;

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
            var comboBoxes = new ComboBox[MAX_DEVICE_LENGTH] { comboDevice1, comboDevice2, comboDevice3, comboDevice4 };

            for (int i = 0; i < MAX_DEVICE_LENGTH; i++)
            {
                var comboBox = comboBoxes[i];

                comboBox.Items.Clear();

                foreach (var device in AudioDevices)
                {
                    var item = new ComboBoxItem();
                    item.Content = device.Name;
                    item.Tag = device.Id;

                    comboBox.Items.Add(item);
                }
            }
        }

        private void play_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < MAX_DEVICE_LENGTH; i++)
            {
                AudioDevice device = CurrentAudioDevices[i];

                if (device != null)
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
            for (int i = 0; i < MAX_DEVICE_LENGTH; i++) CurrentAudioDevices[i] = null;

            var checkBoxes = new CheckBox[MAX_DEVICE_LENGTH] { checkEnabled1, checkEnabled2, checkEnabled3, checkEnabled4 };
            var comboBoxes = new ComboBox[MAX_DEVICE_LENGTH] { comboDevice1, comboDevice2, comboDevice3, comboDevice4 };
            var textBoxes = new TextBox[MAX_DEVICE_LENGTH] { textOffset1, textOffset2, textOffset3, textOffset4 };

            for (int i = 0; i < MAX_DEVICE_LENGTH; i++)
            {
                var checkBox = checkBoxes[i];
                var comboBox = comboBoxes[i];
                var textBox = textBoxes[i];
                AudioDevice device = null;

                if (checkBox.IsChecked.HasValue && checkBox.IsChecked.Value)
                {
                    var item = comboBox.SelectedItem as ComboBoxItem;

                    if (item != null)
                    {
                        string id = (string)item.Tag;
                        int offset = int.Parse(textBox.Text);

                        device = AudioDevices.Where(x => x.Id == id).First();
                        device.Offset = offset;
                    }
                }

                CurrentAudioDevices[i] = device;
            }
        }

        private void buttonaddoffsetapply_Click(object sender, RoutedEventArgs e)
        {
            AudioMechanism = 0;

            TextBox[] textAddOffsets = new TextBox[MAX_DEVICE_LENGTH] { textaddoffset1, textaddoffset2, textaddoffset3, textaddoffset4 };
            int[] additionalOffsets = new int[MAX_DEVICE_LENGTH] { 0, 0, 0, 0 };
            bool[] isMuted = new bool[MAX_DEVICE_LENGTH] { checkMute1.IsChecked.Value, checkMute2.IsChecked.Value, checkMute3.IsChecked.Value, checkMute4.IsChecked.Value };

            for (int i = 0; i < MAX_DEVICE_LENGTH; i++)
            {
                var textAddOffset = textAddOffsets[i];
                if (int.TryParse(textAddOffset.Text, out int offset)) additionalOffsets[i] = offset;
            }

            if (!WasapiCapture.IsInitialized)
            {
                WasapiCapture.Initialize();
                WasapiCapture.DataAvailable += WasapiCapture_DataAvailable;

                for (int i = 0; i < MAX_DEVICE_LENGTH; i++)
                {
                    var device = CurrentAudioDevices[i];

                    if (device != null)
                    {
                        var buffered = new BufferedWaveProvider(WasapiCapture.WaveFormat) { DiscardOnBufferOverflow = true };
                        var converted = new WdlResamplingSampleProvider(buffered.ToSampleProvider(), 44100).ToStereo();
                        var volumeProvider = new VolumeSampleProvider(converted) { Volume = 1.0f };
                        int offset = additionalOffsets[i];

                        Buffers[i] = new AdditionalBuffer(buffered, volumeProvider, offset);

                        device.InitPlayback();
                        PassAudioToDevice(volumeProvider, device);
                    }
                }
                
                WasapiCapture.Record();
                play_Click(null, null);
            }

            for (int i = 0; i < MAX_DEVICE_LENGTH; i++)
            {
                int currentOffset = additionalOffsets[i];
                var buffered = Buffers[i];

                if (buffered == null) continue;
                if (buffered.Offset != currentOffset) buffered.Offset = currentOffset;

                if (isMuted[i] && buffered.VolumeProvider.Volume == 1.0f) buffered.VolumeProvider.Volume = 0.0f;
                else if (!isMuted[i] && buffered.VolumeProvider.Volume == 0.0f) buffered.VolumeProvider.Volume = 1.0f;
            }
        }

        private void WasapiCapture_DataAvailable(object sender, byte[] buffer) //called every 50ms
        {
            for (int i = 0; i < MAX_DEVICE_LENGTH; i++)
            {
                AdditionalBuffer buffered = Buffers[i];
                DateTime date = DateTime.Now;

                if (buffered == null) continue;
                if (buffered.Offset > 0) date = date.AddMilliseconds(buffered.Offset);
 
                DeviceAudioBuffer bufferInfo = new DeviceAudioBuffer(i + 1, date, buffered.Buffer, buffer);
                DeviceBuffers.Add(bufferInfo);
            }
        }

        public void ProcessBufferBackground()
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

                Thread.Sleep(1);
                //await Task.Delay(1);
            }
        }

        private void checkEnabled1_Checked(object sender, RoutedEventArgs e) { EnableDevice(1, true); }
        private void checkEnabled2_Checked(object sender, RoutedEventArgs e) { EnableDevice(2, true); }
        private void checkEnabled3_Checked(object sender, RoutedEventArgs e) { EnableDevice(3, true); }
        private void checkEnabled4_Checked(object sender, RoutedEventArgs e) { EnableDevice(4, true); }

        private void checkEnabled1_Unchecked(object sender, RoutedEventArgs e) { EnableDevice(1, false); }
        private void checkEnabled2_Unchecked(object sender, RoutedEventArgs e) { EnableDevice(2, false); }
        private void checkEnabled3_Unchecked(object sender, RoutedEventArgs e) { EnableDevice(3, false); }
        private void checkEnabled4_Unchecked(object sender, RoutedEventArgs e) { EnableDevice(4, false); }

        private void EnableDevice(int deviceNumber, bool enabled)
        {
            switch (deviceNumber)
            {
                case 1:
                    comboDevice1.IsEnabled = enabled;
                    textOffset1.IsEnabled = enabled;
                    textaddoffset1.IsEnabled = enabled;
                    checkMute1.IsEnabled = enabled;
                    break;

                case 2:
                    comboDevice2.IsEnabled = enabled;
                    textOffset2.IsEnabled = enabled;
                    textaddoffset2.IsEnabled = enabled;
                    checkMute2.IsEnabled = enabled;
                    break;

                case 3:
                    comboDevice3.IsEnabled = enabled;
                    textOffset3.IsEnabled = enabled;
                    textaddoffset3.IsEnabled = enabled;
                    checkMute3.IsEnabled = enabled;
                    break;

                case 4:
                    comboDevice4.IsEnabled = enabled;
                    textOffset4.IsEnabled = enabled;
                    textaddoffset4.IsEnabled = enabled;
                    checkMute4.IsEnabled = enabled;
                    break;
            }
        }

        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            GetAudioDevices();
            AddAudioDevicesToComboBoxes();
        }

        private void initButton_Click(object sender, RoutedEventArgs e)
        {
            WasapiCapture.Stop();
            WasapiCapture.DataAvailable -= WasapiCapture_DataAvailable;

            AudioMechanism = 0;

            for (int i = 0; i < MAX_DEVICE_LENGTH; i++)
            {
                var device = CurrentAudioDevices[i];

                if (device != null)
                {
                    device.Playback.Stop();
                    device.Playback.Dispose();
                    CurrentAudioDevices[i] = null;
                }
                Buffers[i] = null;
            }
            DeviceBuffers.Clear();

            refreshButton_Click(null, null);

            checkEnabled1.IsChecked = false;
            checkEnabled2.IsChecked = false;
            checkEnabled3.IsChecked = false;
            checkEnabled4.IsChecked = false;

            textOffset1.Text = "0";
            textOffset2.Text = "0";
            textOffset3.Text = "0";
            textOffset4.Text = "0";

            textaddoffset1.Text = "0";
            textaddoffset2.Text = "0";
            textaddoffset3.Text = "0";
            textaddoffset4.Text = "0";

            checkMute1.IsChecked = false;
            checkMute2.IsChecked = false;
            checkMute3.IsChecked = false;
            checkMute4.IsChecked = false;
        }
    }
}
