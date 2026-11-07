using Microsoft.EntityFrameworkCore;
using TicketingCRM.DataConnection.Configuration;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.Domain.Forms.DTOs;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Infrastructure.Forms;
using Xunit;
namespace TicketingCRM.Infrastructure.Tests;

public sealed class FormServiceTests
{
    private static FormService Service(CustomerDeliveryFixture f, CustomerDeliveryFixture.Actor? actor = null) => new(new WorkspaceSessionFactory(new CustomerDeliveryFixture.Factory(f.Tenant)), actor ?? f.OwnerActor);
    private static PublicFormService Public() => new(new PublicFormSessionFactory(DatabaseProvider.PostgreSql, Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!));
    private static CreateFormRequest Request() => new("Tell us what you think", "Feedback and contact details", [new("name", "Name", "Text", true, []), new("email", "Email", "Email", false, []), new("vote", "Your choice", "Choice", true, ["Yes", "No"]), new("notes", "Notes", "Long text", false, [])]);
    private static SubmitFormRequest Answers() => new(Guid.NewGuid(), new() { ["name"] = "Visitor", ["email"] = "visitor@example.test", ["vote"] = "Yes", ["notes"] = "Useful feedback" });
    private static async Task<FormSummary> Published(CustomerDeliveryFixture f)
    {
        var service = Service(f); var id = await service.CreateAsync(Request());
        var draft = Assert.Single((await service.ListAsync()).Items, x => x.Id == id);
        await service.ChangeStateAsync(id, new(draft.Version, true));
        return Assert.Single((await service.ListAsync()).Items, x => x.Id == id);
    }

