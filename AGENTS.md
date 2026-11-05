# App Souls project standards

App Souls is the product name. Tetrarchia is the company. Keep this distinction in UI, documentation and future naming changes. Existing TicketingCRM project identifiers remain until an intentional technical rename.

The approved architecture is in `docs/planning/Tetrarchia CRM and Licensing Architecture Plan.docx`. Deliver it in reviewable phases. Never present a prototype, demonstration dataset or unimplemented security control as production functionality.

## Structure and dependencies

- `TicketingCRM/` is the Blazor Application host, containing views, view models, controllers, middleware and hubs.
- `src/TicketingCRM.Domain/` contains definitions only: models, DTOs, enums and service interfaces grouped by service. It references no solution project and contains no business implementation.
- `src/TicketingCRM.Core/` references Domain and implements reusable file processing, security, email and configuration capabilities. Configuration models may live here as the approved exception.
- `src/TicketingCRM.DataConnection/` references Domain and owns DbContexts, provider migrations, repository interfaces and implementations, transactions and outbox persistence. Repository contracts are the approved exception to Domain interface ownership.
- `src/TicketingCRM.Infrastructure/` references Domain, Core and DataConnection and implements business services.
- Application references these layers for composition. Views and view models consume Domain service contracts; they never access repositories or DbContexts directly.
- Register capabilities using root `IServiceCollection` extensions. Add `WebApplication` extensions only for actual host integration. Keep ASP.NET references away from Domain.

## Data and tenancy

Resolve tenant scope from authenticated membership and a validated tenant selection. Request tenant IDs never establish authority. Enforce tenant and customer ownership on every operation, relationship, cache, document path, background job and hub subscription. Do not add permissive default tenant contexts.

Use one context per service operation and share it across repositories in one unit of work. Explicit transaction retries must coordinate with the provider execution strategy. External effects use a durable outbox; retries require idempotency. Never claim that a database transaction can roll back a delivered email or published file.

Support PostgreSQL and SQL Server with separate migrations and provider validation. Provider switching on an existing database requires an explicit migration workflow. Do not automatically migrate production databases on every application startup.

## Security and configuration

Do not commit secrets, private keys, connection strings or recovery material. Replace appsettings configuration only when the vault bootstrap, key protection, validation and recovery are implemented. A vault cannot hold its sole unlock key. Keep bootstrap inputs minimal and outside public web content. Logs and errors must redact sensitive values.

Use reviewed cryptographic libraries, authenticated encryption and separate issuer signatures. Never embed a universal private decryption key in a client or executable. Signing private keys remain with the issuer. Reject invalid artifacts before granting rights, and preserve the previous valid artifact on failed updates.

Treat published license schemas, scheme revisions, provider versions, extensions and entitlement snapshots as immutable. Generated C#, JavaScript, Java and Python models must preserve a common wire format and never contain private keys. Custom encryption code must run in an isolated, bounded worker and cannot replace signature verification or access issuer keys.

Protected DLL distribution requires an evaluated loader, signed manifests, installation-specific key provisioning, offline startup/recovery and platform qualification. Keep renewal communication available after usage-license expiry. Encryption and obfuscation cannot guarantee immunity to memory inspection or reverse engineering. Record technical limits honestly.

Use a threat model and versioned security requirements. The proposed assurance baseline is OWASP ASVS 5.0.0 Level 2, with selected Level 3 controls for privileged and cryptographic operations. Security-sensitive changes need evidence covering authorization, tenant isolation, tampering, recovery and relevant abuse cases. Fix high and critical findings or document a time-limited owner-approved risk acceptance before production release.

## Blazor design and performance

Use UI UX Pro Max guidance for UI work. Maintain semantic light blue and dark theme tokens. Support system theme, explicit preferences, visible focus, accessible labels, responsive navigation and reduced motion. Use CSS opacity and transform animations; avoid layout-shifting effects and unnecessary animation libraries.

Keep views focused and move state and service orchestration into view models. Bound and paginate data; virtualize large lists where appropriate. Avoid blocking work, redundant rendering and long-lived DbContexts in Blazor circuits. Do not claim Angular-equivalent speed without measurements; validate navigation, rendering and network behavior against agreed budgets.

## Verification and delivery

- Build the solution with `dotnet build TicketingCRM.slnx`.
- Use the relevant installed skills before .NET test authoring or test execution.
- Add tests for meaningful business, security, compatibility and failure behavior when that behavior is implemented.
- Verify both themes, mobile layouts, keyboard navigation and reduced motion for UI changes.
- Qualify native Windows, Linux and FreeBSD runtime and dependencies independently. A Linux VM on a FreeBSD host is not native FreeBSD support.
- Keep architecture decisions and implementation status current. Report what is implemented, how it was verified and what remains.

## Mandatory interaction and motion rules

Every new or changed view MUST use the shared App Souls motion system: animated sidebar hover and keyboard focus, active navigation feedback, page-entry transitions, and dropdown/calendar opening transitions. Animate opacity and transform with the shared duration/easing tokens; avoid layout shifts, repeated decorative motion and animation libraries for simple effects. Every control MUST retain a visible focus indicator and return focus after dismissal. All motion MUST honor `prefers-reduced-motion`. Verify pointer, keyboard, mobile and both themes before delivery.

