using System.Drawing;

namespace libirimagerNet
{
    public class ThermalPaletteImage
    {
        public ThermalPaletteImage(ushort[,] thermalImage, Bitmap paletteImage)
        {
            ThermalImage = thermalImage;
            PaletteImage = paletteImage;
        }

        public ushort[,] ThermalImage { get; }

        public Bitmap PaletteImage { get; }
    }
}