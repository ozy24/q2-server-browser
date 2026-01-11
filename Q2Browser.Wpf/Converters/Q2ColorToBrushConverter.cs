using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Q2Browser.Core.Protocol;

namespace Q2Browser.Wpf.Converters;

public class Q2ColorToBrushConverter : IValueConverter
{
    private static readonly Dictionary<char, Brush> ColorBrushes = new()
    {
        ['0'] = new SolidColorBrush(Color.FromRgb(0, 0, 0)),       // Black
        ['1'] = new SolidColorBrush(Color.FromRgb(255, 0, 0)),     // Red
        ['2'] = new SolidColorBrush(Color.FromRgb(0, 255, 0)),     // Green
        ['3'] = new SolidColorBrush(Color.FromRgb(255, 255, 0)),  // Yellow
        ['4'] = new SolidColorBrush(Color.FromRgb(0, 0, 255)),    // Blue
        ['5'] = new SolidColorBrush(Color.FromRgb(0, 255, 255)),  // Cyan
        ['6'] = new SolidColorBrush(Color.FromRgb(255, 0, 255)),  // Magenta
        ['7'] = new SolidColorBrush(Color.FromRgb(255, 255, 255)), // White
        ['8'] = new SolidColorBrush(Color.FromRgb(128, 128, 128)), // Gray
        ['9'] = new SolidColorBrush(Color.FromRgb(255, 128, 128)), // Light Red
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text) return value;
        
        // For now, just strip color codes. Full color rendering would require a custom TextBlock
        return Q2ColorParser.StripColorCodes(text);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

