# Server configuration vault foundation

App Souls uses `FileConfigurationVault` in Core for local encrypted server settings. The Application host must explicitly integrate it before replacing appsettings providers. This foundation does not supply authentication, an administration UI or platform-wide production certification.

## Bootstrap contract

Construct `VaultBootstrapOptions` from two absolute operator-supplied paths: vault file and external key file. Use separate directories outside source control, deployment archives and web roots. The host must reject locations beneath its content/web root and must not log bootstrap inputs containing credentials. No key exists in application defaults. Register the vault and the validated result of `LoadAsync()` only after startup validation succeeds.

`ServerConfiguration` contains the database provider and connection string, HTTPS public base URL, and SMTP host, port, sender and credentials. SMTP defaults to disabled; enabled transport requires TLS. This object contains secrets: never serialize it into browser state, log it, or return it from controllers. Keep masked response DTOs separate.

Optional `LicenseSigningPrivateKeyPem` accepts an explicitly provisioned RSA private key of at least 3072 bits. An absent signing key permits CRM configuration but the licensing service must refuse issuance. `DataProtectionKeyDirectory` must be an absolute protected path when supplied; the host must require it before using persistent authentication cookies and configure key-at-rest protection separately. Never use the vault encryption key as a signing or authentication key.

The current backend supports Unix file protection: dedicated parent directories must be 0700, files 0600, and paths cannot traverse symbolic links. Provision directories under a trusted, operator-controlled ancestor; the service account must own the directories and keys. Do not place these files under shared directories writable by other accounts. The implementation checks mode bits, not POSIX ACL grants or file ownership. Operators must verify owner and ACLs during provisioning. Linux is the exercised platform. FreeBSD requires native qualification. Windows fails closed until a Windows ACL or native protected-key provider is implemented and qualified.

## Operations for host CLI integration

- `InitializeAsync(configuration)` validates configuration, creates a fresh random 256-bit external key and writes a new encrypted vault. Existing key or vault blocks initialization. Never call this automatically on public startup.
- `LoadAsync()` authenticates and validates settings; absent, corrupted, permissive or wrong-key files fail startup. It never creates replacement keys.
- `SaveAsync(configuration)` authenticates current state before replacing it. A local exclusive file lock serializes mutations. Temporary files use owner-only permissions from creation, are flushed before atomic same-directory rename, and are removed on failure. Multi-host coordination and stale-edit version checking are not implemented.
- `ExportRecoveryAsync(destination)` writes an authenticated ciphertext backup to a new absolute path and refuses overwrite. It intentionally excludes the unlock key.
- `RestoreRecoveryAsync(source)` verifies the backup with the separately provisioned original key before atomically replacing the active vault. This is an explicit administrative operation; it may restore an older configuration. Stop the host before restoration and revalidate database/provider compatibility before restart.

The bootstrap command should consume configuration through a protected input file or secure prompt, never command-line secrets. Delete temporary plaintext input through the operator's approved process after success. On partial initialization the external key is deliberately retained. Investigate and use an explicit operator recovery procedure; do not silently reset or regenerate keys.

## Recovery and limitations

Back up the encrypted vault and external key through separate access-controlled channels. Recovery requires both. Restore the original key with 0600 permissions into its dedicated 0700 directory, invoke `RestoreRecoveryAsync` against the backup, then verify startup. Loss of the key makes the ciphertext unrecoverable. The key is protected by filesystem access controls, not encrypted by itself or hardware-backed. Host administrators and compromised service processes can access decrypted settings. Key rotation, HSM integration, audit orchestration and rollback-resistant monotonic configuration revisions remain future work.

Envelope version 1 uses AES-256-GCM, a new random 96-bit nonce for each write, a 128-bit tag and an authenticated version header. Input is bounded to 1 MiB. Encryption plaintext buffers and key byte arrays are cleared after operations; managed configuration strings necessarily remain in application memory. Errors intentionally omit supplied values. The implementation uses .NET cryptographic primitives rather than custom cryptography.

