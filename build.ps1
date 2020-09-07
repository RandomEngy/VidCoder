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

function UpdateAppxManifest($fileName, $version)
{
    $newVersionText = 'Version="' + $version + '"';
    $tmpFile = $fileName + ".tmp"

    Get-Content $fileName | 
    %{$_ -replace '(?<=<Identity[^/]+)Version="([\d\.]+)"', $newVersionText } | Out-File -Encoding utf8 $tmpFile

    Move-Item $tmpFile $fileName -force
}

function CopyFromOutput($fileName, $buildFlavor) {
    $dest = ".\Installer\Files\"
    $source = ".\VidCoder\bin\$buildFlavor\"
    copy ($source + $fileName) ($dest + $fileName); ExitIfFailed
}

function CopyFromOutputArchSpecific($fileName, $buildFlavor) {
    $dest = ".\Installer\Files\"
    $source64 = ".\VidCoder\bin\$buildFlavor\x64\"
    copy ($source64 + $fileName) ($dest + $fileName); ExitIfFailed
}

function CopyLib($fileName) {
    $dest = ".\Installer\Files\"
    $source = ".\Lib\"
    copy ($source + $fileName) ($dest + $fileName); ExitIfFailed
}

function CopyGeneral($fileName) {
    $dest = ".\Installer\Files"
    copy $fileName $dest; ExitIfFailed
}

function CopyLanguage($language, $buildFlavor) {
    $dest = ".\Installer\Files\"
    $source = ".\VidCoder\bin\$buildFlavor\"
    copy ($source + $language) ($dest + $language) -recurse; ExitIfFailed
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

if ($beta) {
    $manifestBaseFileName = "Package-Beta"
} else {
    $manifestBaseFileName = "Package-Stable"
}

UpdateAppxManifest "VidCoderPackage\$manifestBaseFileName.appxmanifest" $version4Part

# Build VidCoder.sln
& $MsBuildExe VidCoder.sln /t:rebuild "/p:Configuration=$configuration;Platform=x64;UapAppxPackageBuildMode=StoreUpload;Version=$version4Part"; ExitIfFailed


# Copy install files to staging folder
$dest = ".\Installer\Files"

ClearFolder $dest; ExitIfFailed

$source = ".\VidCoder\bin\$buildFlavor\"

# Files from the main output directory (some architecture-specific)
$outputDirectoryFiles = @(
    "VidCoder.exe",
    "VidCoder.pdb",
    "VidCoder.exe.config",
    "VidCoderCommon.dll",
    "VidCoderCommon.pdb",
    "VidCoderWorker.exe",
    "VidCoderWorker.exe.config",
    "VidCoderWorker.pdb",
    "VidCoderCLI.exe",
    "VidCoderCLI.pdb",
    "VidCoderWindowlessCLI.exe",
    "VidCoderWindowlessCLI.pdb",
    "ColorPickerWPF.dll",
    "ControlzEx.dll",
    "DesktopBridge.Helpers.dll",
    "DryIoc.dll",
    "DynamicData.dll",
    "Fluent.dll",
    "Microsoft.AnyContainer.dll",
    "Microsoft.AnyContainer.DryIoc.dll",
    "Microsoft.WindowsAPICodePack.dll",
    "Microsoft.WindowsAPICodePack.Shell.dll",
    "Microsoft.WindowsAPICodePack.ShellExtensions.dll",
    "Microsoft.Xaml.Behaviors.dll",
    "Newtonsoft.Json.dll",
    "Omu.ValueInjecter.dll",
    "Ookii.Dialogs.Wpf.dll",
    "Ookii.Dialogs.Wpf.pdb",
    "PipeMethodCalls.dll",
    "ReactiveUI.dll",
    "ReactiveUI.WPF.dll",
    "Splat.dll",
    "System.Data.SQLite.dll",
    "System.Net.Http.dll",
    "System.Reactive.dll",
    "System.Reactive.Core.dll",
    "System.Reactive.Experimental.dll",
    "System.Reactive.Interfaces.dll",
    "System.Reactive.Linq.dll",
    "System.Reactive.PlatformServices.dll",
    "System.Reactive.Providers.dll",
    "System.Reactive.Runtime.Remoting.dll",
    "System.Reactive.Windows.Forms.dll",
    "System.Reactive.Windows.Threading.dll",
    "System.Runtime.WindowsRuntime.dll",
    "System.ValueTuple.dll",
    "Ude.dll",
    "WriteableBitmapEx.Wpf.dll")

foreach ($outputDirectoryFile in $outputDirectoryFiles) {
    CopyFromOutput $outputDirectoryFile $buildFlavor
}

CopyFromOutputArchSpecific "SQLite.Interop.dll" $buildFlavor

# General files
$generalFiles = @(
    ".\Lib\HandBrake.Interop.dll",
    ".\Lib\HandBrake.Interop.pdb",
    ".\VidCoder\Encode_Complete.wav",
    ".\VidCoder\Icons\File\VidCoderPreset.ico",
    ".\VidCoder\Icons\File\VidCoderQueue.ico",
    ".\License.txt",
    ".\ThirdPartyLicenses.txt")

foreach ($generalFile in $generalFiles) {
    CopyGeneral $generalFile
}

# Files from Lib folder
CopyLib "hb.dll"

# Languages
$languages = @(
    "hu",
    "es",
    "eu",
    "pt",
    "pt-BR",
    "fr",
    "de",
    "zh",
    "zh-Hant",
    "it",
    "cs",
    "ja",
    "pl",
    "ru",
    "nl",
    "ka",
    "tr",
    "ko",
    "bs",
    "id",
    "ar")

foreach ($language in $languages) {
    CopyLanguage $language $buildFlavor
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

& $winRarExe a -sfx -z".\Installer\VidCoderRar.conf" -iicon".\VidCoder\VidCoder_icon.ico" -r -ep1 $portableExeWithoutExtension .\Installer\Files\** | Out-Null
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
