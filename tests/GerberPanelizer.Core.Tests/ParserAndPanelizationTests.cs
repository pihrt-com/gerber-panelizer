using System.IO.Compression;
using System.Text.RegularExpressions;
using GerberPanelizer.Core.Exporting;
using GerberPanelizer.Core.Geometry;
using GerberPanelizer.Core.Models;
using GerberPanelizer.Core.Panelization;
using GerberPanelizer.Core.Parsing;
using Xunit;

namespace GerberPanelizer.Core.Tests;

public sealed class ParserAndPanelizationTests
{
    [Fact]
    public void GerberParser_ReadsFusionMetricFormatAndProfileBounds()
    {
        const string gerber = "%MOMM*%\n%FSLAX34Y34*%\n%ADD10C,0.200*%\nD10*\nX0Y0D02*\nX100000Y0D01*\nX100000Y50000D01*\nM02*";

        var document = new GerberParser().Parse(gerber);

        Assert.Equal(CoordinateUnit.Millimetres, document.CoordinateFormat.Unit);
        Assert.Equal(3, document.CoordinateFormat.IntegerDigits);
        Assert.Equal(4, document.CoordinateFormat.DecimalDigits);
        Assert.Equal(10.2, document.Bounds.Width, 6);
        Assert.Equal(5.2, document.Bounds.Height, 6);
    }

    [Fact]
    public void GerberParser_ReadsRectangularAndObroundSmtPadFlashes()
    {
        const string gerber = "%MOMM*%\n%FSLAX34Y34*%\n%ADD10R,1.600X0.800*%\n%ADD11O,2.000X1.000*%\nD10*\nX10000Y20000D03*\nD11*\nX30000Y20000D03*\nM02*";

        var document = new GerberParser().Parse(gerber);
        var flashes = document.Operations.Where(operation => operation.Type == GerberOperationType.Flash).ToArray();

        Assert.Collection(
            flashes,
            flash =>
            {
                Assert.Equal(GerberApertureShape.Rectangle, flash.Aperture.Shape);
                Assert.Equal(1.6, flash.Aperture.WidthMillimetres, 6);
                Assert.Equal(.8, flash.Aperture.HeightMillimetres, 6);
            },
            flash =>
            {
                Assert.Equal(GerberApertureShape.Obround, flash.Aperture.Shape);
                Assert.Equal(2, flash.Aperture.WidthMillimetres, 6);
                Assert.Equal(1, flash.Aperture.HeightMillimetres, 6);
            });
    }

    [Fact]
    public void ExcellonParser_ReadsFusionMetricDrillCoordinates()
    {
        const string drill = "M48\nMETRIC,TZ,000.000\nT1C0.800\n%\nG90\nM71\nT1\nX19050Y27940\nM30\n";

        var document = new ExcellonParser().Parse(drill);

        var hit = Assert.Single(document.Hits);
        Assert.Equal(19.05, hit.Position.X, 6);
        Assert.Equal(27.94, hit.Position.Y, 6);
        Assert.Equal(0.8, document.Tools[1], 6);
    }

    [Fact]
    public void ExcellonParser_ReadsM72InchDrillCoordinates()
    {
        const string drill = "%\nM48\nM72\nT01C0.0320\n%\nT01\nX3150Y13150\nM30\n";

        var document = new ExcellonParser().Parse(drill);

        var hit = Assert.Single(document.Hits);
        Assert.Equal(CoordinateUnit.Inches, document.CoordinateFormat.Unit);
        Assert.Equal(8.001, hit.Position.X, 6);
        Assert.Equal(33.401, hit.Position.Y, 6);
        Assert.Equal(0.8128, document.Tools[1], 6);
    }

    [Fact]
    public void LayerClassifier_RecognizesLegacyDrdExcellonWithLeadingPercent()
    {
        const string drill = "%\nM48\nM72\nT01C0.0320\n%\nT01\nX3150Y3150\nM30\n";

        Assert.Equal(FileFormat.Excellon, LayerClassifier.DetectFormat("board.NC-DRILL.drd", drill));
        Assert.Equal(ManufacturingLayerKind.Drill, LayerClassifier.DetectLayer("board.NC-DRILL.drd"));
    }

