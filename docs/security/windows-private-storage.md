# Windows private storage requirements and evidence

Revision 1 — 5 November 2026. Development verification on native Windows; this is not production certification. The assurance target remains ASVS 5.0.0 Level 2 with selected Level 3 controls, as defined in the architecture plan.

## Boundary and threats

The protected assets are the prepared administrator password hash/identity, vault unlock key, encrypted configuration, authentication-key material and private installer files. Untrusted local users must not receive access through inherited ACLs, permissive existing files, junctions or alternate data streams. The application must retain its prior valid state after tampering or failed recovery. The running account, host administrators and operators able to replace trusted parent directories remain trusted; this implementation cannot exclude them. Use a dedicated account and owner-controlled local installation tree with persistent ACL support. Changing accounts or moving material requires an explicit operator recovery/transfer workflow.

No request-supplied path chooses an artifact key. Artifact IDs remain opaque GUIDs and business services still authorize tenant/customer ownership. This storage change does not establish new tenant authority, change licensing signatures, run migrations on startup, or generate a guided-setup authorization token. Unix mode checks retain their existing POSIX ACL/owner limitations; Linux and FreeBSD need separate qualification.

## Requirement coverage

| Requirement | Evidence |
|---|---|
| WP-1: Create Windows directories with protected owner-only DACLs and files with effective owner-only grants inside those protected directories. The filesystem may recalculate the file DACL protection bit; validation still requires owner-only grants. Reject unsafe existing file/directory ACLs without rewriting them. | `PublicFileAndDirectoryAclsAreRejectedWithoutRewritingExistingPermissions`, plus direct owner/ACL assertions in the encrypted preparation and vault tests. |
| WP-2: Reject junction/reparse traversal and alternate data streams. | `DirectoryJunctionsAndAlternateDataStreamsAreRejected`. |
| WP-3: Encrypt and authenticate the exact prepared identity; reject tampering and expired material, retain identity on retries, clean successful material and create no setup token. | `PreparedAdministratorSurvivesRetryAndRejectsTamperingAndExpiryWithoutCreatingSetupTokens`; isolated HTTP preparation reached step 2 and retained prepared state after host restart. |
| WP-4: Preserve the existing vault/key-ring format and reject unauthenticated recovery before replacing valid state. | `VaultAndAuthenticationKeysRoundTripAndFailedRecoveryPreservesPreviousConfiguration`. |
| WP-5: Close exclusive Windows file handles before artifact publication; retain original data when publication collides. | `ArtifactPublicationClosesTheWindowsHandleAndRetainsPrivatePermissions`. |
| WP-6: Share the approved password policy across provisioning paths, accepting inclusive 8/1024 boundaries and rejecting missing character classes, null, shorter and longer inputs. | `ValidPasswordAcceptsInclusiveLengthBoundaries`, `InvalidPasswordsAreRejectedWithoutReturningThePassword`, `PasswordBeyondMaximumLengthIsRejected`. |
| Structural cleanup: isolate composition and remove repeated state/parsing/operation code while keeping Domain independent. | `dotnet build TicketingCRM.slnx`; `Program.cs` has one service-registration call backed by Infrastructure; host-specific controllers/Blazor/cookies/OpenAPI stay in Application. |
| Setup presentation retains shared controls, focus, themes and responsive layout. | Browser: provider selection updates PostgreSQL/SQL Server ports, Escape returns focus, explicit Light/Dark survives reload, no horizontal overflow at 390px and 1440px. Shared reduced-motion rules inspected; emulated reduced-motion execution remains open. |

Final automated command: `dotnet test TicketingCRM.slnx --no-build --logger trx --results-directory .git/verification/lifecycle-final` with isolated PostgreSQL and SQL Server connections. Passed: **162**, Failed: **0**, Skipped: **0** (Core 48, DataConnection 17, Infrastructure 97). Build: zero warnings/errors. See [database lifecycle evidence](../configuration/database-lifecycle-and-transfers.md) for live activation and transfer verification.

Remaining qualification: concurrent/recovery drills across real service identities and deployment ACL policies, native Linux/FreeBSD regression, reduced-motion emulation, broader authenticated-view regression, and independent security review. No production release approval or risk acceptance is implied.
