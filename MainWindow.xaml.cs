﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
using Microsoft.Win32;

using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace MultiAudioSync
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public const int MAX_DEVICE_LENGTH = 2;

        public List<AudioDevice> AudioDevices { get; set; } = new List<AudioDevice>();
        public List<AudioDevice> CurrentAudioDevices { get; set; } = new List<AudioDevice>();

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            GetAudioDevices();
            AddAudioDevicesToComboBoxes();
        }

        private void buttonPath_Click(object sender, RoutedEventArgs e)
        {
            buttonApply_Click(null, null);

            var ofd = new OpenFileDialog();
            ofd.Filter = "Mp3 File|*.mp3|Flac File|*.flac|Wav File|*.wav";

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
    }
}
