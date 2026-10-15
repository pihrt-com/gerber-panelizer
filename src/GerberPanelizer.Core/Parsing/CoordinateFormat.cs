using System.Globalization;

namespace GerberPanelizer.Core.Parsing;

public enum CoordinateUnit { Millimetres, Inches }

public sealed record CoordinateFormat(int IntegerDigits, int DecimalDigits, bool TrailingZeroSuppression, CoordinateUnit Unit)
{
    public static CoordinateFormat GerberDefault { get; } = new(2, 4, true, CoordinateUnit.Inches);
    public static CoordinateFormat ExcellonMetricDefault { get; } = new(3, 3, true, CoordinateUnit.Millimetres);

    public double Parse(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new FormatException("A coordinate token cannot be empty.");
        }

        var negative = token[0] == '-';
        var digits = negative || token[0] == '+' ? token[1..] : token;
        double value;
        if (digits.Contains('.', StringComparison.Ordinal))
        {
            value = double.Parse(digits, CultureInfo.InvariantCulture);
        }
        else
        {
            if (!digits.All(char.IsDigit))
            {
                throw new FormatException($"Invalid coordinate token '{token}'.");
            }

            var padded = TrailingZeroSuppression
                ? digits.PadLeft(IntegerDigits + DecimalDigits, '0')
                : digits.PadRight(IntegerDigits + DecimalDigits, '0');
            value = long.Parse(padded, CultureInfo.InvariantCulture) / Math.Pow(10, DecimalDigits);
        }

        if (negative) value = -value;
        return Unit == CoordinateUnit.Inches ? value * 25.4 : value;
    }

    public string FormatMillimetres(double millimetres)
    {
        var inUnit = Unit == CoordinateUnit.Inches ? millimetres / 25.4 : millimetres;
        var scale = Math.Pow(10, DecimalDigits);
        var scaled = (long)Math.Round(inUnit * scale, MidpointRounding.AwayFromZero);
        var sign = scaled < 0 ? "-" : string.Empty;
        var digits = Math.Abs(scaled).ToString(CultureInfo.InvariantCulture).PadLeft(IntegerDigits + DecimalDigits, '0');
        return sign + digits;
    }
}
