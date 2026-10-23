namespace GerberPanelizer.Core.IO;

public static class AtomicFileWriter
{
    public static async Task CopyAndMoveAsync(Stream source, string temporaryPath, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        await using (var target = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await source.CopyToAsync(target, cancellationToken);
            await target.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, destinationPath, overwrite: true);
    }
}
