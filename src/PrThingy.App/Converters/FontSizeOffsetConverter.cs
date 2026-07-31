using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace PrThingy.App.Converters;

public sealed class FontSizeOffsetConverter : IValueConverter
{
    private const double MIN_RESULT_FONT_SIZE = 1;

    public static readonly FontSizeOffsetConverter Instance = new FontSizeOffsetConverter();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double baseFontSize)
            return AvaloniaProperty.UnsetValue;

        double offset = parameter switch
        {
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
            double numeric => numeric,
            _ => 0
        };

        return Math.Max(MIN_RESULT_FONT_SIZE, baseFontSize + offset);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
