using System.Text;
using System.Text.RegularExpressions;

namespace Q2Browser.Core.Protocol;

public static class Q2ColorParser
{
    private static readonly Regex ColorCodeRegex = new(@"\^[0-9]", RegexOptions.Compiled);

    public static string StripColorCodes(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return ColorCodeRegex.Replace(input, string.Empty);
    }

    public static string ConvertToHtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var colors = new Dictionary<char, string>
        {
            ['0'] = "#000000", // Black
            ['1'] = "#FF0000", // Red
            ['2'] = "#00FF00", // Green
            ['3'] = "#FFFF00", // Yellow
            ['4'] = "#0000FF", // Blue
            ['5'] = "#00FFFF", // Cyan
            ['6'] = "#FF00FF", // Magenta
            ['7'] = "#FFFFFF", // White
            ['8'] = "#808080", // Gray
            ['9'] = "#FF8080", // Light Red
        };

        var result = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '^' && i + 1 < input.Length && char.IsDigit(input[i + 1]))
            {
                var colorCode = input[i + 1];
                if (colors.TryGetValue(colorCode, out var color))
                {
                    result.Append($"<span style=\"color: {color}\">");
                }
                i++; // Skip the digit
            }
            else
            {
                result.Append(input[i]);
            }
        }

        return result.ToString();
    }
}

