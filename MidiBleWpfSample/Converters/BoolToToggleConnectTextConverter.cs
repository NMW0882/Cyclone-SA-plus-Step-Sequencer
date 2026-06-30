using System;
using System.Globalization;
using System.Windows.Data;

namespace MidiBleWpfSample.Converters
{
    public class BoolToToggleConnectTextConverter : IValueConverter
    {
        // 接続中なら "DISCONNECT❌️"、未接続なら "CONNECT✅️" を返す
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isConnected)
            {
                return isConnected ? "DISCONNECT❌️" : "CONNECT✅️";
            }
            return "CONNECT";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
