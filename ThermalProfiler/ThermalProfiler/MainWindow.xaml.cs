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

        private Stopwatch sw_autd = new Stopwatch();
        private Stopwatch sw_thermo = new Stopwatch();



        private async Task ImageGrabberMethod()
        {
            AUTD autd = new AUTD();
            autd.AddDevice(Vector3d.Zero, Vector3d.Zero);
            //autd.AddDevice(Vector3d.Zero,Vector3d.Zero);
            //autd.AddDevice(new Vector3d(0, 150,300), new Vector3d(AUTD.Pi, 0, 0));


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

            //const double x = AUTD.AUTDWidth / 2;
            //const double y = AUTD.AUTDHeight / 2;
            //const double z = 150;

            //var focalPoint = new Vector3d(x, y, z);

            //var mod = Modulation.Static();

            //var gain = Gain.FocalPoint(focalPoint);


            const double x = AUTD.AUTDWidth / 2 - 20;
            const double y = 75;
            const double z = 150.0;

            autd.SilentMode = false;

            var mod = Modulation.Static();
            autd.Send(mod);


            //熱画像デモ
            //const double radius = 10;
            //const int size = 10;
            //var center = new Vector3d(x, y, z);
            //var stm = autd.STM();
            //for (var i = 0; i < size; i++)
            //{
            //    var theta = 2 * AUTD.Pi * i / size;
            //    var r = new Vector3d(Math.Cos(theta), 0, 0);
            //    var f = Gain.FocalPoint(center + radius * r);
            //    stm.AddSTMGain(f);
            //}
            //stm.StartSTM(0.3);

            ////定在波LM
            const double radius = 20;
            const int size = 50;
            var center = new Vector3d(x, y, z);
            var stm = autd.STM();
            for (var i = 0; i < size; i++)
            {
                var theta = 2 * AUTD.Pi * i / size;
                var r = new Vector3d(Math.Cos(theta), Math.Sin(theta),0);
                var f = Gain.FocalPoint(center + radius * r);
                stm.AddSTMGain(f);
            }
            stm.StartSTM(1);



            sw_autd.Start();
            sw_thermo.Start();

            long t0 = 0;

            long t = 0;
            long delta_time = 0;

            double T0 = 0;

            double T = 0;
            double delta_T = 0;

            ThermalPaletteImage images;

            long radiatingTime = 100;
            long intervalTime = 2000;

            int x_T = 133;
            int y_T = 183;

            byte amplitude = 255;

            byte ampStep = 5;

            var array_T0 = new double[288, 382];

            int trial_times = 0;

            //var directoryName = "exp/" + DateTime.Now.ToString("yyyy_MM_dd_HH");
            var directoryName = "exp/thermochromizm/" + DateTime.Now.ToString("yyyy_MM_dd_HH");


            if (!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);

            while (_grabImage)
            {
                try
                {
                    images = _irDirectInterface.ThermalPaletteImage;
                    PaletteImage.Value = images.PaletteImage;


                    if (trial_times >= 100)
                    {
                        amplitude += ampStep;
                        trial_times = 0;
                    }

                    //ToDo: 余計なものを削る
                    //T = ConvertToTemp(images.ThermalImage[x_T, y_T]);

                    //t = sw_autd.ElapsedMilliseconds;

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

                        //T0 = ConvertToTemp(images.ThermalImage[x_T, y_T]);
                        t0 = sw_autd.ElapsedMilliseconds;
                        //gain = Gain.FocalPoint(focalPoint, amplitude);
                        //autd.Send(gain,mod);
                        //isNotAppendedGain = false;
                    }
                    else if (sw_autd.ElapsedMilliseconds > intervalTime + radiatingTime)
                    {
                        autd.Stop();
                        sw_autd.Restart();
                        isNotAppendedGain = true;
                    }

                    if (!isNotAppendedGain)
                    {
                        //var maxTemp = GetMaxTemperatuer(images);//全体の最高温度の点の座標を見たいとき

                        //delta_T = ConvertToTemp(images.ThermalImage[x_T, y_T]) - T0;
                        
                        delta_time = sw_autd.ElapsedMilliseconds - t0;

                        var sb = new StringBuilder();
                        for (var i = 0; i < images.ThermalImage.GetLength(0); i++)
                        {
                            for (var j = 0; j < images.ThermalImage.GetLength(1); j++)
                            {
                                if (j != 0) sb.Append(",");
                                delta_T = ConvertToTemp(images.ThermalImage[i, j]) - array_T0[i, j];
                               
                                sb.Append((delta_T * 1000) / delta_time); //T'を求める
                                //sb.Append(delta_T); //dTを求める
                            }
                            sb.AppendLine();
                        }


                        if (delta_time > 450)
                        {
                            using var sw = new StreamWriter(directoryName + "/" + "duty" + amplitude
                                + "_trial" + trial_times.ToString() + "_t" + delta_time + ".csv");

                            sw.Write(sb.ToString());

                            trial_times++;
                        }
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
