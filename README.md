# App Souls

CRM and software licensing by Tetrarchia. The implementation is under active development; [implementation status](docs/architecture/implementation-status.md) records working features and remaining approved scope.

## Run locally

Use the .NET 10 SDK. Build with `dotnet build TicketingCRM.slnx`, then run `dotnet run --project TicketingCRM`. The default `guided-setup` profile opens the local development server at `http://localhost:5176` and keeps its vault under the local application-data `appsouls/guided-setup` directory. Earlier development databases and vaults are preserved separately.

1. Open `/setup`. First-time setup opens directly and does not require an authorization token.
2. Prepare your administrator: organization, name, email, password and confirmation. This creates a protected temporary file containing a password hash, before any schema migration or account insertion.
3. Choose PostgreSQL/SQL Server and enter the server, port, database/catalog, database username and password. App Souls constructs the connection string on the server. A protected local preset pre-fills only non-secret details; leave the password blank to keep it only for that exact connection. Then enter the public HTTPS address and optional email settings.
4. Confirm migration and activation. The server applies migrations, atomically seeds the prepared administrator/organization/membership, saves the vault, removes temporary material and reloads its services.
5. Sign in with the administrator you prepared. Create customer companies at `/customers` and staff/customer accounts at `/users`. Server settings and future integration credentials are managed at `/settings`.

MFA is optional, including for administrators. The login form contains email and password only. Multiple memberships choose organization names after password verification. Accounts that explicitly enable MFA verify it in a separate step. Email is disabled by default until configured; queued acknowledgements are never described as delivered.

See [guided setup and vault operations](docs/configuration/server-vault.md) for protected locations, recovery and platform limits. The file-key provider currently fails closed on Windows pending qualified key protection. No default administrator or password is shipped.

## Existing deployments and local administration

For an explicit CLI deployment workflow use `--no-launch-profile` and the intended `APPSOULS_VAULT_PATH`/`APPSOULS_KEY_PATH`. For example: `dotnet run --project TicketingCRM --no-launch-profile -- --migrate`. Migrations remain explicit; they never run on ordinary startup. If a prepared administrator file exists, the migration command seeds that exact user. Existing installations retain their accounts; initial setup cannot replace them.

The existing `--vault-init`, `--vault-update`, `--vault-export`, `--vault-restore`, `--provision-member` and `--grant-platform-admin` commands remain available with private files outside the repository. Vendor owners do not automatically receive platform privileges. Production defaults to loopback port 5000 behind a qualified TLS configuration; `APPSOULS_LISTEN_URLS` explicitly overrides listeners. Never commit initialization files or credentials.

## Member initialization input

A private JSON input contains `TenantName` for a new workspace (or `TenantId` for an existing one), `Email`, `DisplayName`, `Password`, `Role` (`Owner`, `Admin`, `Technical`, `Customer`) and optional `CustomerId`. Customer roles require `CustomerId`; staff roles cannot carry it. Passwords require at least 8 characters with upper/lowercase, numbers and symbols. Existing accounts gain a membership without resetting their password.

## Verify

`dotnet test TicketingCRM.slnx` runs cryptographic checks and configured relational suites. For live PostgreSQL tests set `APPSOULS_TEST_DB_CONNECTION` to an **isolated test database** through a protected environment mechanism. Relational suites skip without this setting; do not interpret skipped tests as validation. Never point tests at production. See [database verification](docs/architecture/database-foundation.md).

`python3 tools/smoke_http.py /absolute/private/existing-login.json http://localhost:5176` checks an existing development/test account's sign-in and authenticated read APIs without printing secrets or creating records. The current smoke helper supports password-only development accounts and named workspace continuation; provide a unique `WorkspaceName` in the private login file for multiple memberships. MFA HTTP checks require a dedicated test authenticator flow.

API examples are in `TicketingCRM/AppSouls.http`. Cookie-authenticated POSTs require both the session and antiforgery verification token. The machine renewal endpoint uses an installation bearer credential, independently of a usage license's expiry. See [licensing implementation](docs/licensing/core-artifact-and-exports.md) and [support workflow](docs/support/bug-and-issue-workflow.md).

## API documentation

HTTP APIs use attribute-routed controllers in `TicketingCRM/Controllers`; business routes must not use Minimal APIs. Scalar is available at `/scalar`, with the OpenAPI specification at `/openapi/v1.json`. Both are available during development and require authenticated platform-administrator access in production. MFA remains optional. Account sign-in/MFA form handlers are deliberately excluded from the API explorer.

Scalar describes session cookies, the antiforgery header for mutations and the installation bearer credential for renewal. Sign in through the application before using cookie-authenticated requests; obtain the antiforgery token from a rendered form. Never save real credentials or license keys in API examples.

Owners and administrators use `/customers` to create customer companies and `/users` to create Admin, Technical or Customer accounts and manage workspace access. Customer accounts require an existing company in the same tenant. Verify identity and email ownership before creating an account; share initial passwords privately. Existing global identities are never adopted or reset through the user-creation form. The CLI membership-provisioning procedure remains available for authorized cross-vendor memberships.

The `/sales` workspace now supports leads, opportunities, owner assignment, UTC follow-ups, stage history, pagination and atomic qualified-lead conversion. See [sales workflow and limits](docs/sales/pipeline.md). Quotations, orders and commercial fulfillment remain planned work.

## Customer delivery milestone

The top-right profile menu opens account settings/security and sign-out. The dedicated `/license-models` builder maps typed attributes to authorized customer and subscription values. `/customers/{id}` connects sales activities, contracts, subscriptions, support and expiring customer account invitations. `/downloads` supports quarantined installation uploads, malware-scan publication, signed manifests and subscription-authorized downloads. SMTP and the local scanner require server configuration; automatic updater clients and production qualification remain future work. See [customer delivery workflows](docs/customer-delivery/workflows.md) and [security boundaries](docs/security/customer-delivery-threat-model.md).

## License

See [LICENSE](LICENSE).
