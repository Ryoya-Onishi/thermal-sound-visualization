/*
 * File: MainWindow.xaml.cs
 * Project: ThermalProfiler
 * Created Date: 18/04/2021
 * Author: Shun Suzuki
 * -----
 * Last Modified: 18/04/2021
 * Modified By: Shun Suzuki (suzuki@hapis.k.u-tokyo.ac.jp)
 * -----
 * Copyright (c) 2021 Hapis Lab. All rights reserved.
 * 
 */

using AUTD3Sharp;
using AUTD3Sharp.Utils;
using libirimagerNet;
using MaterialDesignThemes.Wpf;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ThermalProfiler.Domain;

namespace ThermalProfiler
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }

    internal class MainWindowModel : INotifyPropertyChanged
    {
#pragma warning disable 414
        public event PropertyChangedEventHandler PropertyChanged = null!;
#pragma warning restore 414

        private static readonly Lazy<MainWindowModel> Lazy = new Lazy<MainWindowModel>(() => new MainWindowModel());
        public static MainWindowModel Instance => Lazy.Value;

        public Bitmap PaletteImage { get; set; }
    }

    internal class MainWindowViewModel : INotifyPropertyChanged
    {
#pragma warning disable 414
        public event PropertyChangedEventHandler PropertyChanged = null!;
#pragma warning restore 414

        private IrDirectInterface _irDirectInterface;
        private bool _grabImage;

        private Task _thermoHandler;

        public AsyncReactiveCommand ButtonPower { get; }
        public AsyncReactiveCommand ButtonConnect { get; }

        public ReactiveProperty<bool> IsConnected { get; set; }
        public ReactiveProperty<Bitmap> PaletteImage { get; set; }

        public MainWindowViewModel()
        {
            PaletteImage = MainWindowModel.Instance.ToReactivePropertyAsSynchronized(m => m.PaletteImage);

            IsConnected = new ReactiveProperty<bool>(false);

            _grabImage = true;
            ButtonPower = new AsyncReactiveCommand();
            ButtonPower.Subscribe(async _ =>
            {
                var vm = new ConfirmDialogViewModel() { Message = { Value = "Are you sure to quit the application?" } };
                var dialog = new ConfirmDialog()
                {
                    DataContext = vm
                };
                var res = await DialogHost.Show(dialog, "MessageDialogHost");
                if (res is bool quit && quit)
                {
                    _grabImage = false;
                    await _thermoHandler;
                    Application.Current.Shutdown();
                }
            });

            ButtonConnect = IsConnected.Select(b => !b).ToAsyncReactiveCommand();
            ButtonConnect.Subscribe(async _ =>
            {
                try
                {
                    _irDirectInterface = IrDirectInterface.Instance;
                    _irDirectInterface.Connect("generic.xml");
                }
                catch (IOException ex)
                {
                    var vm = new ErrorDialogViewModel() { Message = { Value = ex.Message } };
                    var dialog = new ErrorDialog()
                    {
                        DataContext = vm
                    };
                    await DialogHost.Show(dialog, "MessageDialogHost");
                    return;
                }

                _thermoHandler = Task.Run(ImageGrabberMethod);
                IsConnected.Value = true;
            });
        }

        public int x0 = 160;
        public int y0 = 190;
        private static string GetIfname()
        {
            var adapters = AUTD.EnumerateAdapters();
            var etherCATAdapters = adapters as EtherCATAdapter[] ?? adapters.ToArray();
            foreach (var (adapter, index) in etherCATAdapters.Select((adapter, index) => (adapter, index)))
            {
                Console.WriteLine($"[{index}]: {adapter}");
            }

            Console.Write("Choose number: ");
            int i;
            while (!int.TryParse(Console.ReadLine(), out i)) { }
            return etherCATAdapters[i].Name;
        }

        private bool isNotAppendedGain = true;

        private Stopwatch sw_autd = new Stopwatch();
        private Stopwatch sw_thermo = new Stopwatch();



        private async Task ImageGrabberMethod()
        {
            AUTD autd = new AUTD();
            autd.AddDevice(Vector3f.Zero, Vector3f.Zero);

            //var ifname = GetIfname();
            var ifname = @"\Device\NPF_{70548BB5-E7B1-4538-91A5-41FA6A1500C2}";

            var link = Link.SOEMLink(ifname, autd.NumDevices);

            if (!autd.OpenWith(link))
            {
                Console.WriteLine(AUTD.LastError);
                return;
            }

            const float x = AUTD.AUTDWidth / 2;
            const float y = AUTD.AUTDHeight / 2;
            const float z = 150;

            var focalPoint = new Vector3f(x, y, z);

            var mod = Modulation.StaticModulation();
            autd.AppendModulation(mod);

            var gain = Gain.FocalPointGain(focalPoint);

            sw_autd.Start();
            sw_thermo.Start();

            long t0 = 0;

            long t = 0;
            long delta_time = 0;

            double T0 = 0;

            double T = 0;
            double delta_T = 0;

            ThermalPaletteImage images;

            long radiatingTime = 160;
            long intervalTime = 10000;

            int x_T = 126;
            int y_T = 182; ;


            while (_grabImage)
            {
                try
                {
                    images = _irDirectInterface.ThermalPaletteImage;
                    PaletteImage.Value = images.PaletteImage;

                    //var maxTemp = GetMaxTemperatuer(images);

                    T = ConvertToTemp(images.ThermalImage[x_T, y_T]);
                    
                    t = sw_autd.ElapsedMilliseconds;

                    if (sw_autd.ElapsedMilliseconds > intervalTime && isNotAppendedGain)
                    {
                        T0 = ConvertToTemp(images.ThermalImage[x_T, y_T]);
                        t0 = sw_autd.ElapsedMilliseconds;
                        gain = Gain.FocalPointGain(focalPoint);
                        autd.AppendGain(gain);
                        isNotAppendedGain = false;
                    }
                    else if (sw_autd.ElapsedMilliseconds > intervalTime + radiatingTime)
                    {
                        autd.Stop();
                        sw_autd.Restart();
                        isNotAppendedGain = true;                      
                    }

                    if (!isNotAppendedGain )
                    {
                        //Console.WriteLine("t and T : " + (sw_autd.ElapsedMilliseconds - t0) + "," + T);

                        delta_T = ConvertToTemp(images.ThermalImage[x_T, y_T]) - T0;
                        delta_time = sw_autd.ElapsedMilliseconds - t0;

                        Console.WriteLine((sw_autd.ElapsedMilliseconds - t0) + "," + delta_T / (delta_time * 0.001));
                    }

                    //Console.WriteLine(T);
                }
                catch (IOException ex)
                {
                    var vm = new ErrorDialogViewModel() { Message = { Value = ex.Message } };
                    var dialog = new ErrorDialog()
                    {
                        DataContext = vm
                    };
                    await DialogHost.Show(dialog, "MessageDialogHost");
                    _grabImage = false;
                }
            }

            autd.Close();
            autd.Dispose();
        }

        private double GetMaxTemperatuer(ThermalPaletteImage images)
        {
            var temp = 0d;
            var DataList_in_area = new List<double>();

            int j_max = 0;
            int i_max = 0;

            for (int i = 0; i < 380; i++)
            {
                for (int j = 0; j < 280; j++)
                {
                    if (temp < ConvertToTemp(images.ThermalImage[j, i]))
                    {
                        temp = ConvertToTemp(images.ThermalImage[j, i]);

                        j_max = j;
                        i_max = i;
                    }
                    DataList_in_area.Add(temp);
                }
            }

            temp = ConvertToTemp(images.ThermalImage[j_max, i_max]);

            Console.WriteLine(j_max + "," + i_max);

            return temp;
        }

        public static double ConvertToTemp(ushort data)
        {
            return (data - 1000.0) / 10.0;
        }

        
    }
}
