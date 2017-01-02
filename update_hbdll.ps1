$nightlyPageResponse = Invoke-WebRequest -Uri "https://handbrake.fr/nightly.php"
$nightlyPageContent = $nightlyPageResponse.Content

$nightlyPageContent -match "http://[^""]+LibHB[^""]+i686.zip" | Out-Null
$x86Url = $matches[0]

$nightlyPageContent -match "http://[^""]+LibHB[^""]+x86_64.zip" | Out-Null
$x64Url = $matches[0]

if (Test-Path .\Import\Hb) {
    Remove-Item .\Import\Hb\* -recurse
}

New-Item -ItemType Directory -Force -Path "Import\Hb" | Out-Null

Add-Type -assembly "system.io.compression.filesystem"

function DownloadHbDll($url, $arch) {
    Invoke-WebRequest -Uri $url -OutFile ("Import\Hb\" + $arch + ".zip")

    [io.compression.zipfile]::ExtractToDirectory("Import\Hb\" + $arch + ".zip", "Import\Hb\" + $arch)

    Copy-Item ("Import\Hb\" + $arch + "\hb.dll") ("Lib\" + $arch + "\hb.dll")
}

DownloadHbDll $x86Url "x86"
DownloadHbDll $x64Url "x64"
