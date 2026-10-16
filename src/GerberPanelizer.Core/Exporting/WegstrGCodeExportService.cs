using System.Globalization;
using System.IO.Compression;
using System.Text;
using Clipper2Lib;
using GerberPanelizer.Core.Geometry;
using GerberPanelizer.Core.Models;
using GerberPanelizer.Core.Panelization;
using GerberPanelizer.Core.Parsing;

namespace GerberPanelizer.Core.Exporting;

public sealed record WegstrExportResult(string ArchivePath, IReadOnlyList<string> Entries, IReadOnlyList<string> Warnings);

/// <summary>Writes Wegstr/Vectric-style metric G-code for copper isolation, drilling, and compensated board outlines.</summary>
public sealed class WegstrGCodeExportService
{
    private const int GeometryPrecision = 4;
    private const double GeometryTolerance = 0.01;

    public WegstrExportResult Export(string archivePath, IEnumerable<BoardPlacement> placements, WegstrMachineProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        var materialized = placements.ToList();
        if (materialized.Count == 0) throw new InvalidOperationException("At least one placed board is required for Wegstr export.");

        var warnings = new List<string>();
        var entries = new List<string>();
        var isolation = BuildIsolationProgram(materialized, profile, warnings);
        var drilling = profile.IncludeDrilling ? BuildDrillingProgram(materialized, profile, warnings) : null;
        var outline = profile.IncludeOutline ? BuildOutlineProgram(materialized, profile, warnings) : null;
        if (isolation is null && drilling is null && outline is null) throw new InvalidOperationException("The panel contains no supported copper, profile paths, or drill hits for Wegstr export.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivePath))!);
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        AddProgram(archive, entries, "01-copper-isolation.txt", isolation);
        AddProgram(archive, entries, "02-drill.txt", drilling);
        AddProgram(archive, entries, "03-board-outline.txt", outline);
        return new WegstrExportResult(archivePath, entries, warnings);
    }

    private static string? BuildIsolationProgram(IReadOnlyList<BoardPlacement> placements, WegstrMachineProfile profile, ICollection<string> warnings)
    {
        var machinePaths = new List<IReadOnlyList<PcbPoint>>();
        foreach (var placement in placements)
        {
            foreach (var file in placement.Board.Files.Where(file => file.Format == FileFormat.Gerber && file.Layer is ManufacturingLayerKind.CopperTop or ManufacturingLayerKind.CopperBottom))
            {
                EnsureLinearGerber(file);
                var document = new GerberParser().Parse(file.Content);
                var copper = BuildCopperPolygons(document, file.Name, warnings);
                if (copper.Count == 0) continue;
                var united = Clipper.Union(copper, [], FillRule.NonZero, GeometryPrecision);
                var isolation = Clipper.InflatePaths(united, profile.IsolationOffsetMillimetres, JoinType.Round, EndType.Polygon, precision: GeometryPrecision, arcTolerance: GeometryTolerance);
                foreach (var path in isolation.Where(path => path.Count >= 3))
                {
                    machinePaths.Add(CloseAndTransform(path, placement, profile));
                }
            }
        }
        if (machinePaths.Count == 0)
        {
            warnings.Add("No supported copper geometry was found; 01-copper-isolation.txt was not created.");
            return null;
        }

        const string toolDescription = "30 deg. spiral carbide engraving bit";
        var program = new GCodeProgram("Gerber Panelizer - copper isolation", "Copper isolation", profile, $"{profile.MillingToolNumber} = {toolDescription}");
        program.Initialize(profile.MillingToolNumber, profile.SafeZMillimetres, profile.SpindleRpm, profile.MillingFeedMillimetresPerMinute, toolDescription);
        WriteClosedPaths(program, OrderPaths(machinePaths), profile.IsolationDepthMillimetres, profile);
        program.Finish(profile.SafeZMillimetres);
        return program.ToString();
    }

    private static string? BuildDrillingProgram(IReadOnlyList<BoardPlacement> placements, WegstrMachineProfile profile, ICollection<string> warnings)
    {
        var hits = new List<(double Diameter, PcbPoint Position)>();
        foreach (var placement in placements)
        {
            foreach (var file in placement.Board.Files.Where(file => file.Format == FileFormat.Excellon))
            {
                var document = new ExcellonParser().Parse(file.Content);
                foreach (var hit in document.Hits)
                {
                    if (!document.Tools.TryGetValue(hit.ToolNumber, out var diameter))
                    {
                        warnings.Add($"{file.Name}: drill hit with undefined tool T{hit.ToolNumber} was skipped.");
                        continue;
                    }
                    hits.Add((diameter, ToMachinePoint(placement.TransformPoint(hit.Position), profile)));
                }
            }
        }
        if (hits.Count == 0)
        {
            warnings.Add("No assigned drill hits were found; 02-drill.txt was not created.");
            return null;
        }

        var groups = hits.GroupBy(hit => Math.Round(hit.Diameter, 6)).OrderBy(group => group.Key).ToList();
        var toolDescriptions = groups.Select((group, index) => $"{index + 1} = drill {group.Key.ToString("0.######", CultureInfo.InvariantCulture)} mm").ToList();
        var program = new GCodeProgram("Gerber Panelizer - drilling", "Drilling", profile, toolDescriptions);
        program.InitializeWithoutTool();
        var toolNumber = 1;
        foreach (var group in groups)
        {
            if (toolNumber > 99) throw new InvalidOperationException("Wegstr export supports at most 99 distinct drill diameters.");
            program.SelectTool(toolNumber, profile.SafeZMillimetres, profile.SpindleRpm, $"drill {group.Key.ToString("0.######", CultureInfo.InvariantCulture)} mm");
            foreach (var hit in group.OrderBy(hit => hit.Position.Y).ThenBy(hit => hit.Position.X))
            {
                program.StartPath(hit.Position, profile.SafeZMillimetres, profile.DrillingDepthMillimetres, profile.PlungeFeedMillimetresPerMinute);
                program.RapidZ(profile.SafeZMillimetres);
            }
            toolNumber++;
        }
        program.Finish(profile.SafeZMillimetres);
        return program.ToString();
    }

    private static string? BuildOutlineProgram(IReadOnlyList<BoardPlacement> placements, WegstrMachineProfile profile, ICollection<string> warnings)
    {
        var machinePaths = new List<IReadOnlyList<PcbPoint>>();
        foreach (var placement in placements)
        {
            foreach (var file in placement.Board.Files.Where(file => file.Format == FileFormat.Gerber && file.Layer == ManufacturingLayerKind.Profile))
            {
                EnsureLinearGerber(file);
                var document = new GerberParser().Parse(file.Content);
                var loops = BuildClosedProfilePaths(document, file.Name, warnings);
                if (loops.Count == 0) continue;
                var normalized = Clipper.Union(loops, [], FillRule.EvenOdd, GeometryPrecision);
                var compensated = Clipper.InflatePaths(normalized, profile.OutlineToolDiameterMillimetres / 2, JoinType.Round, EndType.Polygon, precision: GeometryPrecision, arcTolerance: GeometryTolerance);
                foreach (var path in compensated.Where(path => path.Count >= 3))
                {
                    machinePaths.Add(CloseAndTransform(path, placement, profile));
                }
            }
        }
        if (machinePaths.Count == 0)
        {
            warnings.Add("No closed profile paths were found; 03-board-outline.txt was not created.");
            return null;
        }

        var toolDescription = $"{profile.OutlineToolDiameterMillimetres.ToString("0.###", CultureInfo.InvariantCulture)} mm end mill";
        var program = new GCodeProgram("Gerber Panelizer - board outline", "Board outline", profile, $"{profile.MillingToolNumber} = {toolDescription}");
        program.Initialize(profile.MillingToolNumber, profile.SafeZMillimetres, profile.SpindleRpm, profile.MillingFeedMillimetresPerMinute, toolDescription);
        WriteClosedPaths(program, OrderPaths(machinePaths), profile.OutlineDepthMillimetres, profile);
        program.Finish(profile.SafeZMillimetres);
        return program.ToString();
    }

    private static PathsD BuildCopperPolygons(GerberDocument document, string fileName, ICollection<string> warnings)
    {
        var result = new PathsD();
        var warnedShapes = new HashSet<GerberApertureShape>();
        foreach (var operation in document.Operations.Where(operation => operation.Type is GerberOperationType.Draw or GerberOperationType.Flash))
        {
            if (operation.Aperture.Shape == GerberApertureShape.Unknown || operation.Aperture.WidthMillimetres <= 0 || operation.Aperture.HeightMillimetres <= 0)
            {
                if (warnedShapes.Add(GerberApertureShape.Unknown)) warnings.Add($"{fileName}: copper objects with an unknown aperture were skipped.");
                continue;
            }
            if (operation.Type == GerberOperationType.Draw)
            {
                if (operation.Aperture.Shape != GerberApertureShape.Circle)
                {
                    if (warnedShapes.Add(operation.Aperture.Shape)) warnings.Add($"{fileName}: {operation.Aperture.Shape} trace apertures are not safe to convert and were skipped.");
                    continue;
                }
                var centreLine = new PathD { ToClipperPoint(operation.Start), ToClipperPoint(operation.End) };
                result.AddRange(Clipper.InflatePaths([centreLine], operation.Aperture.WidthMillimetres / 2, JoinType.Round, EndType.Round, precision: GeometryPrecision, arcTolerance: GeometryTolerance));
                continue;
            }

            var flash = BuildFlash(operation.End, operation.Aperture);
            if (flash is null)
            {
                if (warnedShapes.Add(operation.Aperture.Shape)) warnings.Add($"{fileName}: {operation.Aperture.Shape} flashes are not safe to convert and were skipped.");
            }
            else
            {
                result.Add(flash);
            }
        }
        return result;
    }

    private static PathD? BuildFlash(PcbPoint center, GerberAperture aperture) => aperture.Shape switch
    {
        GerberApertureShape.Circle => Circle(center, aperture.WidthMillimetres / 2),
        GerberApertureShape.Rectangle => Rectangle(center, aperture.WidthMillimetres, aperture.HeightMillimetres),
        GerberApertureShape.Obround => Obround(center, aperture.WidthMillimetres, aperture.HeightMillimetres),
        _ => null,
    };

    private static PathD Circle(PcbPoint center, double radius)
    {
        const int count = 48;
        var path = new PathD(count);
        for (var index = 0; index < count; index++)
        {
            var angle = index * Math.Tau / count;
            path.Add(new PointD(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius));
        }
        return path;
    }

    private static PathD Rectangle(PcbPoint center, double width, double height)
    {
        var halfWidth = width / 2;
        var halfHeight = height / 2;
        return
        [
            new PointD(center.X - halfWidth, center.Y - halfHeight),
            new PointD(center.X + halfWidth, center.Y - halfHeight),
            new PointD(center.X + halfWidth, center.Y + halfHeight),
            new PointD(center.X - halfWidth, center.Y + halfHeight),
        ];
    }

    private static PathD Obround(PcbPoint center, double width, double height)
    {
        if (Math.Abs(width - height) < .000001) return Circle(center, width / 2);
        var horizontal = width > height;
        var radius = Math.Min(width, height) / 2;
        var halfLine = (Math.Max(width, height) - Math.Min(width, height)) / 2;
        var line = horizontal
            ? new PathD { new(center.X - halfLine, center.Y), new(center.X + halfLine, center.Y) }
            : new PathD { new(center.X, center.Y - halfLine), new(center.X, center.Y + halfLine) };
        return Clipper.InflatePaths([line], radius, JoinType.Round, EndType.Round, precision: GeometryPrecision, arcTolerance: GeometryTolerance)[0];
    }

    private static PathsD BuildClosedProfilePaths(GerberDocument document, string fileName, ICollection<string> warnings)
    {
        var result = new PathsD();
        PathD? current = null;
        foreach (var segment in document.Operations.Where(operation => operation.Type == GerberOperationType.Draw))
        {
            var start = ToClipperPoint(segment.Start);
            var end = ToClipperPoint(segment.End);
            if (current is null || Distance(current[^1], start) > .001)
            {
                AddIfClosed(result, current, fileName, warnings);
                current = [start, end];
            }
            else
            {
                current.Add(end);
            }
        }
        AddIfClosed(result, current, fileName, warnings);
        return result;
    }

    private static void AddIfClosed(PathsD result, PathD? path, string fileName, ICollection<string> warnings)
    {
        if (path is null || path.Count < 3) return;
        if (Distance(path[0], path[^1]) > .01)
        {
            warnings.Add($"{fileName}: an open profile path was skipped.");
            return;
        }
        path.RemoveAt(path.Count - 1);
        result.Add(path);
    }

    private static IReadOnlyList<PcbPoint> CloseAndTransform(PathD path, BoardPlacement placement, WegstrMachineProfile profile)
    {
        var result = path.Select(point => ToMachinePoint(placement.TransformPoint(new PcbPoint(point.x, point.y)), profile)).ToList();
        if (result.Count > 0) result.Add(result[0]);
        return result;
    }

    private static PcbPoint ToMachinePoint(PcbPoint panelPoint, WegstrMachineProfile profile) => new(panelPoint.X, panelPoint.Y - profile.WorkAreaHeightMillimetres);

    private static IEnumerable<IReadOnlyList<PcbPoint>> OrderPaths(IEnumerable<IReadOnlyList<PcbPoint>> source)
    {
        var remaining = source.Where(path => path.Count > 1).ToList();
        var current = new PcbPoint(0, 0);
        while (remaining.Count > 0)
        {
            var bestIndex = 0;
            var bestDistance = double.PositiveInfinity;
            for (var index = 0; index < remaining.Count; index++)
            {
                var distance = Distance(current, remaining[index][0]);
                if (distance < bestDistance)
                {
                    bestIndex = index;
                    bestDistance = distance;
                }
            }
            var best = remaining[bestIndex];
            remaining.RemoveAt(bestIndex);
            yield return best;
            current = best[^1];
        }
    }

    private static void WriteClosedPaths(GCodeProgram program, IEnumerable<IReadOnlyList<PcbPoint>> paths, double depth, WegstrMachineProfile profile)
    {
        foreach (var path in paths)
        {
            program.StartPath(path[0], profile.SafeZMillimetres, depth, profile.PlungeFeedMillimetresPerMinute);
            foreach (var point in path.Skip(1)) program.Cut(point);
            program.RapidZ(profile.SafeZMillimetres);
        }
    }

    private static void EnsureLinearGerber(ManufacturingFile file)
    {
        if (file.Content.Contains("G02", StringComparison.OrdinalIgnoreCase) || file.Content.Contains("G03", StringComparison.OrdinalIgnoreCase) || file.Content.Contains("G36", StringComparison.OrdinalIgnoreCase) || file.Content.Contains("G37", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Gerber '{file.Name}' contains arcs or regions that cannot yet be converted safely to Wegstr G-code.");
        }
    }

    private static PointD ToClipperPoint(PcbPoint point) => new(point.X, point.Y);
    private static double Distance(PointD left, PointD right) => Math.Sqrt(Math.Pow(left.x - right.x, 2) + Math.Pow(left.y - right.y, 2));
    private static double Distance(PcbPoint left, PcbPoint right) => Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2));

    private static void AddProgram(ZipArchive archive, ICollection<string> entries, string name, string? content)
    {
        if (content is null) return;
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
        entries.Add(name);
    }

    private sealed class GCodeProgram
    {
        private readonly StringBuilder output = new();
        private int lineNumber = 120;

        public GCodeProgram(string title, string toolpathName, WegstrMachineProfile profile, string toolDescription)
            : this(title, toolpathName, profile, [toolDescription])
        {
        }

        public GCodeProgram(string title, string toolpathName, WegstrMachineProfile profile, IReadOnlyList<string> toolDescriptions)
        {
            output.Append("( ").Append(title).Append(" )\r\n")
                .Append("( for Wegstr from Gerber Panelizer )\r\n")
                .Append("( Material Size )\r\n")
                .Append("( X= ").Append(FormatCoordinate(profile.WorkAreaWidthMillimetres))
                .Append(", Y= ").Append(FormatCoordinate(profile.WorkAreaHeightMillimetres))
                .Append(", Z= ").Append(FormatCoordinate(profile.MaterialThicknessMillimetres)).Append(" )\r\n")
                .Append("()\r\n")
                .Append("(Toolpaths used in this file:)\r\n")
                .Append('(').Append(toolpathName).Append(")\r\n")
                .Append("(Tools used in this file:)\r\n");
            foreach (var description in toolDescriptions) output.Append('(').Append(description).Append(")\r\n");
        }

        public void Initialize(int toolNumber, double safeZ, int spindleRpm, double feed, string toolDescription)
        {
            InitializeWithoutTool();
            SelectTool(toolNumber, safeZ, spindleRpm, toolDescription);
            Command($"X0.000Y0.000F{FormatFeed(feed)}");
        }

        public void InitializeWithoutTool()
        {
            Command("G00G21G17G90G40G49G80");
            Command("G71G91.1");
        }

        public void SelectTool(int toolNumber, double safeZ, int spindleRpm, string description)
        {
            Command($"T{toolNumber}M06");
            Command($" ({SanitizeComment(description)})");
            Command($"G00G43Z{FormatCoordinate(safeZ)}H{toolNumber}");
            Command($"S{spindleRpm}M03");
            Command("G94");
        }

        public void StartPath(PcbPoint point, double safeZ, double depth, double plungeFeed)
        {
            Command($"G00X{FormatCoordinate(point.X)}Y{FormatCoordinate(point.Y)}Z{FormatCoordinate(safeZ)}");
            Command("G00Z0.000");
            Command($"G1Z{FormatCoordinate(depth)}F{FormatFeed(plungeFeed)}");
        }

        public void Cut(PcbPoint point) => Command($"G1X{FormatCoordinate(point.X)}Y{FormatCoordinate(point.Y)}");
        public void RapidZ(double z) => Command($"G00Z{FormatCoordinate(z)}");

        public void Finish(double safeZ)
        {
            RapidZ(safeZ);
            Command("G00X0.000Y0.000");
            Command("M09");
            Command("M30");
            output.Append("%\r\n");
        }

        public override string ToString() => output.ToString();
        private void Command(string command)
        {
            output.Append('N').Append(lineNumber).Append(command).Append("\r\n");
            lineNumber += 10;
        }
        private static string SanitizeComment(string value) => value.Replace('(', '[').Replace(')', ']');
        private static string FormatCoordinate(double value) => (Math.Abs(value) < .0005 ? 0 : value).ToString("0.000", CultureInfo.InvariantCulture);
        private static string FormatFeed(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
