using System.Globalization;
using System.Text.RegularExpressions;
using GerberPanelizer.Core.Geometry;

namespace GerberPanelizer.Core.Parsing;

public sealed class GerberParser
{
    private static readonly Regex FormatRegex = new(@"%FS[LT][AI]X(?<xi>\d)(?<xd>\d)Y(?<yi>\d)(?<yd>\d)\*%", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ApertureRegex = new(@"%ADD(?<code>\d+)(?<shape>[A-Z]),?(?<parameters>[^*%]*)\*%", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CoordinateRegex = new(@"(?<axis>[XY])(?<value>[+-]?\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OperationRegex = new(@"D(?<code>0?[123])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ApertureSelectRegex = new(@"^D(?<code>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public GerberDocument Parse(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var warnings = new List<string>();
        var format = ReadFormat(content, warnings);
        var apertures = ReadApertures(content, format, warnings);
        var operations = new List<GerberOperation>();
        var current = new PcbPoint(0, 0);
        var selectedAperture = 0;
        var interpolation = GerberOperationType.Move;
        var bounds = PcbBounds.Empty;
        var maximumApertureDiameter = 0d;

        var commandContent = Regex.Replace(content, "%.*?%", string.Empty, RegexOptions.Singleline);
        foreach (var rawToken in commandContent.Replace("\r", string.Empty).Split('*', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = rawToken.Trim();
            if (token.Length == 0 || token.StartsWith("G04", StringComparison.OrdinalIgnoreCase)) continue;
            var apertureSelection = ApertureSelectRegex.Match(token);
            if (apertureSelection.Success && int.TryParse(apertureSelection.Groups["code"].Value, out var code) && code >= 10)
            {
                selectedAperture = code;
                continue;
            }

            var operationMatch = OperationRegex.Match(token);
            if (operationMatch.Success)
            {
                interpolation = operationMatch.Groups["code"].Value.TrimStart('0') switch
                {
                    "1" => GerberOperationType.Draw,
                    "2" => GerberOperationType.Move,
                    "3" => GerberOperationType.Flash,
                    _ => interpolation,
                };
            }

            if (!token.Contains('X') && !token.Contains('Y')) continue;
            var next = ReadPoint(token, current, format, warnings);
            var diameter = apertures.TryGetValue(selectedAperture, out var aperture) ? aperture : 0;
            operations.Add(new GerberOperation(interpolation, current, next, diameter));
            if (interpolation == GerberOperationType.Draw)
            {
                bounds = bounds.Include(current).Include(next);
                maximumApertureDiameter = Math.Max(maximumApertureDiameter, diameter);
            }
            else if (interpolation == GerberOperationType.Flash)
            {
                bounds = bounds.Include(next);
                maximumApertureDiameter = Math.Max(maximumApertureDiameter, diameter);
            }

            current = next;
        }

        if (bounds.IsEmpty && operations.Count > 0)
        {
            bounds = operations.Aggregate(PcbBounds.Empty, (currentBounds, operation) => currentBounds.Include(operation.End));
            warnings.Add("No drawing or flash operation was found; bounds are based on coordinates only.");
        }
        else if (!bounds.IsEmpty && maximumApertureDiameter > 0)
        {
            bounds = bounds.Inflate(maximumApertureDiameter / 2);
        }

        return new GerberDocument(format, bounds, operations, warnings);
    }

    private static CoordinateFormat ReadFormat(string content, ICollection<string> warnings)
    {
        var unit = content.Contains("%MOMM*%", StringComparison.OrdinalIgnoreCase) ? CoordinateUnit.Millimetres : CoordinateUnit.Inches;
        var match = FormatRegex.Match(content);
        if (!match.Success)
        {
            warnings.Add("No Gerber coordinate format statement was found; using 2.4 inch coordinates.");
            return CoordinateFormat.GerberDefault with { Unit = unit };
        }

        if (match.Groups["xi"].Value != match.Groups["yi"].Value || match.Groups["xd"].Value != match.Groups["yd"].Value)
        {
            warnings.Add("Independent X/Y coordinate formats are not supported; X format is used for both axes.");
        }

        return new CoordinateFormat(int.Parse(match.Groups["xi"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["xd"].Value, CultureInfo.InvariantCulture), true, unit);
    }

    private static Dictionary<int, double> ReadApertures(string content, CoordinateFormat format, ICollection<string> warnings)
    {
        var apertures = new Dictionary<int, double>();
        foreach (Match match in ApertureRegex.Matches(content))
        {
            if (match.Groups["shape"].Value.Equals("C", StringComparison.OrdinalIgnoreCase) && double.TryParse(match.Groups["parameters"].Value.Split('X')[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                apertures[int.Parse(match.Groups["code"].Value, CultureInfo.InvariantCulture)] = format.Unit == CoordinateUnit.Inches ? value * 25.4 : value;
            }
            else
            {
                warnings.Add($"Aperture D{match.Groups["code"].Value} is not circular; its stroke width is approximated as zero.");
            }
        }

        return apertures;
    }

    private static PcbPoint ReadPoint(string token, PcbPoint previous, CoordinateFormat format, ICollection<string> warnings)
    {
        var x = previous.X;
        var y = previous.Y;
        foreach (Match match in CoordinateRegex.Matches(token))
        {
            try
            {
                if (match.Groups["axis"].Value.Equals("X", StringComparison.OrdinalIgnoreCase)) x = format.Parse(match.Groups["value"].Value);
                else y = format.Parse(match.Groups["value"].Value);
            }
            catch (FormatException exception)
            {
                warnings.Add(exception.Message);
            }
        }

        return new PcbPoint(x, y);
    }
}
