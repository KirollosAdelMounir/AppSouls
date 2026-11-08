# SQL Server connection configuration — 8 November 2026

Guided setup and System Settings use the same structured fields and server-side `SqlConnectionStringBuilder`. Authentication can use SQL Server credentials or the application service identity (`Integrated Security=true`). Integrated connections contain no username/password. Saved connection metadata includes authentication and local certificate trust, never the password. Blank SQL passwords are reused only for the exact saved target, authentication and TLS/trust settings.

| Endpoint | Server field | Port field | Application host |
| --- | --- | --- | --- |
| Default TCP instance / listener | `sql.example.test` | `1433` or configured port | Windows / Linux |
| IPv6 TCP | `2001:db8::1` | Configured port | Windows / Linux |
| Explicit TCP address | `tcp:sql.example.test,1444` | Blank | Windows / Linux |
| Named instance discovery | `sql.example.test\SALES` | Blank | Windows / Linux; SQL Browser/firewall must permit discovery |
| Named instance with static port | `sql.example.test\SALES` | Configured port | Windows / Linux; explicit TCP avoids discovery |
| LocalDB | `(localdb)\MSSQLLocalDB` | Blank | Windows only; service identity authentication; TLS off |
| Shared LocalDB instance | `(localdb)\.\SharedInstance` | Blank | Windows only; instance must already be shared and accessible |
| Named pipes | `np:localhost\SQLEXPRESS` or `np:\\localhost\pipe\sql\query` | Blank | Windows only; server protocol must be enabled |
| Shared memory | `lpc:.\SQLEXPRESS` | Blank | Windows only; local SQL Server |

For a plain SQL Server hostname, a blank port leaves default resolution to SqlClient. Enter the port either in Server or Port, never both. Entering a backslash in Server clears the default TCP port in the form. Select service identity explicitly for LocalDB. The catalog name remains required: App Souls manages database lifecycle through confirmed setup, not arbitrary MDF attachment.

## Service identity

On IIS, SqlClient uses the worker's Windows security context. Configure the app pool/service account and grant that identity the required SQL login and database permissions. A local virtual app-pool account and a remote domain identity are different deployment cases; for a remote SQL Server, use an appropriately provisioned domain service identity or machine-account arrangement. The browser user's App Souls login never selects or impersonates a database identity. No Windows password is collected or stored for integrated authentication. LocalDB belongs to its Windows owner; a developer's instance is not automatically available to an IIS app pool. Profile loading and permissions must be provisioned outside the web application. A service-hosted SQL Server instance is preferable for server deployments.

On Linux, use TCP. SQL credentials work when the SQL Server permits SQL authentication. Integrated authentication requires externally provisioned Kerberos libraries/configuration, a valid service credential cache/keytab workflow, SQL Server SPNs, DNS and ticket renewal for the unprivileged systemd service identity. Selecting integrated authentication does not perform domain enrollment or provision/renew tickets. LocalDB, named pipes and shared memory are rejected on Linux with configuration errors.

See Microsoft's [SqlClient authentication guidance](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/authentication-sql-server?view=sql-server-ver17), [connection string guidance](https://learn.microsoft.com/en-us/sql/connect/ado-net/connection-string-syntax?view=sql-server-ver17), [LocalDB documentation](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb), and [IIS app-pool identity guidance](https://learn.microsoft.com/en-us/iis/manage/configuring-security/application-pool-identities-and-sql-server-express).

## Transport and lifecycle

Remote connections retain mandatory TLS with certificate validation. Provision a trusted server certificate matching the endpoint. The explicit certificate trust override is limited to local development endpoints, requires TLS and defaults off. LocalDB does not provide TLS, so the form requires TLS off rather than promising encrypted LocalDB traffic. Builder output and database lifecycle transport checks agree on local named-pipe/shared-memory endpoints. Test connection performs inspection only; catalog creation/migration still requires the existing explicit setup/maintenance confirmation.

## Verification and remaining qualification

The solution builds with zero warnings/errors using an isolated artifacts directory because Visual Studio and the running application lock the ordinary output. Connection-builder regression tests cover SQL/integrated credentials, instance/TCP/IPv6 forms, local Windows protocols, malformed/ambiguous endpoints, plaintext restrictions, secret-free metadata and exact-target password reuse. The isolated Windows guided form successfully authenticated to `(localdb)\MSSQLLocalDB` and confirmed a missing test catalog without creating it. Browser checks covered pointer selection, keyboard opening/Escape focus return, visible focus, Light/Dark and a 375px layout without horizontal overflow. The existing shared dropdown animation and reduced-motion CSS are reused; OS reduced-motion emulation was not exercised.

Native IIS identity provisioning, remote SQL Server authentication/TLS, Linux Kerberos/ticket renewal and native Linux protocol rejection still require deployment qualification. No FreeBSD qualification is claimed. This phase does not add Microsoft Entra/access-token authentication, database-file attachment, failover-partner/availability-group tuning, arbitrary driver options or credential provisioning. These must not be represented as fully supported deployment scenarios merely because SqlClient offers connection-string keywords for them.
