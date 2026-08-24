using System;
using System.Globalization;
using System.Windows.Data;

namespace GameRoutines
{
    public static class CadenceDisplay
    {
        public static string GetName(object cadence)
        {
            if ((cadence is ResetCadence reset && reset == ResetCadence.BiWeekly) ||
                (cadence is ReminderCadence reminder && reminder == ReminderCadence.BiWeekly))
            {
                return "Biweekly";
            }

            return cadence?.ToString() ?? string.Empty;
        }
    }

    public sealed class CadenceDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return CadenceDisplay.GetName(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
