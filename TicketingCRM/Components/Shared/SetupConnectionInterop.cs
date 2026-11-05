using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
namespace TicketingCRM.Components.Shared;

public sealed class SetupConnectionInterop(IJSRuntime runtime):IAsyncDisposable
{
    private IJSObjectReference? module;
    private IJSObjectReference? binding;
    public async Task InitializeAsync(ElementReference form)
    {
        module=await runtime.InvokeAsync<IJSObjectReference>("import","./Components/Shared/ServerConfigurationSetup.razor.js");
        binding=await module.InvokeAsync<IJSObjectReference>("initialize",form);
    }
    public async ValueTask DisposeAsync()
    {
        try{if(binding is not null){await binding.InvokeVoidAsync("dispose");await binding.DisposeAsync();}if(module is not null)await module.DisposeAsync();}
        catch(JSDisconnectedException) { }
    }
}
