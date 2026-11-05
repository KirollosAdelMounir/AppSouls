# Core artifact and model exports

This milestone implements a standalone codec and primitive model exporter. Issuance authorization, immutable schema publication, recipient enrollment, trusted-key selection and renewal orchestration must be provided by authenticated business services. The codec returning bytes does not authorize product use.

## Artifact version 1

`LicenseArtifactProtector` accepts externally supplied DER PKCS8 private keys and SPKI public keys. No keys are generated for storage, embedded or logged. It supports RSA 3072, 4096 and 8192-bit keys, RSA-OAEP-SHA256 key wrapping, AES-256-GCM with fresh random 256-bit content key and 96-bit nonce, and separate RSA-PSS-SHA256 issuer signatures. Signatures cover the exact payload bytes prefixed by an application/version context. Payloads are bounded to 1 MiB.

The binary envelope contains a fixed magic/version header, big-endian wrapped-key length, wrapped key, nonce, 128-bit tag and ciphertext. Ciphertext contains a big-endian signature length, signature and exact original payload. The fixed header, length and wrapped key are authenticated as associated data. Import bounds the envelope before allocation, authenticates ciphertext, verifies the signature against the caller-selected issuer trust key, then returns payload bytes. Content keys and temporary decrypted buffers are cleared. Runtime copies and caller payload/key buffers remain the caller's responsibility.

Only this profile is implemented. The five selectable license constructions, custom provider workers, protected DLL loading, algorithm migration, key vault adapters and production key lifecycle remain separate milestones. Do not advertise them as available.

## Model export version 1

`LicenseModelCodeGenerator.Export` accepts `LicenseModelDefinitionDto` and returns C#, JavaScript, Java, Python and manifest text. Output filenames are fixed. Field names are bounded ASCII identifiers stored as string literals, so language keywords are valid wire names and do not become executable source identifiers. Required/optional and nullable are independent.

Supported fields: text, Boolean, integer, decimal, UUID identifier and UTC timestamp. Integer and decimal wire values are decimal strings in every language to avoid precision loss. Identifiers use UUID D syntax. UTC timestamps use `YYYY-MM-DDTHH:mm:ss[.fraction]Z`, with at most seven fractional digits. Unknown fields and invalid primitive values are rejected. Nested objects, lists and enumerations are explicitly rejected until the schema contains their shape and allowed-value metadata.

C# exports a JsonElement parser/serializer; JavaScript and Python export JSON parser/serializer functions. Java exports a Java 17+ Map validator, requiring a separately chosen JSON adapter. Integration parsers must reject duplicate JSON properties before validation. These initial exports are not a cryptographic client SDK and do not regenerate schema revisions or contain secrets. Original signed bytes must be verified before parsing and rights evaluation. Do not reserialize JSON to reconstruct signed data.

## Verification

The focused Core test project covers exact-byte roundtrip with fresh ciphertext, mutation of header/key/nonce/tag/ciphertext, wrong recipient and issuer keys, malformed/oversize inputs, weak key rejection, source-safe identifiers, duplicate field IDs and unsupported types. Independent generated C#, JavaScript, Python and Java fixture checks validate a large integer string, Boolean and nullable text and reject wrong types and missing required fields. These checks are local Linux evidence; Windows and FreeBSD qualification remains outstanding.

## Operational licensing service

`ILicensingService` now exposes tenant-authorized schema publication, installation enrollment, signed encrypted issuance, customer downloads and four-language model ZIP export. Every operation revalidates active membership and tenant. Only Owner/Admin members without a customer scope publish or issue. Customer members enroll and download only within their customer ownership. Published schemas and Ready artifacts are immutable in persistence.

Enrollment accepts only an RSA public key and generates a random 256-bit renewal credential returned once. Only its SHA256 hash is stored. Capacity allocation touches the entitlement enrollment counter and inserts the installation in one database commit, so optimistic concurrency aborts a conflicting allocation. If the one-time credential response is lost, retry identifies the existing installation but cannot disclose its credential again; operational credential recovery and rotation are pending.

The issuer creates an installation-specific artifact from approved entitlement identity, limits, features and validity, plus schema-validated custom attributes. Custom attributes are nested under `Custom` and cannot overwrite protected identity or rights fields. Per-installation revision numbers are unique. An issuance operation ID permits safe retry for the same installation/schema; callers must retain an unchanged request until it completes. Renewal currently means staff issuing the next artifact after the commercial entitlement has been approved; commercial billing and entitlement amendment workflows are separate.

Authenticated cookie endpoints under `/api/licensing` expose schema publication, enrollment, issuance, model export and license downloads. Cookie POST requests require an antiforgery token. `/api/licensing/latest?currentRevision=N` accepts an installation Bearer credential rather than a cookie or tenant header. It returns the latest Ready artifact for that installation, including after the installed license has expired, or 204 when none is available. Installation revocation and tenant/product/entitlement revocation prevent retrieval. Consumers must verify, scope-check and atomically apply returned artifacts; automatic application in external products requires the separate client SDK integration.

The Blazor workspace supports attribute construction, schema publication, installation registration, issuance and downloads. The current issuance form uses a validated JSON editor for custom values. Rich per-type automatic field rendering, recipient proof-of-possession enrollment challenges, machine credential recovery/rotation, audit dashboards and a production assurance review remain pending. The UI must not advertise those capabilities as completed.
