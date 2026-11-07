using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketingCRM.DataConnection.DbContexts;
using TicketingCRM.DataConnection.Repository;
using TicketingCRM.DataConnection.Repository.Interfaces;
using TicketingCRM.DataConnection.Repository.Implementations;
using TicketingCRM.DataConnection.Transactions;
using TicketingCRM.Domain.Persistence.Models;
using TicketingCRM.Domain.Tenancy.Interfaces;
using Xunit;
namespace TicketingCRM.DataConnection.Tests;

public sealed class RepositoryTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly Guid tenant = Guid.NewGuid(), foreignTenant = Guid.NewGuid(), customer = Guid.NewGuid(), foreignCustomer = Guid.NewGuid();
    private sealed record Scope(Guid TenantId) : ITenantContext;
    private AppDbContext Open(Guid scope) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options, new Scope(scope));
    public RepositoryTests()
    {
        connection.Open();
        using (var db = Open(tenant))
        {
            db.Database.EnsureCreated();
            db.Tenants.Add(new Tenant { Id = tenant, TenantId = tenant, Name = "A" });
            db.Customers.Add(new Customer { Id = customer, TenantId = tenant, Name = "A customer" });
            for (var i = 1; i <= 3; i++) db.Contracts.Add(new Contract { TenantId = tenant, CustomerId = customer, Reference = "Contract " + i, Status = i == 3 ? "Draft" : "Active" });
            db.SaveChanges();
        }
        using var other = Open(foreignTenant);
        other.Tenants.Add(new Tenant { Id = foreignTenant, TenantId = foreignTenant, Name = "B" });
        other.Customers.Add(new Customer { Id = foreignCustomer, TenantId = foreignTenant, Name = "B private customer" });
        other.Contracts.Add(new Contract { TenantId = foreignTenant, CustomerId = foreignCustomer, Reference = "Foreign", Status = "Active" });
        other.SaveChanges();
    }
    [Fact]
    public async Task PredicatesTypedAndStringIncludesStayTenantScopedAndMaterializeRelatedEntities()
    {
        await using var db = Open(tenant); var repo = new Repository<Contract, Guid>(db);
        var item = await repo.GetByUniqueDetailAsync(x => x.Reference == "Contract 1", default, x => x.Customer);
        Assert.NotNull(item); Assert.Equal("A customer", item.Customer.Name);
        db.ChangeTracker.Clear();
        var included = await repo.GetQueryableWithIncludes(x => x.Status == "Active", "Customer").OrderBy(x => x.Reference).ToListAsync();
        Assert.Equal(2, included.Count); Assert.All(included, x => Assert.Equal(customer, x.Customer.Id));
        Assert.True(await repo.AnyAsync(x => x.Reference == "Contract 2")); Assert.Equal(2, await repo.CountAsync(x => x.Status == "Active"));
        Assert.False(await repo.AnyAsync(x => x.Reference == "Foreign"));
        var same = await repo.GetByIdAsync(item.Id, default, x => x.Customer); Assert.Equal(item.Id, same!.Id); Assert.Equal(tenant, same.Customer.TenantId);
    }
    [Fact]
    public async Task QuerySupportsNestedFilteredIncludesStablePagingAndProjection()
    {
        await using var db = Open(tenant); var repo = new Repository<Contract, Guid>(db);
        var query = repo.GetQuery(new RepositoryQuery<Contract> {
            Predicate = x => x.Reference.StartsWith("Contract"),
            Includes = q => q.Include(x => x.Customer).ThenInclude(x => x.Contracts.Where(c => c.Status == "Active")),
            OrderBy = q => q.OrderBy(x => x.Reference).ThenBy(x => x.Id), Skip = 1, Take = 1 });
        var item = Assert.Single(await query.ToListAsync()); Assert.Equal("Contract 2", item.Reference);
        Assert.Equal(2, item.Customer.Contracts.Count); Assert.All(item.Customer.Contracts, x => Assert.Equal("Active", x.Status));
        var projection = await repo.GetReadOnlyQueryable(x => x.Status == "Active").OrderBy(x => x.Reference).Take(1).Select(x => new { x.Reference, Customer = x.Customer.Name }).SingleAsync();
        Assert.Equal("Contract 1", projection.Reference); Assert.Equal("A customer", projection.Customer);
        Assert.Throws<ArgumentException>(() => repo.GetQuery(new() { Skip = 1, Take = 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => repo.GetQuery(new() { Take = 0 }));
    }
    [Fact]
    public async Task ReadOnlyQueriesDoNotTrackOrPersistChangesAndCancellationIsForwarded()
    {
        await using var db = Open(tenant); var repo = new Repository<Contract, Guid>(db);
        var item = await repo.GetReadOnlyQueryable(x => x.Reference == "Contract 1", x => x.Customer).SingleAsync();
        item.Customer.Name = "Must not persist"; Assert.Empty(db.ChangeTracker.Entries()); Assert.Equal(0, await db.SaveChangesAsync());
        await using var verify = Open(tenant); Assert.Equal("A customer", (await verify.Customers.SingleAsync()).Name);
        var queried = await repo.GetQuery(new() { Tracking = false, Predicate = x => x.Reference == "Contract 2" }).SingleAsync(); Assert.Equal("Contract 2", queried.Reference); Assert.Empty(db.ChangeTracker.Entries());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.GetByUniqueDetailAsync(x => true, cancellation.Token));
    }
    [Fact]
    public async Task FindNeverReturnsATrackedForeignTenantEntityAndMissingScopeCannotReadOrWrite()
    {
        await using var db = Open(tenant);
        await db.Customers.IgnoreQueryFilters().SingleAsync(x => x.Id == foreignCustomer); // Simulate a previously tracked entity: Find must still query with filters.
        var repo = new Repository<Customer, Guid>(db); Assert.Null(await repo.FindAsync(foreignCustomer)); Assert.Equal(customer, (await repo.FindAsync(customer))!.Id);
        await using var missing = Open(Guid.Empty); var missingRepo = new Repository<Customer, Guid>(missing);
        Assert.False(await missingRepo.AnyAsync(x => true)); Assert.Null(await missingRepo.GetByIdAsync(customer));
        missingRepo.Add(new Customer { TenantId = tenant, Name = "Blocked" }); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => missing.SaveChangesAsync());
        repo.Add(new Customer { TenantId = foreignTenant, Name = "Blocked foreign write" }); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task CachedRepositoriesShareAContextAndStageUntilOneCommitWithAtomicFailure()
    {
        await using var db = Open(tenant); await using var unit = new UnitOfWork(db);
        var customers = unit.Repository<Customer>(); Assert.Same(customers, unit.Repository<Customer>());
        var added = new Customer { TenantId = tenant, Name = "Staged" }; await customers.AddAsync(added);
        unit.Repository<Product>().Add(new Product { TenantId = tenant, Code = "STAGED", Name = "Staged product" });
        await using (var observer = Open(tenant)) { Assert.False(await observer.Customers.AnyAsync(x => x.Id == added.Id)); Assert.Empty(await observer.Products.ToListAsync()); }
        await unit.CommitAsync();
        await using (var observer = Open(tenant)) { Assert.True(await observer.Customers.AnyAsync(x => x.Id == added.Id)); Assert.Single(await observer.Products.ToListAsync()); }
        var failed = new Customer { TenantId = tenant, Name = "Rollback" }; customers.Add(failed);
        unit.Repository<Contract>().Add(new Contract { TenantId = tenant, CustomerId = foreignCustomer, Reference = "Invalid foreign relationship" });
        await Assert.ThrowsAsync<DbUpdateException>(() => unit.SaveChangesAsync());
        await using var final = Open(tenant); Assert.False(await final.Customers.AnyAsync(x => x.Id == failed.Id)); Assert.False(await final.Contracts.AnyAsync(x => x.Reference == "Invalid foreign relationship"));
    }
    [Fact]
    public async Task RangeChangesDetachedUpdatesAndOptimisticConcurrencyUseTheSharedCommit()
    {
        await using var db = Open(tenant); await using var unit = new UnitOfWork(db); var repo = unit.Repository<Product>();
        var products = new[] { new Product { TenantId = tenant, Code = "ONE" }, new Product { TenantId = tenant, Code = "TWO" }, new Product { TenantId = tenant, Code = "THREE" } };
        repo.AddRange(products); await unit.SaveChangesAsync(); repo.RemoveRange(products.Take(2));
        await using (var observer = Open(tenant)) Assert.Equal(3, await observer.Products.CountAsync());
        await unit.SaveChangesAsync(); Assert.Equal(1, await repo.CountAsync());
        unit.ClearTracking(); var detached = await repo.GetReadOnlyQueryable().SingleAsync(); detached.Name = "Updated detached"; repo.Update(detached); await unit.SaveChangesAsync();
        await using var staleDb = Open(tenant); var staleRepo = new Repository<Product, Guid>(staleDb); var stale = (await staleRepo.FindAsync(detached.Id))!;
        detached.Name = "Winner"; repo.Update(detached); await unit.SaveChangesAsync(); stale.Name = "Loser";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync());
        await using var verify = Open(tenant); Assert.Equal("Winner", (await verify.Products.SingleAsync()).Name);
    }
    [Fact]
    public async Task TransactionFailureDiscardsAllRepositoryWritesAndFactoryOwnsItsContext()
    {
        await using (var db = Open(tenant))
        {
            await using var unit = new UnitOfWork(db);
            await Assert.ThrowsAsync<IOException>(() => unit.ExecuteInTransactionAsync(_ => { unit.Repository<Customer>().Add(new Customer { TenantId = tenant, Name = "Aborted" }); throw new IOException("Injected failure"); }));
        }
        await using (var check = Open(tenant)) Assert.False(await check.Customers.AnyAsync(x => x.Name == "Aborted"));
        var factory = new WorkspaceSessionFactory(new Factory(this)); var session = await factory.OpenAsync();
        var repository = session.Repository<Customer>(); await session.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.CountAsync());
        Assert.Throws<ObjectDisposedException>(() => session.Repository<Customer>());
    }
    private sealed class Factory(RepositoryTests owner) : IDbContextFactory<AppDbContext> { public AppDbContext CreateDbContext() => owner.Open(owner.tenant); }
    [Fact]
    public async Task RootRegistrationResolvesRepositoriesAndUnitOfWorkAgainstTheSameScopedContext()
    {
        await using var services = new ServiceCollection().AddDataConnection().AddScoped(_ => Open(tenant)).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>(); var repository = scope.ServiceProvider.GetRequiredService<IRepository<Customer, Guid>>();
        var added = new Customer { TenantId = tenant, Name = "Same DI context" }; repository.Add(added);
        Assert.Same(added, scope.ServiceProvider.GetRequiredService<AppDbContext>().Entry(added).Entity);
        unit.Repository<Product>().Add(new Product { TenantId = tenant, Code = "SHARED" }); await unit.SaveChangesAsync();
        using var verify = Open(tenant); Assert.True(verify.Customers.Any(x => x.Id == added.Id)); Assert.True(verify.Products.Any(x => x.Code == "SHARED"));
    }
    [Fact]
    public void InfrastructureCannotReintroduceContextQueriesOrTenantFilterBypasses()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TicketingCRM.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var files = Directory.GetFiles(Path.Combine(root.FullName, "src", "TicketingCRM.Infrastructure"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)).ToArray();
        Assert.NotEmpty(files);
        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            foreach (var forbidden in new[] { "using Microsoft.EntityFrameworkCore;", "IgnoreQueryFilters(", "IDbContextFactory<", "DatabaseLifecycle.Open(", "PreparedAdministratorSeeder.Open(", ".Query<" })
                Assert.False(source.Contains(forbidden, StringComparison.Ordinal), $"{Path.GetFileName(path)} bypasses repository persistence: {forbidden}");
        }
    }
    public void Dispose() => connection.Dispose();
}
