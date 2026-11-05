using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;

namespace TicketingCRM.Security;

internal sealed record BootstrapPaths(VaultBootstrapOptions Vault, string PresetPath)
{
    internal static BootstrapPaths FromEnvironment()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "appsouls");
        if (Environment.GetEnvironmentVariable("APPSOULS_SETUP_PROFILE") == "guided") root = Path.Combine(root, "guided-setup");
        return new(new VaultBootstrapOptions(
            Environment.GetEnvironmentVariable("APPSOULS_VAULT_PATH") ?? Path.Combine(root, "vault", "server.vault"),
            Environment.GetEnvironmentVariable("APPSOULS_KEY_PATH") ?? Path.Combine(root, "keys", "server.key")),
            Environment.GetEnvironmentVariable("APPSOULS_SETUP_PRESET_PATH") ?? Path.Combine(root, "setup", "server-preset.json"));
    }

    internal void ValidateOutsideContentRoot(string contentRoot, ServerConfiguration? configuration)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentRoot));
        IEnumerable<string> paths = [Vault.VaultPath, Vault.KeyPath, PresetPath];
        if (configuration is not null) paths = paths.Append(configuration.DataProtectionKeyDirectory);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new InvalidOperationException("Setup and key paths must be absolute and outside the application content root.");
            var fullPath = Path.GetFullPath(path);
            if (fullPath.Equals(root, comparison) || fullPath.StartsWith(root + Path.DirectorySeparatorChar, comparison))
                throw new InvalidOperationException("Setup and key material must be outside the application content root.");
        }
    }
}