## Verification evidence

A local isolated console probe on Linux exercised encrypted round-trip, absent plaintext credentials in ciphertext, refusal to reinitialize, save and recovery, rejection of changed ciphertext, refusal to save over damaged data, wrong-key rejection and permissive-key rejection. This is foundation smoke evidence, not a committed automated security suite or a penetration test. Production gates still require persistent regression tests, ACL and ownership validation, failure injection, concurrent writers and independent security review.

## Authentication key ring encryption

`VaultXmlKeyEncryptor` and `VaultXmlKeyDecryptor` protect newly persisted ASP.NET authentication key XML using AES-256-GCM with a separate HKDF-SHA256 purpose derived from the external vault key. Register bootstrap options and `IXmlEncryptor` as singletons, configure `KeyManagementOptions.XmlEncryptor` to that instance, and persist the ring in the explicitly protected configured directory. The decryptor uses the Data Protection `IServiceProvider` activation contract to resolve bootstrap options after process restart.

Existing plaintext ring entries do not become encrypted merely by adding this registration. Inventory and explicitly migrate or re-provision any existing ring through an approved recovery process, preserving valid session recovery requirements. Never delete an existing ring automatically. Back up both encrypted ring files and the separate external master key. Master-key rotation requires coordinated rewrapping or retaining old decryptors and keys; simply replacing the master key makes previous protected ring data unreadable.

An isolated Linux probe verified XML round-trip, ciphertext plaintext absence, tag tampering rejection and a full Data Protection protect/unprotect across separate service-provider instances with an encrypted on-disk ring. It also verified that a disabled SMTP processor returns without touching the database or claiming delivery. These remain smoke probes rather than the persistent production regression suite.

## Platform administration and live server settings

The Settings page now manages PostgreSQL/SQL Server connection strings, TLS SMTP configuration and named integration credential sets. It requires a database-verified `IsPlatformAdministrator` account, active workspace membership and a current security stamp. Vendor ownership alone does not grant global server access.

The UI never retrieves stored connection strings, passwords or integration credential values. A blank secret input preserves its existing value. Integration names and credential names are metadata; do not put secrets in names or URLs. An integration can omit its base URL when it only needs a connection string or credential set. Provided base URLs must use HTTPS without userinfo, queries or fragments. External adapters must still enforce their own network destination policy before making requests; saving an integration makes no network call.

Database changes are staged in the encrypted vault. The running database factory remains on its current connection until restart. Back up the database/vault, apply explicit migrations against the new target, and restart. PostgreSQL-to-SQL Server changes require a deliberate data migration, not merely editing the provider field. SMTP settings update the running sender after an authenticated vault save. Disabled SMTP leaves acknowledgements queued.

