[CmdletBinding()]
param([Parameter(Mandatory)][string]$InstallerPath)
$ErrorActionPreference = 'Stop'
$dataRoot = Join-Path $env:LocalAppData 'IracingSetupManager'
$marker = Join-Path $dataRoot 'upgrade-preservation-test.marker'
New-Item -ItemType Directory -Force $dataRoot | Out-Null
$value = [guid]::NewGuid().ToString('N')
Set-Content -LiteralPath $marker -Value $value -NoNewline
$installer = (Resolve-Path $InstallerPath).Path
foreach ($stage in @('installation', 'mise à niveau')) {
    $process = Start-Process -FilePath $installer -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$stage échouée : code $($process.ExitCode)" }
    if ((Get-Content -LiteralPath $marker -Raw) -ne $value) { throw "Les données utilisateur n’ont pas survécu à la $stage." }
}

$uninstaller = Join-Path $env:LocalAppData 'Programs\IracingSetupManager\unins000.exe'
if (-not (Test-Path $uninstaller)) { throw 'Le programme de désinstallation est absent.' }
$uninstall = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -Wait -PassThru
if ($uninstall.ExitCode -ne 0) { throw "Désinstallation échouée : code $($uninstall.ExitCode)" }
if ((Get-Content -LiteralPath $marker -Raw) -ne $value) { throw 'La désinstallation a supprimé les données utilisateur.' }
Remove-Item -LiteralPath $marker -Force
Write-Host 'Test réussi : installation, mise à niveau et désinstallation conservent les données utilisateur.'
