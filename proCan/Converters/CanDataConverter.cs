using System;
using System.Globalization;
using System.Windows.Data;

namespace CanTraceDecoder.Converters
{
    public class CanDataConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte[] ArrayValue)
            {
                var formattedBytes = ArrayValue.Select(b => b.ToString("X2"));
                // Formatierung der CAN-ID als Hexadezimal mit führenden Nullen (4 Stellen)
                return string.Join(" ", formattedBytes);
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
