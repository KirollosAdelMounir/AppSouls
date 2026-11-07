# App Souls workspace expansion — implementation ledger

Requested 7 November 2026. Preserve existing tenant/customer ownership, guided activation, protected configuration and shared transfer/motion controls.

## Work packages

1. Focused routes for lists, details, create/edit, activities/history; separate Leads and Opportunities; role-aware navigation and server authorization.
2. Form-specific editing/submissions, additional validated answer types and Excel export with safe cell encoding.
3. Release-led downloads, private upload/unpublished views, explicit operating systems and validated mobile store links. Stream installation uploads up to 8 GiB without weakening quarantine/scanner enforcement or exceeding native IIS single-request limits.
4. Customer joined date, owned products/licenses/support terms and detailed orders/contracts; separate activity and onboarding views.
5. Developer catalog versus sales pricing; controlled pricing descriptions; streamlined licensing routes.
6. Sales Manager invoice entry per order/contract and generated PDF, preserving immutable invoice details and totals. Country-specific tax-authority compliance depends on the requested jurisdiction.
7. Customer-initiated Sales/Technical live chat and rich ticket conversations/attachments/urgency, with ownership and product eligibility enforced server-side.
8. Full-width, focused settings/account/security sections.
9. Provider-specific row-level security and targeted indexes, including explicit narrow directory/public/background persistence boundaries and least-privilege runtime verification.
10. Protected distribution: evaluated loader, installation-specific provisioning, signed manifest and recovery qualification required. No claim that encrypting DLLs prevents reverse engineering.

## Review and evidence

Update each package here as implemented; source-only and pending qualification are distinct from delivered functionality. No demonstration data is production functionality. Test role/tenant/customer denials, concurrency, malformed input, transfer failures, themes/mobile/keyboard/reduced motion and both provider migration paths.

## Implemented phase — 7 November 2026

The requested invoice scope is **PDF invoices only**. No tax-authority integration, tax-compliance certification, payment capture or delivery guarantee is implied.

| Work package | Implemented behavior | Verification |
| --- | --- | --- |
| Focused workspaces | Separate Leads/Opportunities navigation; list/new/detail/history/activity/stage views; customer overview/software/licenses/contracts/orders/support/activity/onboarding; separate member list/create; separate settings sections. | Browser route and responsive checks recorded below; server permissions remain independent of links. |
| Forms | Draft edit with version checks; immutable published question set; separate per-form submissions; Number, Phone, Website, Rating and Yes/No types; private XLSX export. | `DraftFormEditingTypedAnswersAndExcelExportPreserveThePublishedQuestionSet`, existing `FormServiceTests`, `ExcelPreservesLiteralUserTextAndNumericAnswersAndRemovesTemporaryFileAfterUse`. |
| Releases | Published/unpublished/upload views; filtering before pagination; explicit Windows/Linux/MacOS/Android/IOS/FreeBSD; validated official mobile store links; 64-bit bounded streaming up to 8 GiB. | `StoreLinksRejectHostSpoofingAndPublishedPagingIgnoresNewerQuarantine`, `InstallationSizeBoundaryUses64BitCountsAndRejectsOversizeBeforeReading`. Full 8 GiB network transfer not performed. |
| Customer commercial detail | Joined UTC date; entitlement/license/support expiry separation; detailed order and contract records; immutable sales-entered line items. | Invoice/role tests below, existing customer lifecycle/delivery tests. Contract views expose currently persisted contract fields, not an authored contract-document editor. |
| Catalog/licensing | Developer product/model creation separate from sales pricing/descriptions; Owner/Admin pricing approval and stale-version protection; separate installation registration/license issuance/model publication views. | `SalesCanRecordOrdersButOnlyManagersIssueInvoicesAndLeadershipApprovesPrices`, existing licensing tests and browser routes. Division Head/C-suite/Chairman approval hierarchy is not modeled as new roles in this phase. |
| PDF invoices | Owner/Admin/Sales Manager issues an immutable invoice per owned order or contract; supplier/buyer/address/tax references, dates, currency, item quantity/price/discount/tax, totals, terms/notes; private PDF download. | `InvoiceSnapshotsAreOwnedImmutableAndIdempotentAndPdfContainsSavedValues`, `PricingRoundsEachLineAndRejectsInvalidQuantitiesAndPrecision`, `InvoicePdfEmbedsItsFontAndPaginatesDetailedLineItems`; sample PDF rendered and its text inspected. |
| Support | Customer-initiated Technical/purchased-product or Sales/optional-catalog chat; team queues; persistent messages with two-second authorized refresh; rich Markdown ticket inquiry/replies; Bug/Issue/Question/Critical Error; four urgency levels; private scanned attachments; preserved five-stage history and acknowledgement outbox. | `ChatRequiresPurchasedTechnicalSoftwareRoutesTeamsAndHidesPrivateStaffNames`, `TicketRepliesAndAttachmentsEnforceOwnershipScanningAndStableRetries`, `RichTextAllowsFormattingButNeverActiveContentOrRemoteImages`, existing support/outbox tests. No online-agent presence or instant assignment claim. |
| Roles/settings | Admin, Technical Developer, Technical Support, Sales, Sales Manager, Customer plus existing Owner/legacy Technical; capability-specific service authorization; full-width profile/password/security and system settings sections. | New role denials and existing administration/settings/session tests; guided connection fields/checks preserved. |
| Native RLS/indexes | Frozen PostgreSQL and SQL Server migrations; native policies on all 27 tenant tables; direct customer ownership predicates; public-form scope; restricted business principal; explicit narrow directory/bootstrap/machine/background paths; query/queue/relationship indexes. | `NativeRowPoliciesStopFilterBypassForeignWritesAndPooledScopeLeaks`, `PublicFormDatabaseScopeCannotReadPrivateTablesOrAnotherFormAndMissingTenantIsDenied`, both cases of `EveryTenantTableHasNativeReadAndWritePoliciesInItsProviderMigration`. Live PostgreSQL migration/read/write/pool checks passed. Native SQL Server remains unqualified. |

