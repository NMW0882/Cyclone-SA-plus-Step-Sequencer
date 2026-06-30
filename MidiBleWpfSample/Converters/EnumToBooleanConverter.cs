using System;
using System.Globalization;
using System.Windows.Data;

namespace MidiBleWpfSample.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string enumValue = value.ToString() ?? string.Empty;
            string targetValue = parameter.ToString() ?? string.Empty;

            return enumValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool useValue || !useValue || parameter == null)
            {
                return Binding.DoNothing;
            }

            string parameterString = parameter.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(parameterString))
            {
                return Binding.DoNothing;
            }

            try
            {
                return Enum.Parse(targetType, parameterString, true);
            }
            catch (ArgumentException)
            {
                return Binding.DoNothing;
            }
        }
    }
}
