/*
 * File: ThermalPaletteImage.cs
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