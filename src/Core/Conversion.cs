using System.Globalization;

namespace Core;

/// <summary>The arithmetic and the text of a conversion.</summary>
public static class Conversion
{
    /// <summary>The amount times the rate, rounded to two decimals and written with the upper-case code.</summary>
    public static string Format(decimal amount, decimal rate, string code) =>
        $"{Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture)} {code.ToUpperInvariant()}";
}
