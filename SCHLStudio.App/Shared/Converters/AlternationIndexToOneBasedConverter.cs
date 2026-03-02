using System;
using System.Globalization;
using System.Windows.Data;

namespace SCHLStudio.App.Shared.Converters
{
    public sealed class AlternationIndexToOneBasedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 1;

            try
            {
                var index = System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return index + 1;
            }
            catch
            {
                return 1;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
