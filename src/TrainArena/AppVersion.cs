using System.Reflection;

namespace TrainArena;

public static class AppVersion
{
    public static string Informational { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? typeof(AppVersion).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>UI/API display — strips optional <c>+metadata</c> (e.g. commit id).</summary>
    public static string Display
    {
        get
        {
            var v = Informational;
            var plus = v.IndexOf('+');
            return plus >= 0 ? v[..plus] : v;
        }
    }
}
