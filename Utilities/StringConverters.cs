using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace CatchCapture.Utilities
{
    public class FirstLineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                // return the first line
                var firstLine = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).FirstOrDefault();
                return firstLine ?? string.Empty;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
