using Microsoft.AspNetCore.SignalR;

namespace TrainArena.Hubs;

/// <summary>
/// SignalR hub for live quiz rooms. Join/host commands land in Task 0.2+.
/// </summary>
public sealed class GameHub : Hub
{
}
