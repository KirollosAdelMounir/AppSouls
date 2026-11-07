# MSI lifecycle work only. Never installs OS features, runtimes or another MSI.
param([ValidateSet('prepare','stop','remove')][string]$Mode,[Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference='Stop'
$Root=[IO.Path]::GetFullPath($Root).TrimEnd('\')
$allowed=@((Join-Path $env:ProgramFiles 'AppSouls'))
if(${env:ProgramFiles(x86)}){$allowed+=(Join-Path ${env:ProgramFiles(x86)} 'AppSouls')}
if($Root -notin $allowed){throw 'Unrecognized MSI installation root.'}
for($item=[IO.DirectoryInfo]$Root;$item;$item=$item.Parent){if((Test-Path -LiteralPath $item.FullName) -and ((Get-Item -LiteralPath $item.FullName).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked installation paths are not supported.'}}
$record=Join-Path $Root 'appsouls-deployment.json'
if(-not(Test-Path -LiteralPath $record)){
 if((Test-Path -LiteralPath $Root) -and @(Get-ChildItem -LiteralPath $Root -Force).Count){throw 'The existing destination is unmanaged.'}
 return
}
$deployment=Get-Content -LiteralPath $record -Raw|ConvertFrom-Json
if(-not $deployment.PackageManaged){throw 'The existing deployment is not managed by MSI.'}
Import-Module WebAdministration
$site=Get-Website -Name AppSouls -ErrorAction SilentlyContinue
$expected=Join-Path $Root 'app'
if($site -and ($site.PhysicalPath -ne $expected -or $site.ApplicationPool -ne 'AppSouls')){throw 'The IIS site was changed; it will not be removed or replaced.'}
if($site){Stop-Website -Name AppSouls}
if(Test-Path 'IIS:\AppPools\AppSouls'){
 if((Get-WebAppPoolState AppSouls).Value -eq 'Started'){Stop-WebAppPool AppSouls}
}
if($Mode -eq 'remove'){
 if($site){Remove-Website -Name AppSouls}
 if(Test-Path 'IIS:\AppPools\AppSouls'){Remove-WebAppPool -Name AppSouls}
 # Certificate, firewall rule, shared dependencies, database and private state remain for deliberate recovery.
 $web=Join-Path $expected 'web.config'
 if(Test-Path -LiteralPath $web){[IO.File]::Delete($web)}
 $deployment.Status='uninstalled';$deployment|ConvertTo-Json|Set-Content -LiteralPath $record -Encoding UTF8
}
