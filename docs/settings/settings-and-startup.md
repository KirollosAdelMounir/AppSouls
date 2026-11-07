# Settings parity and automatic existing-installation upgrades

Account Settings (`/account/settings`) and System Settings (`/settings`) now fill the available screen width, with responsive sections. Account security and database maintenance use the same wide settings layout. Sign-in keeps its compact layout.

System Settings includes the structured database fields used during initial setup: provider, host, port, catalog, username, password and TLS. Both screens share `DatabaseConnectionFields` and `ServerConnectionViewModel`; provider-specific connection strings are built on the server. Saved passwords are never returned. A blank password preserves the existing connection only when provider, host, port, catalog, username and TLS match exactly. Database checks inspect server/catalog/schema without migrating or saving. Database copy/switch operations retain the existing confirmed maintenance handoff and transfer progress.

The screen also exposes organization name, public HTTPS address, all SMTP fields, a TLS/authentication check, integration credentials, and links to account name/password and user administration. An SMTP check sends no email, saves no configuration and never reuses a saved password against a different host, port or username. Failed checks retain typed values for correction. Public address changes update the encrypted vault and the running configuration after a successful save. Organization names update only the authenticated selected tenant. Global identity email is displayed in Account Settings; this change does not introduce an unverified identity/email replacement path.

## Startup policy approved on 2026-11-06

A configured installation validates its current provider migration history and schema, then automatically applies pending migrations **before** the HTTP host and outbox worker start. A current database does not receive migration-history writes. An incompatible or failed upgrade stops startup with a redacted diagnostic; no application traffic is served against an unverified schema. This path requires a recognized installation with an existing account and never creates or initializes a fresh catalog. The first migration, prepared administrator and activation still use explicitly confirmed guided setup. Explicit copy/switch maintenance and recovery workflows remain available.

The ordering pauses this process's requests and workers. Provider migration locks serialize migration execution; they do not stop writers in other processes. Multi-instance deployments must coordinate their rollout and drain other writers. Existing backups and provider-specific migration compatibility remain operational responsibilities. Failed multi-migration runs may have committed earlier successful migrations; startup revalidates the recorded lineage on retry rather than claiming the entire upgrade can always be rolled back.

## Verification

`dotnet build TicketingCRM.slnx` passed with zero warnings/errors. `dotnet test TicketingCRM.slnx --no-build` against an isolated PostgreSQL cluster: **Passed: 171, Failed: 0, Skipped: 6**. Skips cover five native Windows storage tests and the cross-provider copy test requiring a separate SQL Server test database. The final SMTP redaction adjustment was also verified with `dotnet test tests/TicketingCRM.Infrastructure.Tests/TicketingCRM.Infrastructure.Tests.csproj --no-build --filter FullyQualifiedName~ServerSettingsServiceTests`: **Passed: 4, Failed: 0, Skipped: 0**.

| Requirement | Evidence |
| --- | --- |
| Existing upgrades preserve identities; current startup is read-only | `DatabaseLifecycleTests.StartupUpgradePreservesExistingIdentityAndCurrentStartupDoesNotWriteHistory` |
| No automatic first migration; unknown history is rejected | `DatabaseLifecycleTests.StartupNeverCreatesOrInitializesAFreshInstallationAndRejectsUnknownHistory` |
| Masked structured settings, tenant scope, URL validation and revoked-session denial | `ServerSettingsServiceTests.SettingsExposeStructuredConnectionWithoutSecretsAndKeepOrganizationScoped` |
| SMTP TLS required, no send/save, exact-target password preservation | `ServerSettingsServiceTests.EmailCheckRequiresTlsDoesNotSendAndNeverPersistsTestValuesOrForwardsSavedSecretsToChangedTarget` |
| Read-only structured database checks and target password restrictions | `ServerSettingsServiceTests.StructuredDatabaseChecksReuseOnlyExactConnectionPasswordsAndNeverModifyVault` |
| Unauthorized maintenance operations cannot run | `DatabaseMaintenanceAuthorizationTests.UnauthorizedCallerCannotInspectScheduleReadProgressOrResumeServices` |

Browser acceptance used an isolated installation deliberately seeded at the migration before PublicForms. Startup upgraded it, Forms queries succeeded, and the prepared account remained usable. Checks covered populated database fields and exact-target password indication, read-only database checks, changed-target validation, general setting saves, an unsaved SMTP check, profile saving, Light/Dark reload persistence, wide desktop layouts, 375px layouts, keyboard selection/Escape focus restoration and reduced motion. First-time setup still prepared its administrator and tested a missing catalog without creating it or bypassing migration confirmation. Final checks also covered the standalone maintenance page and protected OpenAPI contracts.

The configured guided installation was then started through the new path. Its SQL Server catalog changed from one pending PublicForms migration to `Current` with zero pending migrations, and its sign-in page returned HTTP 200. The existing vault connection and account credentials were retained.
