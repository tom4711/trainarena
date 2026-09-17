using TrainArena.Game;
using TrainArena.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<GameSessionStore>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapRazorPages();
app.MapHub<GameHub>("/hubs/game");
app.MapGet("/health", () => Results.Ok(new { status = "ok", app = "TrainArena" }));
app.Run();

public partial class Program;
