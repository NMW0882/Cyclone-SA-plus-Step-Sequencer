using System;
using System.Globalization;
using System.Windows.Data;

namespace MidiBleWpfSample.Converters
{
    public class BoolToConnectTextConverter : IValueConverter
    {
        // 接続中なら "Disconnect"、未接続なら "Connect" を返す
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool connected && connected)
                return "Disconnect";
            return "Connect";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
