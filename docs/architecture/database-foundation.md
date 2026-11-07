# Database foundation

The application uses EF Core 10 with PostgreSQL and SQL Server providers. AppDbContext integrates ASP.NET Core Identity users and roles plus tenant memberships, customers, products, contracts, entitlements, support reports/history, durable notification outbox, immutable published license schemas, issued license revisions and installations.

## Composition

Call `AddDataConnection(provider, connectionString)` using the selected vault configuration. This registers a scoped `IDbContextFactory<AppDbContext>` and a scoped direct context for Identity integration. Business services should create and dispose one factory context per operation; do not retain a context in a Blazor circuit. Register `ITenantContext` from verified identity membership. Missing or empty scope returns no tenant rows and rejects tenant writes. Identity users and role tables are global; their authorization needs the membership directory.

For authenticated membership discovery only, a narrowly audited service may bypass the membership query filter with a predicate on the authenticated user ID. Other filter bypass, raw SQL and bulk update paths require explicit review. The data layer cannot authorize access to customer records by itself; services must apply customer ownership in addition to the tenant filter.

Tenant-owned rows use a tenant query filter, write enforcement and tenant-aware relational foreign keys. Guid concurrency tokens reject stale writes. Published schema revisions and Ready license revisions reject updates and deletion. Direct SQL can bypass application protections and must remain restricted to approved administration.

`Repository<TEntity>` and `UnitOfWork` share an explicit operation context. Transactions commit staged changes and outbox entries together. They reject automatic retry strategies: a retry coordinator must create a fresh context and consult a durable idempotency key before replay. No retry strategy is enabled by default.

## Machine renewal and enrollment

Machine credentials are random high-entropy tokens stored only as SHA-256 hashes. A global unique hash index prevents a credential mapping to multiple installations. The machine renewal lookup bypasses query filters only inside a credential-anchored join that binds tenant, customer, product, entitlement and installation and checks active/revocation states. An expired usage term does not disable retrieval of a ready renewal. Tenant headers and public references never grant machine access.

License revisions are unique per tenant, installation and revision. Enrollment increments Entitlement.EnrollmentCounter in the same save as the new installation. Its concurrency token forces a competing enrollment to fail and roll back rather than exceed capacity; the caller should retry with a fresh context and recheck the limit. Live PostgreSQL tests verify this failure path.

The InstallationLicenses migrations add a required installation association to issued licenses. Existing production license rows would require an explicit backfill to their correct installations before applying those migrations. The CredentialUniqueness migration narrows the credential hash to 64 characters and creates a global unique index; review existing hashes before applying. Initial development had no existing issued licenses to backfill.

## Explicit schema administration

For configured installations, startup validates the existing provider migration lineage and applies pending migrations before accepting requests or starting the outbox worker. Startup does not create or initialize a fresh catalog; the first migration remains part of confirmed guided activation. Keep deployment backups and review migration compatibility. Database copy/switch operations still use explicit maintenance confirmation.

Set `APPSOULS_DB_CONNECTION` for the administrative process and run `dotnet ef database update --project src/TicketingCRM.DataConnection --context PostgreSqlAppDbContext` or choose `SqlServerAppDbContext` for SQL Server. Do not place production secrets in shell history. The design-time placeholder connection supports model generation only.

Each provider has its own derived context, migration folder, snapshot and history table. Generate future changes for both contexts; inspect provider SQL and validate upgrades against both engines before release. EF tooling 10.0.11 currently installed generated the initial migrations using EF runtime 10.0.14; aligning the deployment tool version is recommended.

NuGet versions were checked against the official gallery on 2 November 2026: EF Core and Identity 10.0.14; Npgsql provider 10.0.3. System.Security.Cryptography.Pkcs is pinned to 10.0.14 to replace an affected transitive dependency identified during restore.

Sources: https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy and https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers

## Validation and limits

Both provider migrations and SQL generation were verified. Focused relational tests use SQLite to validate tenant-filter behavior, fail-closed writes, ownership immutability, cross-tenant foreign keys and concurrency. SQLite tests do not qualify PostgreSQL or SQL Server engine behavior. A live PostgreSQL test database also verified Identity membership provisioning and atomic support ticket/history/outbox commit and rollback after applying the PostgreSQL migrations. Eight tests passed with no skips on 2 November 2026. SQL Server live-engine upgrades, transaction/outbox concurrency under contention and backup/recovery tests remain required before production deployment. The live-engine test explicitly skips when APPSOULS_TEST_DB_CONNECTION is absent; supply an isolated test database to enable it.
