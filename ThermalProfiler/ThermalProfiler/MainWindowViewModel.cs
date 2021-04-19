/*
 * File: MainWindowViewModel.cs
 * Project: ThermalProfiler
 * Created Date: 19/04/2021
 * Author: Shun Suzuki
 * -----
 * Last Modified: 19/04/2021
 * Modified By: Shun Suzuki (suzuki@hapis.k.u-tokyo.ac.jp)
 * -----
 * Copyright (c) 2021 Hapis Lab. All rights reserved.
 * 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using libirimagerNet;
using MaterialDesignExtensions.Controls;
using MaterialDesignThemes.Wpf;
using PS4000Lib;
using Reactive.Bindings;
using ThermalProfiler.Domain;
using Range = PS4000Lib.Enum.Range;

namespace ThermalProfiler
{
    internal class MainWindowViewModel : INotifyPropertyChanged
    {
#pragma warning disable 414
        public event PropertyChangedEventHandler PropertyChanged = null!;
#pragma warning restore 414

        private bool _grabImage;

        private Task? _thermoHandler;
        public OptrisColoringPalette[] Palettes { get; } = (OptrisColoringPalette[])Enum.GetValues(typeof(OptrisColoringPalette));
        public ReactiveProperty<OptrisColoringPalette> Palette { get; set; }
        public OptrisPaletteScalingMethod[] Scalings { get; } = (OptrisPaletteScalingMethod[])Enum.GetValues(typeof(OptrisPaletteScalingMethod));
        public ReactiveProperty<OptrisPaletteScalingMethod> Scaling { get; set; }

        public string[] Ports => SerialPort.GetPortNames();
        public ReactiveProperty<string> Port { get; set; }
        private SerialPort _sp;

        private PS4000 _pico;
        private bool _measure;
        private List<Task> _tasks;

        private string _tmpPath = "";

        public ReactiveProperty<string> CurrentMicrophoneTemp { get; set; }
        public ReactiveProperty<string> DataFolder { get; set; }

        public AsyncReactiveCommand ButtonPower { get; }
        public AsyncReactiveCommand ButtonConnect { get; }
        public AsyncReactiveCommand ButtonStart { get; }
        public AsyncReactiveCommand ButtonFinish { get; }
        public AsyncReactiveCommand ButtonSelectFolder { get; }

        public ReactiveProperty<bool> IsConnected { get; set; }
        public ReactiveProperty<bool> IsStarted { get; set; }
        public ReactiveProperty<Bitmap> PaletteImage { get; set; }

        public ReactiveProperty<float> FocusX { get; set; }
        public ReactiveProperty<float> FocusY { get; set; }
        public ReactiveProperty<float> FocusZ { get; set; }
        public ReactiveProperty<byte> Duty { get; set; }

        public MainWindowViewModel()
        {
            _thermoHandler = null;
            _sp = new SerialPort();
            _pico = new PS4000();
            _tasks = new List<Task>();

            PaletteImage = new ReactiveProperty<Bitmap>();
            IsConnected = new ReactiveProperty<bool>(false);
            IsStarted = new ReactiveProperty<bool>(false);
            DataFolder = new ReactiveProperty<string>();

            FocusX = new ReactiveProperty<float>(90);
            FocusY = new ReactiveProperty<float>(70);
            FocusZ = new ReactiveProperty<float>(150);
            Duty = new ReactiveProperty<byte>(255);

            Palette = new ReactiveProperty<OptrisColoringPalette>(OptrisColoringPalette.Iron);
            Scaling = new ReactiveProperty<OptrisPaletteScalingMethod>(OptrisPaletteScalingMethod.MinMax);
            Port = new ReactiveProperty<string>();

            CurrentMicrophoneTemp = new ReactiveProperty<string>();

            Palette.Subscribe(p =>
            {
                if (!IsConnected.Value) return;
                IrDirectInterface.Instance.SetPaletteFormat(p, Scaling.Value);
            });
            Scaling.Subscribe(s =>
            {
                if (!IsConnected.Value) return;
                IrDirectInterface.Instance.SetPaletteFormat(Palette.Value, s);
            });

            ButtonConnect = IsConnected.Select(b => !b).ToAsyncReactiveCommand();
            ButtonConnect.Subscribe(Connect);

            ButtonStart = IsStarted.Select(b => !b).ToAsyncReactiveCommand();
            ButtonStart.Subscribe(Start);

            ButtonFinish = IsStarted.Select(b => b).ToAsyncReactiveCommand();
            ButtonFinish.Subscribe(Finish);

            ButtonSelectFolder = new AsyncReactiveCommand();
            ButtonSelectFolder.Subscribe(SelectFolder);

            ButtonPower = new AsyncReactiveCommand();
            ButtonPower.Subscribe(async _ =>
            {
                var vm = new ConfirmDialogViewModel { Message = { Value = "Are you sure to quit the application?" } };
                var dialog = new ConfirmDialog
                {
                    DataContext = vm
                };
                var res = await DialogHost.Show(dialog, "MessageDialogHost");
                if (res is bool quit && quit)
                {
                    _grabImage = false;
                    if (_thermoHandler != null) await _thermoHandler;
                    IrDirectInterface.Instance.Disconnect();

                    _sp.Close();
                    _pico.Dispose();

                    Application.Current.Shutdown();
                }
            });
        }

        private async Task Connect()
        {
            try
            {
                IrDirectInterface.Instance.Connect("generic.xml");

                _sp = new SerialPort { PortName = Port.Value, BaudRate = 115200 };
                _sp.Open();
                _sp.DataReceived += SerialPortOnDataReceived;

                _pico = new PS4000();
                _pico.Open();
                _pico.ChannelA.Enabled = true;
                _pico.ChannelB.Enabled = true;
                _pico.SamplingRateHz = 10_000_000;
                _pico.BufferSize = 20000;
                _pico.ChannelA.Range = Range.Range500MV;
                _pico.ChannelB.Range = Range.Range50V;
                _pico.ChannelA.Attenuation = 1;
                _pico.ChannelB.Attenuation = 10;
                BlockData.Delimiter = ",";
                BlockData.ShowADC = false;
            }
            catch (Exception ex)
            {
                var vm = new ErrorDialogViewModel { Message = { Value = ex.Message } };
                var dialog = new ErrorDialog
                {
                    DataContext = vm
                };
                await DialogHost.Show(dialog, "MessageDialogHost");
                return;
            }

            _measure = false;
            _grabImage = true;
            _thermoHandler = Task.Run(ImageGrabberMethod);
            IsConnected.Value = true;
        }


        private async Task SelectFolder()
        {
            var dialogArgs = new OpenDirectoryDialogArguments
            {
                Width = 600,
                Height = 800,
                CreateNewDirectoryEnabled = true
            };
            var result = await OpenDirectoryDialog.ShowDialogAsync("MessageDialogHost", dialogArgs);
            if (result.Canceled) return;
            try
            {
                DataFolder.Value = result.Directory;
            }
            catch
            {
                var vm = new ErrorDialogViewModel { Message = { Value = "Failed to open folder." } };
                var dialog = new ErrorDialog
                {
                    DataContext = vm
                };
                await DialogHost.Show(dialog, "MessageDialogHost");
            }
        }

        private async Task Start()
        {
            if (!Directory.Exists(DataFolder.Value))
            {
                var vm = new ErrorDialogViewModel { Message = { Value = "Data folder does not exist." } };
                var dialog = new ErrorDialog
                {
                    DataContext = vm
                };
                await DialogHost.Show(dialog, "MessageDialogHost");
                return;
            }

            IsStarted.Value = true;
            _measure = true;
            _tasks = new List<Task>();
            var now = DateTime.Now;
            _tmpPath = Path.Join(DataFolder.Value, now.ToString("yyyy-MM-dd_HH-mm-ss"));
            if (!Directory.Exists(_tmpPath)) Directory.CreateDirectory(_tmpPath);
        }

        private async Task Finish()
        {
            IsStarted.Value = false;
            _measure = false;
            await Task.WhenAll(_tasks);
            _tasks.Clear();
        }

        private void SerialPortOnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!(sender is SerialPort sp)) return;

            var data = sp.ReadLine();
            if (string.IsNullOrEmpty(data)) return;
            if (!double.TryParse(data, out var temp)) return;

            CurrentMicrophoneTemp.Value = temp.ToString(CultureInfo.InvariantCulture);
        }

        private async Task ImageGrabberMethod()
        {
            while (_grabImage)
            {
                try
                {
                    var images = IrDirectInterface.Instance.ThermalPaletteImage;
                    PaletteImage.Value = images.PaletteImage;

                    if (!_measure) continue;
                    var micData = await _pico.CollectBlockImmediateAsync();
                    var micTemp = CurrentMicrophoneTemp.Value;
                    var task = Task.Run(() =>
                    {
                        SaveData(images.ThermalImage, micData, micTemp);
                    });
                    _tasks.Add(task);
                }
                catch (IOException ex)
                {
                    var vm = new ErrorDialogViewModel { Message = { Value = ex.Message } };
                    var dialog = new ErrorDialog
                    {
                        DataContext = vm
                    };
                    await DialogHost.Show(dialog, "MessageDialogHost");
                    _grabImage = false;
                }
            }
        }

        public static double ConvertToTemp(ushort data) => (data - 1000.0) / 10.0;

        private void SaveData(ushort[,] thermalImage, BlockData micData, string micTemp)
        {
            var now = DateTime.Now.Ticks;
            while (File.Exists(Path.Join(_tmpPath, "thermo_" + now + ".bin"))) now++;

            var sb = new StringBuilder();
            for (var i = 0; i < thermalImage.GetLength(0); i++)
            {
                for (var j = 0; j < thermalImage.GetLength(1); j++)
                {
                    if (j != 0) sb.Append(", ");
                    sb.Append(ConvertToTemp(thermalImage[i, j]));
                }
                sb.AppendLine();
            }
            var sw = new StreamWriter(Path.Join(_tmpPath, "thermo_" + now + ".csv"));
            sw.WriteLine(sb);
            sw.Dispose();

            sw = new StreamWriter(Path.Join(_tmpPath, "mic_" + now + ".csv"));
            sw.WriteLine(micData);
            sw.Dispose();

            sw = new StreamWriter(Path.Join(_tmpPath, "micTemp_" + now + ".csv"));
            sw.WriteLine(micTemp);
            sw.Dispose();
        }
    }
}