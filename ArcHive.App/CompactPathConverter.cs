using System.Globalization;
using System.Windows.Data;
using ArcHive.Core;
namespace ArcHive.App;
public sealed class CompactPathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => DisplayPaths.Compact(value as string);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
