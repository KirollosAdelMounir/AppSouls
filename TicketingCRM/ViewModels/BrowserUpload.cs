namespace TicketingCRM.ViewModels;
public sealed record BrowserUpload(string Filename,Stream Content,CancellationToken CancellationToken,Guid OperationId);
