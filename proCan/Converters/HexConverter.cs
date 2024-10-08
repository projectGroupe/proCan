using System;
using System.Globalization;
using System.Windows.Data;

namespace CanTraceDecoder.Converters
{
    public class HexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                // Formatierung der CAN-ID als Hexadezimal mit führenden Nullen (4 Stellen)
                return $"0x{intValue:X}";
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Optional: Implementieren Sie dies bei Bedarf
            throw new NotImplementedException();
        }
    }
}