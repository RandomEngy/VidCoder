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

function CreateIssFile($version, $beta, $debugBuild, $arch) {
    $tokens = @{}

    if ($arch -eq "x86") {
        $tokens["x64Directives"] = ""
        $tokens["x86UninstallLine"] = ""
        
        if ($beta) {
            $appId = "VidCoder-Beta-x86"
        } else {
            $appId = "VidCoder"
        }
    } else {
        $tokens["x64Directives"] = "ArchitecturesAllowed=x64`r`nArchitecturesInstallIn64BitMode=x64"
        $tokens["x86UninstallLine"] = "UninstallX86Version();"

        if ($beta) {
            $appId = "VidCoder-Beta-x64"
        } else {
            $appId = "VidCoder-x64"
        }
    }


    $tokens["arch"] = $arch
    $tokens["version"] = $version
    $tokens["appId"] = $appId
    if ($beta) {
        $tokens["appName"] = "VidCoder Beta"
        $tokens["appNameNoSpace"] = "VidCoderBeta"
        $tokens["folderName"] = "VidCoder-Beta"
        $tokens["outputBaseFileName"] = "VidCoder-" + $version + "-Beta-" + $arch
        $tokens["appVerName"] = "VidCoder " + $version + " Beta (" + $arch + ")"
    } else {
        $tokens["appName"] = "VidCoder"
        $tokens["appNameNoSpace"] = "VidCoder"
        $tokens["folderName"] = "VidCoder"
        $tokens["outputBaseFileName"] = "VidCoder-" + $version + "-" + $arch
        $tokens["appVerName"] = "VidCoder " + $version + " (" + $arch + ")"
    }

    if ($debugBuild) {
        $tokens["outputDirectory"] = "BuiltInstallers\Test"
    } else {
        $tokens["outputDirectory"] = "BuiltInstallers"
    }

    ReplaceTokens "Installer\VidCoder.iss.txt" ("Installer\VidCoder-" + $arch + "-gen.iss") $tokens
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

function CopyFromOutput($fileName, $buildFlavor) {
    $dest86 = ".\Installer\Files\x86\"
    $dest64 = ".\Installer\Files\x64\"

    $source86 = ".\VidCoder\bin\x86\$buildFlavor\"
    $source64 = ".\VidCoder\bin\x64\$buildFlavor\"

    copy ($source86 + $fileName) ($dest86 + $fileName); ExitIfFailed
    copy ($source64 + $fileName) ($dest64 + $fileName); ExitIfFailed
}

function CopyFromOutputArchSpecific($fileName, $buildFlavor) {
    $dest86 = ".\Installer\Files\x86\"
    $dest64 = ".\Installer\Files\x64\"

    $source86 = ".\VidCoder\bin\x86\$buildFlavor\x86\"
    $source64 = ".\VidCoder\bin\x64\$buildFlavor\x64\"

    copy ($source86 + $fileName) ($dest86 + $fileName); ExitIfFailed
    copy ($source64 + $fileName) ($dest64 + $fileName); ExitIfFailed
}

function CopyLibBoth($fileName) {
    $dest86 = ".\Installer\Files\x86\"
    $dest64 = ".\Installer\Files\x64\"

    $source86 = ".\Lib\x86\"
    $source64 = ".\Lib\x64\"

    copy ($source86 + $fileName) ($dest86 + $fileName); ExitIfFailed
    copy ($source64 + $fileName) ($dest64 + $fileName); ExitIfFailed
}

function CopyGeneral($fileName) {
    $dest86 = ".\Installer\Files\x86"
    $dest64 = ".\Installer\Files\x64"

    copy $fileName $dest86; ExitIfFailed
    copy $fileName $dest64; ExitIfFailed
}

function CopyLanguage($language, $buildFlavor) {
    $dest86 = ".\Installer\Files\x86\"
    $dest64 = ".\Installer\Files\x64\"

    $source86 = ".\VidCoder\bin\x86\$buildFlavor\"
    $source64 = ".\VidCoder\bin\x64\$buildFlavor\"

    copy ($source86 + $language) ($dest86 + $language) -recurse; ExitIfFailed
    copy ($source64 + $language) ($dest64 + $language) -recurse; ExitIfFailed
}

function UpdateLatestJson($latestFile, $versionShort, $versionTag, $installerFile) {
    $latestJsonObject = Get-Content -Raw -Path $latestFile | ConvertFrom-Json

    $latestJsonObject.LatestVersion = $versionShort
    $latestJsonObject.DownloadUrl = "https://github.com/RandomEngy/VidCoder/releases/download/$versionTag/$installerFile"
    $latestJsonObject.ChangelogUrl = "https://github.com/RandomEngy/VidCoder/releases/tag/$versionTag"

    $latestJsonObject | ConvertTo-Json | Out-File $latestFile
}

# Master switch for if this branch is beta
$beta = $false

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
$versionLong = $versionShort + ".0"

# Put version numbers into AssemblyInfo.cs files
UpdateAssemblyInfo "VidCoder\Properties\AssemblyInfo.cs" $versionLong
UpdateAssemblyInfo "VidCoderWorker\Properties\AssemblyInfo.cs" $versionLong

# Build VidCoder.sln
& $DevEnvExe VidCoder.sln /Rebuild ($configuration + "|x86"); ExitIfFailed
& $DevEnvExe VidCoder.sln /Rebuild ($configuration + "|x64"); ExitIfFailed

# Run sgen to create *.XmlSerializers.dll
& ($NetToolsFolder + "\sgen.exe") /f /a:"VidCoder\bin\x86\$buildFlavor\VidCoderCommon.dll"; ExitIfFailed
& ($NetToolsFolder + "\x64\sgen.exe") /f /a:"VidCoder\bin\x64\$buildFlavor\VidCoderCommon.dll"; ExitIfFailed


# Copy install files to staging folder
$dest86 = ".\Installer\Files\x86"
$dest64 = ".\Installer\Files\x64"

ClearFolder $dest86; ExitIfFailed
ClearFolder $dest64; ExitIfFailed

$source86 = ".\VidCoder\bin\x86\$buildFlavor\"
$source64 = ".\VidCoder\bin\x64\$buildFlavor\"

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
    "Hardcodet.Wpf.TaskbarNotification.dll",
    "Newtonsoft.Json.dll",
    "FastMember.dll",
    "Microsoft.Practices.Unity.dll",
    "ReactiveUI.dll",
    "Splat.dll",
    "System.Data.SQLite.dll",
    "System.Reactive.Core.dll",
    "System.Reactive.Interfaces.dll",
    "System.Reactive.Linq.dll",
    "System.Reactive.PlatformServices.dll",
    "System.Reactive.Windows.Threading.dll",
    "Ude.dll",
    "Xceed.Wpf.Toolkit.dll")

