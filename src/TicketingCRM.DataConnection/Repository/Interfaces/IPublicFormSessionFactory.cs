namespace TicketingCRM.DataConnection.Repository.Interfaces;

/// <summary>Resolves only a published public form capability; never accepts a caller's tenant ID.</summary>
public interface IPublicFormSessionFactory
{
    Task<IWorkspaceSession?> OpenAsync(string publicKey, CancellationToken cancellationToken = default);
}
