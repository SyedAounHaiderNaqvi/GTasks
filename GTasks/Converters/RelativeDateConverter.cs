using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;
using System.Globalization;

namespace GTasks.Converters
{
    internal class RelativeDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return "";

            DateTime dateTime;

            if (value is DateTime dt)
            {
                dateTime = dt;
            }
            else if (value is DateTimeOffset dto)
            {
                dateTime = dto.LocalDateTime;
            }
            else if (DateTime.TryParse(
                value.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                dateTime = parsed.ToLocalTime();
            }
            else
            {
                return "";
            }

            // If modified today, show time
            if (dateTime.Date == DateTime.Now.Date)
            {
                return dateTime.ToString("h:mm tt");
            }

            // Otherwise show month + day
            return dateTime.ToString("MMM d");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException(); // not needed
        }
    }
}
