using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Administration.Interfaces;
using TicketingCRM.Domain.Workspace.DTOs;
using TicketingCRM.Domain.Workspace.Interfaces;
namespace TicketingCRM.ViewModels;
public sealed class UserAdministrationViewModel(IUserAdministrationService service, IWorkspaceService workspace)
{
    public IReadOnlyList<ManagedUserDto> Users { get; private set; } = [];
    public IReadOnlyList<CustomerDto> Customers { get; private set; } = [];
    public CreateManagedUserRequest Input { get; private set; } = new();
    public bool Loading { get; private set; } = true;
    public bool Allowed { get; private set; }
    public bool Busy { get; private set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public async Task LoadAsync()
    {
        try { Users = await service.ListAsync(); Customers = (await workspace.GetAsync()).Customers; Allowed = true; }
        catch (UnauthorizedAccessException) { Allowed = false; }
        catch { Error = "Users could not be loaded. Try again."; }
        finally { Loading = false; }
    }
    public async Task CreateAsync()
    {
        if (Busy) return;
        Busy = true; Error = null; Message = null;
        try
        {
            await service.CreateAsync(Input); Input = new();
            Message = "Account created. Share its initial password through a private channel. No password was emailed.";
            await LoadAsync();
        }
        catch (ArgumentException e) { Error = e.Message; }
        catch (InvalidOperationException e) { Error = e.Message; }
        catch (UnauthorizedAccessException) { Error = "Administrator access is required."; Allowed = false; }
        catch { Error = "The account could not be created. Refresh to check its status before retrying."; }
        finally { Input.InitialPassword = ""; Busy = false; }
    }
    public async Task ChangeAccessAsync(ManagedUserDto user)
    {
        if (Busy) return;
        Busy = true; Error = null; Message = null;
        try { await service.ChangeAccessAsync(new(user.MembershipId, user.Version, !user.IsActive)); Message = "Workspace access updated."; await LoadAsync(); }
        catch (ArgumentException e) { Error = e.Message; }
        catch (InvalidOperationException e) { Error = e.Message; }
        catch (UnauthorizedAccessException) { Error = "Administrator access is required."; Allowed = false; }
        catch { Error = "Access could not be changed. Refresh before trying again."; }
        finally { Busy = false; }
    }
}
