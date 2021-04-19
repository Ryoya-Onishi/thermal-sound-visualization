/*
 * File: FloatToStringConverter.cs
 * Project: Converter
 * Created Date: 19/04/2021
 * Author: Shun Suzuki
 * -----
 * Last Modified: 19/04/2021
 * Modified By: Shun Suzuki (suzuki@hapis.k.u-tokyo.ac.jp)
 * -----
 * Copyright (c) 2021 Hapis Lab. All rights reserved.
 * 
 */

using System;
using System.Globalization;
using System.Windows.Data;

namespace ThermalProfiler.Converter
{
    internal class FloatToStringConverter : IValueConverter
    {
        private string? _currentString;

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is float v)) return null;

            if (string.IsNullOrEmpty(_currentString)) return v.ToString(culture);

            string format;
            if (_currentString.EndsWith(culture.NumberFormat.NumberDecimalSeparator))
            {
                format = @$"{new string('0', _currentString.Length - 1)}\{culture.NumberFormat.NumberDecimalSeparator}";
            }
            else
            {
                var pos = _currentString.IndexOf(culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);
                if (pos < 0) return v.ToString(culture);
                var digitLength = _currentString.Length - pos - 1;
                format = @$"{new string('0', pos)}.{new string('0', digitLength)}";
            }
            return v.ToString(format);
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            _currentString = value as string;
            if (!string.IsNullOrEmpty(_currentString) && float.TryParse(_currentString, out var v)) return v;
            return null;
        }
    }
}