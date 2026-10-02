using TrainArena.Data;
using TrainArena.Data.Entities;
using TrainArena.Game;
using TrainArena.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<RoomCodeGenerator>();
builder.Services.AddSingleton<GameSessionStore>();
builder.Services.AddSingleton(sp =>
    new QuestionImageStore(sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath));

var dbPath = Environment.GetEnvironmentVariable("TRAINARENA_DB")
    ?? Path.Combine(builder.Environment.ContentRootPath, "trainarena.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

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
app.MapGet("/health", () => Results.Ok(new { status = "ok", app = "TrainArena" }));
app.MapGet("/api/quizzes", async (AppDbContext db) =>
{
    var list = await db.Quizzes
        .AsNoTracking()
        .OrderBy(q => q.Title)
        .Select(q => new { q.Id, q.Title, QuestionCount = q.Questions.Count })
        .ToListAsync();
    return Results.Ok(list);
});
app.Run();

public partial class Program;
