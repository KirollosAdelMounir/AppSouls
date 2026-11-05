# Customer delivery threat model v1

Scope: account settings, customer onboarding, mapped licenses and installation delivery. Baseline: proposed OWASP ASVS 5.0.0 L2, with selected privileged/cryptographic L3 controls. This document records implemented boundaries and required assurance work; it does not certify compliance or promise immunity to breaches.

## Assets and trust boundaries

Assets include authenticated membership/stamps, customer identity and subscriptions, invitation proofs, issuer signing keys, license artifacts, private installation packages and notification jobs. Boundaries are the browser/controller, authenticated tenant services, pre-authentication invitation locator, database, encrypted vault/data-protection key ring, SMTP provider, private filesystem and local malware daemon. Operators, issuer keys and scanner deployment must be trusted and separately protected.

## Requirements and evidence

| ID | Requirement | Evidence |
| --- | --- | --- |
| CD-AUTH-1 | Validate active tenant, actual membership/role/customer and authenticated security stamp on operations. | Shared `WorkspaceAccess`; existing service authorization suite; customer/role/stamp denial cases. |
| CD-OWN-1 | Request IDs do not authorize another customer or tenant; relationship constraints prevent cross-scope joins. | Tenant filters; composite customer/opportunity/contract and membership FKs; invitation foreign-relationship test; subscriber/foreign download tests. |
| CD-INV-1 | No usable account, password or verified email before proof acceptance; do not adopt existing global identities. | Atomic pending identity/membership/invitation/outbox; global-identity rejection test; proof validation before scoped claims. |
| CD-INV-2 | Proofs are high entropy, hashed at rest, encrypted in the outbox, expiring, single-use and race-safe. | Wrong/expired/replay/concurrent claim tests; reissue invalidates old proof; no proof in GET URL query. |
| CD-ACC-1 | Self-service password changes require current password and revoke previous sessions. | `SelfProfileAndPasswordChangesRespectMembershipAndRevokeOldSessionStamp`; rate-limited antiforgery-protected form handler. |
| CD-LIC-1 | Customer mappings are allowlisted, type checked, resolved from owned data and immutable in issued artifacts. | `MappedAttributesAreResolvedFromOwnedCustomerAndCannotBeOverridden`; existing artifact tampering and ownership cases. |
| CD-FILE-1 | Private bounded uploads never execute or become downloadable before a clean scan and signature. | Path/empty/duplicate/quota checks; quarantine tests; unavailable/rejected scanner denial. |
| CD-FILE-2 | Signed metadata must match the release record; verified bytes, current subscriptions and revocation gate every download. | Hash/signature/metadata mutation tests; unentitled, foreign-tenant and revoked entitlement/release denial. |
| CD-AUD-1 | Preserve stage/activity actors and UTC timestamps; published schemas, activities and published/revoked release metadata cannot be rewritten. | Immutable history guards, sales follow-up tests and publishing/revocation actor fields. |
| CD-WEB-1 | Controllers enforce authentication, antiforgery, rate limits, safe error bodies and cancellation. | Controller attributes, HTTP antiforgery rejection checks and local authenticated acceptance flow. |

## Residual qualification

An independent security assessment and abuse testing remain required before production. Outstanding qualification includes live SMTP delivery/crash races, real ClamAV daemon/signatures/limits, filesystem/database interruption recovery and retention, aggregate temporary-upload concurrency/volume pressure, distributed rate limiting/edge DDoS protection, backup and key recovery drills, load/accessibility budgets and native Windows/FreeBSD support. Selected implementation controls do not replace host hardening, patching, least-privilege database/scanner accounts, TLS deployment or incident response. No high/critical production finding is accepted by this milestone; future findings require fixes or a time-limited owner-approved acceptance under project standards.
