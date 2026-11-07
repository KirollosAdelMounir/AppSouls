# Minimal public forms — phase 1

App Souls provides a small collection-form builder at `/forms` for organization owners and administrators. It collects feedback, anonymous poll choices, enquiry/lead details and other text answers. Collected lead details remain submissions; this phase does not automatically create Sales leads.

## Workflow

1. Enter a title and description/privacy notice, then add and reorder up to 20 questions. Supported answer types: Text, Long text, Email and Choice. Set required answers as needed; Choice accepts 2–20 options.
2. Save a draft, then Publish. Copy the displayed public URL and share it. The host must be reachable through its configured HTTPS deployment for external visitors.
3. Visitors submit without an account. They receive confirmation only after persistence succeeds. Invalid submissions keep answers on the returned page. Repeat requests with the same operation ID and normalized answers do not duplicate a response.
4. View responses in the CRM, 20 at a time. Form and response lists are paginated. Dates are UTC.
5. Close collection to disable the public page and new receipts. Reopening retains the same link and prior responses. Questions are fixed after saving in this minimal phase; create another form to change questions. Published questions and received answers are immutable through application persistence.

Each form permits at most 10,000 submissions. Polls allow repeat participation; neither a public link nor an operation ID proves a person's identity or enforces one vote per person. No attachments, email notifications, automatic lead conversion, branching, embedded HTML, public response listing, export, deletion or advanced reporting is implemented in this phase. File transfers are deliberately absent.

## Deployment

Separate additive `PublicForms` migrations exist for PostgreSQL and SQL Server. An existing configured installation applies pending migrations automatically before requests and outbox processing start. First-time activation still requires guided setup. Explicit database copy/switch operations retain the maintenance workflow. The migrations must be applied before using `/forms`. Never point a verification harness at the live catalog.

## Security requirements v1

- **PF-01: private management.** Membership and current security stamp are revalidated for every management/read operation. Only owners/admins without customer scope can manage forms or read responses. Lists remain scoped to the selected authenticated tenant; composite foreign keys prevent cross-tenant form/submission relationships.
- **PF-02: public scope.** A random 256-bit public key resolves only an active tenant's currently published form. The narrow pre-authentication repository lookup grants no private-read capability. Scope is derived from that exact form, never a request tenant ID. Publication and active-tenant state are rechecked in the scoped unit of work.
- **PF-03: safe receipt.** POST requires antiforgery proof, bounds request size/field count and validates every answer against stored questions. Unknown fields, invalid choices and invalid required/email answers fail before persistence. User text is rendered as escaped text, not HTML. Responses are not included in public reads, logs, cookies or URLs.
- **PF-04: atomicity and retries.** Receipt and count update commit atomically. The form concurrency token serializes receipts against closure and the response quota. A stale concurrent receipt fails without a partially saved response; retry with the same operation ID is safe while the form remains open. Closed forms reject all new POSTs, including retries. No external side effects occur.
- **PF-05: abuse bounds.** Public GET/POST share a per-process, per-remote-IP fixed-window limit of 20 requests/minute. Forms cap field lengths, options and receipt count. Limiting is not bot prevention, a distributed quota or a guarantee against denial of service. HTTPS, proxy/IP handling, edge controls, privacy notice and operator retention procedures require deployment review. Do not collect credentials, payment details or other sensitive data through these forms.
- **PF-06: maintenance and recovery.** The dedicated public context factory carries the maintenance read-only interceptor. Existing vault/database recovery procedures apply. Closing disables access without deleting collected records. A public URL is shareable and is not a secret authorizing private data access.

## Verification evidence

`FormServiceTests` exercises draft/publish/close/reopen, persisted answers, idempotent retries, tenant/role/stamp isolation, invalid and oversized answers, inactive tenants, stale state changes, quotas, pagination, closure/receipt races, immutable published questions/answers and cross-tenant foreign keys. Real PostgreSQL is used; fixtures and temporary HTTP/browser environments are isolated from the application database. See the implementation status for the latest executed results and remaining platform/provider qualification.

The proposed assurance baseline remains ASVS 5.0.0 Level 2 with selected Level 3 controls from the project threat model. This feature does not claim ASVS certification or independently assessed production readiness.

### Requirement coverage

All tests below are in `FormServiceTests`.

| Requirement | Test |
| --- | --- |
| Draft privacy, publication, receipt, closure and reopening | `DraftIsPrivatePublishExposesOnlyQuestionsAndCloseRetainsResponses` |
| Duplicate receipt retries and changed-payload rejection | `ReceiptIsIdempotentAndChangedRetryCannotOverwriteAnswers` |
| Tenant, customer, role and revoked-session isolation | `ForeignTenantsCustomersTechnicalStaffAndRevokedSessionsCannotReadOrManage` |
| Stored-schema validation and bounded answers | `TamperedChoicesUnknownFieldsMissingRequiredInvalidEmailAndOversizeResponsesDoNotPersist` |
| Inactive tenants and invalid public keys | `InactiveTenantAndInvalidPublicCapabilitiesRejectSubmission` |
| Stale mutations and receipt quota | `StalePublicationVersionCannotCloseAfterReceiptAndResponseLimitIsEnforced` |
| Invalid question schemas | `InvalidQuestionDefinitionsAreRejectedBeforePersistence` |
| Bounded pagination without duplicated/missing responses | `SubmissionsAreBoundedAndPagedWithoutLosingOrRepeatingRecords` |
| Atomic rollback when collection closes during receipt | `CloseRacingWithReceiptRollsBackSubmission` |
| Cross-tenant foreign keys and immutable questions/answers | `PersistenceRejectsForeignFormRelationshipsAndChangesToPublishedQuestionsOrReceivedAnswers` |

Executed: `dotnet build TicketingCRM.slnx` (zero warnings/errors), then `dotnet test TicketingCRM.slnx --no-build` against an isolated, password-authenticated PostgreSQL cluster. Results: **Passed: 166, Failed: 0, Skipped: 6**. The skips are five native Windows private-storage tests and one cross-provider round-trip requiring SQL Server. All ten new form tests executed. Both providers' migration snapshots match the model; SQL Server idempotent migration SQL generated successfully. A live SQL Server migration and native Windows/FreeBSD runtime qualification remain pending.

An isolated HTTP/browser acceptance run covered create/reorder/save/publish, anonymous form loading/receipt, missing-antiforgery rejection, tampered-choice rejection with answer retention, private API rejection for anonymous visitors, escaped response rendering in the CRM, close-to-404 behavior, HTTP 429 limiting, pointer and keyboard selection/Escape focus restoration, Light/Dark persistence after reload, 375px layouts, and reduced motion. No production configuration or database was changed.

Final startup checks also confirmed an unconfigured server still returns the setup page, configured service registration works, browser back navigation re-enables submission, and Scalar/OpenAPI describes public form routes as anonymous with antiforgery required for POST.
