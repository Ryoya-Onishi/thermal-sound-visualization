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
using System.Text;
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

        private Stopwatch sw_all = new Stopwatch();


        private Stopwatch sw_autd = new Stopwatch();
        private Stopwatch sw_thermo = new Stopwatch();



        private async Task ImageGrabberMethod()
        {
            AUTD autd = new AUTD();
            autd.AddDevice(Vector3d.Zero, Vector3d.Zero);
            autd.AddDevice(new Vector3d(0, 0, 0), Vector3d.Zero);
            //autd.AddDevice(Vector3d.Zero, Vector3d.Zero);
            //autd.AddDevice(new Vector3d(0, 206.4, 0), Vector3d.Zero);
            //autd.AddDevice(new Vector3d(250, 0, 0), Vector3d.Zero);
            //autd.AddDevice(new Vector3d(250, 151.4, 0), Vector3d.Zero);

            //var ifname = GetIfname();
            var ifname = @"\Device\NPF_{70548BB5-E7B1-4538-91A5-41FA6A1500C2}";

            var link = Link.SOEM(ifname, autd.NumDevices);

            if (!autd.Open(link))
            {
                Console.WriteLine(AUTD.LastError);
                return;
            }

            foreach (var (firm, index) in autd.FirmwareInfoList().Select((firm, i) => (firm, i)))
                Console.WriteLine($"AUTD {index}: {firm}");

            const double x = 90;
            const double y = 76;
            const double z = 150;

            var focalPoint = new Vector3d(x, y, z);

            var mod = Modulation.Static();

            var gain = Gain.FocalPoint(focalPoint);

            sw_all.Start();
            sw_autd.Start();
            sw_thermo.Start();

            long t0 = 0;

            long t = 0;
            long delta_time = 0;

            double T0 = 0;

            double T = 0;
            double delta_T = 0;

            ThermalPaletteImage images;

            long radiatingTime = 300000;
            long intervalTime = 30000;

            byte amplitude = 255;

            byte ampStep = 5;

            var array_T0 = new double[288, 382];

            int trial_times = 0;

            int frameNum = 0;

            var directoryName = @"D:\onishi_Local2/Nature/exp_for_paper/standingWave/" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm");

            int z_change = 170;
            float y_change = 151.4f;


            if (!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);

            while (_grabImage)
            {
                try
                {
                    images = _irDirectInterface.ThermalPaletteImage;
                    PaletteImage.Value = images.PaletteImage;

                    if (trial_times >= 1)
                    {
                        if (amplitude >= 255)
                        {
                            //break;
                        }
                        //y_change += 1;
                        //z_change += 1;

                        //amplitude -= ampStep;
                        trial_times = 0;

                    }

                    if (sw_autd.ElapsedMilliseconds > intervalTime && isNotAppendedGain)
                    {
                        array_T0 = new double[images.ThermalImage.GetLength(0), images.ThermalImage.GetLength(1)]; ;

                        for (var i = 0; i < images.ThermalImage.GetLength(0); i++)
                        {
                            for (var j = 0; j < images.ThermalImage.GetLength(1); j++)
                            {
                                array_T0[i, j] = ConvertToTemp(images.ThermalImage[i, j]);
                            }
                        }

                        t0 = sw_autd.ElapsedMilliseconds;
                        //gain = Gain.FocalPoint(new Vector3d(x,y_change,z_change), amplitude); //AUTD照射位置を変える
                        gain = Gain.FocalPoint(new Vector3d(x, y, z), amplitude); //AUTD照射

                        autd.Send(gain);
                        isNotAppendedGain = false;

                    }
                    else if (sw_autd.ElapsedMilliseconds > intervalTime + radiatingTime)
                    {
                        autd.Stop();
                        sw_autd.Restart();
                        isNotAppendedGain = true;
                        trial_times += 1;
                    }

                    if (true)
                    {
                        delta_time = sw_autd.ElapsedMilliseconds - t0;

                        var sb = new StringBuilder();
                        for (var i = 0; i < images.ThermalImage.GetLength(0); i++)
                        {
                            for (var j = 0; j < images.ThermalImage.GetLength(1); j++)
                            {
                                if (j != 0) sb.Append(",");
                                delta_T = ConvertToTemp(images.ThermalImage[i, j]) - array_T0[i, j];


                                //sb.Append((delta_T * 1000) / delta_time); //T'を求める
                                //sb.Append(delta_T); //dTを求める
                                sb.Append(ConvertToTemp(images.ThermalImage[i, j]));
                            }
                            sb.AppendLine();
                        }

                        if (true)
                        {
                            //if (!Directory.Exists(directoryName + "/duty" + amplitude)) Directory.CreateDirectory(directoryName + "/duty" + amplitude);

                            //using var sw = new StreamWriter(directoryName + "/" + "z" + z_change  + "_trial" + trial_times.ToString() + "_t" + delta_time + "interval" + intervalTime +  ".csv");
                            //using var sw = new StreamWriter(directoryName + "/" + "y" + y_change + "_trial" + trial_times.ToString() + "_t" + delta_time + "interval" + intervalTime + ".csv");

                            //using var sw = new StreamWriter(directoryName + "/" + delta_time + ".csv");
                            //using var sw = new StreamWriter(directoryName + "/"  +"duty" + amplitude +"_" +delta_time + ".csv");

                            using var sw = new StreamWriter(directoryName + "/" + sw_all.ElapsedMilliseconds + ".csv");

                            sw.Write(sb.ToString());

                            //isNotAppendedGain = true; //一回目だけdT_dtを取得
                        }
                    }
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