    [PostgreSqlFact]
    public async Task DraftIsPrivatePublishExposesOnlyQuestionsAndCloseRetainsResponses()
    {
        using var f = await CustomerDeliveryFixture.Create(); var service = Service(f); var id = await service.CreateAsync(Request());
        var draft = Assert.Single((await service.ListAsync()).Items); Assert.Equal("Draft", draft.Status);
        var publicService = Public(); Assert.Null(await publicService.GetAsync(draft.PublicKey));
        await Assert.ThrowsAsync<ArgumentException>(() => publicService.SubmitAsync(draft.PublicKey, Answers()));
        await service.ChangeStateAsync(id, new(draft.Version, true));
        var visible = await publicService.GetAsync(draft.PublicKey); Assert.NotNull(visible); Assert.Equal(Request().Fields.Select(x => (x.Key, x.Label, x.Type, x.Required)), visible.Fields.Select(x => (x.Key, x.Label, x.Type, x.Required))); Assert.Equal(new[] { "Yes", "No" }, visible.Fields[2].Options);
        await publicService.SubmitAsync(draft.PublicKey, Answers());
        var current = Assert.Single((await service.ListAsync()).Items);
        await service.ChangeStateAsync(id, new(current.Version, false));
        Assert.Null(await publicService.GetAsync(draft.PublicKey));
        await Assert.ThrowsAsync<ArgumentException>(() => publicService.SubmitAsync(draft.PublicKey, Answers()));
        var saved = Assert.Single((await service.SubmissionsAsync(id)).Items); Assert.Equal("Useful feedback", saved.Answers["notes"]);
        var closed = Assert.Single((await service.ListAsync()).Items); Assert.Equal(1, closed.SubmissionCount);
        await service.ChangeStateAsync(id, new(closed.Version, true)); Assert.NotNull(await publicService.GetAsync(draft.PublicKey));
    }
    [PostgreSqlFact]
    public async Task ReceiptIsIdempotentAndChangedRetryCannotOverwriteAnswers()
    {
        using var f = await CustomerDeliveryFixture.Create(); var form = await Published(f); var request = Answers(); var publicService = Public();
        await publicService.SubmitAsync(form.PublicKey, request); await publicService.SubmitAsync(form.PublicKey, request);
        var changed = request with { Answers = new(request.Answers) { ["vote"] = "No" } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => publicService.SubmitAsync(form.PublicKey, changed));
        var response = Assert.Single((await Service(f).SubmissionsAsync(form.Id)).Items); Assert.Equal("Yes", response.Answers["vote"]);
        Assert.Equal(1, Assert.Single((await Service(f).ListAsync()).Items).SubmissionCount);
    }
    [PostgreSqlFact]
    public async Task ForeignTenantsCustomersTechnicalStaffAndRevokedSessionsCannotReadOrManage()
    {
        using var f = await CustomerDeliveryFixture.Create(); using var other = await CustomerDeliveryFixture.Create(); var form = await Published(f);
        Assert.Empty((await Service(other).ListAsync()).Items);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(other).SubmissionsAsync(form.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(other).ChangeStateAsync(form.Id, new(form.Version, false)));
        foreach (var actor in new[] { f.CustomerActor, f.TechnicalActor, f.OwnerActor with { SecurityStamp = "revoked" }, f.TechnicalActor with { Role = "Admin" } })
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f, actor).ListAsync());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f, actor).CreateAsync(Request()));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f, actor).SubmissionsAsync(form.Id));
        }
        await Public().SubmitAsync(form.PublicKey, Answers());
        await using var db = CustomerDeliveryFixture.Open(f.Tenant);
        Assert.Equal(f.Tenant, Assert.Single(await db.FormSubmissions.ToListAsync()).TenantId);
    }
    [PostgreSqlFact]
    public async Task TamperedChoicesUnknownFieldsMissingRequiredInvalidEmailAndOversizeResponsesDoNotPersist()
    {
        using var f = await CustomerDeliveryFixture.Create(); var form = await Published(f);
        foreach (var pair in new[] { ("vote", "Other"), ("name", " "), ("email", "not an email"), ("tenantId", Guid.NewGuid().ToString()), ("notes", new string('x', 2001)) })
        {
            var request = Answers(); request.Answers[pair.Item1] = pair.Item2;
            await Assert.ThrowsAsync<ArgumentException>(() => Public().SubmitAsync(form.PublicKey, request));
        }
        Assert.Empty((await Service(f).SubmissionsAsync(form.Id)).Items);
        Assert.Equal(0, Assert.Single((await Service(f).ListAsync()).Items).SubmissionCount);
    }
    [PostgreSqlFact]
    public async Task InactiveTenantAndInvalidPublicCapabilitiesRejectSubmission()
    {
        using var f = await CustomerDeliveryFixture.Create(); var form = await Published(f);
        Assert.Null(await Public().GetAsync(new string('a', 64)));
        await Assert.ThrowsAsync<ArgumentException>(() => Public().SubmitAsync("bad", Answers()));
        await using (var db = CustomerDeliveryFixture.Open(f.Tenant)) { (await db.Tenants.SingleAsync()).IsActive = false; await db.SaveChangesAsync(); }
        Assert.Null(await Public().GetAsync(form.PublicKey));
        await Assert.ThrowsAsync<ArgumentException>(() => Public().SubmitAsync(form.PublicKey, Answers()));
    }
    [PostgreSqlFact]
    public async Task StalePublicationVersionCannotCloseAfterReceiptAndResponseLimitIsEnforced()
    {
        using var f = await CustomerDeliveryFixture.Create(); var form = await Published(f);
        await Public().SubmitAsync(form.PublicKey, Answers());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).ChangeStateAsync(form.Id, new(form.Version, false)));
        await using (var db = CustomerDeliveryFixture.Open(f.Tenant)) { (await db.CollectionForms.SingleAsync()).SubmissionCount = 10000; await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<ArgumentException>(() => Public().SubmitAsync(form.PublicKey, Answers()));
        Assert.Single((await Service(f).SubmissionsAsync(form.Id)).Items);
    }
    [PostgreSqlFact]
    public async Task InvalidQuestionDefinitionsAreRejectedBeforePersistence()
    {
        using var f = await CustomerDeliveryFixture.Create(); var service = Service(f);
        var valid = Request();
        foreach (var request in new[] { valid with { Title = " " }, valid with { Fields = [] }, valid with { Fields = [new("same", "One", "Text", true, []), new("same", "Two", "Email", false, [])] }, valid with { Fields = [new("vote", "Vote", "Choice", true, ["Yes", "Yes"])] }, valid with { Fields = [new("file", "Upload", "File", false, [])] } })
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));
        Assert.Empty((await service.ListAsync()).Items);
    }
    [PostgreSqlFact]
    public async Task SubmissionsAreBoundedAndPagedWithoutLosingOrRepeatingRecords()
    {
        using var f = await CustomerDeliveryFixture.Create(); var form = await Published(f);
        for (var i = 0; i < 21; i++) await Public().SubmitAsync(form.PublicKey, Answers());
        var first = await Service(f).SubmissionsAsync(form.Id); var second = await Service(f).SubmissionsAsync(form.Id, 2);
        Assert.Equal(20, first.Items.Count); Assert.True(first.HasMore); Assert.Single(second.Items); Assert.False(second.HasMore);
        Assert.Equal(21, first.Items.Concat(second.Items).Select(x => x.Id).Distinct().Count());
    }
    private sealed class PausedReceiptFactory(TicketingCRM.DataConnection.Repository.Interfaces.IPublicFormSessionFactory inner) : TicketingCRM.DataConnection.Repository.Interfaces.IPublicFormSessionFactory
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<TicketingCRM.DataConnection.Repository.Interfaces.IWorkspaceSession?> OpenAsync(string key, CancellationToken ct = default)
        {
            var session = await inner.OpenAsync(key, ct);
            return session is null ? null : new PausedSession(session, this);
        }
        private sealed class PausedSession(TicketingCRM.DataConnection.Repository.Interfaces.IWorkspaceSession inner, PausedReceiptFactory owner) : TicketingCRM.DataConnection.Repository.Interfaces.IWorkspaceSession
        {
            public TicketingCRM.DataConnection.Repository.Interfaces.IRepository<T, Guid> Repository<T>() where T : class => inner.Repository<T>();
            public void ClearTracking() => inner.ClearTracking();
            public Task<int> SaveChangesAsync(CancellationToken ct = default) => CommitAsync(ct);
            public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default) => inner.ExecuteInTransactionAsync(action, ct);
            public async Task<int> CommitAsync(CancellationToken ct = default)
            {
                owner.Reached.SetResult(); await owner.Resume.Task.WaitAsync(ct); return await inner.CommitAsync(ct);
            }
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
    [PostgreSqlFact]
    public async Task CloseRacingWithReceiptRollsBackSubmission()
    {
        using var f = await CustomerDeliveryFixture.Create(); var form = await Published(f);
        var gate = new PausedReceiptFactory(new PublicFormSessionFactory(DatabaseProvider.PostgreSql, Environment.GetEnvironmentVariable("APPSOULS_TEST_DB_CONNECTION")!));
        var pending = new PublicFormService(gate).SubmitAsync(form.PublicKey, Answers());
        await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try { await Service(f).ChangeStateAsync(form.Id, new(form.Version, false)); }
        finally { gate.Resume.TrySetResult(); }
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => pending);
        Assert.Empty((await Service(f).SubmissionsAsync(form.Id)).Items);
        var closed = Assert.Single((await Service(f).ListAsync()).Items); Assert.Equal("Closed", closed.Status); Assert.Equal(0, closed.SubmissionCount);
    }
    [PostgreSqlFact]
    public async Task PersistenceRejectsForeignFormRelationshipsAndChangesToPublishedQuestionsOrReceivedAnswers()
    {
        using var f = await CustomerDeliveryFixture.Create(); using var other = await CustomerDeliveryFixture.Create(); var form = await Published(f);
        await using (var foreign = CustomerDeliveryFixture.Open(other.Tenant))
        {
            foreign.FormSubmissions.Add(new FormSubmission { TenantId = other.Tenant, FormId = form.Id, OperationId = Guid.NewGuid(), SubmittedAtUtc = DateTimeOffset.UtcNow });
            await Assert.ThrowsAsync<DbUpdateException>(() => foreign.SaveChangesAsync());
        }
        await using (var db = CustomerDeliveryFixture.Open(f.Tenant))
        {
            (await db.CollectionForms.SingleAsync()).Title = "Changed meaning";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await Public().SubmitAsync(form.PublicKey, Answers());
        await using (var db = CustomerDeliveryFixture.Open(f.Tenant))
        {
            (await db.FormSubmissions.SingleAsync()).AnswersJson = "{}";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        Assert.Equal("Yes", Assert.Single((await Service(f).SubmissionsAsync(form.Id)).Items).Answers["vote"]);
    }

}
