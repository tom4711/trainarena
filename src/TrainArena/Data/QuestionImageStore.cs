namespace TrainArena.Data;

/// <summary>
/// Stores optional question images under wwwroot/uploads.
/// </summary>
public sealed class QuestionImageStore
{
    public const long DefaultMaxBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private readonly string _contentRoot;
    private readonly long _maxBytes;

    public QuestionImageStore(string contentRoot, long maxBytes = DefaultMaxBytes)
    {
        _contentRoot = contentRoot;
        _maxBytes = maxBytes;
    }

    public string UploadsPhysicalPath => Path.Combine(_contentRoot, "wwwroot", "uploads");

    public async Task<string> SaveAsync(
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException("Nur Bilder (JPEG, PNG, WebP, GIF) erlaubt.");
        }

        if (content.CanSeek && content.Length > _maxBytes)
        {
            throw new InvalidOperationException($"Bild zu groß (max. {_maxBytes / (1024 * 1024)} MB).");
        }

        var ext = ExtensionFor(contentType, fileName);
        Directory.CreateDirectory(UploadsPhysicalPath);
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var physical = Path.Combine(UploadsPhysicalPath, storedName);

        await using (var fs = File.Create(physical))
        {
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                total += read;
                if (total > _maxBytes)
                {
                    fs.Close();
                    File.Delete(physical);
                    throw new InvalidOperationException($"Bild zu groß (max. {_maxBytes / (1024 * 1024)} MB).");
                }

                await fs.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }

        return "/uploads/" + storedName;
    }

    public void TryDelete(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath) || !publicPath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var name = Path.GetFileName(publicPath);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var physical = Path.Combine(UploadsPhysicalPath, name);
        if (File.Exists(physical))
        {
            File.Delete(physical);
        }
    }

    private static string ExtensionFor(string contentType, string fileName)
    {
        var fromName = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(fromName)
            && fromName.Length <= 5
            && fromName.All(c => c == '.' || char.IsLetterOrDigit(c)))
        {
            return fromName.ToLowerInvariant();
        }

        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".bin"
        };
    }
}
