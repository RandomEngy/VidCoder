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

#$wc = New-Object System.Net.WebClient
#$wc.Credentials = New-Object System.Net.NetworkCredential("username","password")
#$nightlyPageContent = $wc.DownloadString("https://handbrake.fr/nightly.php")

$nightlyPageResponse = Invoke-WebRequest -Uri "https://github.com/HandBrake/handbrake-snapshots/releases/tag/win" -UseBasicParsing
$nightlyPageContent = $nightlyPageResponse.Content

$nightlyPageContent -match "/HandBrake[^""]+x86_64-Win_GUI.zip" | Out-Null
$remoteFile = $matches[0]
$url = "https://github.com/" + $remoteFile

if (Test-Path .\Import\Hb) {
    Remove-Item .\Import\Hb\* -recurse
}

New-Item -ItemType Directory -Force -Path "Import\Hb" | Out-Null

Add-Type -assembly "system.io.compression.filesystem"

Invoke-WebRequest -Uri $url -OutFile ("Import\hb.zip") -UseBasicParsing

[io.compression.zipfile]::ExtractToDirectory("Import\hb.zip", "Import\Hb\")

Copy-Item "Import\Hb\HandBrake\hb.dll" "Lib\hb.dll"