Administrators provision platform privileges locally with `--grant-platform-admin /absolute/path/private-user-input.json` (the file contains an existing account's Email). Input files must have private permissions. This command does not expose an unauthenticated web setup path.

Local recovery commands:

- `--vault-update /absolute/path/private-server-input.json` updates an existing authenticated vault; preserve every required configuration field in the input.
- `--vault-export /absolute/path/recovery/server.backup` writes ciphertext only to a new protected recovery location.
- `--vault-restore /absolute/path/recovery/server.backup` authenticates before replacing the vault; the matching external key must still be available.

Back up the external unlock key separately. Without it, vault backups and authentication key rings cannot be recovered. Do not place plaintext initialization files in the repository or webroot. Remove temporary plaintext provisioning inputs after recording needed administrator access in an appropriate password manager.

## Guided setup with prepared administrator

A new unconfigured server now opens `/setup` directly. The flow follows the approved order: **prepare administrator → migrate database → seed prepared administrator → activate services → sign in**. First-time setup opens directly without an authorization token, token file or setup authorization cookie. Antiforgery checks and rate limits remain enabled. The first form prepares organization/name/email and a confirmed password. It writes an AES-GCM encrypted `.administrator.pending` file with an Identity password hash, random identity/tenant IDs and security stamp; a separate `.administrator.key` protects that file. It does not create database schema or accounts at this stage.

The second form chooses PostgreSQL/SQL Server, a database connection, public HTTPS address and optional TLS SMTP settings. The operator must confirm migration. After migrations succeed, one transaction seeds the exact prepared user, tenant and Owner membership. The seeded user also has platform-configuration privileges; MFA is optional. The server saves its protected vault, removes the temporary administrator file/key, reloads host services in the same process. Sign in with the email/password prepared in step 1. The screen never returns stored credentials.

If migration, seed or vault activation fails, the prepared identity remains for retry. A database committed immediately before a vault failure can resume only when it contains the exact prepared identity and membership. A different existing identity is rejected. Interrupted first-vault creation can resume using its existing protected unlock key; it cannot overwrite an active vault. Cleanup failures after activation emit a redacted operator warning rather than trapping the host in setup mode. Resuming or retrying an expired prepared administrator renews its lifetime without changing its password hash or identity. Setup remains closed once the configured database contains an identity, including in an existing pre-reload service instance.

`dotnet run --project TicketingCRM` defaults to the `guided-setup` development profile. It stores its configuration under the local application-data `appsouls/guided-setup` directory, separate from earlier development installations. Use `--no-launch-profile` for an explicit deployment/legacy bootstrap workflow; `APPSOULS_SETUP_PROFILE=guided` selects the guided directory explicitly. Normal vault/key overrides remain available. Deployment platforms still require qualification; the file-key provider currently fails closed on Windows.

An optional private preset at `appsouls/guided-setup/setup/server-preset.json` (or `APPSOULS_SETUP_PRESET_PATH`) may supply a local development connection. It is a bootstrap input, not a committed configuration file. It must be owner-only and outside application content. The full-width setup form shows provider, server, port, database/catalog, username and TLS settings, never the stored password or full connection string. Provider-specific connection builders safely quote credentials on the server. A blank password preserves a preset only when provider, host, port, catalog, username and TLS mode all match; changing the target requires entering its password. Remote connections require certificate-validated TLS; loopback development connections may disable TLS. It is removed after activation. Future integration credentials remain configurable in authenticated server settings.

For a fresh disposable localhost test, `tools/smoke_setup.py` accepts a private input file, verifies prepare/migration/seed/reload/platform-admin sign-in, and never prints credentials. It creates accounts and applies schema and must not target an existing deployment. Existing CLI provisioning remains available. The older token-based setup route and token-generation command are removed.

### Setup connection checks and activation diagnostics

Step 2 provides **Test database connection** and **Test email connection**. Both use first-time-only, antiforgery-protected, rate-limited controller actions, with bounded connection attempts and no secret values in responses. They do not save configuration or consume the prepared administrator.

The database check builds the selected provider connection, authenticates and runs `SELECT 1` without migrating or creating accounts. It rejects an existing identity database that does not match the prepared administrator. A successful check is not a guarantee of schema migration permissions. SMTP checks mandatory TLS (implicit TLS on 465, STARTTLS on other ports) and configured authentication; it sends no message and cannot prove inbox delivery.

Connection results appear beside their buttons, with loading state and focus return. Fields are temporarily disabled while a check is running. Failed activation now returns a fixed, nonsecret reason beside the form and keeps entered settings in the current page. Missing catalog, rejected credentials, permissions, HTTPS public-address validation and existing accounts have distinct outcomes. Passwords and optional email fields accept blank values where permitted; they are not implicitly required by MVC validation. A blank database password is still allowed only for an unchanged protected preset.

The previous generic failure log recorded only an exception type, so its exact historical cause cannot be recovered. New logs include fixed failure codes and code locations, never raw provider exception messages. Unknown errors remain redacted. Setup requires an HTTPS public base address even when the local development listener uses HTTP.
