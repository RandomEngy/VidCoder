[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True)]
  [string]$versionShort,
  [switch]$beta,
  [switch]$debugBuild = $false
)

. ./build_common.ps1

function Publish($folderNameSuffix, $publishProfileName, $version4part, $productName) {
    $mainPublishFolderPath = ".\VidCoder\bin\publish-$folderNameSuffix"
    if (Test-Path -Path $mainPublishFolderPath) {
        Get-ChildItem -Path $mainPublishFolderPath -Include * -File -Recurse | foreach { $_.Delete()}
    }

    $workerPublishFolderPath = ".\VidCoderWorker\bin\publish-$folderNameSuffix"
    if (Test-Path -Path $workerPublishFolderPath) {
        Get-ChildItem -Path $workerPublishFolderPath -Include * -File -Recurse | foreach { $_.Delete()}
    }

    # Publish to VidCoder\bin\publish
    $publishProjects = @('VidCoder','VidCoderWorker')

    foreach ($publishProject in $publishProjects)
    {
        & dotnet publish .\$publishProject\$publishProject.csproj /p:PublishProfile=$publishProfileName /p:Version=$version4part "/p:Product=$productName" -c $configuration
    }

    # Copy some extra files for the installer
    $extraFiles = @(
        ".\VidCoder\Icons\File\VidCoderPreset.ico",
        ".\VidCoder\Icons\File\VidCoderQueue.ico")

    foreach ($extraFile in $extraFiles) {
        copy $extraFile $mainPublishFolderPath; ExitIfFailed
    }
}


function SignExe($filePath) {
    & signtool sign /a /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $filePath
}

if ($debugBuild) {
    $buildFlavor = "Debug"
} else {
    $buildFlavor = "Release"
}

$branch = &git rev-parse --abbrev-ref HEAD
if ($beta -and ($branch -eq "master")) {
    ExitWithError "Current branch is master but build calls for beta"
}

if (!$beta -and ($branch -eq "beta")) {
    ExitWithError "Current branch is beta but build calls for stable"
}

if ($beta) {
    $configuration = $buildFlavor + "-Beta"
    $productName = "VidCoder Beta"
} else {
    $configuration = $buildFlavor
    $productName = "VidCoder"
}

# Get master version number
$version4Part = $versionShort + ".0.0"

# Publish the files
Write-Host "Publishing installer..."
Publish "installer" "InstallerProfile" $version4part $productName

Write-Host "Publishing portable..."
Publish "portable" "PortableProfile" $version4part $productName

# We need to copy some files from the Worker publish over to the main publish output, because the main publish output doesn't properly set the Worker to self-contained mode
copy ".\VidCoderWorker\bin\publish-portable\VidCoderWorker*" ".\VidCoder\bin\publish-portable"

# Create portable installer

Write-Host "Creating portable installer..."
if ($beta) {
    $betaNameSection = "-Beta"
} else {
    $betaNameSection = ""
}

$binaryNameBase = "VidCoder-$versionShort$betaNameSection"

if ($debugBuild) {
    $builtInstallerFolder = "Installer\BuiltInstallers\Test"
} else {
    $builtInstallerFolder = "Installer\BuiltInstallers"
}

New-Item -ItemType Directory -Force -Path ".\$builtInstallerFolder"

$portableExeWithoutExtension = ".\$builtInstallerFolder\$binaryNameBase-Portable"
$portableExeWithExtension = $portableExeWithoutExtension + ".exe"

DeleteFileIfExists $portableExeWithExtension

$winRarExe = "c:\Program Files\WinRar\WinRAR.exe"

& $winRarExe a -sfx -z".\Installer\VidCoderRar.conf" -iicon".\VidCoder\VidCoder_icon.ico" -r -ep1 $portableExeWithoutExtension .\VidCoder\bin\publish-portable\** | Out-Null
ExitIfFailed

SignExe $portableExeWithExtension; ExitIfFailed

# Sign executables in publish-installer

$publishedExes = Get-ChildItem -Path .\VidCoder\bin\publish-installer\ -Filter *.exe
foreach ($exeFile in $publishedExes) {
    SignExe $exeFile.FullName; ExitIfFailed
}

# Create zip file with binaries
$zipFilePath = ".\Installer\BuiltInstallers\$binaryNameBase.zip"
DeleteFileIfExists $zipFilePath

& $winRarExe a -afzip -ep1 -r $zipFilePath .\VidCoder\bin\publish-installer\

# Build Velopack installer
if ($beta) {
    $packId = "VidCoder.Beta"
    $releaseDirSuffix = "Beta"
} else {
    $packId = "VidCoder.Stable"
    $releaseDirSuffix = "Stable"
}

$releaseDir = ".\Installer\Releases-$releaseDirSuffix"

vpk pack `
    -x `
    -y `
    --packId $packId `
    --packTitle "$productName" `
    --packVersion ($versionShort + ".0") `
    --packAuthors RandomEngy `
    --packDir .\VidCoder\bin\publish-installer `
    --mainExe VidCoder.exe `
    --icon .\Installer\VidCoder_Setup.ico `
    --outputDir $releaseDir `
    --splashImage .\Installer\InstallerSplash.png `
    --signParams "/a /fd SHA256 /tr http://timestamp.digicert.com /td SHA256" `
    --framework net8.0.5-x64

ExitIfFailed;
Copy-Item -Path ("$releaseDir\" + $packId + "-win-Setup.exe") -Destination ".\Installer\BuiltInstallers\$binaryNameBase.exe" -Force

WriteSuccess

Write-Host
