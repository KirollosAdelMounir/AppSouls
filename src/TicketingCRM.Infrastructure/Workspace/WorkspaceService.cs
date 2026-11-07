using System.Net.Mail;
using System.Text.RegularExpressions;
using TicketingCRM.DataConnection.Repository.Queries;

using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.Domain.Identity.Interfaces;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Workspace.Interfaces;

namespace TicketingCRM.Infrastructure.Workspace;

public sealed class WorkspaceService(IWorkspaceSessionFactory sessions, ICurrentActor actor) : IWorkspaceService
{
    private async Task AuthorizeAsync(IWorkspaceSession session,bool staff,CancellationToken ct)=>await TicketingCRM.Infrastructure.Security.WorkspaceAccess.RequireAsync(session,actor,sales:staff,ct:ct);
    public async Task<ContractDto> ContractAsync(Guid id,CancellationToken cancellationToken=default)
    {
        await using var s=await sessions.OpenAsync(cancellationToken);await AuthorizeAsync(s,false,cancellationToken);
        var row=await s.Repository<Contract>().GetReadOnlyQueryable().SingleOrDefaultAsync(x=>x.Id==id,cancellationToken)??throw new ArgumentException("Contract not found.");
        if(actor.CustomerId is not null||actor.Role=="Technical Developer")throw new UnauthorizedAccessException();
        return new(row.Id,row.CustomerId,row.Reference,row.Status,row.StartsAtUtc,row.EndsAtUtc);
    }
    public async Task<WorkspaceSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var s = await sessions.OpenAsync(cancellationToken); await AuthorizeAsync(s, false, cancellationToken);
        var customers = s.Repository<Customer>().GetAllQueryable(); var contracts = s.Repository<Contract>().GetAllQueryable(); var entitlements = s.Repository<Entitlement>().GetAllQueryable();
        if (actor.CustomerId is { } customer) { customers = customers.Where(x => false); contracts = contracts.Where(x => false); entitlements = entitlements.Where(x => x.CustomerId == customer); }
        if (actor.Role == "Technical Developer") { customers = customers.Where(x => false); contracts = contracts.Where(x => false); entitlements = entitlements.Where(x => false); }
        var products = s.Repository<Product>().GetAllQueryable();
        if (actor.CustomerId is { } c) products = products.Where(p => s.Repository<Entitlement>().GetAllQueryable().Any(e => e.ProductId == p.Id && e.CustomerId == c));
        return new(await customers.OrderBy(x => x.Name).Take(100).Select(x => new CustomerDto(x.Id,x.Name,x.Email,x.CompanyNumber,x.CreatedAtUtc)).ToListAsync(cancellationToken),
            await products.OrderBy(x => x.Name).Take(100).Select(x => new ProductDto(x.Id,x.Name,x.Code,x.LicenseExtension)).ToListAsync(cancellationToken),
            await contracts.OrderByDescending(x => x.StartsAtUtc).Take(100).Select(x => new ContractDto(x.Id,x.CustomerId,x.Reference,x.Status,x.StartsAtUtc,x.EndsAtUtc)).ToListAsync(cancellationToken),
            await entitlements.OrderByDescending(x => x.StartsAtUtc).Take(100).Select(x => new EntitlementDto(x.Id,x.CustomerId,x.ProductId,x.ContractId,x.InstallationLimit,x.StartsAtUtc,x.ExpiresAtUtc,x.SupportExpiresAtUtc)).ToListAsync(cancellationToken));
    }
    public async Task<Guid> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var name = Text(request.Name,160); var email = Text(request.Email,254); if (!MailAddress.TryCreate(email,out _)) throw new ArgumentException("Enter a valid email address.");
        await using var s = await sessions.OpenAsync(cancellationToken); await AuthorizeAsync(s,true,cancellationToken);
        var item = new Customer { TenantId=actor.TenantId,Name=name,Email=email,CompanyNumber=Text(request.CompanyNumber,100,false),CreatedAtUtc=DateTimeOffset.UtcNow }; s.Repository<Customer>().Add(item); await s.CommitAsync(cancellationToken);return item.Id;
    }
    public async Task<Guid> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var extension=Text(request.LicenseExtension,24);if (!Regex.IsMatch(extension,@"^\.[a-zA-Z][a-zA-Z0-9]{1,20}$") || new[]{".exe",".dll",".js",".sh",".bat",".cmd",".ps1"}.Contains(extension.ToLowerInvariant())) throw new ArgumentException("Choose a safe license file extension.");
        await using var s=await sessions.OpenAsync(cancellationToken);await TicketingCRM.Infrastructure.Security.WorkspaceAccess.RequireAsync(s, actor, developer: true, ct: cancellationToken);
        if(actor.Role == "Technical") throw new UnauthorizedAccessException();
        var item=new Product{TenantId=actor.TenantId,Name=Text(request.Name,160),Code=Text(request.Code,64),LicenseExtension=extension};s.Repository<Product>().Add(item);await s.CommitAsync(cancellationToken);return item.Id;
    }
    public async Task<Guid> CreateContractAsync(CreateContractRequest request,CancellationToken cancellationToken=default)
    {
        await using var s=await sessions.OpenAsync(cancellationToken);await AuthorizeAsync(s,true,cancellationToken);
        if(!await s.Repository<Customer>().GetAllQueryable().AnyAsync(x=>x.Id==request.CustomerId,cancellationToken))throw new ArgumentException("Customer not found.");
        if(request.EndsAtUtc<=request.StartsAtUtc)throw new ArgumentException("End must follow start.");
        var item=new Contract{TenantId=actor.TenantId,CustomerId=request.CustomerId,Reference=Text(request.Reference,100),StartsAtUtc=request.StartsAtUtc.ToUniversalTime(),EndsAtUtc=request.EndsAtUtc?.ToUniversalTime()};s.Repository<Contract>().Add(item);await s.CommitAsync(cancellationToken);return item.Id;
    }
    public async Task<Guid> ActivateContractAsync(Guid id,CancellationToken cancellationToken=default)
    {
        await using var s=await sessions.OpenAsync(cancellationToken);await AuthorizeAsync(s,true,cancellationToken);
        var item=await s.Repository<Contract>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==id,cancellationToken)??throw new ArgumentException("Contract not found.");
        if(item.Status is not ("Draft" or "Active"))throw new InvalidOperationException("Contract cannot be activated.");item.Status="Active";await s.CommitAsync(cancellationToken);return item.Id;
    }
    public async Task<Guid> GrantEntitlementAsync(GrantEntitlementRequest request,CancellationToken cancellationToken=default)
    {
        await using var s=await sessions.OpenAsync(cancellationToken);await AuthorizeAsync(s,true,cancellationToken);
        var contract=await s.Repository<Contract>().GetAllQueryable().SingleOrDefaultAsync(x=>x.Id==request.ContractId && x.Status=="Active",cancellationToken)??throw new ArgumentException("An active contract is required.");
        if(request.InstallationLimit is <1 or >100000 || !await s.Repository<Product>().GetAllQueryable().AnyAsync(x=>x.Id==request.ProductId && x.IsActive,cancellationToken))throw new ArgumentException("Check product and installation limit.");
        if(request.ExpiresAtUtc<=contract.StartsAtUtc || contract.EndsAtUtc is not null && (request.ExpiresAtUtc is null || request.ExpiresAtUtc>contract.EndsAtUtc))throw new ArgumentException("License term must fall within the contract term.");
        if(request.SupportExpiresAtUtc <= contract.StartsAtUtc)throw new ArgumentException("Support expiry must follow the contract start date.");
        var item=new Entitlement{TenantId=actor.TenantId,CustomerId=contract.CustomerId,ContractId=contract.Id,ProductId=request.ProductId,InstallationLimit=request.InstallationLimit,StartsAtUtc=contract.StartsAtUtc,ExpiresAtUtc=request.ExpiresAtUtc?.ToUniversalTime(),SupportExpiresAtUtc=request.SupportExpiresAtUtc?.ToUniversalTime()};s.Repository<Entitlement>().Add(item);await s.CommitAsync(cancellationToken);return item.Id;
    }
    internal static string Text(string? value,int max,bool required=true){var text=value?.Trim()??"";if(required&&text.Length==0||text.Length>max)throw new ArgumentException($"Text must contain {(required?"1":"0")} to {max} characters.");return text;}
}
