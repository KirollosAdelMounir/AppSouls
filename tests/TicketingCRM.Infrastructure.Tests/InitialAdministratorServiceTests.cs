using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Tenancy.Interfaces;
using TicketingCRM.Infrastructure.Administration;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;
public sealed class InitialAdministratorServiceTests
{
    private sealed record Scope(Guid TenantId) : ITenantContext;
    [PostgreSqlFact]
    public async Task ProtectedSetupCreatesExactlyOneAdministratorAndClosesAgainstReplayAndConcurrentClaims()
    {
        // A newly-created disposable database is essential: bootstrap authority cannot be tested in an existing populated database.
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION"));
        var database = "appsouls_bootstrap_" + Guid.NewGuid().ToString("N");
        await using var administration = new NpgsqlConnection(builder.ConnectionString); await administration.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE {database}", administration)) await command.ExecuteNonQueryAsync();
        builder.Database = database;
        var root = Path.Combine(Path.GetTempPath(), "appsouls-first-admin-" + Guid.NewGuid().ToString("N"));
        var keyDirectory = Path.Combine(root, "key"); Directory.CreateDirectory(keyDirectory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(keyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString, x => x.MigrationsHistoryTable("__PostgreSqlMigrationsHistory")).Options;
            await using var db = new PostgreSqlAppDbContext(options, new Scope(Guid.Empty)); await db.Database.MigrateAsync();
            var authorization = new InitialSetupAuthorization(new(Path.Combine(root, "vault", "config"), Path.Combine(keyDirectory, "master")));
            await authorization.PrepareAsync();
            using var content = JsonDocument.Parse(await File.ReadAllTextAsync(authorization.TokenPath));
            var token = content.RootElement.GetProperty("Token").GetString()!;
            var service = new InitialAdministratorService(new ServerConfiguration { DatabaseConnectionString = builder.ConnectionString }, authorization);
            var input = new InitialAdministratorRequest { SetupToken = new string('Z', 64), OrganizationName = "First vendor", DisplayName = "Initial administrator", Email = "first@example.test", Password = "Initial-Password-Verification-123!" };
            Assert.True(await service.IsAvailableAsync());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(input)); Assert.Empty(await db.Users.ToListAsync());
            input.SetupToken = token;
            async Task<bool> Attempt()
            {
                try { await service.CreateAsync(input); return true; }
                catch (Exception e) when (e is UnauthorizedAccessException or InvalidOperationException) { return false; }
            }
            var outcomes = await Task.WhenAll(Attempt(), Attempt()); Assert.Single(outcomes, x => x);
            var user = Assert.Single(await db.Users.AsNoTracking().ToListAsync());
            Assert.True(user.IsPlatformAdministrator); Assert.True(user.EmailConfirmed); Assert.False(user.TwoFactorEnabled);
            Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user, user.PasswordHash!, input.Password));
            var membership = Assert.Single(await db.TenantMemberships.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            Assert.Equal("Owner", membership.Role); Assert.Equal(user.Id, membership.UserId);
            var tenant = Assert.Single(await db.Tenants.IgnoreQueryFilters().AsNoTracking().ToListAsync()); Assert.Equal(tenant.Id, membership.TenantId);
            Assert.False(await service.IsAvailableAsync()); Assert.False(File.Exists(authorization.TokenPath));
            // Even a newly prepared file cannot reopen bootstrap after the committed administrator exists.
            await authorization.PrepareAsync();
            using var fresh = JsonDocument.Parse(await File.ReadAllTextAsync(authorization.TokenPath)); input.SetupToken = fresh.RootElement.GetProperty("Token").GetString()!;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(input)); Assert.Single(await db.Users.AsNoTracking().ToListAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var command = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", administration); await command.ExecuteNonQueryAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
