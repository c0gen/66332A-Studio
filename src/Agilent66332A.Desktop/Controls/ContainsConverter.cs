using System.Globalization;
using System.Windows.Data;

namespace Agilent66332A.Desktop.Controls;

public sealed class ContainsConverter : IValueConverter
{
    public bool MatchStart { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text || parameter is not string pattern) return false;
        return MatchStart
            ? text.StartsWith(pattern, StringComparison.Ordinal)
            : text.Contains(pattern, StringComparison.Ordinal);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
