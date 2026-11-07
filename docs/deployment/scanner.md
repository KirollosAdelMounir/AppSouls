# App Souls scanner dependency setup

For a complete new server installation including .NET and IIS/nginx, use [the full server installer](server-installation.md). The helpers below provision the scanner alone.

Malware scanning is a built-in dependency for publishing installer packages. App Souls connects to local ClamAV on `127.0.0.1:3310` automatically. You do not need to create a generic integration or enter a credential. Each installation remains quarantined until its own scan returns an explicit clean verdict.

The published server package includes this directory as `install/`, outside `wwwroot`:

- `install-server-dependencies.sh` and `scanner_setup.py`: native package installation and configuration for Fedora, Debian/Ubuntu with systemd, and FreeBSD.
- `install-server-dependencies.ps1`: Windows package/service installation.

Run dependency installation before starting the server or completing guided activation. Installers need OS administrator privileges when dependencies/configuration are missing. The web application never elevates itself, downloads executable dependencies or changes host services from an HTTP request.

## Unix server installation

From a published server directory:

```sh
sudo sh install/install-server-dependencies.sh
```

From a source checkout, use `sudo sh tools/install-server-dependencies.sh`. Root shells can omit `sudo`. FreeBSD uses `sh` and `pkg`; it does not require a Linux VM. The wrapper installs Python 3 if needed for an actual installation. `--check` is read-only, and `--plan fedora|debian|ubuntu|freebsd` prints the package/service plan without running it.

The installer installs native ClamAV packages, configures loopback-only TCP, downloads vendor-verified signature databases using FreshClam, enables the daemon and definition updater, then waits for the local daemon to respond. Stream/file limits match the app's exact 536,870,912-byte upload limit; expanded scan size is bounded separately. Limit overruns and encrypted content are configured to raise scanner alerts rather than silently pass incomplete inspection. Package rejection does not always mean a confirmed malware infection.

Existing working local scanners are reused without changing their configuration or updater. An administrator can opt into the backed-up managed configuration for an existing package-managed service with `--configure-existing` (PowerShell: `-ConfigureExisting`). Their definition freshness and resource/scan limits remain the operator's responsibility; a successful connection check does not certify those settings. Otherwise, modified configuration files receive private timestamped backups, unrelated directives are preserved, and linked configuration paths are rejected. Failed definition initialization aborts setup and attempts to restart the updater; it does not claim the scanner is ready. Network access to the native package repositories and ClamAV signature mirrors is required for a fresh online install.

## Windows server installation

In an elevated Windows PowerShell session:

```powershell
.\install\install-server-dependencies.ps1
```

The default path uses Microsoft's WinGet `Cisco.ClamAV` package, whose manifest validates installer hashes. Without WinGet, the helper automatically downloads the matching stable MSI from the official Cisco-Talos GitHub release and verifies its API SHA-256 digest before execution. Invalid Authenticode signatures are rejected; an unsigned official online asset is accepted only after that digest check. For offline installation, supply an official, signed MSI and its independently verified digest:

```powershell
.\install\install-server-dependencies.ps1 -ClamAvMsi C:\Deployment\clamav.msi -ExpectedSha256 <verified-sha256>
```

The script configures the local daemon and updater, grants their LocalService account access to a private ProgramData definition directory, updates signatures, registers native services and enables automatic startup. It preserves a responding existing scanner and refuses to replace service registrations pointing at another installation. Review vendor MSI updates and signatures in your packaging pipeline; App Souls does not redistribute ClamAV executables in this source repository.

## Existing/custom-managed scanner

An existing managed service on port 3310 works automatically. A different local port can still be configured through System Settings → Integration credentials: identifier `malware-scanner`, credential `Port`, blank base URL. Existing explicit overrides are retained; invalid values fail closed. Saving an override updates the running configuration snapshot after the encrypted vault save succeeds, so it no longer requires restarting App Souls. No remote ClamAV host is accepted.

Guided setup and System Settings both offer **Check scanner connection**. This sends the bounded ClamAV PING request; it neither installs software nor scans customer data. A responding daemon is distinct from a package passing its scan, signature freshness, archive coverage and independent production security qualification. CRM setup can still proceed while scanning is unavailable, but installer publication cannot bypass it.

## Sources and qualification

The platform plans follow [ClamAV package guidance](https://docs.clamav.net/manual/Installing/Packages.html), [configuration guidance](https://docs.clamav.net/manual/Usage/Configuration.html), [native Windows service guidance](https://docs.clamav.net/manual/Usage/Services.html), [Fedora package information](https://packages.fedoraproject.org/pkgs/clamav/clamav-freshclam/) and the [Microsoft package manifest](https://github.com/microsoft/winget-pkgs/tree/master/manifests/c/Cisco/ClamAV).

The existing-daemon reuse/check path is exercised on native Fedora. Fresh-install command plans and configuration/failure behavior have isolated tests; these are not evidence of running a fresh privileged OS installation. Windows PowerShell and native FreeBSD installation still require native qualification. Native Windows/Linux/FreeBSD application qualification and a full malware-engine assessment remain independent release requirements. Do not present the scripts as having passed platform testing that has not occurred.
