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
        #region fields

        private static IrDirectInterface _instance;
        private bool _isAutomaticShutterActive;

        #endregion

        #region ctor

        private IrDirectInterface() { }

        ~IrDirectInterface()
        {
            Disconnect();
        }

        #endregion

        #region properties

        public static IrDirectInterface Instance => _instance ??= new IrDirectInterface();

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

                var thermalImage = new float[thermalHeight, thermalWidth];
                var paletteImage = new Bitmap(paletteWidth, paletteHeight, pixelFormat);
                var rect = new Rectangle(0, 0, paletteImage.Width, paletteImage.Height);
                var data = paletteImage.LockBits(rect, ImageLockMode.ReadWrite, paletteImage.PixelFormat);
                CheckResult(NativeMethods.evo_irimager_get_thermal_palette_image(thermalWidth, thermalHeight, thermalImage, paletteImage.Width, paletteImage.Height, data.Scan0));
                paletteImage.UnlockBits(data);

                return new ThermalPaletteImage(thermalImage, paletteImage);
            }
        }

        public Bitmap PaletteImage
        {
            get
            {
                const PixelFormat pixelFormat = PixelFormat.Format24bppRgb;
                CheckConnectionState();
                CheckResult(NativeMethods.evo_irimager_get_palette_image_size(out var width, out var height));
                var image = new Bitmap(width, height, pixelFormat);
                var rect = new Rectangle(0, 0, image.Width, image.Height);
                var data = image.LockBits(rect, ImageLockMode.ReadWrite, image.PixelFormat);
                CheckResult(NativeMethods.evo_irimager_get_palette_image(out width, out height, data.Scan0));
                image.UnlockBits(data);
                return image;
            }
        }
        #endregion

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

        public ushort[,] GetThermalImage()
        {
            CheckConnectionState();
            CheckResult(NativeMethods.evo_irimager_get_thermal_image_size(out var width, out var height));
            var buffer = new ushort[height, width];
            CheckResult(NativeMethods.evo_irimager_get_thermal_image(out _, out _, buffer));
            return buffer;
        }

        public void SetPaletteFormat(OptrisColoringPalette format, OptrisPaletteScalingMethod scale)
        {
            CheckConnectionState();
            CheckResult(NativeMethods.evo_irimager_set_palette((int)format));
            CheckResult(NativeMethods.evo_irimager_set_palette_scale((int)scale));
        }

        public void SetTemperatureRange(int min, int max)
        {
            CheckConnectionState();
            CheckResult(NativeMethods.evo_irimager_set_temperature_range(min, max));
        }

        public void TriggerShutter()
        {
            CheckConnectionState();
            CheckResult(NativeMethods.evo_irimager_trigger_shutter_flag());
        }

        public void SetRadiationParameters(float emissivity, float transmissivity)
        {
            SetRadiationParameters(emissivity, transmissivity, -999.0f);
        }

        public void SetRadiationParameters(float emissivity, float transmissivity, float ambient)
        {
            if (emissivity < 0 || 1 < emissivity) throw new ArgumentOutOfRangeException(nameof(emissivity), "Valid range is 0..1");
            if (transmissivity < 0 || 1 < transmissivity) throw new ArgumentOutOfRangeException(nameof(transmissivity), "Valid range is 0..1");

            CheckConnectionState();
            CheckResult(NativeMethods.evo_irimager_set_radiation_parameters(emissivity, transmissivity, ambient));
        }

        private static void CheckResult(int result)
        {
            if (result < 0) throw new IOException($"Internal camera error: {result}");
        }

        private void CheckConnectionState()
        {
            if (!IsConnected) throw new IOException($"Camera is disconnected. Please connect first.");
        }
    }
}