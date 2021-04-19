/*
 * File: IrDirectInterface.cs
 * Project: libirimagerNet
 * Created Date: 18/04/2021
 * Author: Shun Suzuki
 * -----
 * Last Modified: 19/04/2021
 * Modified By: Shun Suzuki (suzuki@hapis.k.u-tokyo.ac.jp)
 * -----
 * Copyright (c) 2021 Hapis Lab. All rights reserved.
 * 
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace libirimagerNet
{
    public enum OptrisColoringPalette
    {
        None = 0,
        AlarmBlue = 1,
        AlarmBlueHi = 2,
        GrayBw = 3,
        GrayWb = 4,
        AlarmGreen = 5,
        Iron = 6,
        IronHi = 7,
        Medical = 8,
        Rainbow = 9,
        RainbowHi = 10,
        AlarmRed = 11
    }

    public enum OptrisPaletteScalingMethod
    {
        None = 0,
        Manual = 1,
        MinMax = 2,
        Sigma1 = 3,
        Sigma3 = 4
    }

    public class IrDirectInterface
    {
        private bool _isAutomaticShutterActive;

        private IrDirectInterface() { }

        ~IrDirectInterface()
        {
            Disconnect();
        }

        private static readonly Lazy<IrDirectInterface> Lazy = new Lazy<IrDirectInterface>(() => new IrDirectInterface());
        public static IrDirectInterface Instance => Lazy.Value;

        public bool IsAutomaticShutterActive
        {
            get => _isAutomaticShutterActive;
            set
            {
                if (_isAutomaticShutterActive == value) return;
                _isAutomaticShutterActive = value;
                CheckResult(NativeMethods.evo_irimager_set_shutter_mode(value ? 1 : 0));
            }
        }

        public bool IsConnected { get; private set; }

        public ThermalPaletteImage ThermalPaletteImage
        {
            get
            {
                CheckConnectionState();

                const PixelFormat pixelFormat = PixelFormat.Format24bppRgb;

                CheckResult(NativeMethods.evo_irimager_get_palette_image_size(out var paletteWidth, out var paletteHeight));
                CheckResult(NativeMethods.evo_irimager_get_thermal_image_size(out var thermalWidth, out var thermalHeight));

                var thermalImage = new ushort[thermalHeight, thermalWidth];
                var paletteImage = new Bitmap(paletteWidth, paletteHeight, pixelFormat);
                var rect = new Rectangle(0, 0, paletteImage.Width, paletteImage.Height);
                var data = paletteImage.LockBits(rect, ImageLockMode.ReadWrite, paletteImage.PixelFormat);
                CheckResult(NativeMethods.evo_irimager_get_thermal_palette_image(thermalWidth, thermalHeight, thermalImage, paletteImage.Width, paletteImage.Height, data.Scan0));
                paletteImage.UnlockBits(data);

                return new ThermalPaletteImage(thermalImage, paletteImage);
            }
        }

        public void Connect(string xmlConfigPath)
        {
            if (!File.Exists(xmlConfigPath)) throw new ArgumentException("XML Config file doesn't exist: " + xmlConfigPath, nameof(xmlConfigPath));

            var error = NativeMethods.evo_irimager_usb_init(xmlConfigPath, "", "");
            if (error < 0) throw new IOException($"Error at camera init: {error}");

            IsConnected = true;
            IsAutomaticShutterActive = true;
        }

        public void Disconnect()
        {
            if (!IsConnected) return;
            CheckResult(NativeMethods.evo_irimager_terminate());
            IsConnected = false;
        }


        public void SetPaletteFormat(OptrisColoringPalette format, OptrisPaletteScalingMethod scale)
        {
            CheckConnectionState();
            CheckResult(NativeMethods.evo_irimager_set_palette((int)format));
            CheckResult(NativeMethods.evo_irimager_set_palette_scale((int)scale));
        }

        private static void CheckResult(int result)
        {
            if (result < 0) throw new IOException($"Internal camera error: {result}");
        }

        private void CheckConnectionState()
        {
            if (!IsConnected) throw new IOException("Camera is disconnected. Please connect first.");
        }
    }
}