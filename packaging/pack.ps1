# Builds the MSIX package for Chain Calc.
#
#   .\packaging\pack.ps1                     unsigned .msix for Store upload (the Store signs it)
#   .\packaging\pack.ps1 -Version 1.0.1.0    bump the package version (the last part must stay 0 for the Store)
#   .\packaging\pack.ps1 -Sign               also sign with a self-signed test certificate for local installs
#
# Output goes to packaging\out\. Needs the .NET SDK and the Windows 10/11 SDK
# (makeappx, makepri, signtool).

param(
    [string]$Version = '1.0.0.0',
    [switch]$Sign
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $PSScriptRoot 'out'
$stage = Join-Path $out 'stage'
$priRoot = Join-Path $out 'pri'
$msix = Join-Path $out "ChainCalculator_${Version}_x64.msix"

$sdkBin = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64\makeappx.exe' |
    Sort-Object { [version]$_.Directory.Parent.Name } | Select-Object -Last 1 -ExpandProperty DirectoryName
if (-not $sdkBin) { throw 'Windows SDK not found; install it from the Visual Studio Installer.' }

function Invoke-Tool([string]$exe) {
    $output = & (Join-Path $sdkBin $exe) @args 2>&1
    if ($LASTEXITCODE -ne 0) { $output | Out-Host; throw "$exe failed with exit code $LASTEXITCODE" }
}

Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage, $priRoot | Out-Null

# Self-contained, because no .NET 10 desktop runtime is available as a Store dependency.
$assemblyVersion = $Version -replace '\.\d+$', ''   # 1.0.0.0 -> 1.0.0
dotnet publish (Join-Path $root 'src\ChainCalculator') -c Release -r win-x64 --self-contained true `
    "-p:Version=$assemblyVersion" -o $stage --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
Get-ChildItem $stage -Filter *.pdb | Remove-Item

$manifest = (Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw) -replace '(<Identity[^>]*?Version=")[^"]+', "`${1}$Version"
Set-Content (Join-Path $stage 'AppxManifest.xml') $manifest -Encoding utf8
Copy-Item (Join-Path $PSScriptRoot 'Assets') $stage -Recurse

# resources.pri maps Assets\Square44x44Logo.png to its scale/targetsize variants.
# Index only the assets, not the whole published app.
Copy-Item (Join-Path $PSScriptRoot 'Assets') $priRoot -Recurse
Copy-Item (Join-Path $stage 'AppxManifest.xml') $priRoot
Invoke-Tool makepri.exe createconfig /cf (Join-Path $out 'priconfig.xml') /dq en-US /pv 10.0.0 /o
# Keep every scale in one resources.pri; split resource packs only work in an .msixbundle.
$priConfig = Join-Path $out 'priconfig.xml'
(Get-Content $priConfig -Raw) -replace '(?s)\s*<packaging>.*?</packaging>', '' | Set-Content $priConfig -Encoding utf8
Invoke-Tool makepri.exe new /pr $priRoot /cf (Join-Path $out 'priconfig.xml') /mn (Join-Path $priRoot 'AppxManifest.xml') /of (Join-Path $stage 'resources.pri') /o

Invoke-Tool makeappx.exe pack /d $stage /p $msix /o

if ($Sign) {
    # The certificate subject must match Identity Publisher in the manifest.
    $publisher = ([xml]$manifest).Package.Identity.Publisher
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $publisher -and $_.NotAfter -gt (Get-Date) } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature `
            -CertStoreLocation Cert:\CurrentUser\My -FriendlyName 'Chain Calc test signing' `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    }
    Invoke-Tool signtool.exe sign /fd SHA256 /sha1 $cert.Thumbprint $msix
    Export-Certificate -Cert $cert -FilePath (Join-Path $out 'ChainCalculator-test.cer') | Out-Null
}

"Package: $msix"
