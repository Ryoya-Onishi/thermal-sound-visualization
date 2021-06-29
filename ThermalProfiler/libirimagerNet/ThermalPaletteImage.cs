using System.Drawing;

namespace libirimagerNet
{
    public class ThermalPaletteImage
    {
        public ThermalPaletteImage(float[,] thermalImage, Bitmap paletteImage)
        {
            ThermalImage = thermalImage;
            PaletteImage = paletteImage;
        }

        public float[,] ThermalImage { get; }

        public Bitmap PaletteImage { get; }
    }
}