foreach ($outputDirectoryFile in $outputDirectoryFiles) {
    CopyFromOutput $outputDirectoryFile $buildFlavor
}

CopyFromOutputArchSpecific "SQLite.Interop.dll" $buildFlavor

# General files
$generalFiles = @(
    ".\Lib\HandBrake.ApplicationServices.dll",
    ".\Lib\HandBrake.ApplicationServices.pdb",
    ".\Lib\Ookii.Dialogs.Wpf.dll",
    ".\Lib\Ookii.Dialogs.Wpf.pdb",
    ".\VidCoder\BuiltInPresets.json",
    ".\VidCoder\Encode_Complete.wav",
    ".\VidCoder\Icons\File\VidCoderPreset.ico",
    ".\VidCoder\Icons\File\VidCoderQueue.ico",
    ".\License.txt")

foreach ($generalFile in $generalFiles) {
    CopyGeneral $generalFile
}

# Architecture-specific files from Lib folder
CopyLibBoth "hb.dll"

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
    "bs")

foreach ($language in $languages) {
    CopyLanguage $language $buildFlavor
}

# fonts folder for subtitles
copy ".\Lib\fonts" ".\Installer\Files\x86" -Recurse
copy ".\Lib\fonts" ".\Installer\Files\x64" -Recurse


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

$portableExeWithoutExtension86 = ".\$builtInstallerFolder\VidCoder-$versionShort$betaNameSection-x86-Portable"
$portableExeWithoutExtension64 = ".\$builtInstallerFolder\VidCoder-$versionShort$betaNameSection-x64-Portable"

DeleteFileIfExists ($portableExeWithoutExtension86 + ".exe")
DeleteFileIfExists ($portableExeWithoutExtension64 + ".exe")

$winRarExe = "c:\Program Files\WinRar\WinRAR.exe"

& $winRarExe a -sfx -z".\Installer\VidCoderRar.conf" -iicon".\VidCoder\VidCoder_icon.ico" -r -ep1 $portableExeWithoutExtension86 .\Installer\Files\x86\** | Out-Null
ExitIfFailed

& $winRarExe a -sfx -z".\Installer\VidCoderRar.conf" -iicon".\VidCoder\VidCoder_icon.ico" -r -ep1 $portableExeWithoutExtension64 .\Installer\Files\x64\** | Out-Null
ExitIfFailed

$latestFileDirectory = "Installer\"
if ($debugBuild) {
    $latestFileDirectory += "Test\"
}

$latestFileBase = $latestFileDirectory + "latest"
if ($beta) {
    $latestFileBase += "-beta"
}

$latestFile86 = $latestFileBase + "-x86.json"
$latestFile64 = $latestFileBase + ".json"

# Update latest.xml files with version
if ($beta)
{
    $versionTag = "v$versionShort-beta"
    $installerFile86 = "VidCoder-$versionShort-Beta-x86.exe"
    $installerFile64 = "VidCoder-$versionShort-Beta-x64.exe"
}
else
{
    $versionTag = "v$versionShort"
    $installerFile86 = "VidCoder-$versionShort-x86.exe"
    $installerFile64 = "VidCoder-$versionShort-x64.exe"
}

UpdateLatestJson $latestFile86 $versionShort $versionTag $installerFile86
UpdateLatestJson $latestFile64 $versionShort $versionTag $installerFile64

# Create x86/x64 .iss files in the correct configuration
CreateIssFile $versionShort $beta $debugBuild "x86"
CreateIssFile $versionShort $beta $debugBuild "x64"

# Build the installers
& $InnoSetupExe Installer\VidCoder-x86-gen.iss; ExitIfFailed
& $InnoSetupExe Installer\VidCoder-x64-gen.iss; ExitIfFailed


WriteSuccess

Write-Host
