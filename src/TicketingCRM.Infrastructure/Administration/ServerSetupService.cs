using TicketingCRM.Core.Security;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Administration.Interfaces;
namespace TicketingCRM.Infrastructure.Administration;

public sealed partial class ServerSetupService(ServerConfiguration? runningConfiguration, FileConfigurationVault vault, VaultBootstrapOptions bootstrap,
    PendingAdministratorFile pending, string? presetPath = null, ILogger<ServerSetupService>? logger = null) : IServerSetupService
{
    private async Task<bool> IsOpen(CancellationToken ct)
    {
        // Read committed configuration too: a completed setup closes even in this pre-reload service instance.
        var configuration = File.Exists(bootstrap.VaultPath) ? await vault.LoadAsync(ct) : runningConfiguration;
        if (configuration is null) return true;
        await using var db = PreparedAdministratorSeeder.Open(configuration);
        return !await db.Users.AnyAsync(ct);
    }
    public async Task<ServerSetupState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        bool open; try { open = await IsOpen(cancellationToken); } catch { open = false; }
        if (!open) return new(false, false, false, "PostgreSql", "");
        var preset = await Preset(cancellationToken);
        return new(true, pending.Exists, preset is not null, preset?.DatabaseProvider.ToString() ?? "PostgreSql", preset?.PublicBaseUrl ?? "https://localhost:7176", SetupDatabaseConnection.Read(preset));
    }
    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = pending.AcquireSetupLock();
        if (!await IsOpen(cancellationToken)) throw new InvalidOperationException("Initial setup is closed.");
        var prepared = await pending.LoadAsync(cancellationToken, allowExpired: true);
        prepared.ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(24); await pending.SaveAsync(prepared, cancellationToken);
    }
    public async Task PrepareAdministratorAsync(PrepareAdministratorRequest request, CancellationToken cancellationToken = default)
    {
        await using var gate = pending.AcquireSetupLock();
        if (!await IsOpen(cancellationToken)) throw new InvalidOperationException("Initial setup is closed.");
        if (pending.Exists) throw new InvalidOperationException("An administrator is already prepared. Continue with migration.");
        var password = request.Password ?? ""; PasswordPolicy.Validate(password);
        var name = (request.DisplayName ?? "").Trim(); var company = (request.OrganizationName ?? "").Trim(); var email = (request.Email ?? "").Trim();
        if (name.Length is < 1 or > 200 || company.Length is < 1 or > 200 || email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new ArgumentException("Enter your name, organization and a valid email address.");
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        await pending.SaveAsync(new PreparedAdministrator
        {
            UserId = user.Id,
            TenantId = Guid.NewGuid(),
            DisplayName = name,
            OrganizationName = company,
            Email = email,
            PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(24)
        }, cancellationToken);
    }
    public async Task MigrateAndActivateAsync(CompleteServerSetupRequest request, CancellationToken cancellationToken = default)
    {
        try { await ActivateCoreAsync(request, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger?.LogWarning("Setup activation failure category {FailureType} at {Location}", exception.GetType().Name, exception.StackTrace); throw SetupFailures.Classify(exception); }
    }
    private async Task ActivateCoreAsync(CompleteServerSetupRequest request, CancellationToken cancellationToken)
    {
        await using var gate = pending.AcquireSetupLock();
        if (!await IsOpen(cancellationToken)) throw new InvalidOperationException("Initial setup is closed.");
        request.DatabaseConnectionString ??= ""; request.PublicBaseUrl ??= ""; request.EmailHost ??= ""; request.SenderAddress ??= ""; request.EmailUserName ??= ""; request.EmailPassword ??= "";
        if (!request.ConfirmMigration) throw new ArgumentException("Confirm database migration to activate the system.");
        var prepared = await pending.LoadAsync(cancellationToken, allowExpired: true);
        if (prepared.ExpiresAtUtc <= DateTimeOffset.UtcNow) { prepared.ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(24); await pending.SaveAsync(prepared, cancellationToken); }
        if (!Enum.TryParse<DatabaseProvider>(request.DatabaseProvider, out var provider) || !Enum.IsDefined(provider)) throw new ArgumentException("Choose a database provider.");
        var preset = await Preset(cancellationToken);
        var hasFields = !string.IsNullOrWhiteSpace(request.DatabaseServer) || !string.IsNullOrWhiteSpace(request.DatabaseCatalog) || !string.IsNullOrWhiteSpace(request.DatabaseUserName) || !string.IsNullOrEmpty(request.DatabasePassword);
        var connection = hasFields ? SetupDatabaseConnection.Build(provider, request, preset) : request.DatabaseConnectionString.Trim();
        if (connection.Length == 0 && preset is not null && preset.DatabaseProvider == provider) connection = preset.DatabaseConnectionString;
        if (connection.Length is < 1 or > 8192) throw new ArgumentException("Enter a valid database connection.");
        if (request.EmailHost.Length > 254 || request.SenderAddress.Length > 254 || request.EmailUserName.Length > 320 || request.EmailPassword.Length > 8192)
            throw new ArgumentException("Email configuration exceeds its limits.");
        if (request.EmailEnabled && !MailAddress.TryCreate(request.SenderAddress, out _)) throw new ArgumentException("Enter a valid sender email.");
        var keyDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(bootstrap.KeyPath)!, "..", "auth-keys"));
        using var rsa = RSA.Create(3072);
        var configuration = new ServerConfiguration
        {
            DatabaseProvider = provider,
            DatabaseConnectionString = connection,
            PublicBaseUrl = request.PublicBaseUrl.Trim(),
            DataProtectionKeyDirectory = keyDirectory,
            LicenseSigningPrivateKeyPem = rsa.ExportRSAPrivateKeyPem(),
            Smtp = new SmtpConfiguration
            {
                Enabled = request.EmailEnabled,
                Host = request.EmailHost.Trim(),
                Port = request.EmailPort,
                FromAddress = request.SenderAddress.Trim(),
                UserName = request.EmailUserName,
                Password = request.EmailPassword,
                RequireTls = true
            }
        };
        configuration.Validate();
        ServerConfiguration? existing = null;
        if (File.Exists(bootstrap.VaultPath))
        {
            existing = await vault.LoadAsync(cancellationToken);
            if (existing.DatabaseProvider != provider || existing.DatabaseConnectionString != connection)
                throw new InvalidOperationException("An initialized vault cannot switch databases during initial setup.");
        }
        // File material remains available if migration, seed or vault activation fails.
        await PreparedAdministratorSeeder.MigrateAndSeedAsync(configuration, prepared, cancellationToken);
        if (existing is not null) await vault.SaveAsync(configuration, cancellationToken);
        else if (File.Exists(bootstrap.KeyPath)) await vault.ResumeInitializationAsync(configuration, cancellationToken);
        else await vault.InitializeAsync(configuration, cancellationToken);
        // Activation is committed. Cleanup failure must not trap the host in setup mode after the vault was saved.
        Cleanup(pending.Complete, "administrator material");
        Cleanup(() => { if (presetPath is not null && File.Exists(presetPath)) File.Delete(presetPath); }, "setup preset");
    }
    private void Cleanup(Action action, string category)
    {
        try { action(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        { logger?.LogWarning("Server setup activated but temporary {Category} cleanup needs operator attention.", category); }
    }
    private async Task<ServerConfiguration?> Preset(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(presetPath) || !File.Exists(presetPath)) return runningConfiguration;
        if (!Path.IsPathFullyQualified(presetPath)) throw new InvalidOperationException("Setup preset requires an absolute path.");
        PrivateFileSystem.PrepareDirectory(presetPath);
        if (!Path.IsPathFullyQualified(presetPath) || new FileInfo(presetPath).LinkTarget is not null || new FileInfo(presetPath).Length > 65536)
            throw new InvalidOperationException("Setup preset is invalid.");
        PrivateFileSystem.CheckFile(presetPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }; options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Deserialize<ServerConfiguration>(await File.ReadAllTextAsync(presetPath, ct), options) ?? throw new InvalidOperationException("Setup preset is invalid.");
    }
}
