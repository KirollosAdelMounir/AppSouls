# Customer lifecycle, license mappings and product delivery

Implemented development milestone, 4 November 2026. App Souls is the product; Tetrarchia is the company. This milestone is not a completed auto-updater client or a production security certification.

## Workspaces and responsibilities

- The top-right account menu displays the authenticated member's initials/name, links to account settings/security, and submits sign-out with antiforgery protection. Account settings support the current user's display name and password. Password changes verify the old password, enforce the common policy and rotate the security stamp to invalidate existing sessions. MFA remains optional; previously enrolled accounts still verify it.
- Customer directory, product catalog and contract management have separate components and view models. Shared operation state, feedback, record tables and selection/date conversion helpers replace repeated orchestration. Views consume Domain contracts. `WorkspaceAccess` centralizes membership, role, active-tenant and security-stamp checks; services retain resource ownership rules.
- `/license-models` owns model construction and language exports. `/licenses` handles installation-related issuance/downloads. The builder no longer lives in the installation workflow.
- `/customers/{id}` composes commercial status, subscriptions, support references, onboarding and a bounded activity timeline. Customer accounts see their own relationships and subscriptions; internal activity/history and staff details are hidden. Technical members can record technical/general notes, while commercial pipeline details require Owner/Admin access.
- `/downloads` manages installation packages and presents subscribed customers' available software. Customer and staff actions are authorized in the backend, independently of visible controls.

## Sales and customer onboarding

1. Create a lead at `/sales`; record calls, meetings, emails, tasks and notes with UTC follow-ups. Recording an Email activity records a conversation; it does not send an email.
2. Qualify and convert the lead. Existing atomic conversion creates its customer and opportunity and retains the lead history. Record further activities against either sales record.
3. Advance the opportunity through Qualified, Proposal and Negotiation to Won. Create and activate a customer contract and grant the purchased software with installation capacity and validity dates.
4. Open the customer lifecycle. Finalization requires a won opportunity, an active/current contract and a current subscription belonging to that customer. One database transaction creates the pending global identity, inactive Customer membership, hashed invitation proof and encrypted notification outbox entry. Existing global identities are never adopted or reset by email match.
5. The SMTP outbox queues an expiring, 48-hour password-setup link. UI acknowledgement says **queued**, never delivered. SMTP must be enabled in server settings. The proof is encrypted in outbox storage; its hash is stored on the invitation. Email URLs put the proof in the fragment so ordinary server request URLs do not contain it. The page copies it into the POST body and removes the fragment from history. Responses disable caching and referrer disclosure.
6. The customer follows the email and chooses a policy-compliant password. A pre-authentication locator finds only the proof's candidate tenant; it grants no authority. The proof is verified before opening the scoped unit of work, then invitation state, active tenant, original security stamp, pending identity and inactive customer membership are rechecked. Password, verified-email state, membership activation and consumed proof commit together. Concurrency and replay checks permit one claim.
7. Owners/admins may reissue an unaccepted invitation for the exact original pending account. Old proofs are invalidated, obsolete pending notifications are superseded, and one replacement notification plus an actor-stamped activity are persisted. Active/global identities cannot be reset using this operation.

Detailed quotations, immutable order acceptance, amendments, multiple customer contacts and advanced CRM reporting remain separate approved work. This milestone provides the connected lead-to-customer subscription path, not those additional commercial documents.

## License model mappings

Published fields have a stable attribute ID, label, primitive type, required/null settings and an approved source. Sources include customer identity/name/email/company number; product identity/name/code; contract reference; installation identity/name; purchased installation limit; and subscription start/expiry. The source automatically selects a compatible type in the builder; backend validation independently enforces it. Perpetual expiry mappings must allow null.

Issuance resolves mapped values from the installation's own customer, entitlement, product and contract in its tenant-scoped operation. Manual input cannot override a mapped field. Inputs and resulting values pass the common wire-format validator. The signed/encrypted artifact freezes the resolved customer and entitlement snapshot; later CRM edits do not alter an issued artifact. Schema changes create new immutable revisions. Generated C#, JavaScript, Java and Python models preserve the wire format and contain no private keys.

Customers can retrieve only license revisions belonging to their own customer membership. Installation enrollment and staff issuance remain explicit. A product-side import/renewal SDK, additional encryption profiles and isolated custom providers remain separate implementation work.

## Installer releases and future update hub

Technical, Admin and Owner members can upload a versioned package for an active product. Supported platform labels are Windows, Linux, FreeBSD and Cross-platform; labels do not qualify native runtime support. Supported package suffixes are `.zip`, `.msi`, `.exe`, `.deb`, `.rpm`, `.pkg` and `.gz`. Files stream into private storage outside public content, use opaque IDs, reject supplied paths, and are bounded to 512 MB. A workspace is limited to 10 GB of recorded package storage; allocation uses a tenant concurrency write. Retention beyond this development milestone requires an operator procedure. Database and filesystem writes are not described as a distributed transaction; interruption/orphan recovery still needs operational qualification.

Uploads are quarantined. Publishing verifies the stored SHA-256, requires a clean scanner verdict, checks integrity again, signs a versioned manifest, records the publishing actor/time and makes the release available. Rejected/unavailable scans cannot publish. Published metadata is immutable; revocation records actor/time and blocks downloads. Revoked history cannot be edited.

The built-in scanner adapter uses **local ClamAV clamd INSTREAM**. Configure an integration named `malware-scanner` in server settings with credential name `Port` containing the daemon's local TCP port. Leave the base URL blank. Only loopback scanner communication is supported. Qualify daemon availability, signature freshness, archive/resource limits and scan-size limits before release; do not expose clamd publicly. No scanner is provisioned automatically and no successful real-daemon scan is claimed by unit tests using a fake scanner.

Customer download authorization requires their current, unrevoked product subscription, active/current contract and active product. Every file and manifest request rechecks ownership. File retrieval verifies both signature-bound metadata and bytes and streams the verified handle. Private filenames and storage paths are never public URLs.

The manifest contract returns exact JSON, a Base64 RSA-PSS/SHA-256 signature, `SignatureContext` and the issuer public key. Verification bytes are UTF-8 of `SignatureContext + "\n" + Json`. Future clients must pin a separately trusted issuer key, verify scope/hash/size/platform/version, enforce rollback policy and use atomic installation/recovery. A key supplied in an untrusted manifest response is not its own trust anchor. This release provides the authenticated download and signed-manifest foundation; scheduled machine update discovery and installer execution are not implemented.
