# Nonprivileged helper/contract tests. Native IIS installation requires a Windows deployment test.
$ErrorActionPreference='Stop'
function Assert-True($Value,[string]$Message) { if(-not $Value){throw $Message} }
function Assert-Throws([scriptblock]$Action,[string]$Message) { $caught=$false;try{ & $Action }catch{$caught=$true};if(-not $caught){throw $Message} }
foreach($file in @('install-server.ps1','install-server-dependencies.ps1','build-windows-installer.ps1','../packaging/windows/msi-hosting.ps1')) {
 $tokens=$null;$errors=$null
 $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file),[ref]$tokens,[ref]$errors)
 Assert-True ($errors.Count -eq 0) "Parse errors in $file"
 if($file -eq 'install-server.ps1') {
  $code=$ast.Find({param($node) $node -is [Management.Automation.Language.StringConstantExpressionAst] -and $node.Value.StartsWith('namespace AppSouls {')},$true)
  Assert-True ($null -ne $code) 'TLS readiness helper is missing'
  Add-Type -TypeDefinition $code.Value
  Assert-True (-not [AppSouls.InstallCertificate]::Validate($null,$null,$null,[Net.Security.SslPolicyErrors]::None)) 'Null certificate was trusted'
 }
 foreach($name in @('Assert-Unlinked','Get-RuntimeRequirement','Assert-VendorUrl','Select-ClamAvAsset','New-PayloadManifest')) {
  $definition=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name},$false)
  if($definition){Invoke-Expression $definition.Extent.Text}
 }
}
$root=Join-Path ([IO.Path]::GetTempPath()) ('AppSouls-Installer-Test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $root 'wwwroot') -Force|Out-Null
try {
 [IO.File]::WriteAllText((Join-Path $root 'TicketingCRM.dll'),'fixture')
 $config=Join-Path $root 'TicketingCRM.runtimeconfig.json';$deps=Join-Path $root 'TicketingCRM.deps.json'
 [IO.File]::WriteAllText($config,'{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.AspNetCore.App","version":"10.0.1"}]}}')
 [IO.File]::WriteAllText($deps,'{"runtimeTarget":{"name":".NETCoreApp,Version=v10.0"}}')
 $required=Get-RuntimeRequirement $root
 Assert-True ($required.Channel -eq '10.0' -and $required.Minimum -eq [version]'10.0.1') 'Incorrect channel/minimum detection'
 [IO.File]::Delete((Join-Path $root 'TicketingCRM.dll'))
 Assert-True ((Get-RuntimeRequirement $root -MetadataOnly).Channel -eq '10.0') 'Dependency bootstrap required an already installed app'
 Assert-Throws {Get-RuntimeRequirement $root} 'Application installation accepted a missing DLL'
 [IO.File]::WriteAllText((Join-Path $root 'TicketingCRM.dll'),'fixture')
 [IO.File]::WriteAllText($deps,'{"runtimeTarget":{"name":".NETCoreApp,Version=v10.0/win-x86"}}')
 Assert-True ((Get-RuntimeRequirement $root).Architecture -eq 'x86') 'x86 publish detection failed'
 [IO.File]::WriteAllText($deps,'{"runtimeTarget":{"name":".NETCoreApp,Version=v10.0/linux-x64"}}')
 Assert-Throws {Get-RuntimeRequirement $root} 'Linux RID was accepted on Windows'
 [IO.File]::WriteAllText($deps,'{"runtimeTarget":{"name":".NETCoreApp,Version=v10.0"}}')
 [IO.File]::WriteAllText($config,'{"runtimeOptions":{"includedFrameworks":[]}}')
 Assert-Throws {Get-RuntimeRequirement $root} 'Self-contained package was accepted'
 [IO.File]::WriteAllText($config,'{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"9.0.0"},{"name":"Microsoft.AspNetCore.App","version":"10.0.0"}]}}')
 Assert-Throws {Get-RuntimeRequirement $root} 'Mixed runtime channels were accepted'
 Assert-VendorUrl 'https://builds.dotnet.microsoft.com/dotnet/runtime.exe'
 Assert-Throws {Assert-VendorUrl 'https://builds.dotnet.microsoft.com.evil.test/runtime.exe'} 'Vendor hostname spoof was accepted'
 Assert-Throws {Assert-VendorUrl 'http://builds.dotnet.microsoft.com/runtime.exe'} 'Plaintext runtime URL was accepted'
 $release=[pscustomobject]@{draft=$false;prerelease=$false;assets=@([pscustomobject]@{name='clamav-1.5.4.win.x64.msi';digest=('sha256:'+('a'*64));browser_download_url='https://github.com/Cisco-Talos/clamav/releases/download/clamav-1.5.4/clamav-1.5.4.win.x64.msi'})}
 Assert-True ((Select-ClamAvAsset $release 'x64').name -eq 'clamav-1.5.4.win.x64.msi') 'Official scanner asset selection failed'
 $release.assets[0].digest=$null
 Assert-Throws {Select-ClamAvAsset $release 'x64'} 'Scanner without digest was accepted'
 $manifest=Join-Path $root 'Payload.wxs';$web=Join-Path $root 'web.config';[IO.File]::WriteAllText($web,'generated-at-install')
 $nested=Join-Path $root 'wwwroot/static';New-Item -ItemType Directory -Path $nested -Force|Out-Null;[IO.File]::WriteAllText((Join-Path $nested 'app.css'),'fixture')
 $output=Join-Path ([IO.Path]::GetTempPath()) ('AppSouls-Payload-'+[guid]::NewGuid().ToString('N')+'.wxs')
 try {
  New-PayloadManifest $root $output
  [xml]$payload=Get-Content -LiteralPath $output -Raw
  $manager=New-Object Xml.XmlNamespaceManager($payload.NameTable);$manager.AddNamespace('w','http://wixtoolset.org/schemas/v4/wxs')
  $files=@($payload.SelectNodes('//w:File',$manager));Assert-True ($files.Count -eq 4) 'Incorrect harvested file count or generated web.config included'
  Assert-True (@($payload.SelectNodes('//w:ComponentRef',$manager)).Count -eq $files.Count) 'Missing MSI feature components'
  [IO.File]::WriteAllText((Join-Path $root 'secret.key'),'test-only')
  Assert-Throws {New-PayloadManifest $root $output} 'Private key was included in the MSI payload'
 } finally {if(Test-Path -LiteralPath $output){[IO.File]::Delete($output)}}
 Write-Output 'Passed: Windows script parsing, runtime/channel/x86 detection, RID/self-contained/mixed-channel rejection, vendor URL restrictions and official scanner digest selection.'
} finally { [IO.Directory]::Delete($root,$true) }
