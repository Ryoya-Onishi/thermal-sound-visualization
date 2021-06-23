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

using libirimagerNet;
using MaterialDesignThemes.Wpf;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ThermalProfiler.Domain;
using AUTD3Sharp;
using System.Collections.Generic;
using AUTD3Sharp.Utils;
using System.Linq;

namespace ThermalProfiler
{
    public partial class MainWindow : Window
    {

        AUTD autd;


        public MainWindow()
        {
            InitializeComponent();

            autd = new AUTD();
            autd.AddDevice(Vector3f.Zero, Vector3f.Zero);

            var ifname = GetIfname();

            var link = Link.SOEMLink(ifname, autd.NumDevices);

            if (!autd.OpenWith(link))
            {
                Console.WriteLine(AUTD.LastError);
                return;
            }

            const float x = AUTD.AUTDWidth / 2;
            const float y = AUTD.AUTDHeight / 2;
            const float z = 150;

            var mod = Modulation.StaticModulation();
            autd.AppendModulation(mod);

            var gain = Gain.FocalPointGain(new Vector3f(x, y, z));
            autd.AppendGain(gain);

            foreach (var (firm, index) in autd.FirmwareInfoList().Select((firm, i) => (firm, i)))
                Console.WriteLine($"AUTD{index}:{firm}");

            
        }

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

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            autd.Dispose();
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

        private async Task ImageGrabberMethod()
        {
            
            while (_grabImage)
            {
                try
                {
                    var images = _irDirectInterface.ThermalPaletteImage;
                    PaletteImage.Value = images.PaletteImage;

                    var rawT = images.ThermalImage[x0, y0];
                    var T = ConvertToTemp(rawT);

                    var maxTemp = GetMaxTemperatuer(images);

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
