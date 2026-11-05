using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
namespace TicketingCRM.Components.Shared;
public sealed class InvitationFormInterop(IJSRuntime runtime):IAsyncDisposable
{
 private IJSObjectReference? module;
 public async Task<bool> InitializeAsync(ElementReference form)
 {
  module=await runtime.InvokeAsync<IJSObjectReference>("import","./Components/Shared/InvitationPasswordForm.razor.js");
  return await module.InvokeAsync<bool>("initialize",form);
 }
 public async ValueTask DisposeAsync(){if(module is not null){try{await module.DisposeAsync();}catch(JSDisconnectedException){}}}
}
