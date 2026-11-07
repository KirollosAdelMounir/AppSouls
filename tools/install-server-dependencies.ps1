#Requires -Version 5.1
#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [switch]$ConfigureExisting,
    [string]$ClamAvMsi,
    [string]$ExpectedSha256
)
$ErrorActionPreference = 'Stop'

function Test-LocalScanner {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        if (-not $client.ConnectAsync('127.0.0.1', 3310).Wait(3000)) { return $false }
        $stream = $client.GetStream()
        $stream.ReadTimeout = 3000
        $bytes = [Text.Encoding]::ASCII.GetBytes("zPING`0")
        $stream.Write($bytes, 0, $bytes.Length)
        $reply = New-Object System.Collections.Generic.List[byte]
        while ($reply.Count -lt 128) {
            $next = $stream.ReadByte()
            if ($next -le 0) { break }
            $reply.Add([byte]$next)
        }
        return [Text.Encoding]::ASCII.GetString($reply.ToArray()).Trim() -eq 'PONG'
    } catch { return $false }
    finally { $client.Dispose() }
}
function Assert-NativeSuccess([string]$Stage) {
    if ($LASTEXITCODE -ne 0) { throw "$Stage failed (exit $LASTEXITCODE). Scanner setup is incomplete." }
}
function Select-ClamAvAsset($Release, [string]$Architecture) {
    if ($Release.draft -or $Release.prerelease) { throw 'A stable official ClamAV release is required.' }
    $suffix = if ($Architecture -eq 'x64') { '.win.x64.msi' } elseif ($Architecture -eq 'x86') { '.win.win32.msi' } else { throw 'Unsupported native Windows scanner architecture.' }
    $asset = $Release.assets | Where-Object { $_.name.EndsWith($suffix) } | Select-Object -First 1
    if (-not $asset -or $asset.digest -notmatch '^sha256:[0-9a-fA-F]{64}$' -or
        $asset.browser_download_url -notmatch '^https://github\.com/Cisco-Talos/clamav/releases/download/[^/]+/clamav-[a-zA-Z0-9.\-]+\.msi$') {
        throw 'No official ClamAV MSI with a verified release digest was found.'
    }
    return $asset
}
function Set-ScannerConfig([string]$Path, [System.Collections.IDictionary]$Values) {
    $lines = @()
    $previousAcl = $null
    if (Test-Path -LiteralPath $Path) {
        $item = Get-Item -LiteralPath $Path
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing a linked configuration: $Path" }
        $previousAcl = Get-Acl -LiteralPath $Path
        $backup = $Path + '.appsouls-backup-' + [DateTime]::UtcNow.Ticks
        Copy-Item -LiteralPath $Path -Destination $backup
        $backupAcl = New-Object System.Security.AccessControl.FileSecurity
        $backupAcl.SetAccessRuleProtection($true, $false)
        foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
            $identity = New-Object System.Security.Principal.SecurityIdentifier($sid)
            $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', 'Allow')
            $backupAcl.AddAccessRule($rule)
        }
        Set-Acl -LiteralPath $backup -AclObject $backupAcl
        $lines = @(Get-Content -LiteralPath $Path)
    }
    $kept = @($lines | Where-Object {
        $text = $_.Trim()
        $name = ($text -split '\s+')[0]
        $text -ne '# App Souls managed scanner settings' -and ($text.StartsWith('#') -or ($name -ne 'Example' -and -not $Values.Contains($name)))
    })
    $kept += '# App Souls managed scanner settings'
    foreach ($name in $Values.Keys) { $kept += "$name $($Values[$name])" }
    $temporary = $Path + '.appsouls-new'
    [IO.File]::WriteAllLines($temporary, [string[]]$kept, [Text.Encoding]::ASCII)
    if ($previousAcl) { Set-Acl -LiteralPath $temporary -AclObject $previousAcl }
    Move-Item -LiteralPath $temporary -Destination $Path -Force
    & icacls.exe $Path /grant '*S-1-5-19:R'
    Assert-NativeSuccess 'Scanner configuration permissions'
}

