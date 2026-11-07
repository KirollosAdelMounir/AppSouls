# App Souls deployment

Preferred distributions are **AppSoulsSetup.exe** (Windows setup wizard containing the MSI) and **AppSoulsSetup-linux-x64.sh** (one self-contained Linux shell installer). Build instructions and Windows qualification limits are in [server installation](server-installation.md#single-file-distributions-and-windows-msi).

For an extracted publish package, these lower-level entry points remain available with OS administrator privileges:

- Linux: `sudo sh install/install-server.sh`
- Windows (elevated Windows PowerShell): `.\install\install-server.ps1`

They install the matching .NET runtime, nginx/IIS, ClamAV and the App Souls site. The default site is local HTTPS with a self-signed bootstrap certificate. Continue with administrator, database and email setup in the browser.

Read [server installation](server-installation.md) for publishing, public hostname/certificate options, upgrades, paths, failure recovery and platform qualification still required. [Scanner-only setup](scanner.md) remains available for existing hosts.
