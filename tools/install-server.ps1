#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Source = (Split-Path -Parent $PSScriptRoot),
    [string]$HostName = 'localhost',
    [ValidateRange(1,65535)][int]$HttpsPort = 443,
    [string]$CertificateThumbprint,
    [switch]$Upgrade,
    [switch]$Plan,
    [switch]$DependenciesOnly,
    [switch]$HostPrepared,
    [switch]$PackageManaged,
    [string]$ClamAvMsi,
    [string]$ClamAvSha256
)
$ErrorActionPreference = 'Stop'
if ($DependenciesOnly -and ($HostPrepared -or $PackageManaged)) { throw 'The dependency and MSI configuration phases must run separately.' }
$SiteName = 'AppSouls'
$MarkerName = 'appsouls-deployment.json'

function Assert-Exit([string]$Stage) {
    if ($LASTEXITCODE -ne 0) { throw "$Stage failed (exit $LASTEXITCODE). Installation is incomplete." }
}
function Assert-Unlinked([string]$Path) {
    $item = [IO.DirectoryInfo]([IO.Path]::GetFullPath($Path))
    while ($item) {
        if ((Test-Path -LiteralPath $item.FullName) -and ((Get-Item -LiteralPath $item.FullName).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Refusing a linked deployment path: $($item.FullName)" }
        $item = $item.Parent
    }
}
function Get-RuntimeRequirement([string]$Directory, [switch]$MetadataOnly) {
    $names = @('TicketingCRM.runtimeconfig.json', 'TicketingCRM.deps.json')
    if (-not $MetadataOnly) { $names += @('TicketingCRM.dll', 'wwwroot') }
    foreach ($name in $names) {
        if (-not (Test-Path -LiteralPath (Join-Path $Directory $name))) { throw "Use the complete dotnet publish output: missing $name" }
    }
    Assert-Unlinked $Directory
    if (Get-ChildItem -LiteralPath $Directory -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } | Select-Object -First 1) { throw 'Published input must not contain linked files/directories.' }
    $options = (Get-Content -LiteralPath (Join-Path $Directory 'TicketingCRM.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions
    $frameworks = @($options.frameworks)
    if ($options.framework) { $frameworks += $options.framework }
    $frameworks = @($frameworks | Where-Object { $_ })
    if ($frameworks.Count -ne 2 -or -not ($frameworks.name -contains 'Microsoft.NETCore.App') -or -not ($frameworks.name -contains 'Microsoft.AspNetCore.App')) { throw 'Use a framework-dependent ASP.NET Core publish with both shared frameworks.' }
    if ($options.rollForward -eq 'Disable' -or $options.applyPatches -eq $false) { throw 'Pinned runtimes are unsupported; enable runtime patch roll-forward.' }
    $versions = @($frameworks | ForEach-Object {
        if ($_.version -notmatch '^\d+\.\d+\.\d+$') { throw 'Only stable .NET versions are supported.' }
        [version]$_.version
    })
    $channels = @($versions | ForEach-Object { "$($_.Major).$($_.Minor)" } | Select-Object -Unique)
    if ($channels.Count -ne 1) { throw 'Both required frameworks must use the same .NET channel.' }
    $osArch = if ([Environment]::Is64BitOperatingSystem) { 'x64' } else { 'x86' }
    if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64' -or $env:PROCESSOR_ARCHITEW6432 -eq 'ARM64') { throw 'Native Windows ARM64 hosting requires separate qualification; use an x64/x86 Windows host.' }
    $arch = $osArch
    $target = (Get-Content -LiteralPath (Join-Path $Directory 'TicketingCRM.deps.json') -Raw | ConvertFrom-Json).runtimeTarget.name
    if ($target -match '/') {
        $rid = ($target -split '/')[-1]
        if ($rid -notin @('win-x64', 'win-x86')) { throw 'The published runtime identifier must target Windows x64/x86.' }
        $arch = $rid.Substring(4)
        if ($arch -eq 'x64' -and $osArch -eq 'x86') { throw 'A 64-bit publish cannot run on a 32-bit host.' }
    }
    return @{ Channel=$channels[0]; Minimum=($versions | Sort-Object -Descending | Select-Object -First 1); Architecture=$arch }
}
function Assert-VendorUrl([string]$Url) {
    $uri = [uri]$Url
    if ($uri.Scheme -ne 'https' -or $uri.Port -ne 443 -or $uri.UserInfo -or $uri.Host -notin @('builds.dotnet.microsoft.com', 'download.visualstudio.microsoft.com')) { throw 'Runtime downloads must use approved HTTPS Microsoft hosts.' }
}
function Get-VendorFile([string]$Url, [string]$Destination) {
    Assert-VendorUrl $Url
    # No cross-host redirects; the metadata provides a direct vendor artifact URL.
    $request = [Net.HttpWebRequest]::Create($Url)
    $request.AllowAutoRedirect = $false
    $request.Timeout = 60000
    $request.ReadWriteTimeout = 60000
    $response = $request.GetResponse()
    try {
        if ([int]$response.StatusCode -ne 200) { throw 'Unexpected vendor download response.' }
        $inputStream = $response.GetResponseStream()
        $output = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew)
        try {
            $buffer = New-Object byte[] 1048576
            [long]$total = 0
            while (($count = $inputStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $total += $count
                if ($total -gt 536870912) { throw 'Runtime download exceeds its bounded size.' }
                $output.Write($buffer, 0, $count)
            }
        } finally { $output.Dispose(); $inputStream.Dispose() }
    } finally { $response.Dispose() }
}
function Set-PrivateDirectory([string]$Directory, [string]$AppIdentity, [string]$Rights='Modify') {
    Assert-Unlinked $Directory
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $acl = New-Object Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
        $identity = New-Object Security.Principal.SecurityIdentifier($sid)
        $rule = New-Object Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    if ($AppIdentity) {
        $rule = New-Object Security.AccessControl.FileSystemAccessRule($AppIdentity, $Rights, 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Directory -AclObject $acl
}
function Save-Record([string]$Path, $Record) {
    $Record | ConvertTo-Json | Set-Content -LiteralPath $Path -Encoding UTF8
}

$HostName = $HostName.ToLowerInvariant().TrimEnd('.')
if ($HostName.Length -gt 253 -or $HostName -notmatch '^[a-z0-9][a-z0-9.-]*$' -or @($HostName.Split('.') | Where-Object { $_ -notmatch '^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$' }).Count) { throw 'Enter a DNS hostname without a URL, port, path or configuration characters.' }
$ip = $null
if ([Net.IPAddress]::TryParse($HostName, [ref]$ip)) { throw 'Use a DNS hostname rather than a raw IP address.' }
$Source = (Resolve-Path -LiteralPath $Source).Path
$required = Get-RuntimeRequirement $Source -MetadataOnly:$DependenciesOnly
$programRoot = if ($required.Architecture -eq 'x86' -and [Environment]::Is64BitOperatingSystem) { ${env:ProgramFiles(x86)} } elseif ($env:ProgramW6432) { $env:ProgramW6432 } else { $env:ProgramFiles }
$destination = Join-Path $programRoot 'AppSouls'
$state = Join-Path $env:ProgramData 'AppSouls\Server'
$recordPath = Join-Path $destination $MarkerName
if ($Plan) {
    @{ RuntimeChannel=$required.Channel; Minimum=$required.Minimum.ToString(); Architecture=$required.Architecture; PackageManaged=[bool]$PackageManaged; Destination=$destination; State=$state; Site=$SiteName; HostName=$HostName; HttpsPort=$HttpsPort; Components=@('IIS with WebSockets', '.NET ASP.NET Core Hosting Bundle', 'ClamAV daemon and definition updater') } | ConvertTo-Json
    return
}
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run the server installer from an elevated Windows PowerShell session.' }
if ([Environment]::Is64BitOperatingSystem -and -not [Environment]::Is64BitProcess) { throw 'Use 64-bit Windows PowerShell on a 64-bit host.' }
$mutex = New-Object Threading.Mutex($false, 'Global\AppSouls-Server-Install')
if (-not $mutex.WaitOne(0)) { $mutex.Dispose(); throw 'Another App Souls installation is running.' }
try {
    $existing=$false
    if ($PackageManaged -and (-not $HostPrepared -or $Source -ne (Join-Path $destination 'app'))) { throw 'MSI mode requires its canonical payload directory and prepared dependencies.' }
    if (-not $DependenciesOnly) {
    foreach ($path in @($destination, $state)) { Assert-Unlinked $path }
    if (-not $PackageManaged -and ($Source.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase) -or $Source -eq $destination)) { throw 'Run upgrades from a separately extracted publish package.' }
    $existing = Test-Path -LiteralPath $recordPath
    if ((Test-Path -LiteralPath $destination) -and -not $existing -and -not $PackageManaged) { throw 'The destination exists without an App Souls deployment record; no files were overwritten.' }
    if ($existing) {
        $previous = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
        if ($PackageManaged) {
            if (-not $previous.PackageManaged) { throw 'Use the standalone deployment maintenance workflow for this existing installation.' }
            $HostName=$previous.HostName; $HttpsPort=$previous.HttpsPort; $CertificateThumbprint=$previous.CertificateThumbprint
        }
        if (-not $Upgrade) { throw 'Use -Upgrade for an intentional stop/copy/start update.' }
        if ($previous.HostName -ne $HostName -or $previous.HttpsPort -ne $HttpsPort -or $previous.Architecture -ne $required.Architecture) { throw 'Existing hostname, port and architecture must match this upgrade.' }
    } elseif (Test-Path -LiteralPath $state) { throw 'Existing server state must be preserved and reconciled before installation.' }
    if ($CertificateThumbprint) {
        if ($CertificateThumbprint -notmatch '^[0-9a-fA-F]{40}$') { throw 'Invalid certificate thumbprint.' }
        $certificate = Get-Item -LiteralPath ('Cert:\LocalMachine\My\' + $CertificateThumbprint)
        if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -lt (Get-Date).AddDays(1) -or -not ($certificate.DnsNameList.Unicode -contains $HostName)) { throw 'The site certificate needs a private key, matching DNS name and valid lifetime.' }
    }
    }
    if (-not $HostPrepared) {
    # IIS must be present before installing/repairing its ASP.NET Core hosting module.
    if (Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
        $result = Install-WindowsFeature Web-Server,Web-WebSockets,Web-AppInit -IncludeManagementTools
        if (-not $result.Success) { throw 'IIS feature installation failed.' }
        if ($result.RestartNeeded -eq 'Yes') { throw 'IIS requires a Windows restart. Restart and rerun this installer.' }
    } else {
        foreach ($feature in @('IIS-WebServerRole', 'IIS-WebServer', 'IIS-ManagementConsole', 'IIS-WebSockets', 'IIS-ApplicationInit')) {
            $result = Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart
            if ($result.RestartNeeded) { throw 'IIS requires a Windows restart. Restart and rerun this installer.' }
        }
    }
    }
    Import-Module WebAdministration
    foreach ($service in @('WAS', 'W3SVC')) {
        Set-Service -Name $service -StartupType Automatic
        if ((Get-Service -Name $service).Status -ne 'Running') { Start-Service -Name $service }
    }
    if (-not $DependenciesOnly) {
    $site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
    if (($site -or (Test-Path "IIS:\AppPools\$SiteName")) -and -not $existing) { throw 'An unrelated AppSouls IIS site or pool already exists.' }
    if ($site -and ((-not $site.PhysicalPath.StartsWith($destination + '\releases\', [StringComparison]::OrdinalIgnoreCase) -and -not ($PackageManaged -and $site.PhysicalPath -eq $Source)) -or $site.ApplicationPool -ne $SiteName)) { throw 'The existing site points outside the managed release directory or application pool.' }
    $bindingAddress = if ($HostName -eq 'localhost') { '127.0.0.1' } else { '*' }
    if ($site) {
        $bindings = @($site.Bindings.Collection)
        if ($bindings.Count -ne 1 -or $bindings[0].protocol -ne 'https' -or $bindings[0].bindingInformation -ne "${bindingAddress}:${HttpsPort}:$HostName") { throw 'Existing IIS bindings differ from this deployment. Reconcile them before upgrading.' }
    }
    foreach ($otherSite in Get-Website) {
        if ($otherSite.Name -eq $SiteName -and $existing) { continue }
        foreach ($binding in $otherSite.Bindings.Collection) {
            if ($binding.protocol -eq 'https' -and $binding.bindingInformation -match (':' + $HttpsPort + ':') -and (($binding.bindingInformation -split ':')[-1] -in @('', $HostName))) { throw 'Another IIS site already owns this HTTPS binding.' }
        }
    }
    }
    if (-not $HostPrepared) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $temporary = Join-Path ([IO.Path]::GetTempPath()) ('AppSouls-' + [guid]::NewGuid().ToString('N'))
    Set-PrivateDirectory $temporary ''
    try {
        $metadataPath = Join-Path $temporary 'releases.json'
        Get-VendorFile ("https://builds.dotnet.microsoft.com/dotnet/release-metadata/$($required.Channel)/releases.json") $metadataPath
        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
        if ($metadata.'channel-version' -ne $required.Channel -or $metadata.'support-phase' -notin @('active', 'maintenance') -or [version]$metadata.'latest-runtime' -lt $required.Minimum) { throw 'Required stable .NET channel is unsupported, mismatched or too old.' }
        $release = $metadata.releases | Where-Object { $_.'aspnetcore-runtime'.version -eq $metadata.'latest-runtime' } | Select-Object -First 1
        $artifact = $release.'aspnetcore-runtime'.files | Where-Object { $_.name -eq 'dotnet-hosting-win.exe' } | Select-Object -First 1
        if (-not $artifact -or $artifact.hash -notmatch '^[0-9a-fA-F]{128}$') { throw 'No verifiable IIS Hosting Bundle is available for this runtime.' }
        $bundle = Join-Path $temporary 'dotnet-hosting.exe'
        Get-VendorFile $artifact.url $bundle
        if ((Get-FileHash -LiteralPath $bundle -Algorithm SHA512).Hash -ne $artifact.hash) { throw 'Hosting Bundle SHA-512 verification failed.' }
        $signature = Get-AuthenticodeSignature -LiteralPath $bundle
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(?:^|,\s*)O=Microsoft Corporation(?:,|$)') { throw 'The Hosting Bundle must have a valid Microsoft signature.' }
        # Repair also handles a bundle installed before IIS. Existing matching runtimes stay side by side.
        $installed = (Test-Path (Join-Path $programRoot ('dotnet\shared\Microsoft.AspNetCore.App\' + $metadata.'latest-runtime'))) -and
            (Test-Path (Join-Path $env:ProgramFiles 'IIS\Asp.Net Core Module\V2\aspnetcorev2.dll'))
        $verb = if ($installed) { '/repair' } else { '/install' }
        $process = Start-Process -FilePath $bundle -ArgumentList @($verb, '/quiet', '/norestart', 'OPT_NO_X86=0') -Wait -PassThru
        if ($process.ExitCode -eq 3010) { throw 'The Hosting Bundle requires a restart. Restart Windows and rerun this installer.' }
        if ($process.ExitCode -ne 0) { throw "Hosting Bundle installation failed (exit $($process.ExitCode))." }
    } finally {
        if (Test-Path -LiteralPath $temporary) { [IO.Directory]::Delete($temporary, $true) }
    }
    }
    $dotnet = Join-Path $programRoot 'dotnet\dotnet.exe'
    $runtimes = & $dotnet --list-runtimes
    Assert-Exit 'Runtime verification'
    foreach ($framework in @('Microsoft.NETCore.App', 'Microsoft.AspNetCore.App')) {
        if (-not ($runtimes | Where-Object { $parts=$_ -split ' ' ; $parts[0] -eq $framework -and $parts[1] -match '^\d+\.\d+\.\d+$' -and $parts[1].StartsWith($required.Channel+'.') -and [version]$parts[1] -ge $required.Minimum })) { throw "Expected $framework runtime was not installed for $($required.Architecture)." }
    }
    if (-not $HostPrepared) {
    # A child process contains the dependency helper's exit statement.
    $powershell = Join-Path $PSHOME 'powershell.exe'
    if ($ClamAvMsi) {
        & $powershell -NoProfile -File (Join-Path $PSScriptRoot 'install-server-dependencies.ps1') -ClamAvMsi $ClamAvMsi -ExpectedSha256 $ClamAvSha256
    } else { & $powershell -NoProfile -File (Join-Path $PSScriptRoot 'install-server-dependencies.ps1') }
    Assert-Exit 'ClamAV dependency installation'
    } else {
        $client=New-Object Net.Sockets.TcpClient
        try {
            if (-not $client.ConnectAsync('127.0.0.1',3310).Wait(3000)) { throw 'Local ClamAV is unavailable.' }
            $stream=$client.GetStream();$stream.ReadTimeout=3000;$bytes=[Text.Encoding]::ASCII.GetBytes("zPING`0");$stream.Write($bytes,0,$bytes.Length)
            $reply=New-Object byte[] 5;$count=0
            while($count -lt 5){$n=$stream.Read($reply,$count,5-$count);if($n -eq 0){break};$count+=$n}
            if([Text.Encoding]::ASCII.GetString($reply,0,$count) -ne "PONG`0"){throw 'Local ClamAV did not return a valid response.'}
        } finally {$client.Dispose()}
    }
    if ($DependenciesOnly) { Write-Host 'Host dependencies are ready; application MSI installation can proceed.'; return }
    if (-not $existing) {
        Set-PrivateDirectory $destination ''
        Save-Record $recordPath @{ HostName=$HostName; HttpsPort=$HttpsPort; Architecture=$required.Architecture; PackageManaged=[bool]$PackageManaged; Status='provisioning' }
    }
    if (-not (Test-Path "IIS:\AppPools\$SiteName")) { New-WebAppPool -Name $SiteName | Out-Null }
    $appIdentity = "IIS AppPool\$SiteName"
    Set-PrivateDirectory $destination $appIdentity 'ReadAndExecute'
    Set-PrivateDirectory $state $appIdentity
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name enable32BitAppOnWin64 -Value ($required.Architecture -eq 'x86')
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name startMode -Value AlwaysRunning
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name processModel.identityType -Value 4
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name processModel.loadUserProfile -Value $true
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name processModel.maxProcesses -Value 1
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
    Set-ItemProperty "IIS:\AppPools\$SiteName" -Name recycling.disallowOverlappingRotation -Value $true
    if (-not $CertificateThumbprint) {
        if ($existing -and $previous.CertificateThumbprint) { $certificate = Get-Item -LiteralPath ('Cert:\LocalMachine\My\' + $previous.CertificateThumbprint) }
        else { $certificate = New-SelfSignedCertificate -DnsName $HostName -CertStoreLocation 'Cert:\LocalMachine\My' -NotAfter (Get-Date).AddDays(90) -FriendlyName 'App Souls bootstrap HTTPS' }
    }
    $releasePath = $Source
    if (-not $PackageManaged) {
        $releasePath = Join-Path $destination ('releases\' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $releasePath -Force | Out-Null
        Get-ChildItem -LiteralPath $Source -Force | Copy-Item -Destination $releasePath -Recurse -Force
    }
    $configPath = Join-Path $releasePath 'web.config'
    [xml]$web = '<configuration><location path="." inheritInChildApplications="false"><system.webServer><security><requestFiltering><requestLimits maxAllowedContentLength="538968064" /></requestFiltering></security><handlers><add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" /></handlers><aspNetCore processPath="dotnet" arguments=".\TicketingCRM.dll" stdoutLogEnabled="false" hostingModel="outofprocess" requestTimeout="01:00:00" startupTimeLimit="180" shutdownTimeLimit="60"><environmentVariables /></aspNetCore></system.webServer></location></configuration>'
    $asp = $web.configuration.location.'system.webServer'.aspNetCore
    $asp.SetAttribute('processPath', $dotnet)
    foreach ($entry in @{
        ASPNETCORE_ENVIRONMENT='Production'; APPSOULS_VAULT_PATH=(Join-Path $state 'vault\server.vault');
        APPSOULS_KEY_PATH=(Join-Path $state 'keys\server.key'); APPSOULS_SETUP_PRESET_PATH=(Join-Path $state 'setup\server-preset.json')
    }.GetEnumerator()) {
        $node = $web.CreateElement('environmentVariable'); $node.SetAttribute('name', $entry.Key); $node.SetAttribute('value', $entry.Value)
        $asp.SelectSingleNode('environmentVariables').AppendChild($node) | Out-Null
    }
    $web.Save($configPath)
    if ($site) { Stop-Website -Name $SiteName }
    if ((Get-WebAppPoolState -Name $SiteName).Value -eq 'Started') { Stop-WebAppPool -Name $SiteName }
    Save-Record $recordPath @{ HostName=$HostName; HttpsPort=$HttpsPort; Architecture=$required.Architecture; PackageManaged=[bool]$PackageManaged; Release=$releasePath; CertificateThumbprint=$certificate.Thumbprint; Status='prepared' }
    try {
        if (-not $site) { New-Website -Name $SiteName -PhysicalPath $releasePath -ApplicationPool $SiteName -IPAddress $bindingAddress -Port $HttpsPort -HostHeader $HostName -Ssl | Out-Null }
        else { Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $releasePath }
        Set-ItemProperty "IIS:\Sites\$SiteName\" -Name applicationDefaults.preloadEnabled -Value $true
        Set-WebBinding -Name $SiteName -Protocol https -BindingInformation "${bindingAddress}:${HttpsPort}:$HostName" -PropertyName sslFlags -Value 1
        $binding = Get-WebBinding -Name $SiteName -Protocol https
        $binding.AddSslCertificate($certificate.Thumbprint, 'My')
        if ((Get-WebAppPoolState -Name $SiteName).Value -ne 'Started') { Start-WebAppPool -Name $SiteName }
        if ((Get-Website -Name $SiteName).State -ne 'Started') { Start-Website -Name $SiteName }
        # IIS/WAS must load the hosting module; do not restart unrelated sites with iisreset.
        $deadline = (Get-Date).AddMinutes(2)
        $ready = $false
        if (-not ('AppSouls.InstallCertificate' -as [type])) {
            Add-Type -TypeDefinition @'
namespace AppSouls {
    public static class InstallCertificate {
        public static string Thumbprint;
        public static bool Validate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate cert,
            System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors errors) {
            return cert != null && string.Equals(cert.GetCertHashString(), Thumbprint, System.StringComparison.OrdinalIgnoreCase);
        }
        public static bool Check(string host, int port) {
            try {
                using (var tcp = new System.Net.Sockets.TcpClient()) {
                    if (!tcp.ConnectAsync("127.0.0.1", port).Wait(5000)) return false;
                    tcp.ReceiveTimeout = 5000; tcp.SendTimeout = 5000;
                    using (var ssl = new System.Net.Security.SslStream(tcp.GetStream(), false, Validate)) {
                        ssl.ReadTimeout = 5000; ssl.WriteTimeout = 5000;
                        ssl.AuthenticateAsClient(host, null, System.Security.Authentication.SslProtocols.Tls12, false);
                        var request = System.Text.Encoding.ASCII.GetBytes("GET /setup HTTP/1.1\r\nHost: " + host + ":" + port + "\r\nConnection: close\r\n\r\n");
                        ssl.Write(request, 0, request.Length);
                        using (var reader = new System.IO.StreamReader(ssl)) {
                            var first = reader.ReadLine();
                            if (first == null || !first.StartsWith("HTTP/1.1 200 ", System.StringComparison.Ordinal)) return false;
                            var buffer = new char[262144]; var count = 0; int read;
                            while (count < buffer.Length && (read = reader.Read(buffer, count, buffer.Length - count)) > 0) count += read;
                            return new string(buffer, 0, count).Contains("App Souls");
                        }
                    }
                }
            } catch { return false; }
        }
    }
}
'@
        }
        [AppSouls.InstallCertificate]::Thumbprint = $certificate.Thumbprint
        try {
            while ((Get-Date) -lt $deadline) {
                if ([AppSouls.InstallCertificate]::Check($HostName, $HttpsPort)) { $ready=$true; break }
                Start-Sleep -Seconds 1
            }
        } finally { [AppSouls.InstallCertificate]::Thumbprint = $null }
        if (-not $ready) { throw 'HTTPS readiness failed. Check the AppSouls IIS site and Windows event logs.' }
        if ($HostName -ne 'localhost') {
            if (-not (Get-NetFirewallRule -Name 'AppSouls-HTTPS' -ErrorAction SilentlyContinue)) { New-NetFirewallRule -Name 'AppSouls-HTTPS' -DisplayName 'App Souls HTTPS' -Direction Inbound -Action Allow -Protocol TCP -LocalPort $HttpsPort | Out-Null }
        }
        Save-Record $recordPath @{ HostName=$HostName; HttpsPort=$HttpsPort; Architecture=$required.Architecture; PackageManaged=[bool]$PackageManaged; Release=$releasePath; CertificateThumbprint=$certificate.Thumbprint; Status='ready' }
    } catch {
        Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
        Stop-WebAppPool -Name $SiteName -ErrorAction SilentlyContinue
        throw
    }
    Write-Host "App Souls installed: https://${HostName}:$HttpsPort/setup"
    if (-not $CertificateThumbprint) { Write-Host 'Bootstrap HTTPS uses a self-signed certificate. Supply a trusted certificate before public use.' }
    Write-Host 'Complete database, administrator and email settings in server configuration. Existing vault, keys and artifacts were retained.'
} finally { $mutex.ReleaseMutex(); $mutex.Dispose() }