if ((Test-LocalScanner) -and -not $ConfigureExisting) {
    Write-Host 'ClamAV already responds on localhost:3310. Existing configuration and services are retained.'
    exit 0
}
if ($ClamAvMsi) {
    if ($ExpectedSha256 -notmatch '^[0-9A-Fa-f]{64}$') { throw 'An offline MSI requires its independently verified SHA-256 digest.' }
    $package = (Resolve-Path -LiteralPath $ClamAvMsi).Path
    if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $ExpectedSha256) { throw 'The ClamAV MSI digest does not match.' }
    if ((Get-AuthenticodeSignature -LiteralPath $package).Status -ne 'Valid') { throw 'The ClamAV MSI signature is invalid.' }
    $install = Start-Process msiexec.exe -ArgumentList @('/i', ('"' + $package + '"'), '/qn', '/norestart') -Wait -PassThru
    if ($install.ExitCode -notin @(0, 3010)) { throw "ClamAV installation failed (exit $($install.ExitCode))." }
} elseif (-not (Test-Path -LiteralPath (Join-Path $env:ProgramFiles 'ClamAV\clamd.exe'))) {
    if (Get-Command winget.exe -ErrorAction SilentlyContinue) {
        & winget.exe install --id Cisco.ClamAV --exact --source winget --silent --accept-package-agreements --accept-source-agreements
        Assert-NativeSuccess 'ClamAV package installation'
    } else {
        # Windows Server commonly has no WinGet. Resolve the official release and its API SHA-256 digest.
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/Cisco-Talos/clamav/releases/latest' -Headers @{ 'User-Agent'='AppSouls-Server-Installer' }
        $architecture = if ([Environment]::Is64BitOperatingSystem) { 'x64' } else { 'x86' }
        $asset = Select-ClamAvAsset $release $architecture
        $temporary = Join-Path ([IO.Path]::GetTempPath()) ('AppSouls-ClamAV-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $temporary | Out-Null
        try {
            & icacls.exe $temporary /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F'
            Assert-NativeSuccess 'ClamAV staging permissions'
            $package = Join-Path $temporary 'clamav.msi'
            Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $package -UseBasicParsing
            if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $asset.digest.Substring(7)) { throw 'Official ClamAV MSI digest verification failed.' }
            $signature = Get-AuthenticodeSignature -LiteralPath $package
            if ($signature.Status -notin @('Valid', 'NotSigned')) { throw 'The ClamAV MSI has an invalid signature.' }
            $install = Start-Process msiexec.exe -ArgumentList @('/i', ('"' + $package + '"'), '/qn', '/norestart') -Wait -PassThru
            if ($install.ExitCode -notin @(0,3010)) { throw "ClamAV installation failed (exit $($install.ExitCode))." }
        } finally { if (Test-Path -LiteralPath $temporary) { [IO.Directory]::Delete($temporary, $true) } }
    }
}
$binaryDirectory = Join-Path $env:ProgramFiles 'ClamAV'
$clamd = Join-Path $binaryDirectory 'clamd.exe'
$freshclam = Join-Path $binaryDirectory 'freshclam.exe'
if (-not (Test-Path -LiteralPath $clamd) -or -not (Test-Path -LiteralPath $freshclam)) { throw 'The official ClamAV executables were not found in Program Files\ClamAV.' }
foreach ($name in @('clamd', 'freshclam')) {
    $existing = Get-CimInstance Win32_Service -Filter ("Name='$name'")
    if ($existing -and $existing.PathName -notlike ('*' + $binaryDirectory + '*')) { throw "The existing $name service uses another installation. Preserve it and configure a managed local scanner instead." }
    if ($existing) { Stop-Service -Name $name -ErrorAction Stop }
}
$data = Join-Path $env:ProgramData 'AppSouls\ClamAV'
New-Item -ItemType Directory -Path $data -Force | Out-Null
if ((Get-Item -LiteralPath $data).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'The ClamAV data directory must not be a linked directory.' }
& icacls.exe $data /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-19:(OI)(CI)M'
Assert-NativeSuccess 'Scanner directory permissions'
$daemonConfig = Join-Path $binaryDirectory 'clamd.conf'
$updateConfig = Join-Path $binaryDirectory 'freshclam.conf'
Set-ScannerConfig $daemonConfig ([ordered]@{
    TCPAddr='127.0.0.1'; TCPSocket='3310'; DatabaseDirectory=('"'+$data+'"');
    StreamMaxLength='536870912'; MaxFileSize='536870912'; MaxScanSize='1073741824';
    MaxThreads='2'; MaxQueue='8'; AlertExceedsMax='yes'; AlertEncrypted='yes'
})
Set-ScannerConfig $updateConfig ([ordered]@{ DatabaseDirectory=('"'+$data+'"'); DatabaseMirror='database.clamav.net'; Checks='12' })
# FreshClam verifies vendor signature databases. Initial update failure aborts setup.
try {
    & $freshclam ("--config-file=$updateConfig")
    Assert-NativeSuccess 'Virus definition update'
} catch {
    if (Get-Service freshclam -ErrorAction SilentlyContinue) { Start-Service freshclam -ErrorAction SilentlyContinue }
    throw
}
foreach ($entry in @(@{ Name='clamd'; Binary=$clamd; Config=$daemonConfig }, @{ Name='freshclam'; Binary=$freshclam; Config=$updateConfig })) {
    if (-not (Get-Service -Name $entry.Name -ErrorAction SilentlyContinue)) {
        & ($entry.Binary) --install-service ("--config-file=$($entry.Config)")
        Assert-NativeSuccess ($entry.Name + ' service registration')
    }
    & sc.exe config $entry.Name obj= 'NT AUTHORITY\LocalService'
    Assert-NativeSuccess ($entry.Name + ' service account')
    Set-Service -Name $entry.Name -StartupType Automatic
    Start-Service -Name $entry.Name
}
$deadline = [DateTime]::UtcNow.AddMinutes(2)
while ([DateTime]::UtcNow -lt $deadline) {
    if (Test-LocalScanner) {
        Write-Host 'ClamAV is responding locally. App Souls connects automatically; no integration entry is required. Each package still requires a clean scan.'
        exit 0
    }
    Start-Sleep -Seconds 1
}
throw 'The scanner did not become ready. Check the daemon and updater services; publishing remains blocked.'
