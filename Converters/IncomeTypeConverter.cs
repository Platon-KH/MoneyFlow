using System;
using System.Globalization;
using System.Windows.Data;

namespace MoneyFlowWPF.Converters
{
    public class IncomeTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isIncome)
            {
                return isIncome ? "Доход" : "Расход";
            }
            return "Неизвестно";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}