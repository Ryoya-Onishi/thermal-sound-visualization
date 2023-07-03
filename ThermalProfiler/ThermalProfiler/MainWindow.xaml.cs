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
using AUTD3Sharp.Link;
using AUTD3Sharp.Utils;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
using AUTD3Sharp.STM;
using libirimagerNet;
using MaterialDesignThemes.Wpf;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Reactive.Linq;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using ThermalProfiler.Domain;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

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
        private bool isNotAppendedGain = true;
        private Stopwatch sw_all = new Stopwatch();
        private Stopwatch sw_autd = new Stopwatch();
        private Stopwatch sw_thermo = new Stopwatch();

        private async Task ImageGrabberMethod()
        {
            var autd = new Controller();
            autd.Geometry.AddDevice(Vector3d.zero, Vector3d.zero);
            autd.Geometry.AddDevice(new Vector3d(0, 151, 0), Vector3d.zero);
            autd.Geometry.AddDevice(new Vector3d(192, 0, 0), Vector3d.zero);
            autd.Geometry.AddDevice(new Vector3d(192, 151, 0), Vector3d.zero);
            autd.Geometry.AddDevice(new Vector3d(384, 0, 0), Vector3d.zero);
            autd.Geometry.AddDevice(new Vector3d(384, 151, 0), Vector3d.zero);

            var onLost = new SOEM.OnLostCallbackDelegate((string msg) =>
            {
                Console.WriteLine($"Unrecoverable error occurred: {msg}");
                Environment.Exit(-1);
            });

            var link = new SOEM().HighPrecision(true).OnLost(onLost).FreeRun(true).Build();
            if (!autd.Open(link))
            {
                Console.WriteLine("Failed to open Controller.");
                return;
            }

            autd.AckCheckTimeoutMs = 20;

            autd.Send(new Clear());
            autd.Send(new Synchronize());

            var firmList = autd.FirmwareInfoList().ToArray();
            Console.WriteLine("==================================== Firmware information ======================================");
            foreach (var firm in firmList)
                Console.WriteLine($"{firm}");
            Console.WriteLine("================================================================================================");

            var config = new SilencerConfig();
            autd.Send(config);

            const double x_center = 272, y_center = 97, z_center = 224;
            const double x_range = 0, y_range = 0, z_range = 0;
            double x = x_center - x_range, y = y_center - y_range, z = z_center - z_range;
            double x_step = 2, y_step = 0.5;

            var focalPoint = new Vector3d(x, y, z);

            //---------------　↓ 実験パラメータ  -----------
            // 1. 名前
            var name = "iwabuchi";
            
            // 2.1 ST -------------------
            //var mod_name = "静圧";
            //var mod = new Static(1);

            //// 2.2 AM -----------
            //var mod_name = "AM30";
            //var mod = new Sine(30);
            //var mod_name = "AM200";
            //var mod = new Sine(200);

            //// 2.3 STM ---------
            var mod_name = "STM";
            var mod = new Static(1);
            var lengthList = new List<double> { 50 }; // mm
            var velocityList = new List<double> { 10, 20, 30, 500 }; // mm/s

            //// 2.3 LM ---------
            //var mod_name = "LM";
            //var mod = new Static(1);
            //var perimeterList = new List<double> { 3,5,7 }; // cm
            //var velocityList = new List<double> { 0.01, 0.1, 1,}; // m/s

            // 3. 照射時間
            long radiatingTime = 1000;
            long intervalTime = 1000;   //シリコン用

            // 5. 試行回数
            int trial_times = 0;
            int max_trial_times = 2;

            // 4. 振幅
            float ampStep = 0.5f;
            float ampStart = 1f;
            float ampFinish = 0.5f;

            // 4. ファイルネーム
            var directoryName = @"I:/iwabuchi_data/whc_thermo_data/指_振幅変化/実験結果/" + name + "/" + mod_name; // + DateTime.Now.ToString("yyyy_MM_dd_HH_mm")

            //------------ ↑ 実験パラメータ-------------------------

            var gain = new Focus(focalPoint);
            float amplitude = ampStart;

            var length_velocity = new List<(double, double)>();
            int lengthVerocityID = 0;

            foreach (var length in lengthList)
                foreach (var velocity in velocityList)
                    length_velocity.Add((length, velocity));

            var perimeter_velocity = new List<(double, double)>();
            int perimeterVerocityID = 0;

            //foreach (var perimeter in perimeterList)
            //    foreach (var velocity in velocityList)
            //        perimeter_velocity.Add((perimeter, velocity));

            sw_all.Start(); sw_autd.Start(); sw_thermo.Start(); 

            long t0 = 0;
            long delta_time = 0;
            double delta_T = 0;

            ThermalPaletteImage images;

            var array_T0 = new double[288, 382];

            var evalCSVPath = directoryName + "/" + "evaluation.csv";

            int seed = Environment.TickCount;
            Random rnd = new Random(seed);
            //Console.WriteLine("{0:F1}, ", rnd.Next(0,2));


            if (!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);

            while (_grabImage)
            {
                try
                {
                    images = _irDirectInterface.ThermalPaletteImage;
                    PaletteImage.Value = images.PaletteImage;

                    if (sw_autd.ElapsedMilliseconds > intervalTime && isNotAppendedGain)
                    {
                        array_T0 = new double[images.ThermalImage.GetLength(0), images.ThermalImage.GetLength(1)]; ;

                        for (var i = 0; i < images.ThermalImage.GetLength(0); i++)
                            for (var j = 0; j < images.ThermalImage.GetLength(1); j++)
                                array_T0[i, j] = ConvertToTemp(images.ThermalImage[i, j]);

                        t0 = sw_autd.ElapsedMilliseconds;

                        if(mod_name == "静圧" || mod_name =="AM30" || mod_name == "AM200")
                        {
                            gain = new Focus(new Vector3d(x, y, z), amplitude);
                            autd.Send(mod, gain);
                        }
                        else if(mod_name == "STM" || mod_name == "LM")
                        {
                            autd.Send(mod);
                            var center = new Vector3d(x, y, z);
                            Console.WriteLine(x+ "," + y + "" + z);
                            var stm = new FocusSTM(autd.SoundSpeed);
                            stm = Set_STM(stm, center, length_velocity[lengthVerocityID].Item1,
                                length_velocity[lengthVerocityID].Item2, mod_name);
                            autd.Send(stm);
                            Console.WriteLine("C = " + length_velocity[lengthVerocityID].Item1 + ", v = " + length_velocity[lengthVerocityID].Item2);
                        }

                        isNotAppendedGain = false;

                    }
                    else if (sw_autd.ElapsedMilliseconds > intervalTime + radiatingTime)
                    {
                        autd.Send(new Stop());

                        autd.Send(new Clear());
                        autd.Send(new Synchronize());

                        sw_autd.Restart();
                        isNotAppendedGain = true;
                        trial_times += 1;

                        Console.WriteLine(amplitude + "," + trial_times);

                        if (trial_times < max_trial_times)
                            continue;
                        else
                        {
                            lengthVerocityID += 1;
                            trial_times = 0;
                            if (lengthVerocityID < length_velocity.Count && mod_name == "STM")
                                continue;
                            else
                            {
                                amplitude -= ampStep;
                                lengthVerocityID = 0;

                                if (Math.Round(amplitude, 1) < ampFinish)
                                {
                                    Console.WriteLine("All done");
                                    break;
                                }
                            }
                        }
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
                                sb.Append(ConvertToTemp(images.ThermalImage[i, j]));
                            }
                            sb.AppendLine();
                        }

                        if (true)
                        {
                            var _directoryName = "";

                            if (mod_name == "静圧" || mod_name == "AM30" || mod_name == "AM200")
                            {
                                _directoryName = "amp_" + Math.Round(amplitude, 2) + "/step_" + trial_times + "_x_" + x;
                            }
                            else if (mod_name == "STM")
                            {
                                _directoryName = "amp_" + Math.Round(amplitude, 1) + "/step_" + trial_times + "_x_" + x + "/length_velocity_"
                                + length_velocity[lengthVerocityID].Item1 + "_" + length_velocity[lengthVerocityID].Item2;
                            }

                            if (!Directory.Exists(directoryName + "/" + _directoryName)) Directory.CreateDirectory(directoryName + "/" + _directoryName);

                            using var sw = new StreamWriter(directoryName + "/" + _directoryName + "/" + sw_all.ElapsedMilliseconds + "_" + (1 - Convert.ToInt32(isNotAppendedGain)) + ".csv");  //照射中か否か

                            sw.Write(sb.ToString());
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

        private FocusSTM Set_STM(FocusSTM stm, Vector3d center, double length, double velocity ,string mod_name)
        {
            const int pointNum = 200;
            for (var i = 0; i < pointNum; i++)
            {
                double d_i = i / (double)pointNum;
                var p = length * new Vector3d(d_i, 0, 0);    
                stm.Add(center + p);
            }
            stm.Frequency = velocity / length;
            Console.WriteLine(stm);
            return stm;
        }
        private FocusSTM Set_LM(FocusSTM stm, Vector3d center, double perimeter, double velocity, string mod_name)
        {
            const int pointNum = 200;
            for (var i = 0; i < pointNum; i++)
            {
                double radius = 10 * perimeter / (2 * Math.PI); // cmなので、10をかけてmmに直す
                var theta = 2.0 * Math.PI * i / pointNum;
                var p = radius * new Vector3d(Math.Cos(theta), Math.Sin(theta), 0);
                stm.Add(center + p);
            }
            stm.Frequency = 1000 * velocity / (perimeter * 10); // m/sなので1000かけてmm/sに、perimeterはcmなので10かけてmmに
            
            return stm;
        }

        public static double ConvertToTemp(ushort data)
        {
            return (data - 1000.0) / 10.0;
        }


    }
}
