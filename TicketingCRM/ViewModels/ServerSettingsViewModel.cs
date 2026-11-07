using TicketingCRM.Domain.Configuration.DTOs;
using TicketingCRM.Domain.Configuration.Interfaces;
namespace TicketingCRM.ViewModels;

public sealed class ServerSettingsViewModel(IServerSettingsService Service)
{
    public TicketingCRM.Domain.Delivery.DTOs.ArtifactScannerAvailability? Scanner;
    public Task CheckScanner() => Save(async () => Scanner = await Service.CheckScannerAsync(), reload: false, clearSecrets: false);
    public ServerSettingsDto? Settings; public bool Loading = true, Busy, EmailEnabled; public string? Error, Message;
    public static readonly string[] Providers = ServerConnectionViewModel.Providers;
    public string PublicBaseUrl = "", OrganizationName = "";
    public string Provider = "PostgreSql", Connection = "", Host = "", Sender = "", UserName = "", Password = "", IntegrationName = "", IntegrationUrl = "", CredentialName = "", CredentialValue = ""; public int Port = 587;
    public async Task InitializeAsync() { try { await Load(); } catch (UnauthorizedAccessException) { } catch { Error = "Server configuration is unavailable."; } finally { Loading = false; } }
    private async Task Load() { Settings = await Service.GetAsync(); PublicBaseUrl = Settings.PublicBaseUrl; OrganizationName = Settings.OrganizationName; Provider = Settings.DatabaseProvider; Host = Settings.SmtpHost; Sender = Settings.SenderAddress; UserName = Settings.SmtpUserName; EmailEnabled = Settings.EmailEnabled; Port = Settings.SmtpPort; }
    public Task SavePublicAddress() => Save(async () => { await Service.UpdatePublicAddressAsync(new(PublicBaseUrl)); Message = "Public address saved."; });
    public Task SaveOrganization() => Save(async () => { await Service.UpdateOrganizationAsync(new(OrganizationName)); Message = "Organization name saved."; });
    public Task TestEmail() => Save(async () => { var result = await Service.TestEmailAsync(new(EmailEnabled, Host, Port, Sender, UserName, string.IsNullOrEmpty(Password) ? null : Password)); if (result.Success) Message = result.Message; else Error = result.Message; }, reload: false, clearSecrets: false);
    public Task SaveEmail() => Save(async () => { await Service.UpdateEmailAsync(new(EmailEnabled, Host, Port, Sender, UserName, string.IsNullOrEmpty(Password) ? null : Password)); Password = ""; Message = "Email settings saved. Queued notifications will use the configured provider."; });
    public Task SaveIntegration() => Save(async () => { await Service.UpdateIntegrationAsync(new(IntegrationName, IntegrationUrl, CredentialName, string.IsNullOrEmpty(CredentialValue) ? null : CredentialValue)); CredentialValue = ""; Message = "Integration configuration saved securely."; });
    private async Task Save(Func<Task> action, bool reload = true, bool clearSecrets = true) { if (Busy) return; Busy = true; Error = null; Message = null; try { await action(); if (reload) await Load(); } catch (ArgumentException e) { Error = e.Message; } catch (UnauthorizedAccessException) { Error = "Platform administrator access is required."; } catch { Error = "Settings could not be saved. The previous configuration has been retained."; } finally { if (clearSecrets) { Connection = ""; Password = ""; CredentialValue = ""; } Busy = false; } }

}
