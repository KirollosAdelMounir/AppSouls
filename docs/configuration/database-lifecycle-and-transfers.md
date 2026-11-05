# Database lifecycle and transfer progress

Implemented 5 November 2026. Development verification is recorded below; this is not production certification.

## Guided setup

Prepare the first administrator before migration. Testing the database connection authenticates against the server's maintenance catalog and inspects the requested application catalog without creating it. A missing catalog is a successful server check, subject to later creation permissions.

**Apply the database migration** explicitly creates a missing catalog, applies the selected provider's migrations and seeds the exact encrypted prepared identity with platform administration and vendor-owner membership. Identity, tenant and membership are transactional. Failed activation retains prepared material for retry. Setup remains token-free and closes after activation. Normal application startup does not run migrations.

## Existing installations

Open **Server settings → Database maintenance** as a platform administrator. Verify a backup and stop every other application instance and external writer before confirming maintenance.

- **Copy** streams the current database into an empty target, including PostgreSQL → SQL Server and SQL Server → PostgreSQL. Target schema uses its own provider migrations. Existing target records are never merged or overwritten.
- **Switch** selects an existing database belonging to this installation. Current migrations cause no schema update; an older, known migration prefix is upgraded while retaining records.
- **Upgrade** applies pending migrations to the active database without entering its connection again.

Unknown, newer, gapped or foreign schemas are rejected. Inspection checks known tables and columns against the last applied migration model. The target must retain the initiating administrator's ID, security stamp and active tenant membership; an email match never authorizes adoption. Fresh setup cannot adopt another populated installation. Reuse requires the original protected installation configuration, external key and authentication ring.

The application enters a separate maintenance host before copying. HTTP gates and read-only persistence interceptors block application writes; email processing stops. Source table locks preserve the copy snapshot. Copied target records commit together after row-count verification; provider identities/sequences are preserved or reseeded. Configuration switches only after target identity and protected-token verification. The maintenance gate remains active until the old host is disposed.

## Progress

Database progress shows source/target provider and catalog, stage history, current table, copied/total rows, rows per second, logical payload bytes, payload rate and elapsed time. Percentage during copying is weighted by counted rows. Logical payload measures UTF-8 text and scalar widths, excluding indexes, compression and database protocol overhead. Engine-run schema migration, catalog creation and commit are indeterminate stages; no invented percentage or byte rate is shown for them.

The shared file-transfer panel shows filename, transferred/total payload, percentage when known, rate, elapsed time, estimated remaining time, stages and cancellation. Sizes scale through B, KB, MB, GB, TB, PB and EB using 1,000 bytes per unit. Exact bytes remain visible. Unknown totals stay indeterminate until measurable. Upload receipt is separate from validation/persistence; browser download handoff is separate from a confirmed file save.

Existing release uploads, installer downloads and license model/revision exports use this panel. New uploads/downloads must use the shared helpers, rather than introducing another progress implementation. Blazor uploads count consumed payload bytes; the reusable XHR helper counts request bytes, including multipart overhead. Downloads use a save stream when available, then a bounded service-worker bridge, then a capped 64 MB Blob fallback. Larger files on browsers lacking streaming use native download handling.

## Recovery and compatibility limits

Progress and the maintenance request are held in memory. A process crash does not automatically resume a transfer. Restart with the existing protected configuration and inspect both catalogs before retrying. Copy failures before target commit roll back records; catalog/schema creation or a committed copy can remain after a later verification/configuration failure. Keep the source and verified backup until cutover is accepted. Database transactions cannot roll back already delivered external effects.

Installation keys are retained, not rotated or exported by copying. Encrypted values from a different key ring are rejected. Custom schema objects are not transported. Provider-specific collation or constraint conflicts can reject a copy; this workflow does not merge conflicts. PostgreSQL timestamp precision differs from SQL Server's 100 ns precision, so arbitrary source values cannot be promised byte-for-byte equivalent. Qualify application-specific data before production transfer. A different Windows service identity needs an explicit protected key/configuration recovery procedure.

## Security requirements and evidence

Threats include cross-installation adoption, unauthorized maintenance, concurrent writes, schema confusion, credential leakage and partial cutover. Connection strings stay out of progress/errors; remote targets require certificate-validated TLS. Controllers require platform administration, antiforgery and rate limits. Source configuration remains active on failures before configuration commit.

| Requirement | Automated evidence |
|---|---|
| Read-only server check; explicit creation; current schema unchanged | `MissingCatalogCheckIsReadOnlyAndExplicitApplyCreatesThenLeavesCurrentHistoryUnchanged` |
| Preserve data on upgrade; reject foreign/unknown schema | `OlderAppSchemaUpgradesWithoutLosingAccountsAndForeignSchemaIsNeverModified`, `HistoryAcceptsOnlyAnOrderedKnownPrefixAndRejectsUnknownNewerOrGappedSchemas` |
| Cross-provider identity, claims, encrypted token and tenant relationships | `PostgreSqlToSqlServerAndBackPreservesIdentityClaimsEncryptedTokensAndTenantRelationships` |
| Atomic records, progress and occupied-target refusal | `CopyPreservesIdentityTenantDataAndProgressAndRefusesAnOccupiedTarget` |
| Authorization and lower-level maintenance write barrier | `UnauthorizedCallerCannotInspectScheduleReadProgressOrResumeServices`, `MaintenanceContextsAllowReadsAndRejectWritesBelowTheHttpAndBlazorBoundary` |
| Transport protection and safe session renewal | `RemoteTransfersRequireVerifiedTlsButLocalDevelopmentConnectionsRemainAvailable`, `RefreshRevalidatesSelectedMembershipAndRestoresCurrentScopeAndVerifiedMfa`, `RevokedForeignOrUnverifiedMfaSessionsAreRejected` |
| Actual consumed upload bytes, cancellation and lazy stream opening | `ReportsOnlyConsumedBytesAndLeavesInputOpen`, `CancellationDoesNotAdvanceReportedProgress`, `RejectedMetadataDoesNotOpenOrStartAnUnconsumedBrowserStream` |

Live isolated Windows checks covered fresh PostgreSQL activation, administrator sign-in, PostgreSQL-to-SQL Server copy/cutover, a 5,243,024-byte release upload and a 4,157-byte export download. Both database engines participate in automated integration tests. Browser checks covered desktop dark theme, mobile light theme without horizontal overflow and Escape focus return. Reduced-motion CSS is present; actual preference emulation, the service-worker browser fallback, native Linux/FreeBSD, multi-instance recovery drills and independent security review remain qualification work.