## Verification and limits

- `dotnet build TicketingCRM.slnx`: zero warnings/errors.
- `dotnet test TicketingCRM.slnx --no-build` with an isolated PostgreSQL database: **205 passed, 0 failed, 6 skipped**. Five skipped checks require native Windows; one cross-provider copy check requires SQL Server. Includes both provider-policy generation cases and a regression check that sign-in directory discovery works while generic unscoped persistence remains denied.
- Both provider snapshots have no pending model changes. SQL Server idempotent migration script generated successfully. This does not substitute for execution on SQL Server.
- Browser verification uses an isolated, synthetic acceptance database and protected temporary credentials outside the repository. PDF and Excel downloads exercised the shared progress/streaming browser handoff and produced valid PDF/XLSX files. No acceptance data or credentials are product data.
- Browser verification passed all 50 reviewed routes in both themes, with no application errors; six representative workflows at 375px in both themes had no page overflow. It exercised customer/staff chat updates without reload, ticket replies, real clean-scanned attachment upload, PDF/XLSX progress downloads and appearance persistence. Invoice table overflow found during review was corrected. Keyboard theme selection/Escape dismissal returned focus; explicit Dark persisted across reload/navigation and ignored OS Light; reduced-motion page animations were disabled; the rich-text toolbar formatted selected text and restored textarea focus.
- Current NuGet vulnerability audit (including transitive packages) reports no known vulnerable packages in the configured source.
- Native scanner size limits remain enforced. Files at or above 2 GiB remain quarantined; smaller files can also exceed configured scanner limits. Browser framed uploads and the lower-limit multipart HTTP API are separate transport paths.
- Protected DLL distribution remains a separate implementation/qualification phase. No encrypted loader, universal client key or reverse-engineering immunity is claimed. See the [versioned threat model and release requirements](../security/workspace-expansion-threat-model.md).
- Remaining larger-workspace improvements include pagination/search for older generic directory/license/customer detail snapshots (currently explicitly bounded to 100), older conversation history beyond the displayed latest 100 messages/replies, a richer contract-document authoring workflow, leadership-specific approval roles, advanced licensing extensions, concurrency/load budgets, complex-script/RTL PDF qualification, native Windows/Linux/FreeBSD distribution qualification and native SQL Server RLS/impersonation tests.
