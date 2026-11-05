using TicketingCRM.Domain.Administration.DTOs;
using TicketingCRM.Domain.Administration.Interfaces;
namespace TicketingCRM.ViewModels;
public sealed class ServerSetupViewModel(IServerSetupService service)
{
    public ServerSetupState? State {get;private set;}
    public string? Error {get;private set;}
    public async Task LoadAsync()
    {try{State=await service.GetStateAsync();}catch{Error="Setup status is unavailable. Check the protected server bootstrap files.";}}
}
