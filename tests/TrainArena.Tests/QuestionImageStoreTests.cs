using TrainArena.Data;

namespace TrainArena.Tests;

public class QuestionImageStoreTests
{
    [Fact]
    public async Task SaveAsync_WritesUnderUploadsAndReturnsPublicPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ta-img-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new QuestionImageStore(root, maxBytes: 1024 * 1024);
            await using var stream = new MemoryStream(Png1x1());

            var path = await store.SaveAsync(stream, "photo.PNG", "image/png");

            Assert.StartsWith("/uploads/", path);
            Assert.True(File.Exists(Path.Combine(root, "wwwroot", path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_RejectsOversizedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ta-img-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new QuestionImageStore(root, maxBytes: 16);
            await using var stream = new MemoryStream(new byte[32]);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.SaveAsync(stream, "big.png", "image/png"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_RejectsNonImageContentType()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ta-img-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var store = new QuestionImageStore(root, maxBytes: 1024);
            await using var stream = new MemoryStream([1, 2, 3]);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.SaveAsync(stream, "x.txt", "text/plain"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] Png1x1() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
        0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE,
        0xD4, 0xEF, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45,
        0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];
}
