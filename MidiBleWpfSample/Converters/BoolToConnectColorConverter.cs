using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MidiBleWpfSample.Converters
{
    public class BoolToConnectColorConverter : IValueConverter
    {
        // 接続中なら赤、未接続なら緑を返す
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool connected && connected)
                return Brushes.Red;
            return Brushes.Green;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
