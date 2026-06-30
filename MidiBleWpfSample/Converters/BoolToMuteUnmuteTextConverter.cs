using System;
using System.Globalization;
using System.Windows.Data;

namespace MidiBleWpfSample.Converters
{
    public class BoolToMuteUnmuteTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMuted) // isPausedからisMutedに意味合いを変更
            {
                // isMutedがtrue (停止中) なら "Unmute" を、false (動作中) なら "Mute" を返す
                return isMuted ? "Unmute (*)" : "Mute (*)";
            }
            return "Mute (*)";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}