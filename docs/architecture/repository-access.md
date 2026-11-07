# Repository access and operation scope

The Fire User Backend reference at `Projects/Backend/User Identity` informed the query/include API and service-to-repository composition. App Souls keeps repository contracts in DataConnection, business service contracts in Domain, and service orchestration in Infrastructure.

## Normal service operations

`IWorkspaceSessionFactory.OpenAsync` creates an operation-owned `UnitOfWork` with one DbContext. Its `Repository<TEntity>()` method returns cached `IRepository<TEntity, Guid>` instances sharing that context. Infrastructure queries and stages writes through those repositories; there is no longer a workspace `Query<T>()` or `Add<T>()` bypass. Views and view models still consume only Domain service contracts.

The generic repository supports predicates; typed includes; string include paths; nested `Include`/`ThenInclude` composition; sorting, skipping and taking; projections over composed queries; tracked and read-only queries; filtered ID and unique-detail lookup; existence/count checks; cancellation; and staged add/update/remove operations, including ranges. `RepositoryQuery<TEntity>` carries query options. Skipped queries must supply ordering; callers should include a unique tie-breaker and bound list sizes. Queries must be materialized before disposing the unit of work.

`RepositoryQueryExtensions` owns asynchronous materialization in DataConnection. Infrastructure can compose LINQ without directly invoking EF context APIs. Licensing issuance now actually includes the contract's customer, using navigation mappings on the existing composite tenant/customer foreign key. Forms use repository query options for stable, bounded pagination. These navigation mappings do not require a database migration; both provider model checks report no pending changes.

Repository mutations stage changes; they do not commit individually. `CommitAsync` / `SaveChangesAsync` persists all staged records together, preserving ticket/history/outbox atomicity, idempotency guards and optimistic concurrency. Tracked updates preserve original concurrency values. Explicit transaction execution retains the existing retry guard: retry with a fresh context and an idempotent operation coordinator rather than replaying mutations against old tracked state. No generic bulk SQL mutation API was added that skips tenant-write checks, immutable-record controls or concurrency validation.

`FindAsync` deliberately queries through tenant filters even when an entity is already tracked. It does not use EF's tracked-entity shortcut. Query composition exposes no tenant-filter bypass option. Root dependency registration resolves repository and unit-of-work interfaces against the same scoped context; Blazor business services use the operation factory so a circuit does not own a long-lived DbContext.

## Specialized persistence

First-administrator creation/seeding, the password-verified sign-in directory, installation membership verification, active-tenant discovery and protected-value enumeration now live in DataConnection repositories. These narrowly defined operations retain their explicit setup, authenticated-installation or credential authority. Filter bypasses needed for pre-authentication or installation-wide discovery are confined to that data layer, rather than exposed to generic service queries.

Customer invitation activation uses a tenant-scoped repository unit of work only after validating its public invitation proof. Outbox processing uses typed repositories for message leases and ownership checks; durable state, retry timing, message IDs, redaction and the distinction between SMTP acceptance and inbox delivery are preserved. This refactor does not claim new live SMTP or crash-race qualification.

## Verification

Baseline and final `dotnet build TicketingCRM.slnx`: zero warnings/errors. Final `dotnet test TicketingCRM.slnx --no-build`, using an isolated PostgreSQL test catalog initialized once before the parallel fixture run: **Passed: 180, Failed: 0, Skipped: 6**. The skips are the existing five native Windows storage checks and one cross-provider copy check requiring separate SQL Server test configuration. Both provider `has-pending-model-changes` checks report no model changes requiring migrations. No application database was changed for this refactor.

| Requirement | Direct evidence in `RepositoryTests` |
| --- | --- |
| Predicates, typed/string includes and related entity loading under tenant scope | `PredicatesTypedAndStringIncludesStayTenantScopedAndMaterializeRelatedEntities` |
| Nested filtered includes, stable paging and projections | `QuerySupportsNestedFilteredIncludesStablePagingAndProjection` |
| Read-only tracking and cancellation | `ReadOnlyQueriesDoNotTrackOrPersistChangesAndCancellationIsForwarded` |
| Filtered lookup despite tracked foreign data; denied missing-scope reads/writes | `FindNeverReturnsATrackedForeignTenantEntityAndMissingScopeCannotReadOrWrite` |
| Cached repositories, shared context, staged commit and atomic failure | `CachedRepositoriesShareAContextAndStageUntilOneCommitWithAtomicFailure` |
| Range changes, detached updates and optimistic concurrency | `RangeChangesDetachedUpdatesAndOptimisticConcurrencyUseTheSharedCommit` |
| Transaction failure and owned-context disposal | `TransactionFailureDiscardsAllRepositoryWritesAndFactoryOwnsItsContext` |
| Root DI composition | `RootRegistrationResolvesRepositoriesAndUnitOfWorkAgainstTheSameScopedContext` |
| Guard against reintroducing context queries/filter bypasses in Infrastructure | `InfrastructureCannotReintroduceContextQueriesOrTenantFilterBypasses` |

Existing relational suites additionally exercise sales conversion, licensing artifacts and ownership, support ticket/outbox commits, forms, settings, invitation activation, initial administrator serialization and database maintenance. The source guard supplements runtime tenant/foreign-key/concurrency tests; it is not a substitute for security assessment.