    [Fact]
    public void ShelfPanelizer_RespectsMarginAndSpacing()
    {
        var board = CreateBoard("test", new PcbBounds(0, 0, 20, 10));
        var panelizer = new ShelfPanelizer();

        var result = panelizer.Arrange([board, board with { Id = "second" }], new PanelDefinition(new PcbSize(50, 30), 2, 3));

        Assert.Equal(2, result.Placements.Count);
        Assert.Equal(2, result.Placements[0].Bounds.MinX, 6);
        Assert.Equal(25, result.Placements[1].Bounds.MinX, 6);
        Assert.Empty(result.UnplacedBoards);
    }

    [Fact]
    public void ShelfPanelizer_RotatesBoardByNinetyDegreesWhenEnabled()
    {
        var board = CreateBoard("rotated", new PcbBounds(0, 0, 25, 15));

        var result = new ShelfPanelizer().Arrange([board], new PanelDefinition(new PcbSize(20, 30), 0, 0), allowRotation: true);

        var placement = Assert.Single(result.Placements);
        Assert.Equal(90, placement.RotationDegrees, 6);
        Assert.Equal(15, placement.Bounds.Width, 6);
        Assert.Equal(25, placement.Bounds.Height, 6);
    }

    [Fact]
    public void BoardStagingLayout_PlacesImportedBoardsToTheRightOfThePanel()
    {
        var boards = new[]
        {
            CreateBoard("first", new PcbBounds(10, 20, 50, 50)),
            CreateBoard("second", new PcbBounds(-5, -5, 25, 20)),
        };

        var result = new BoardStagingLayout().Arrange(boards, new PcbSize(200, 130), 3);

        Assert.Equal(2, result.Placements.Count);
        Assert.All(result.Placements, placement => Assert.True(placement.Bounds.MinX >= 205));
        Assert.All(result.Placements, placement => Assert.True(placement.Bounds.MinY >= 0 && placement.Bounds.MaxY <= result.WorkspaceSize.Height));
        Assert.All(result.Placements, placement => Assert.True(placement.Bounds.MaxY <= result.WorkspaceSize.Height - 15));
        Assert.True(result.WorkspaceSize.Width > 200);
        Assert.Equal(130, result.WorkspaceSize.Height, 6);
    }

    [Fact]
    public void PanelPlacementRules_DistinguishesInsideStagedAndBoundaryCrossingBoards()
    {
        var board = CreateBoard("board", new PcbBounds(0, 0, 20, 10));
        var panel = new PcbSize(100, 60);

        Assert.Equal(PanelPlacementState.Inside, PanelPlacementRules.Classify(new BoardPlacement(board, new PcbPoint(5, 5)), panel));
        Assert.Equal(PanelPlacementState.Outside, PanelPlacementRules.Classify(new BoardPlacement(board, new PcbPoint(105, 5)), panel));
        Assert.Equal(PanelPlacementState.CrossingBoundary, PanelPlacementRules.Classify(new BoardPlacement(board, new PcbPoint(95, 5)), panel));
    }

    [Fact]
    public void PanelExport_RetainsApertureMacroWithoutEmittingItsBodyAsCommands()
    {
        const string gerber = "%MOMM*%\n%FSLAX34Y34*%\n%AMOC8*\n5,1,8,0,0,1.0X$1,22.5*%\n%ADD10OC8,0.100*%\nD10*\nX0Y0D03*\nM02*";
        var board = new ImportedBoard("macro", "macro", [new ManufacturingFile("copper_top.gbr", "copper_top.gbr", ManufacturingLayerKind.CopperTop, FileFormat.Gerber, gerber, new PcbBounds(0, 0, 1, 1), [])], new PcbBounds(0, 0, 1, 1));
        var archivePath = Path.Combine(Path.GetTempPath(), $"gerber-panelizer-{Guid.NewGuid():N}.zip");

        try
        {
            var result = new PanelExportService().Export(archivePath, [new BoardPlacement(board, new PcbPoint(0, 0))]);
            using var archive = ZipFile.OpenRead(archivePath);
            using var reader = new StreamReader(archive.GetEntry("GerberFiles/copper_top.gbr")!.Open());
            var exported = reader.ReadToEnd();

            Assert.Contains("%AMGP0_OC8*", exported);
            Assert.Equal(1, exported.Split("5,1,8,0,0,1.0X$1,22.5", StringSplitOptions.None).Length - 1);
            Assert.DoesNotContain("5,1,8,0,0,1.0X$1,22.5*\nD10", exported);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
        }
    }

