# App Souls server installation

The published server package contains an elevated installer for Windows/IIS and Linux/nginx. It installs host dependencies, copies application files, creates and starts the site, configures private persistent storage and checks HTTPS readiness. Database connection details, initial administrator preparation, first migration confirmation, public URL and SMTP remain in guided server configuration. No administrator/password, database connection or vault key is supplied by the installer.

These installers are implemented, with verification evidence below. They are not yet a qualified turnkey production distribution on every supported operating system.

## Publish once, deploy the resulting package

Use the .NET SDK on the build machine, not on the server:

```sh
dotnet publish TicketingCRM/TicketingCRM.csproj -c Release --self-contained false -p:UseAppHost=false -o ./artifacts/server
```

Copy/extract that complete directory onto the destination server, then run its `install/` entry point. An OS-specific framework-dependent publish is also accepted when its runtime identifier matches the target. The installer rejects source/build-only, self-contained, mismatched-architecture and pinned-runtime packages. Portable packages choose the host architecture. Linux supports x64/arm64; Windows supports x64/x86 and places x86 builds under Program Files (x86) on a 64-bit OS. Native Windows ARM64 is not qualified by this installer.

The app's `TicketingCRM.runtimeconfig.json` determines the .NET major/minor channel and minimum patch; the installer resolves the latest supported stable patch in that channel from Microsoft release metadata. It does not choose a version from whichever unrelated runtime is already installed. Windows installs/repairs the ASP.NET Core **Hosting Bundle after IIS**; Linux uses the ASP.NET Core runtime archive, including the base .NET runtime, in a private deployment runtime directory. Payloads are verified against Microsoft SHA-512 metadata before use; Windows also verifies the Microsoft Authenticode signer. Existing system-wide runtime installations are retained. Runtime security updates require rerunning an intentional deployment upgrade; a private Linux runtime is not updated by the OS package manager.

## Linux

Supported automatic package plans: Fedora, Debian and Ubuntu with systemd. Run from the extracted publish directory:

```sh
sudo sh install/install-server.sh
```

A read-only preview requires Python 3.10 or newer and does not install packages, download runtimes or modify files:

```sh
python3 install/server_setup.py --plan
```

For a public DNS hostname with an existing PEM certificate and matching unencrypted private key:

```sh
sudo sh install/install-server.sh --hostname crm.example.com --certificate /secure/fullchain.pem --certificate-key /secure/privkey.pem
```

The full Linux installer requires Python 3.10 or newer (as supplied by current supported distributions). The installer installs nginx, runtime OS prerequisites and ClamAV; sets up signature updates; creates the dedicated `appsouls` service account; and deploys beneath `/opt/AppSouls`. App files/runtime are root-owned and read-only to the application. `/var/lib/appsouls` holds the encrypted vault, its separate key directory, authentication keys, prepared setup material and private artifacts. It is not served by nginx. nginx proxies HTTPS to loopback Kestrel on port 5198 and supports Blazor WebSockets, streaming uploads and long connections. Only loopback peers can supply one hop of forwarded client/scheme headers. Client-provided forwarded headers are replaced by nginx.

Managed files:

- `/opt/AppSouls/releases/`, `/opt/AppSouls/runtimes/`, `current` and `runtime`: versioned payloads and active links.
- `/opt/AppSouls/deployment.json`: root-only nonsecret deployment record.
- `/etc/systemd/system/appsouls.service`: unprivileged app, automatic start/restart, private temporary directory, restricted filesystem writes.
- `/etc/nginx/conf.d/appsouls.conf`: HTTPS reverse proxy; `nginx -t` must succeed before reload.
- `/etc/appsouls/tls/`: root-only private TLS key directory, separate from application data.

