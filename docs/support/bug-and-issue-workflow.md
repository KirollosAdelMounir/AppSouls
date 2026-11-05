# App Souls bug and issue reports

Bug / Issue Report is a ticket type within Support. The customer reports a problem with software purchased from the vendor. Four progress stages are followed by a separate final closure, giving five stored statuses exactly matching the requested lifecycle.

| Status | Meaning | Required action |
| --- | --- | --- |
| Submitted | The report is durably recorded | Authorized reporter submits; ticket, initial history and acknowledgement outbox commit together |
| Received by a technical member | A named technical member has accepted responsibility | Assign an authorized member of the same vendor tenant and record acceptance |
| Solution in progress | Technical work is underway | Assigned technical member begins work |
| Completed | A resolution has been provided and awaits closure | Record resolution and customer-visible result |
| Closed | The report lifecycle is finished | Authorized customer or support member closes with a reason |

Initial implementation follows adjacent transitions. Never let customers assign staff or start technical work. A customer may close a Completed ticket belonging to their company if permitted by their role. Support may close it under its vendor policy. Automatic closure and reopening require an explicit later policy; do not silently reopen or skip stages. Persist actor ID, UTC timestamp, old/new stage and optimistic concurrency version for each transition, even when the customer-facing history hides the actor's identity.

## Intake

Collect report kind (Bug or Issue), product, title, description, application version, environment, reproduction steps, expected result and actual result. Require reproduction and expected/actual details for bugs. Support issues can describe symptoms without a reproduction sequence. Determine customer and reporter from authorized membership and verified identity, rather than trusting submitted IDs or an arbitrary recipient email. Validate product entitlement and tenant ownership. Link an installation or license when supplied and authorized. Bound text lengths and encode all displayed user input. Attachments are a later increment requiring file size/type checks, isolated scanning and authorized download storage; do not accept unsafe uploads in the meantime.

## Submission and acknowledgement email

A successful submission returns a stable reference and starts at Submitted. Use a unique operation ID scoped to tenant and reporter to prevent duplicate reports on retries or repeated clicks. The same database transaction commits the ticket, first history event and notification outbox. Return success only after the commit. The outbox sends an acknowledgement to the verified reporter with a tenant-aware authenticated progress URL. Never build the URL from an untrusted Host header or expose a public anonymous progress endpoint.

Notification workers retry transient failures with bounded backoff, record delivery state, and expose failures to authorized support operators. A stable notification ID prevents repeat processing; SMTP can still deliver a duplicate after an ambiguous acknowledgement, so do not promise exactly-once email. A queued email is not yet delivered. Report availability must not depend on immediate email success. Use a configured mail transport and protected credentials, never secrets stored in browser state or source.

The email contains the reference, initial stage and progress link. Keep the bug description, environment details, attachments, licensing secrets and internal comments out of email. A progress link requires customer authentication and resource authorization every time; a reference or opaque ID alone is not permission.

## Email copy

Subject: App Souls — report {reference} received

We received your Bug / Issue Report. Its current stage is Submitted.

You can check its progress in App Souls: {authenticated progress link}

The stages are Submitted → Received by a technical member → Solution in progress → Completed → Closed.

App Souls by Tetrarchia

## Progress view

Show the current status, all five labels, submitted/updated times in the user's selected timezone, and chronological customer-visible updates. Keep internal notes and staff personal details private. Stage changes may notify the reporter using the same outbox mechanism; the submission acknowledgement is mandatory. Email-link navigation must return the customer to the requested report after sign-in.

## Delivery status and acceptance

Authenticated submission, tracking, technical stage updates, atomic persistence and the acknowledgement outbox are implemented. SMTP transport is configurable by a platform administrator in Server settings. With SMTP disabled, submissions remain valid and acknowledgements remain queued. Live external email delivery still needs verification against the configured provider.

Verify a successful authorized submission creates one report, one initial history entry and one outbox job. Failed transactions leave none. Repeated operation IDs do not create duplicate tickets. Another tenant/customer cannot read, alter or infer the report. Receipt without a technical assignee and completion without a resolution must fail. Validate concurrency conflicts and forbidden stage jumps. Test notification retries and mail outages without losing reports. Verify the authenticated email link shows the correct report and all five statuses, and never reveals internal comments. Check form validation, keyboard focus, stage labels, light/dark themes, mobile layout and reduced motion.
