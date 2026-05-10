add-type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(
        ServicePoint srvPoint, X509Certificate certificate,
        WebRequest request, int certificateProblem) {
        return true;
    }
}
"@
$AllProtocols = [System.Net.SecurityProtocolType]'Ssl3,Tls,Tls11,Tls12'
[System.Net.ServicePointManager]::SecurityProtocol = $AllProtocols
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy

$RepoRoot = $PSScriptRoot

function Get-HandBrakeNightlyGuiZipFileName {
    param(
        [Parameter(Mandatory)]
        [string] $PageContent,
        [Parameter(Mandatory)]
        [ValidateSet('x86_64', 'arm64')]
        [string] $Architecture
    )
    $suffix = "${Architecture}-Win_GUI.zip"
    $pattern = 'HandBrake[^"<>]+' + [regex]::Escape($suffix)
    if (-not ($PageContent -match $pattern)) {
        throw "Could not find $Architecture HandBrake nightly zip on the releases page."
    }
    return $matches[0]
}

function Install-HbDllFromHandBrakeGuiZip {
    param(
        [Parameter(Mandatory)]
        [string] $ZipUrl,
        [Parameter(Mandatory)]
        [string] $ZipDownloadPath,
        [Parameter(Mandatory)]
        [string] $ExtractDirectory,
        [Parameter(Mandatory)]
        [string] $DestinationHbDllPath
    )
    Invoke-WebRequest -Uri $ZipUrl -OutFile $ZipDownloadPath -UseBasicParsing
    [System.IO.Compression.ZipFile]::ExtractToDirectory($ZipDownloadPath, $ExtractDirectory)
    $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $DestinationHbDllPath)
    Copy-Item (Join-Path $ExtractDirectory 'HandBrake\hb.dll') $DestinationHbDllPath
}

#$wc = New-Object System.Net.WebClient
#$wc.Credentials = New-Object System.Net.NetworkCredential("username","password")
#$nightlyPageContent = $wc.DownloadString("https://handbrake.fr/nightly.php")

$nightlyPageContent = (
    Invoke-WebRequest -Uri "https://github.com/HandBrake/handbrake-snapshots/releases/tag/win" -UseBasicParsing
).Content

$downloadBaseUrl = "https://github.com/HandBrake/HandBrake-snapshots/releases/download/win/"

$importHbRoot = Join-Path $RepoRoot "Import\Hb"
if (Test-Path $importHbRoot) {
    Remove-Item (Join-Path $importHbRoot '*') -Recurse -Force
}

Add-Type -AssemblyName "System.IO.Compression.FileSystem"

$platforms = @(
    @{ HandBrakeArch = 'x86_64'; ImportSubdir = 'x64'; ZipFileName = 'hb-x64.zip'; LibRelative = 'Lib\x64\hb.dll' }
    @{ HandBrakeArch = 'arm64'; ImportSubdir = 'arm64'; ZipFileName = 'hb-arm64.zip'; LibRelative = 'Lib\arm64\hb.dll' }
)

foreach ($p in $platforms) {
    $zipName = Get-HandBrakeNightlyGuiZipFileName -PageContent $nightlyPageContent -Architecture $p.HandBrakeArch
    Write-Host "Downloading $zipName..."
    $extractDir = Join-Path $importHbRoot $p.ImportSubdir
    $zipPath = Join-Path (Join-Path $RepoRoot "Import") $p.ZipFileName
    $libHbDll = Join-Path $RepoRoot $p.LibRelative
    $null = New-Item -ItemType Directory -Force -Path $extractDir
    Install-HbDllFromHandBrakeGuiZip `
        -ZipUrl ($downloadBaseUrl + $zipName) `
        -ZipDownloadPath $zipPath `
        -ExtractDirectory $extractDir `
        -DestinationHbDllPath $libHbDll
}
