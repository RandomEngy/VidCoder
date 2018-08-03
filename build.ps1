[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True)]
  [string]$versionShort,
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

    ReplaceTokens "Installer\VidCoder.iss.txt" ("Installer\VidCoder-gen.iss") $tokens
}

function UpdateAssemblyInfo($fileName, $version) {
    $newVersionText = 'AssemblyVersion("' + $version + '")';
    $newFileVersionText = 'AssemblyFileVersion("' + $version + '")';

    $tmpFile = $fileName + ".tmp"

    Get-Content $fileName | 
    %{$_ -replace 'AssemblyVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)', $newVersionText } |
    %{$_ -replace 'AssemblyFileVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)', $newFileVersionText } > $tmpFile

    Move-Item $tmpFile $fileName -force
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

function UpdateLatestJson($latestFile, $versionShort, $versionTag, $installerFile) {
    $latestJsonObject = Get-Content -Raw -Path $latestFile | ConvertFrom-Json

    $latestJsonObject.LatestVersion = $versionShort
    $latestJsonObject.DownloadUrl = "https://github.com/RandomEngy/VidCoder/releases/download/$versionTag/$installerFile"
    $latestJsonObject.ChangelogUrl = "https://github.com/RandomEngy/VidCoder/releases/tag/$versionTag"

    $latestJsonObject | ConvertTo-Json | Out-File $latestFile
}

# Master switch for if this branch is beta
$beta = $true

if ($debugBuild) {
    $buildFlavor = "Debug"
} else {
    $buildFlavor = "Release"
}

if ($beta) {
    $configuration = $buildFlavor + "-Beta"
} else {
    $configuration = $buildFlavor
}

# Get master version number
$version4Part = $versionShort + ".0.0"

# Put version numbers into AssemblyInfo.cs files
UpdateAssemblyInfo "VidCoder\Properties\AssemblyInfo.cs" $version4Part
UpdateAssemblyInfo "VidCoderWorker\Properties\AssemblyInfo.cs" $version4Part

if ($beta) {
    $manifestBaseFileName = "Package-Beta"
} else {
    $manifestBaseFileName = "Package-Stable"
}

UpdateAppxManifest "VidCoderPackage\$manifestBaseFileName.appxmanifest" $version4Part

# Build VidCoder.sln
& $MsBuildExe VidCoder.sln /t:rebuild "/p:Configuration=$configuration;Platform=x64;UapAppxPackageBuildMode=StoreUpload"; ExitIfFailed

# Run sgen to create *.XmlSerializers.dll
& ($NetToolsFolder + "\x64\sgen.exe") /f /a:"VidCoder\bin\$buildFlavor\VidCoderCommon.dll"; ExitIfFailed


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
    "VidCoderCommon.XmlSerializers.dll",
    "VidCoderWorker.exe",
    "VidCoderWorker.exe.config",
    "VidCoderWorker.pdb",
    "Omu.ValueInjecter.dll",
    "VidCoderCLI.exe",
    "VidCoderCLI.pdb",
    "VidCoderWindowlessCLI.exe",
    "VidCoderWindowlessCLI.pdb",
    "Microsoft.Practices.ServiceLocation.dll",
    "Newtonsoft.Json.dll",
    "Microsoft.Practices.Unity.dll",
    "ReactiveUI.dll",
    "Splat.dll",
    "DesktopBridge.Helpers.dll",
    "System.Data.SQLite.dll",
    "System.Reactive.dll",
    "System.Reactive.Core.dll",
    "System.Reactive.Interfaces.dll",
    "System.Reactive.Linq.dll",
    "System.Reactive.PlatformServices.dll",
    "System.Reactive.Windows.Threading.dll",
    "System.Windows.Interactivity.dll",
    "Ude.dll",
    "Xceed.Wpf.Toolkit.dll",
    "Fluent.dll",
    "Fluent.pdb",
    "ControlzEx.dll",
    "ControlzEx.pdb")

foreach ($outputDirectoryFile in $outputDirectoryFiles) {
    CopyFromOutput $outputDirectoryFile $buildFlavor
}

CopyFromOutputArchSpecific "SQLite.Interop.dll" $buildFlavor

# General files
$generalFiles = @(
    ".\Lib\HandBrake.Interop.dll",
    ".\Lib\HandBrake.Interop.pdb",
    ".\Lib\Ookii.Dialogs.Wpf.dll",
    ".\Lib\Ookii.Dialogs.Wpf.pdb",
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
    "id")

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

UpdateLatestJson $latestFile $versionShort $versionTag $installerFile

# Create .iss files in the correct configuration
CreateIssFile $versionShort $beta $debugBuild

# Build the installers
& $InnoSetupExe Installer\VidCoder-gen.iss; ExitIfFailed


WriteSuccess

Write-Host
