[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True)]
  [string]$versionShort,
  [switch]$beta,
  [switch]$debugBuild = $false
)

. ./build_common.ps1

function ReplaceTokens($inputFile, $outputFile, $replacements) {
    $fileContent = Get-Content $inputFile

    foreach($key in $($replacements.keys)){
        $fileContent = $fileContent -replace ("%" + $key + "%"), $replacements[$key]
    }

    Set-Content $outputFile $fileContent
}

function CreateIssFile($version, $beta, $debugBuild) {
    $tokens = @{}

    if ($beta) {
        $appId = "VidCoder-Beta-x64"
    } else {
        $appId = "VidCoder-x64"
    }

    $tokens["version"] = $version
    $tokens["appId"] = $appId
    if ($beta) {
        $tokens["appName"] = "VidCoder Beta"
        $tokens["appNameNoSpace"] = "VidCoderBeta"
        $tokens["folderName"] = "VidCoder-Beta"
        $tokens["outputBaseFileName"] = "VidCoder-" + $version + "-Beta"
        $tokens["appVerName"] = "VidCoder " + $version + " Beta (Installer)"
        $tokens["x86AppId"] = "VidCoder-Beta-x86"
    } else {
        $tokens["appName"] = "VidCoder"
        $tokens["appNameNoSpace"] = "VidCoder"
        $tokens["folderName"] = "VidCoder"
        $tokens["outputBaseFileName"] = "VidCoder-" + $version
        $tokens["appVerName"] = "VidCoder " + $version + " (Installer)"
        $tokens["x86AppId"] = "VidCoder"
    }

    if ($debugBuild) {
        $tokens["outputDirectory"] = "BuiltInstallers\Test"
    } else {
        $tokens["outputDirectory"] = "BuiltInstallers"
    }

    ReplaceTokens "Installer\VidCoder.iss.txt" "Installer\VidCoder-gen.iss" $tokens
}

function CopyExtra($fileName) {
    $dest = ".\VidCoder\bin\publish"
    copy $fileName $dest; ExitIfFailed
}

function CreateLatestJson($outputFilePath, $versionShort, $versionTag, $installerFile) {
    $latestTemplateFile = "Installer\latest-template.json"

    $tokens = @{
        versionShort = $versionShort;
        versionTag = $versionTag;
        installerFile = $installerFile
    }

    ReplaceTokens "Installer\latest-template.json" $outputFilePath $tokens
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
} else {
    $configuration = $buildFlavor
}

# Get master version number
$version4Part = $versionShort + ".0.0"

Get-ChildItem -Path ".\VidCoder\bin\publish" -Include * -File -Recurse | foreach { $_.Delete()}

# Publish to VidCoder\bin\publish
& dotnet publish .\VidCoder.sln "/p:PublishProfile=FolderProfile;Version=$version4part" -c $configuration

# Copy some extra files for the installer
$extraFiles = @(
    ".\VidCoder\Icons\File\VidCoderPreset.ico",
    ".\VidCoder\Icons\File\VidCoderQueue.ico")

foreach ($extraFile in $extraFiles) {
    CopyExtra $extraFile
}

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

DeleteFileIfExists ($portableExeWithoutExtension + ".exe")

$winRarExe = "c:\Program Files\WinRar\WinRAR.exe"

& $winRarExe a -sfx -z".\Installer\VidCoderRar.conf" -iicon".\VidCoder\VidCoder_icon.ico" -r -ep1 $portableExeWithoutExtension .\VidCoder\bin\publish\** | Out-Null
ExitIfFailed

$latestFileDirectory = "Installer\"
if ($debugBuild) {
    $latestFileDirectory += "Test\"
}

$latestFileBase = $latestFileDirectory + "latest"
if ($beta) {
    $latestFileBase += "-beta"
}

$latestFile = $latestFileBase + ".json"

# Update latest.json file with version
if ($beta)
{
    $versionTag = "v$versionShort-beta"
    $installerFile = "VidCoder-$versionShort-Beta.exe"
}
else
{
    $versionTag = "v$versionShort"
    $installerFile = "VidCoder-$versionShort.exe"
}

CreateLatestJson $latestFile $versionShort $versionTag $installerFile

# Create .iss files in the correct configuration
CreateIssFile $versionShort $beta $debugBuild

# Build the installers
& $InnoSetupExe Installer\VidCoder-gen.iss; ExitIfFailed


WriteSuccess

Write-Host
