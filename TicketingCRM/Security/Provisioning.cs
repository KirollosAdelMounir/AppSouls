using TicketingCRM.Core.Security;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.Core.SystemConfiguration.Models;
using TicketingCRM.DataConnection;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.Domain.Persistence.Models;

namespace TicketingCRM.Security;

public static class Provisioning
{
    public static AppDbContext Open(ServerConfiguration configuration, Guid tenantId = default)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        var provider = (TicketingCRM.DataConnection.Configuration.DatabaseProvider)(int)configuration.DatabaseProvider;
        DependencyInjection.ConfigureProvider(options, provider, configuration.DatabaseConnectionString);
        return configuration.DatabaseProvider == TicketingCRM.Core.SystemConfiguration.Models.DatabaseProvider.PostgreSql
            ? new PostgreSqlAppDbContext(options.Options, new FixedTenant(tenantId)) : new SqlServerAppDbContext(options.Options, new FixedTenant(tenantId));
    }
    public static async Task CreateMemberAsync(ServerConfiguration configuration, string inputPath)
    {
        PrivateFileSystem.CheckFile(Path.GetFullPath(inputPath));
        var input = JsonSerializer.Deserialize<ProvisionMember>(await File.ReadAllTextAsync(inputPath)) ?? throw new ArgumentException("Invalid member input.");
        PasswordPolicy.Validate(input.Password);
        if (!System.Net.Mail.MailAddress.TryCreate(input.Email, out _) || input.Role is not ("Owner" or "Admin" or "Technical" or "Customer")) throw new ArgumentException("Invalid email or role.");
        var tenant = input.TenantId ?? Guid.NewGuid(); await using var db = Open(configuration, tenant); await using var transaction = await db.Database.BeginTransactionAsync();
        if (input.TenantId is null) { if (string.IsNullOrWhiteSpace(input.TenantName)) throw new ArgumentException("Tenant name is required."); db.Tenants.Add(new Tenant { Id = tenant, TenantId = tenant, Name = input.TenantName }); }
        else if (!await db.Tenants.AnyAsync(x => x.Id == tenant && x.IsActive)) throw new ArgumentException("Tenant not found.");
        if (input.Role == "Customer" && (input.CustomerId is null || !await db.Customers.AnyAsync(x => x.Id == input.CustomerId))) throw new ArgumentException("Customer membership requires an existing customer.");
        if (input.Role != "Customer" && input.CustomerId is not null) throw new ArgumentException("Staff cannot carry customer scope.");
        var normalized = input.Email.ToUpperInvariant(); var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized);
        if (user is null) { user = new ApplicationUser { Id = Guid.NewGuid(), UserName = input.Email, NormalizedUserName = normalized, Email = input.Email, NormalizedEmail = normalized, EmailConfirmed = true, DisplayName = input.DisplayName, SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N"), LockoutEnabled = true }; user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, input.Password); db.Users.Add(user); }
        if (await db.TenantMemberships.AnyAsync(x => x.UserId == user.Id)) throw new InvalidOperationException("Membership already exists.");
        db.TenantMemberships.Add(new TenantMembership { TenantId = tenant, UserId = user.Id, CustomerId = input.CustomerId, Role = input.Role }); await db.SaveChangesAsync(); await transaction.CommitAsync();
        Console.WriteLine($"Member provisioned. Workspace ID: {tenant}");
    }
    private sealed class ProvisionMember
    {
        public Guid? TenantId { get; set; }
        public string TenantName { get; set; } = ""; public string Email { get; set; } = ""; public string Password { get; set; } = ""; public string Role { get; set; } = "Owner"; public string DisplayName { get; set; } = ""; public Guid? CustomerId { get; set; }
    }
}