    [Fact]
    public void PanelExport_ConvertsInchGerberAndAppliesRotationAndMirroring()
    {
        const string gerber = "%MOIN*%\n%FSLAX24Y24*%\n%ADD10R,0.100X0.200*%\nD10*\nX5000Y10000D03*\nM02*";
        var board = new ImportedBoard("inch", "inch", [new ManufacturingFile("profile.gbr", "profile.gbr", ManufacturingLayerKind.Profile, FileFormat.Gerber, gerber, new PcbBounds(0, 0, 50.8, 50.8), [])], new PcbBounds(0, 0, 50.8, 50.8));
        var archivePath = Path.Combine(Path.GetTempPath(), $"gerber-panelizer-{Guid.NewGuid():N}.zip");

        try
        {
            new PanelExportService().Export(archivePath, [new BoardPlacement(board, new PcbPoint(10, 20), 90, BoardMirrorMode.MirrorX)]);
            var exported = ReadEntry(archivePath, "GerberFiles/profile.gbr");

            Assert.Contains("%ADD10R,5.08X2.54*%", exported);
            Assert.Contains("X0035400000Y0058100000D03", exported);
        }
        finally
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
        }
    }

    [Fact]
    public void PanelExport_WritesUnambiguousDecimalMetricDrillCoordinates()
    {
        const string drill = "M48\nMETRIC,TZ,000.000\nT1C0.800\n%\nG90\nM71\nT1\nX19050Y27940\nM30\n";
        var board = new ImportedBoard("drill", "drill", [new ManufacturingFile("drill.xln", "drill.xln", ManufacturingLayerKind.Drill, FileFormat.Excellon, drill, new PcbBounds(19, 27, 20, 29), [])], new PcbBounds(19, 27, 20, 29));
        var archivePath = Path.Combine(Path.GetTempPath(), $"gerber-panelizer-{Guid.NewGuid():N}.zip");

        try
        {
            new PanelExportService().Export(archivePath, [new BoardPlacement(board, new PcbPoint(5, 6))]);
            var exported = ReadEntry(archivePath, "DrillFiles/drill.xln");

            Assert.Contains("METRIC,TZ\n", exported);
            Assert.Contains("X24.05Y33.94", exported);
            Assert.DoesNotContain("X0024050000", exported);
        }
        finally
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
        }
    }

    [Fact]
    public void WegstrExport_WritesWegstrIsolationDrillAndOutlinePrograms()
    {
        const string profile = "%MOMM*%\n%FSLAX34Y34*%\n%ADD10C,0.200*%\nD10*\nX0Y0D02*\nX100000Y0D01*\nX100000Y50000D01*\nX0Y50000D01*\nX0Y0D01*\nM02*";
        const string copper = "%MOMM*%\n%FSLAX34Y34*%\n%ADD10C,0.400*%\n%ADD11R,1.000X2.000*%\nD10*\nX20000Y25000D02*\nX80000Y25000D01*\nD11*\nX80000Y25000D03*\nM02*";
        const string drill = "M48\nMETRIC,TZ,000.000\nT1C0.800\n%\nT1\nX5000Y2500\nM30\n";
        var files = new ManufacturingFile[]
        {
            new("profile.gbr", "profile.gbr", ManufacturingLayerKind.Profile, FileFormat.Gerber, profile, new PcbBounds(0, 0, 10, 5), []),
            new("copper.gbr", "copper.gbr", ManufacturingLayerKind.CopperBottom, FileFormat.Gerber, copper, new PcbBounds(1.8, 1.5, 8.5, 3.5), []),
            new("drill.xln", "drill.xln", ManufacturingLayerKind.Drill, FileFormat.Excellon, drill, new PcbBounds(4.6, 2.1, 5.4, 2.9), []),
        };
        var board = new ImportedBoard("wegstr", "wegstr", files, new PcbBounds(0, 0, 10, 5));
        var archivePath = Path.Combine(Path.GetTempPath(), $"gerber-panelizer-{Guid.NewGuid():N}.zip");

        try
        {
            var result = new WegstrGCodeExportService().Export(archivePath, [new BoardPlacement(board, new PcbPoint(2, 3), 0, BoardMirrorMode.MirrorX)], new WegstrMachineProfile { IncludeOutline = true, IncludeOutlineInCopper = false });
            var isolation = ReadEntry(archivePath, "01-copper-isolation.txt");
            var drillProgram = ReadEntry(archivePath, "02-drill.txt");
            var outline = ReadEntry(archivePath, "03-board-outline.txt");

            Assert.Equal(["01-copper-isolation.txt", "02-drill.txt", "03-board-outline.txt"], result.Entries);
            Assert.Contains("( X= 200.000, Y= 130.000, Z= 1.500 )", isolation);
            Assert.Contains("N120G00G21G17G90G40G49G80", isolation);
            Assert.Contains("S12000M03", isolation);
            Assert.Contains("G1Z-0.080F170.0", isolation);
            Assert.Contains("Y-", isolation);
            Assert.Single(Regex.Matches(isolation, "G1Z-0\\.080"));
            Assert.DoesNotMatch(@"Y(?!0\.000)(?:\d)", isolation);
            Assert.DoesNotContain("M05", isolation);
            Assert.Contains("G00X7.000Y-124.500Z2.000", drillProgram);
            Assert.Contains("G1Z-1.800F170.0", drillProgram);
            Assert.Contains("G1Z-1.700F170.0", outline);
            Assert.EndsWith("M30\r\n%\r\n", drillProgram);
        }
        finally
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
        }
    }

    [Fact]
    public void WegstrExport_AddsEachBoardProfileToCopperIsolationByDefault()
    {
        const string profile = "%MOMM*%\n%FSLAX34Y34*%\n%ADD10C,0.200*%\nD10*\nX0Y0D02*\nX100000Y0D01*\nX100000Y50000D01*\nX0Y50000D01*\nX0Y0D01*\nM02*";
        const string copper = "%MOMM*%\n%FSLAX34Y34*%\n%ADD10C,0.400*%\nD10*\nX20000Y25000D02*\nX80000Y25000D01*\nM02*";
        var board = new ImportedBoard("marked-outline", "marked-outline", [
            new ManufacturingFile("profile.gbr", "profile.gbr", ManufacturingLayerKind.Profile, FileFormat.Gerber, profile, new PcbBounds(0, 0, 10, 5), []),
            new ManufacturingFile("copper.gbr", "copper.gbr", ManufacturingLayerKind.CopperTop, FileFormat.Gerber, copper, new PcbBounds(1.8, 2.3, 8.2, 2.7), []),
        ], new PcbBounds(0, 0, 10, 5));
        var archivePath = Path.Combine(Path.GetTempPath(), $"gerber-panelizer-{Guid.NewGuid():N}.zip");

        try
        {
            new WegstrGCodeExportService().Export(archivePath, [new BoardPlacement(board, new PcbPoint(2, 3))], new WegstrMachineProfile());
            var isolation = ReadEntry(archivePath, "01-copper-isolation.txt");

            Assert.Equal(2, Regex.Matches(isolation, "G1Z-0\\.080").Count);
            Assert.Contains("G1X2.000Y-127.000", isolation);
            Assert.Contains("G1X12.000Y-127.000", isolation);
        }
        finally
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
        }
    }

    [Theory]
    [InlineData("soldermask_top.gbr", ManufacturingLayerKind.SolderMaskTop)]
    [InlineData("soldermask_bottom.gbr", ManufacturingLayerKind.SolderMaskBottom)]
    [InlineData("silkscreen_top.gbr", ManufacturingLayerKind.SilkScreenTop)]
    [InlineData("silkscreen_bottom.gbr", ManufacturingLayerKind.SilkScreenBottom)]
    [InlineData("solderpaste_top.gbr", ManufacturingLayerKind.PasteTop)]
    [InlineData("solderpaste_bottom.gbr", ManufacturingLayerKind.PasteBottom)]
    public void LayerClassifier_PrioritizesSpecificFusionLayerNames(string fileName, ManufacturingLayerKind expected)
    {
        Assert.Equal(expected, LayerClassifier.DetectLayer(fileName));
    }

    private static ImportedBoard CreateBoard(string id, PcbBounds bounds) => new(id, id, [], bounds);

    private static string ReadEntry(string archivePath, string entryName)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        using var reader = new StreamReader(archive.GetEntry(entryName)!.Open());
        return reader.ReadToEnd();
    }
}