Use the shared SoulDropdown for selection controls, including Appearance, and SoulDateTimePicker for date/time selection. Native time entry inside the shared picker is allowed. Do not introduce an unstyled native select as a substitute. Explicit Light/Dark preferences MUST persist across navigation and reloads; OS appearance changes apply only to System. Maintain a single appearance controller per document.

## Mandatory transfer progress

Every existing or new browser upload/download must use the shared transfer progress system, including product installers, licenses and model exports. Show the file, stage, transferred bytes, total size when known, rate, percentage when measurable, elapsed time and cancellation/failure/completion state. Scale sizes and rates automatically through B, KB, MB, GB, TB and larger units; retain exact byte counts. The current display uses decimal SI units (1000 bytes per KB).

Use `TransferReadStream` for server-consumed streams and `transfer-progress.js` for browser downloads/HTTP uploads. Download links use `download` or `data-transfer-download` and a safe filename. Future features must integrate these shared paths rather than create another progress implementation. Keep buffering bounded; use streaming file/browser sinks for large downloads. Unknown totals and database-engine stages remain indeterminate, never a fabricated percentage. Receipt of upload bytes is separate from successful validation/persistence. Browser download handoff is separate from confirmation that a file was saved to disk.

Database copy/switch/upgrade runs only after explicit maintenance confirmation, with application writes and outbox processing paused, compatible migration lineage checked, original keys retained and target data verified before changing the vault connection. PostgreSQL and SQL Server use their own migrations; cross-provider copies preserve identifiers and relationships and must fail without overwriting existing target data when conversion or constraints are incompatible.

## Bug and issue support tickets

Support tickets MUST include Bug / Issue Reports. Preserve five statuses representing four progress stages plus final closure: Submitted → Received by a technical member → Solution in progress → Completed → Closed. Do not collapse Completed into Closed. Receipt requires an assigned technical member; record actor and UTC timestamp for every transition. Resolve tenant/customer ownership from authenticated membership. Public ticket references never authorize access.

Successful submission MUST atomically persist the ticket, initial history and an outbox notification. Send an acknowledgement email to the authorized reporter containing the ticket reference and an authenticated progress link. Never show “email sent” for a queued or failed message. Keep sensitive report details out of email. Delivery retries must be idempotent; notification failure must not lose or duplicate a ticket. Tracking MUST show current stage and customer-visible history while hiding internal notes and private staff data. Completed requires a resolution; Closed requires a closure reason and authorized customer or support action. See `docs/support/bug-and-issue-workflow.md` for the feature contract and acceptance criteria.

## HTTP API implementation

All application HTTP APIs MUST use attribute-routed controllers in `TicketingCRM/Controllers/`. Do not introduce business or account routes using Minimal APIs or an Endpoints folder. Keep authorization, antiforgery validation, rate limits and cancellation forwarding explicit. Controllers consume Domain service interfaces; persistence remains behind services/repositories. OpenAPI and Scalar host mapping are infrastructure registrations, not an exception for implementing business handlers. Maintain Scalar documentation for API contracts; production documentation requires platform-administrator authorization. Never place real credentials or tokens in examples, schemas or documentation defaults.

## Approved account setup and sign-in decisions

The full architecture plan is approved for implementation; do not request approval again to continue its authorized scope. Deliver and verify reviewable phases and keep remaining work explicit.

MFA is optional for now, including administrators. Do not force enrollment or add a privileged-MFA gate. Accounts that explicitly enable MFA must still verify it; do not silently bypass enrolled MFA. The primary sign-in form contains email and password only: no Workspace ID, authenticator or recovery-code fields. For multiple authenticated memberships, present organization names through the shared selector after password verification. A protected, short-lived continuation cookie does not grant application access; revalidate membership and security stamp before issuing the actual session.

Fresh server configuration includes creating a first administrator with platform configuration access and vendor-owner membership. First-time server setup does not require an authorization token. Atomically create identity, tenant and membership, serialize concurrent claims and permanently close initial setup once an identity exists. Never ship a default administrator/password. Owners and administrators manage staff/customer accounts within their authenticated tenant. Never adopt another tenant's global identity or reset its credentials from an email match. Keep MFA optional for newly created accounts.

## Approved guided server activation sequence

Prepare the first administrator before running migrations. Persist the prepared identity in a bounded, encrypted temporary file outside public content; it contains an Identity password hash, never the password, and uses a separate owner-protected key. Migration must seed that exact prepared identity with platform administration and vendor-owner membership, then activate the protected configuration and host services. Identity, tenant and membership persist atomically. Retain prepared material on failure so retries can resume the exact identity; never adopt or replace an existing account by matching an email. Remove temporary administrator material after successful activation. Migrations remain explicit, authorized setup actions, never routine startup side effects. The guided development profile must preserve earlier databases/configuration and must not ship a default administrator/password.

The approved password minimum is 8 characters, with the existing uppercase, lowercase, number and symbol checks. Keep setup, user creation, CLI provisioning, Identity configuration and visible validation text consistent. Do not restore the former 14-character minimum.

First-time guided setup MUST NOT require or generate a server setup authorization token, token file or setup authorization cookie. Keep antiforgery validation, rate limits, encrypted temporary administrator storage, explicit migration confirmation and closure after activation.
