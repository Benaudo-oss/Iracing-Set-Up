[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')][string]$Version = '1.2.9.27',
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64',
    [string]$CertificateThumbprint
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$dotnet = @(
    (Join-Path (Split-Path -Parent $repo) 'tools\dotnet\dotnet.exe'),
    (Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $dotnet) { throw 'Le SDK .NET 10 est requis pour construire cet installateur.' }
$publish = Join-Path $repo "artifacts\publish\$Runtime"
$output = Join-Path $repo 'artifacts\installer'
$project = Join-Path $repo 'src\IracingSetupManager.App\IracingSetupManager.App.csproj'
$iss = Join-Path $PSScriptRoot 'IracingSetupManager.iss'

& $dotnet publish $project --configuration Release --runtime $Runtime --self-contained true --output $publish --no-restore --disable-build-servers --maxcpucount:1 `
    -p:Version=$Version -p:FileVersion=$Version -p:AssemblyVersion=$Version
if ($LASTEXITCODE -ne 0) { throw 'La publication Windows a échoué.' }

# WinUI publie ses fichiers MUI dans un dossier par langue. L’interface de
# l’application étant uniquement française, ne conserver que fr-FR.
$languageFolders = Get-ChildItem -LiteralPath $publish -Directory | Where-Object {
    if ($_.Name -eq 'fr-FR') { return $false }
    $files = @(Get-ChildItem -LiteralPath $_.FullName -File -Recurse)
    $files.Count -gt 0 -and @($files | Where-Object { $_.Extension -ne '.mui' }).Count -eq 0
}
foreach ($folder in $languageFolders) {
    Remove-Item -LiteralPath $folder.FullName -Recurse -Force
}
$remainingLanguageFolders = Get-ChildItem -LiteralPath $publish -Directory | Where-Object {
    $files = @(Get-ChildItem -LiteralPath $_.FullName -File -Recurse)
    $files.Count -gt 0 -and @($files | Where-Object { $_.Extension -ne '.mui' }).Count -eq 0
}
if (@($remainingLanguageFolders).Count -ne 1 -or $remainingLanguageFolders[0].Name -ne 'fr-FR') {
    throw 'Le nettoyage des ressources linguistiques a échoué.'
}

$signTool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $CertificateThumbprint) {
    $CertificateThumbprint = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } | Sort-Object NotAfter -Descending | Select-Object -First 1 -ExpandProperty Thumbprint
}
$canSign = $CertificateThumbprint -and $signTool
if ($canSign) {
    $appExecutable = Join-Path $publish 'IracingSetupManager.App.exe'
    & $signTool.FullName sign /sha1 $CertificateThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $appExecutable
    if ($LASTEXITCODE -ne 0) { throw "La signature a échoué pour $appExecutable" }
}

$iscc = @((Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source), "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 est requis. Installez JRSoftware.InnoSetup avec winget.' }
New-Item -ItemType Directory -Force -Path $output | Out-Null
& $iscc "/DAppVersion=$Version" "/DPublishDir=$publish" "/DOutputDir=$output" $iss
if ($LASTEXITCODE -ne 0) { throw 'La génération de cet installateur a échoué.' }

$installer = Join-Path $output "IracingSetupManager-$Version-win-x64-setup.exe"
if ($canSign) {
    & $signTool.FullName sign /sha1 $CertificateThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $installer
    if ($LASTEXITCODE -ne 0) { throw "La signature a échoué pour $installer" }
    Write-Host "Installateur signé : $installer"
} else { Write-Warning 'Aucun certificat utilisable ou SignTool absent : installateur non signé.' }

$hash = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$installer.sha256" -Value "$hash  $(Split-Path -Leaf $installer)" -Encoding ascii
Write-Host "Installateur généré : $installer"
