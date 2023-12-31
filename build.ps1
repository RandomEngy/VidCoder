[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True)]
  [string]$versionShort,
  [string]$p,
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
    & dotnet publish .\VidCoder.sln /p:PublishProfile=$publishProfileName /p:Version=$version4part "/p:Product=$productName" -c $configuration

    # Copy some extra files for the installer
    $extraFiles = @(
        ".\VidCoder\Icons\File\VidCoderPreset.ico",
        ".\VidCoder\Icons\File\VidCoderQueue.ico")

    foreach ($extraFile in $extraFiles) {
        copy $extraFile $mainPublishFolderPath; ExitIfFailed
    }
}

function SignExe($filePath) {
    & signtool sign /f D:\certs\ComodoIndividualCertv2.pfx /p $p /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $filePath
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

# Build Squirrel installer
Set-Alias Squirrel ($env:USERPROFILE + "\.nuget\packages\clowd.squirrel\2.11.1\tools\Squirrel.exe")
if ($beta) {
    $packId = "VidCoder.Beta"
    $releaseDirSuffix = "Beta"
} else {
    $packId = "VidCoder.Stable"
    $releaseDirSuffix = "Stable"
}

$releaseDir = ".\Installer\Releases-$releaseDirSuffix"

Squirrel pack `
    --packId $packId `
    --packTitle "$productName" `
    --packVersion ($versionShort + ".0") `
    --packAuthors RandomEngy `
    --packDirectory .\VidCoder\bin\publish-installer `
    --icon .\Installer\VidCoder_Setup.ico `
    --releaseDir $releaseDir `
    --splashImage .\Installer\InstallerSplash.png `
    --signParams "/f D:\certs\ComodoIndividualCertv2.pfx /p $p /fd SHA256 /tr http://timestamp.digicert.com /td SHA256" `
    --framework net6.0.2-x64

ExitIfFailed;
Move-Item -Path ("$releaseDir\" + $packId + "Setup.exe") -Destination ".\Installer\BuiltInstallers\$binaryNameBase.exe" -Force

WriteSuccess

Write-Host
