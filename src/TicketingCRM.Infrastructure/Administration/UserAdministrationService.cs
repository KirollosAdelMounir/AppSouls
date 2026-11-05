using TicketingCRM.Core.Security;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Identity;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Administration.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;

namespace TicketingCRM.Infrastructure.Administration;

public sealed class UserAdministrationService(IWorkspaceSessionFactory sessions, ICurrentActor actor) : IUserAdministrationService
{
    private async Task AuthorizeAsync(IWorkspaceSession session, CancellationToken ct) => await TicketingCRM.Infrastructure.Security.WorkspaceAccess.RequireAsync(session, actor, administrator: true, ct: ct);
    public async Task<IReadOnlyList<ManagedUserDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var session = await sessions.OpenAsync(cancellationToken);
        await AuthorizeAsync(session, cancellationToken);
        return await (from member in session.Query<TenantMembership>()
                      join user in session.Query<ApplicationUser>() on member.UserId equals user.Id
                      orderby user.DisplayName, member.Id
                      select new ManagedUserDto(member.Id, user.Id, user.DisplayName, user.Email ?? "", member.Role,
                          member.CustomerId, member.IsActive, member.Version)).Take(100).ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateAsync(CreateManagedUserRequest request, CancellationToken cancellationToken = default)
    {
        await using var session = await sessions.OpenAsync(cancellationToken);
        await AuthorizeAsync(session, cancellationToken);
        var name = request.DisplayName.Trim(); var email = request.Email.Trim();
        if (name.Length is < 1 or > 200) throw new ArgumentException("Enter a name of up to 200 characters.");
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new ArgumentException("Enter a valid email address.");
        PasswordPolicy.Validate(request.InitialPassword);
        if (!request.EmailOwnershipVerified) throw new ArgumentException("Verify the user's email ownership before creating an account.");
        if (request.Role is not ("Admin" or "Technical" or "Customer")) throw new ArgumentException("Choose a supported user role.");
        if (request.Role == "Customer")
        {
            if (request.CustomerId is null || !await session.Query<Customer>().AnyAsync(x => x.Id == request.CustomerId, cancellationToken))
                throw new ArgumentException("Choose a customer in this workspace.");
        }
        else if (request.CustomerId is not null) throw new ArgumentException("Staff accounts cannot be bound to a customer.");
        var normalized = email.ToUpperInvariant();
        // Never adopt or reset a global identity merely because a vendor supplies its email.
        if (await session.Query<ApplicationUser>().AnyAsync(x => x.NormalizedEmail == normalized || x.NormalizedUserName == normalized, cancellationToken))
            throw new InvalidOperationException("This account cannot be created. Use a distinct email or the authorized membership provisioning procedure.");
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = normalized,
            Email = email,
            NormalizedEmail = normalized,
            DisplayName = name,
            EmailConfirmed = true,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, request.InitialPassword);
        var member = new TenantMembership { TenantId = actor.TenantId, UserId = user.Id, Role = request.Role, CustomerId = request.CustomerId };
        session.Add(user); session.Add(member);
        await session.CommitAsync(cancellationToken); // One atomic SaveChanges transaction for identity and membership.
        return member.Id;
    }

    public async Task ChangeAccessAsync(ChangeMemberAccessRequest request, CancellationToken cancellationToken = default)
    {
        await using var session = await sessions.OpenAsync(cancellationToken);
        await AuthorizeAsync(session, cancellationToken);
        var member = await session.Query<TenantMembership>().SingleOrDefaultAsync(x => x.Id == request.MembershipId, cancellationToken)
            ?? throw new ArgumentException("Member not found.");
        if (member.UserId == actor.UserId || member.Role == "Owner") throw new ArgumentException("Owner and current-account access cannot be changed here.");
        if (member.Version != request.ExpectedVersion) throw new InvalidOperationException("The member changed. Refresh before trying again.");
        member.IsActive = request.IsActive;
        await session.CommitAsync(cancellationToken);
    }
}
