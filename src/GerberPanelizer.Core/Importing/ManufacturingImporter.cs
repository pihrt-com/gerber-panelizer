using System.IO.Compression;
using System.Text;
using GerberPanelizer.Core.Geometry;
using GerberPanelizer.Core.Models;
using GerberPanelizer.Core.Parsing;
using SharpCompress.Archives.Rar;
using SharpCompress.Readers;

namespace GerberPanelizer.Core.Importing;

public sealed class ManufacturingImporter
{
    private const long MaximumArchiveEntryBytes = 32L * 1024 * 1024;
    private const long MaximumArchiveBytes = 128L * 1024 * 1024;
    private readonly GerberParser _gerberParser = new();
    private readonly ExcellonParser _excellonParser = new();

    public ImportResult ImportFiles(IEnumerable<string> filePaths)
    {
        var boards = new List<ImportedBoard>();
        var issues = new List<ImportIssue>();
        var looseFiles = new List<ManufacturingFile>();
        foreach (var path in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                issues.Add(new ImportIssue(path, "The selected file does not exist.", true));
                continue;
            }

            if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ImportZip(path, boards, issues);
                continue;
            }

            if (Path.GetExtension(path).Equals(".rar", StringComparison.OrdinalIgnoreCase))
            {
                ImportRar(path, boards, issues);
                continue;
            }

            var file = ReadManufacturingFile(path, File.ReadAllText(path, Encoding.UTF8));
            if (file is null) issues.Add(new ImportIssue(path, "The file is not a recognised Gerber or Excellon document.", false));
            else looseFiles.Add(file);
        }

        if (looseFiles.Count > 0)
        {
            var name = looseFiles.Count == 1 ? Path.GetFileNameWithoutExtension(looseFiles[0].Name) : "Imported layers";
            boards.Add(CreateBoard(name, looseFiles, looseFiles.Select(file => file.SourcePath).ToArray()));
        }

        return new ImportResult(boards, issues);
    }

    private void ImportZip(string path, ICollection<ImportedBoard> boards, ICollection<ImportIssue> issues)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var files = new List<ManufacturingFile>();
            foreach (var entry in archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)))
            {
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var file = ReadManufacturingFile(entry.FullName, reader.ReadToEnd());
                if (file is not null) files.Add(file);
            }

            if (files.Count == 0) issues.Add(new ImportIssue(path, "The archive contains no recognised Gerber or Excellon files.", true));
            else boards.Add(CreateBoard(Path.GetFileNameWithoutExtension(path), files, [path]));
        }
        catch (InvalidDataException)
        {
            issues.Add(new ImportIssue(path, "The selected ZIP archive is corrupt or unsupported.", true));
        }
    }

    private void ImportRar(string path, ICollection<ImportedBoard> boards, ICollection<ImportIssue> issues)
    {
        try
        {
            using var archive = RarArchive.OpenArchive(path, new ReaderOptions());
            var files = new List<ManufacturingFile>();
            long totalUncompressedBytes = 0;
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory && entry.LinkTarget is null))
            {
                var entryKey = entry.Key;
                if (string.IsNullOrWhiteSpace(entryKey)) continue;
                if (entry.Size < 0 || entry.Size > MaximumArchiveEntryBytes)
                {
                    issues.Add(new ImportIssue(path, $"RAR entry '{entryKey}' exceeds the {MaximumArchiveEntryBytes / (1024 * 1024)} MB safety limit.", true));
                    return;
                }

                if (entry.Size > MaximumArchiveBytes - totalUncompressedBytes)
                {
                    issues.Add(new ImportIssue(path, $"RAR archive exceeds the {MaximumArchiveBytes / (1024 * 1024)} MB total uncompressed safety limit.", true));
                    return;
                }

                totalUncompressedBytes += entry.Size;
                using var reader = new StreamReader(entry.OpenEntryStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var file = ReadManufacturingFile(entryKey, reader.ReadToEnd());
                if (file is not null) files.Add(file);
            }

            if (files.Count == 0) issues.Add(new ImportIssue(path, "The archive contains no recognised Gerber or Excellon files.", true));
            else boards.Add(CreateBoard(Path.GetFileNameWithoutExtension(path), files, [path]));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            issues.Add(new ImportIssue(path, $"The selected RAR archive is corrupt, unreadable, or unsupported: {exception.Message}", true));
        }
    }

    private ManufacturingFile? ReadManufacturingFile(string path, string content)
    {
        var format = LayerClassifier.DetectFormat(path, content);
        return format switch
        {
            FileFormat.Gerber => CreateGerberFile(path, content),
            FileFormat.Excellon => CreateExcellonFile(path, content),
            _ => null,
        };
    }

    private ManufacturingFile CreateGerberFile(string path, string content)
    {
        var document = _gerberParser.Parse(content);
        return new ManufacturingFile(Path.GetFileName(path), path, LayerClassifier.DetectLayer(path), FileFormat.Gerber, content, document.Bounds, document.Warnings);
    }

    private ManufacturingFile CreateExcellonFile(string path, string content)
    {
        var document = _excellonParser.Parse(content);
        return new ManufacturingFile(Path.GetFileName(path), path, ManufacturingLayerKind.Drill, FileFormat.Excellon, content, document.Bounds, document.Warnings);
    }

    private static ImportedBoard CreateBoard(string name, IReadOnlyList<ManufacturingFile> files, IReadOnlyList<string> sourceFilePaths)
    {
        var profile = files.FirstOrDefault(file => file.Layer == ManufacturingLayerKind.Profile && !file.Bounds.IsEmpty);
        var bounds = profile?.Bounds ?? files.Aggregate(PcbBounds.Empty, (current, file) => current.Include(file.Bounds));
        return new ImportedBoard(Guid.NewGuid().ToString("N"), name, files, bounds) { SourceFilePaths = sourceFilePaths };
    }
}
