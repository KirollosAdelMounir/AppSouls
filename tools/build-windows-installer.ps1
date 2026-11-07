# Build on Windows; produces an application MSI and a single Burn setup EXE.
param([Parameter(Mandatory=$true)][string]$Version,[string]$Output=(Join-Path $PSScriptRoot '..\artifacts\installers'))
function New-PayloadManifest([string]$publish,[string]$payload) {
$wix='http://wixtoolset.org/schemas/v4/wxs'
$doc=New-Object Xml.XmlDocument;$root=$doc.CreateElement('Wix',$wix);[void]$doc.AppendChild($root)
$fragment=$doc.CreateElement('Fragment',$wix);[void]$root.AppendChild($fragment)
$directory=$doc.CreateElement('DirectoryRef',$wix);$directory.SetAttribute('Id','APPDIR');[void]$fragment.AppendChild($directory)
$group=$doc.CreateElement('ComponentGroup',$wix);$group.SetAttribute('Id','Payload');[void]$fragment.AppendChild($group)
$directories=@{''=$directory}
$sha=[Security.Cryptography.SHA256]::Create()
try {
 foreach($file in Get-ChildItem -LiteralPath $publish -Force -Recurse|Sort-Object FullName){
  if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked publish inputs are forbidden.'}
  if($file.PSIsContainer){continue}
  if($file.Extension -in @('.key','.pem','.pfx','.p12','.vault') -or $file.Name -in @('server-preset.json','server-input.json','owner-input.json')){throw 'Private material must not be packaged.'}
  $relative=$file.FullName.Substring($publish.Length+1)
  if($relative -eq 'web.config'){continue}
  $parts=$relative.Split([IO.Path]::DirectorySeparatorChar);$parent=''
  for($i=0;$i -lt $parts.Length-1;$i++){
   $next=if($parent){$parent+'\'+$parts[$i]}else{$parts[$i]}
   if(-not $directories.ContainsKey($next)){
    $hash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($next.ToLowerInvariant())))).Replace('-','')
    $node=$doc.CreateElement('Directory',$wix);$node.SetAttribute('Id','D'+$hash.Substring(0,24));$node.SetAttribute('Name',$parts[$i]);[void]$directories[$parent].AppendChild($node);$directories[$next]=$node
   };$parent=$next
  }
  $hashBytes=$sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($relative.ToLowerInvariant()));$hash=([BitConverter]::ToString($hashBytes)).Replace('-','')
  $component=$doc.CreateElement('Component',$wix);$component.SetAttribute('Id','C'+$hash.Substring(0,24));$component.SetAttribute('Guid',([Guid]::new([byte[]]$hashBytes[0..15])).ToString());$component.SetAttribute('Bitness','always64')
  $item=$doc.CreateElement('File',$wix);$item.SetAttribute('Id','F'+$hash.Substring(0,24));$item.SetAttribute('Source',$file.FullName);$item.SetAttribute('KeyPath','yes');[void]$component.AppendChild($item);[void]$directories[$parent].AppendChild($component)
  $reference=$doc.CreateElement('ComponentRef',$wix);$reference.SetAttribute('Id',$component.GetAttribute('Id'));[void]$group.AppendChild($reference)
 }
} finally {$sha.Dispose()}
$doc.Save($payload)
}
$ErrorActionPreference='Stop'
if($env:OS -ne 'Windows_NT'){throw 'MSI/Burn compilation requires a Windows build machine.'}
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Version must be major.minor.build.'}
$v=[version]$Version;if($v.Major -gt 255 -or $v.Minor -gt 255 -or $v.Build -gt 65535){throw 'MSI version limits exceeded.'}
$repo=Split-Path -Parent $PSScriptRoot;$Output=[IO.Path]::GetFullPath($Output)
$work=Join-Path $Output ('work-'+$Version)
if(Test-Path -LiteralPath $work){throw 'Build work directory already exists; choose a clean output directory.'}
New-Item -ItemType Directory -Path $work -Force|Out-Null
$publish=Join-Path $work 'publish'
function Checked([scriptblock]$Command){& $Command;if($LASTEXITCODE -ne 0){throw 'Installer build failed.'}}
Checked {dotnet publish (Join-Path $repo 'TicketingCRM\TicketingCRM.csproj') -c Release -r win-x64 --self-contained false -p:UseAppHost=false "-p:Version=$Version" -o $publish}
# Omit generated hosting config: the MSI's configuration-only action creates it with installation-specific private paths.
[IO.File]::Delete((Join-Path $publish 'web.config'))
$payload=Join-Path $work 'Payload.wxs'
New-PayloadManifest $publish $payload
$runnerProject=Join-Path $repo 'packaging\windows\Runner\Runner.csproj'
$runnerOutput=Join-Path $work 'runner'
Checked {dotnet build $runnerProject -c Release "-p:PublishDir=$publish" -o $runnerOutput}
$runner=Join-Path $runnerOutput 'AppSoulsHostSetup.exe'
$msiOutput=Join-Path $work 'msi'
Checked {dotnet build (Join-Path $repo 'packaging\windows\Msi\AppSouls.wixproj') -c Release "-p:PublishDir=$publish" "-p:RunnerPath=$runner" "-p:ProductVersion=$Version" "-p:PayloadSource=$payload" -o $msiOutput}
$msi=Join-Path $msiOutput 'AppSouls.msi'
Checked {dotnet build (Join-Path $repo 'packaging\windows\Bundle\AppSouls.Bundle.wixproj') -c Release "-p:RunnerPath=$runner" "-p:MsiPath=$msi" "-p:ProductVersion=$Version" -o $Output}
Copy-Item -LiteralPath $msi -Destination (Join-Path $Output 'AppSouls.msi')
Write-Host "Built $Output\AppSoulsSetup.exe and AppSouls.msi. Outputs are unsigned; production signing and native installation qualification remain required."
