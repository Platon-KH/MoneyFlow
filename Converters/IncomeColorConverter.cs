using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MoneyFlowWPF.Converters
{
    public class IncomeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isIncome)
            {
                return isIncome ? Brushes.Green : Brushes.Red;
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
