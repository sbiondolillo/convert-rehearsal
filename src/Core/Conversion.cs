using System.Globalization;

namespace Core;

/// <summary>The arithmetic and the format of a converted amount.</summary>
public static class Conversion
{
    /// <summary>The amount at the rate, rounded to two places, away from zero.</summary>
    public static decimal Convert(decimal amount, decimal rate) =>
        Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);

    /// <summary>The converted amount with two places, then the code in upper case, whatever the locale.</summary>
    public static string Format(decimal amount, decimal rate, string code) =>
        $"{Convert(amount, rate).ToString("F2", CultureInfo.InvariantCulture)} {code.ToUpperInvariant()}";
}