On SELinux-enabled hosts the installer labels managed TLS material, adds an unclaimed custom HTTPS port to the HTTP port type when needed, and enables the documented nginx/httpd network-connect boolean for the loopback proxy. It refuses to relabel ports owned by another service. This boolean affects other httpd-domain processes too. Existing unrelated configuration is retained. For a public hostname, an already active firewalld or ufw installation receives the HTTPS port rule. It does not disable a firewall or modify external/network firewalls. Default local-only deployment opens no firewall rule. Hosting supports `--https-port` and `--backend-port`; occupied ports fail rather than stopping another service. Logs are available through `journalctl -u appsouls` and nginx's normal logs.

## Windows

Run the extracted package's script from elevated **Windows PowerShell 5.1**, using 64-bit PowerShell on a 64-bit OS:

```powershell
.\install\install-server.ps1
```

Read-only planning does not require elevation:

```powershell
.\install\install-server.ps1 -Plan
```

To use a public DNS hostname and a certificate already installed with its private key in `LocalMachine\My`:

```powershell
.\install\install-server.ps1 -HostName crm.example.com -CertificateThumbprint <certificate-thumbprint>
```

The script enables IIS, WebSockets and application initialization; installs the matching Hosting Bundle; installs/configures ClamAV; creates an `AppSouls` application pool and HTTPS website; copies files into Program Files\AppSouls (or Program Files (x86) for x86); and configures an identity-specific writable ProgramData\AppSouls\Server directory outside application content. The app pool uses its own unprivileged virtual identity, no managed CLR, disabled idle shutdown and no overlapping recycling. ANCM hosts the app out of process on its allocated loopback port with pairing-token validation, preserving the guided activation host-restart flow. Code receives read/execute access; application state is writable only by the app identity and OS administrators/system. Newly created private key subdirectories use the application's stricter owner-only ACLs.

Existing unrelated IIS sites/pools, certificates, application state and bindings are not adopted or deleted. The site starts automatically and initialization preloads the app so startup migration checks and background processing do not depend on the first visitor. A public hostname gets an AppSouls HTTPS firewall rule; localhost binds loopback only. IIS feature/Hosting Bundle installations that request an OS restart stop with instructions to restart and rerun; the script does not reboot the server or reset all IIS sites. Windows event logs provide hosting failure details.

ClamAV uses WinGet when available, with a verified official Cisco-Talos release download as the automatic fallback for Windows Server without WinGet. See [scanner deployment](scanner.md) for optional offline MSI arguments. The full installer forwards `-ClamAvMsi` and `-ClamAvSha256` to that helper.

## HTTPS and first configuration

With no hostname/certificate arguments, both installers create a loopback-only `https://localhost/setup` site and a 90-day **self-signed bootstrap certificate**. Browsers will warn until that certificate is explicitly trusted. The installer does not add it to clients' trust stores. Provide a CA-issued certificate for public use; DNS ownership, certificate issuance/renewal and external routing cannot be inferred from the app package. Supplied certificates should include their chain and must be renewed by the deployment operator. Rerun an upgrade with a replacement certificate when needed.

A supplied public hostname creates a network-accessible binding. Finish first administrator setup promptly on a controlled network before allowing untrusted access; first-time setup intentionally has no authorization token. The installer creates neither application identities nor migrations itself. Guided setup prepares the exact administrator and requires explicit confirmation of first migration. On configured installations the existing application startup path checks/applies pending compatible provider migrations before serving and outbox startup.

## Upgrade and failure behavior

Rerunning without `--upgrade` (Windows `-Upgrade`) refuses an existing installation. For an upgrade, use a new, separately extracted publish package and the same hostname/ports/architecture, then pass that switch. Existing vault, keys, artifacts and identity are preserved; application files go into a new release directory. This is a planned stop/copy/start deployment and interrupts existing connections. It is not a database provider switch or cross-provider copy.

On failure the installer reports an error rather than reporting a ready site. A failure after service startup stops the new app/site. Installed OS packages, scanner configuration and completed host preparation may remain; reruns can resume installations with a deployment record. Failed pre-record preparation or existing unmanaged paths require operator reconciliation. Previous release directories are retained, but no automatic binary/database rollback is attempted: startup migrations may already have changed the database. Back up data and key material before upgrades and retain a tested recovery procedure. Package updates may require Windows restarts.

