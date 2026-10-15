using System.IO.Compression;
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
    public void PanelExport_RetainsApertureMacroWithoutEmittingItsBodyAsCommands()
    {
        const string gerber = "%MOMM*%\n%FSLAX34Y34*%\n%AMOC8*\n5,1,8,0,0,1.0X$1,22.5*%\n%ADD10OC8,0.100*%\nD10*\nX0Y0D03*\nM02*";
        var board = new ImportedBoard("macro", "macro", [new ManufacturingFile("copper_top.gbr", "copper_top.gbr", ManufacturingLayerKind.CopperTop, FileFormat.Gerber, gerber, new PcbBounds(0, 0, 1, 1), [])], new PcbBounds(0, 0, 1, 1));
        var archivePath = Path.Combine(Path.GetTempPath(), $"gerber-panelizer-{Guid.NewGuid():N}.zip");

        try
        {
            new PanelExportService().Export(archivePath, [new BoardPlacement(board, new PcbPoint(0, 0))]);
            using var archive = ZipFile.OpenRead(archivePath);
            using var reader = new StreamReader(archive.GetEntry("GerberFiles/copper_top.gbr")!.Open());
            var exported = reader.ReadToEnd();

            Assert.Contains("%AMGP0_OC8*", exported);
            Assert.Equal(1, exported.Split("5,1,8,0,0,1.0X$1,22.5", StringSplitOptions.None).Length - 1);
            Assert.DoesNotContain("5,1,8,0,0,1.0X$1,22.5*\nD10", exported);
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
}
