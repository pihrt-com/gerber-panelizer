using System.Globalization;
using System.Text.RegularExpressions;
using GerberPanelizer.Core.Geometry;

namespace GerberPanelizer.Core.Parsing;

public sealed class ExcellonParser
{
    private static readonly Regex ToolRegex = new(@"^T(?<tool>\d+)C(?<diameter>[+-]?\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SelectToolRegex = new(@"^T(?<tool>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CoordinateRegex = new(@"(?<axis>[XY])(?<value>[+-]?\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DecimalFormatRegex = new(@"(?:METRIC|INCH)[^\r\n]*,(?<format>0+\.0+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ExcellonDocument Parse(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var warnings = new List<string>();
        var format = ReadFormat(content, warnings);
        var tools = new Dictionary<int, double>();
        var hits = new List<ExcellonHit>();
        var currentTool = 0;
        var previous = new PcbPoint(0, 0);

        foreach (var rawLine in content.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith(';') || line is "%" or "M48" || line.StartsWith("M", StringComparison.OrdinalIgnoreCase) || line.StartsWith("G", StringComparison.OrdinalIgnoreCase)) continue;
            var toolDefinition = ToolRegex.Match(line);
            if (toolDefinition.Success)
            {
                var diameter = double.Parse(toolDefinition.Groups["diameter"].Value, CultureInfo.InvariantCulture);
                tools[int.Parse(toolDefinition.Groups["tool"].Value, CultureInfo.InvariantCulture)] = format.Unit == CoordinateUnit.Inches ? diameter * 25.4 : diameter;
                continue;
            }

            var toolSelection = SelectToolRegex.Match(line);
            if (toolSelection.Success)
            {
                currentTool = int.Parse(toolSelection.Groups["tool"].Value, CultureInfo.InvariantCulture);
                continue;
            }

            if (!line.Contains('X') && !line.Contains('Y')) continue;
            var point = ReadPoint(line, previous, format, warnings);
            hits.Add(new ExcellonHit(currentTool, point));
            previous = point;
        }

        var bounds = PcbBounds.Empty;
        foreach (var hit in hits)
        {
            var radius = tools.TryGetValue(hit.ToolNumber, out var diameter) ? diameter / 2 : 0;
            bounds = bounds.Include(hit.Position).Inflate(radius);
        }

        if (hits.Any(hit => hit.ToolNumber == 0)) warnings.Add("One or more drill hits do not select a tool.");
        return new ExcellonDocument(format, tools, hits, bounds, warnings);
    }

    private static CoordinateFormat ReadFormat(string content, ICollection<string> warnings)
    {
        var unit = content.Contains("INCH", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(content, @"(?m)^\s*M72\s*$", RegexOptions.IgnoreCase)
            ? CoordinateUnit.Inches
            : CoordinateUnit.Millimetres;
        var match = DecimalFormatRegex.Match(content);
        if (!match.Success)
        {
            var fallback = unit == CoordinateUnit.Inches ? new CoordinateFormat(2, 4, true, unit) : CoordinateFormat.ExcellonMetricDefault;
            warnings.Add($"No Excellon decimal format was found; using {fallback.IntegerDigits}.{fallback.DecimalDigits} coordinates.");
            return fallback;
        }

        var parts = match.Groups["format"].Value.Split('.');
        return new CoordinateFormat(parts[0].Length, parts[1].Length, true, unit);
    }

    private static PcbPoint ReadPoint(string line, PcbPoint previous, CoordinateFormat format, ICollection<string> warnings)
    {
        var x = previous.X;
        var y = previous.Y;
        foreach (Match match in CoordinateRegex.Matches(line))
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
