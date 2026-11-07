using TicketingCRM.Domain.Forms.DTOs;
namespace TicketingCRM.Domain.Forms.Interfaces;

public interface IFormService
{
    Task<FormDefinition> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateFormRequest request, CancellationToken cancellationToken = default);
    Task<FormExport> ExportAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FormPage> ListAsync(int page = 1, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateFormRequest request, CancellationToken cancellationToken = default);
    Task ChangeStateAsync(Guid id, ChangeFormStateRequest request, CancellationToken cancellationToken = default);
    Task<FormSubmissionsPage> SubmissionsAsync(Guid id, int page = 1, CancellationToken cancellationToken = default);
}
public interface IPublicFormService
{
    Task<PublicFormDto?> GetAsync(string publicKey, CancellationToken cancellationToken = default);
    Task SubmitAsync(string publicKey, SubmitFormRequest request, CancellationToken cancellationToken = default);
}
