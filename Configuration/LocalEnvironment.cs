using System.IO;

namespace TableSnap.Configuration;

internal static class LocalEnvironment
{
    private static readonly HashSet<string> AllowedKeys =
    [
        "OPENAI_API_KEY",
        "TABLESNAP_MODEL"
    ];

    public static void Load()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, ".env"),
            Path.Combine(AppContext.BaseDirectory, ".env")
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null) return;

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            var separator = trimmed.IndexOf('=');
            if (separator < 1) continue;

            var key = trimmed[..separator].Trim();
            if (!AllowedKeys.Contains(key) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                continue;

            var value = trimmed[(separator + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];

            if (value.Length > 0)
                Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
        }
    }
}
