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
    & dotnet publish .\VidCoder.sln "/p:PublishProfile=$publishProfileName;Version=$version4part;Product=$productName" -c $configuration

    # Copy some extra files for the installer
    $extraFiles = @(
        ".\VidCoder\Icons\File\VidCoderPreset.ico",
        ".\VidCoder\Icons\File\VidCoderQueue.ico")

    foreach ($extraFile in $extraFiles) {
        copy $extraFile $mainPublishFolderPath; ExitIfFailed
    }
}

function SignExe($installerPath) {
    & signtool sign /f D:\certs\ComodoIndividualCertv2.pfx /p $p /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $installerPath
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
Publish "installer" "InstallerProfile" $version4part $productName
Publish "portable" "PortableProfile" $version4part $productName

# We need to copy some files from the Worker publish over to the main publish output, because the main publish output doesn't properly set the Worker to self-contained mode
copy ".\VidCoderWorker\bin\publish-portable\VidCoderWorker*" ".\VidCoder\bin\publish-portable"

# Create portable installer

if ($beta) {
    $betaNameSection = "-Beta"
} else {
    $betaNameSection = ""
}

if ($debugBuild) {
    $builtInstallerFolder = "Installer\BuiltInstallers\Test"
} else {
    $builtInstallerFolder = "Installer\BuiltInstallers"
}

New-Item -ItemType Directory -Force -Path ".\$builtInstallerFolder"

$portableExeWithoutExtension = ".\$builtInstallerFolder\VidCoder-$versionShort$betaNameSection-Portable"
$portableExeWithExtension = $portableExeWithoutExtension + ".exe"

DeleteFileIfExists $portableExeWithExtension

$winRarExe = "c:\Program Files\WinRar\WinRAR.exe"

#& $winRarExe a -sfx -z".\Installer\VidCoderRar.conf" -iicon".\VidCoder\VidCoder_icon.ico" -r -ep1 $portableExeWithoutExtension .\VidCoder\bin\publish-portable\** | Out-Null
#ExitIfFailed

#SignExe $portableExeWithExtension; ExitIfFailed

# Build Squirrel installer
Set-Alias Squirrel ($env:USERPROFILE + "\.nuget\packages\clowd.squirrel\2.7.34-pre\tools\Squirrel.exe")
if ($beta) {
    $packName = "VidCoder-Beta"
    $releaseDirSuffix = "Beta"
} else {
    $packName = "VidCoder"
    $releaseDirSuffix = "Stable"
}

Squirrel pack `
    --packName $packName `
    --packVersion $versionShort `
    --packAuthors RandomEngy `
    --packDirectory .\VidCoder\bin\publish-installer `
    --setupIcon .\Installer\VidCoder_Setup.ico `
    --releaseDir .\Installer\Releases-$releaseDirSuffix `
    --splashImage .\Installer\InstallerSplash.png `
    --signParams "/f D:\certs\ComodoIndividualCertv2.pfx /p $p /fd SHA256 /tr http://timestamp.digicert.com /td SHA256" `
    --framework net6-x64

WriteSuccess

Write-Host
