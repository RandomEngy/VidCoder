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

$nightlyPageResponse = Invoke-WebRequest -Uri "https://handbrake.fr/nightly.php"
$nightlyPageContent = $nightlyPageResponse.Content

$nightlyPageContent -match "https://[^""]+LibHB[^""]+x86_64.zip" | Out-Null
$url = $matches[0]

if (Test-Path .\Import\Hb) {
    Remove-Item .\Import\Hb\* -recurse
}

New-Item -ItemType Directory -Force -Path "Import\Hb" | Out-Null

Add-Type -assembly "system.io.compression.filesystem"

Invoke-WebRequest -Uri $url -OutFile ("Import\hb.zip")

[io.compression.zipfile]::ExtractToDirectory("Import\hb.zip", "Import\Hb\")

Copy-Item "Import\Hb\hb.dll" "Lib\hb.dll"