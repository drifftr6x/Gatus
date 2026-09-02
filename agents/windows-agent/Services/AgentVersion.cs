using System.Reflection;

namespace SentinelKiosk.Agent.Services;

/// <summary>
/// The running agent's version, from assembly informational version (set via
/// csproj &lt;InformationalVersion&gt;). Falls back to assembly version, then "0.0.0".
/// </summary>
public static class AgentVersion
{
    public static readonly string Current = Resolve();

    private static string Resolve()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // Strip any "+commitsha" metadata suffix
            var plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }
        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
