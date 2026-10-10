using System.Net;
using System.Net.Sockets;
using TrainArena;
using TrainArena.Data;
using TrainArena.Data.Entities;
using TrainArena.Game;
using TrainArena.Hubs;
using Microsoft.EntityFrameworkCore;

// Published / single-file: content next to the binary (cwd is unreliable).
// macOS .app: MacOS/ = exe, Resources/ = bundled wwwroot (often read-only / App Translocation).
// Writable state (DB, uploads, logs) → ~/Library/Application Support/TrainArena.
static bool TryGetMacAppResources(out string resourcesPath)
{
    resourcesPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources"));
    return Directory.Exists(Path.Combine(resourcesPath, "wwwroot"));
}

static string ResolveDataRoot(string? bundleResources)
{
    if (bundleResources is null)
        return AppContext.BaseDirectory;

    var data = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TrainArena");
    Directory.CreateDirectory(data);

    // Sync static web assets from the sealed bundle; keep user uploads intact.
    var wwwSrc = Path.Combine(bundleResources, "wwwroot");
    var wwwDst = Path.Combine(data, "wwwroot");
    if (Directory.Exists(wwwSrc))
        SyncWwwroot(wwwSrc, wwwDst);

    foreach (var name in new[] { "appsettings.json", "appsettings.Production.json", "appsettings.Development.json" })
    {
        var src = Path.Combine(bundleResources, name);
        var dst = Path.Combine(data, name);
        if (File.Exists(src) && !File.Exists(dst))
            File.Copy(src, dst);
    }

    return data;
}

static void SyncWwwroot(string sourceDir, string destDir)
{
    Directory.CreateDirectory(destDir);
    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(sourceDir, file);
        if (rel.StartsWith("uploads" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || rel.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rel, "uploads", StringComparison.OrdinalIgnoreCase))
            continue;

        var target = Path.Combine(destDir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, overwrite: true);
    }
}

static string PickListeningUrl(string preferred)
{
    // Prefer ASPNETCORE_URLS / --urls already applied by the host; this only picks a free port
    // when the configured default is busy (common after a previous TrainArena still runs).
    if (!Uri.TryCreate(preferred.Replace("0.0.0.0", "127.0.0.1"), UriKind.Absolute, out var uri)
        || !uri.IsLoopback && uri.Host != "0.0.0.0" && uri.Host != "*")
    {
        // Non-standard URL — leave as-is.
        return preferred;
    }

    var host = preferred.Contains("0.0.0.0", StringComparison.Ordinal) ? "0.0.0.0" : uri.Host;
    var startPort = uri.Port > 0 ? uri.Port : 5175;
    for (var p = startPort; p < startPort + 20; p++)
    {
        if (IsPortFree(p))
            return $"http://{host}:{p}";
    }

    return preferred;
}

static bool IsPortFree(int port)
{
    try
    {
        using var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        listener.Stop();
        return true;
    }
    catch (SocketException)
    {
        return false;
    }
}

static void WriteStartupLog(string dataRoot, string message)
{
    try
    {
        var path = Path.Combine(dataRoot, "startup.log");
        File.AppendAllText(path, $"{DateTimeOffset.Now:o} {message}{Environment.NewLine}");
    }
    catch
    {
        // ignore logging failures
    }
}

var hasBundle = TryGetMacAppResources(out var bundleResources);
var dataRoot = ResolveDataRoot(hasBundle ? bundleResources : null);
WriteStartupLog(dataRoot, $"Starting TrainArena {AppVersion.Display}; dataRoot={dataRoot}");

try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = dataRoot,
    });
    builder.Services.AddRazorPages();
    builder.Services.AddSignalR(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.EnableDetailedErrors = true;
        }
    });
    builder.Services.AddSingleton<RoomCodeGenerator>();
    builder.Services.AddSingleton<GameSessionStore>();
    builder.Services.AddSingleton(sp =>
        new QuestionImageStore(sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath));

    var dbPath = Environment.GetEnvironmentVariable("TRAINARENA_DB")
        ?? Path.Combine(dataRoot, "trainarena.db");
    var dbDir = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dbDir))
        Directory.CreateDirectory(dbDir);

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    var preferredUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
        ?? builder.Configuration["urls"]
        ?? "http://0.0.0.0:5175";
    var listenUrl = PickListeningUrl(preferredUrls);
    builder.WebHost.UseUrls(listenUrl);
    WriteStartupLog(dataRoot, $"Listen URL: {listenUrl}");

    var app = builder.Build();

    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads"));

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedData.EnsureSeededAsync(db);
    }

    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapRazorPages();
    app.MapHub<GameHub>("/hubs/game");
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "ok",
        app = "TrainArena",
        version = AppVersion.Display,
    }));
    app.MapGet("/api/quizzes", async (AppDbContext db) =>
    {
        var list = await db.Quizzes
            .AsNoTracking()
            .OrderBy(q => q.Title)
            .Select(q => new { q.Id, q.Title, QuestionCount = q.Questions.Count })
            .ToListAsync();
        return Results.Ok(list);
    });

    WriteStartupLog(dataRoot, "Host starting");
    await app.RunAsync();
}
catch (Exception ex)
{
    WriteStartupLog(dataRoot, $"FATAL: {ex}");
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
    throw;
}

public partial class Program;
