using System.Text.Json;

namespace GerberPanelizer.Core.Projects;

public static class PanelProjectStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static void Save(string path, PanelProject project)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A project file directory is required.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(project, SerializerOptions));
        File.Move(temporaryPath, path, true);
    }

    public static PanelProject Load(string path)
    {
        var project = JsonSerializer.Deserialize<PanelProject>(File.ReadAllText(path), SerializerOptions)
            ?? throw new InvalidDataException("The project file contains no project data.");
        if (project.FormatVersion != 1) throw new InvalidDataException($"Unsupported project format version {project.FormatVersion}.");
        return project;
    }
}