## Verification and remaining qualification

Unit tests and nonprivileged deployment checks are recorded in `docs/architecture/implementation-status.md` in the source repository. A passing script/plan test does not qualify a real clean OS installation. Native Windows IIS, fresh privileged Linux installation, reboot recovery, trusted certificate renewal and failure recovery must be exercised in clean deployment environments before production release. The Linux installer does not claim native FreeBSD support; the separate scanner helper still has a FreeBSD plan.

The implementation follows Microsoft's [IIS hosting guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/), [nginx/systemd guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx/), and [official release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json). See also [scanner configuration and limits](scanner.md).

## Single-file distributions and Windows MSI

Build machines can now produce these customer-facing files:

- **AppSoulsSetup.exe**: Windows x64 wizard built with WiX Burn. It runs IIS/.NET/ClamAV provisioning first, outside Windows Installer, then installs the application MSI. It needs network access for vendor/OS prerequisites. It does not depend on the ASP.NET runtime before installing that runtime; its small bootstrap helper targets the OS .NET Framework 4.7.2 (Windows Server 2019/Windows 10 1809 or newer baseline, subject to the app runtime's supported OS matrix).
- **AppSouls.msi**: application payload, upgrade/repair/uninstall and configuration-only hosting actions. Its files live under Program Files\AppSouls\app. Standalone MSI use requires already-provisioned dependencies; it deliberately never starts another MSI. Use the EXE for a new server. Initial MSI wizard deployment uses local HTTPS; existing MSI deployments retain their recorded hostname/certificate. Standalone script installations cannot silently be adopted by MSI.
- **AppSoulsSetup-linux-x64.sh**: one self-extracting shell file containing the full app and existing host installer. Run `sudo sh AppSoulsSetup-linux-x64.sh`, with the same hostname, certificate and upgrade arguments as the extracted-package installer. `sh AppSoulsSetup-linux-x64.sh --plan` is a nonprivileged preview. It verifies the embedded payload checksum before extraction/installation and cleans its private temporary files. The checksum detects corruption; it is not a publisher signature. Native/vendor dependencies still require network access. Bootstrap utilities are coreutils, tar and gzip, ordinarily present on the supported distributions.

Windows MSI upgrade requires a strictly increasing major.minor.build version within MSI limits. Uninstall removes the validated MSI-owned site/pool and application files, but preserves database, vault, keys, artifacts, certificates, firewall rule and shared prerequisites for intentional recovery. It refuses to alter a site whose ownership/path was changed. Windows Installer file rollback cannot reverse an already-applied database migration; failure may leave hosting stopped and needs operator recovery. A shared dependency or restart failure is not reported as a successful application installation.

From a Windows build machine with the .NET SDK:

```powershell
.\tools\build-windows-installer.ps1 -Version 1.0.0
```

This restores pinned WiX 6.0.2 build packages and outputs the MSI and EXE into `artifacts/installers`. On a Linux build machine, publish first and build the one-file distribution with Python 3.11 or newer:

```sh
dotnet publish TicketingCRM/TicketingCRM.csproj -c Release -r linux-x64 --self-contained false -p:UseAppHost=false -o artifacts/server
python3 tools/build-linux-installer.py --source artifacts/server --output artifacts/installers/AppSoulsSetup-linux-x64.sh
```

The manually dispatched **Build App Souls installers** GitHub Actions workflow provides Windows and Linux build jobs. Generated binaries are ignored by source control. Windows artifacts are unsigned build outputs until a trusted publisher-signing stage is supplied; no signing key or credentials are embedded. Native WiX MSI/Burn compilation and end-to-end Windows install/repair/upgrade/uninstall still need a Windows build/test run; parser, XML and cross-compilation checks on Linux do not establish that qualification.

The separate setup chain follows [Microsoft's guidance against nested MSI installations](https://learn.microsoft.com/en-us/windows/win32/msi/windows-installer-best-practices) and [WiX Burn package chaining](https://docs.firegiant.com/wix/tools/burn/).
