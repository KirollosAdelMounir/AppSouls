using System.Text.Json.Nodes;
using TicketingCRM.Core.SystemConfiguration;
using Xunit;
namespace TicketingCRM.Core.Tests;
public sealed class InitialSetupAuthorizationTests
{
    [Fact]
    public async Task SetupTokenIsPrivateBoundedExpiringAndConsumed()
    {
        if (OperatingSystem.IsWindows()) return; // Qualified Windows key protection remains a separate requirement.
        var root = Path.Combine(Path.GetTempPath(), "appsouls-setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "key")); File.SetUnixFileMode(Path.Combine(root, "key"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var capability = new InitialSetupAuthorization(new(Path.Combine(root, "vault", "config"), Path.Combine(root, "key", "master")));
            Assert.False(await capability.ValidateAsync(new string('A', 64)));
            await capability.PrepareAsync();
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(capability.TokenPath));
            var content = JsonNode.Parse(await File.ReadAllTextAsync(capability.TokenPath))!; var token = content["Token"]!.GetValue<string>();
            Assert.True(await capability.ValidateAsync(token)); Assert.False(await capability.ValidateAsync("short"));
            Assert.False(await capability.ValidateAsync(new string('Z', 64)));
            await Assert.ThrowsAsync<IOException>(() => capability.PrepareAsync());
            content["ExpiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"); await File.WriteAllTextAsync(capability.TokenPath, content.ToJsonString());
            Assert.False(await capability.ValidateAsync(token)); await capability.EnsurePreparedAsync(); var renewed=JsonNode.Parse(await File.ReadAllTextAsync(capability.TokenPath))!["Token"]!.GetValue<string>(); Assert.NotEqual(token,renewed);Assert.True(await capability.ValidateAsync(renewed)); capability.Complete(); Assert.False(File.Exists(capability.TokenPath));
            await capability.PrepareAsync(); content = JsonNode.Parse(await File.ReadAllTextAsync(capability.TokenPath))!;
            Assert.NotEqual(token, content["Token"]!.GetValue<string>());
            File.SetUnixFileMode(capability.TokenPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
            await Assert.ThrowsAsync<InvalidOperationException>(() => capability.ValidateAsync(content["Token"]!.GetValue<string>()));
        }
        finally { Directory.Delete(root, true); }
    }
}
