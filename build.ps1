. ./build_common.ps1

function ReplaceTokens($inputFile, $outputFile, $replacements) {
    $fileContent = Get-Content $inputFile

    foreach($key in $($replacements.keys)){
        $fileContent = $fileContent -replace ("%" + $key + "%"), $replacements[$key]
    }

    Set-Content $outputFile $fileContent
}

function CreateIssFile($version, $beta, $arch) {
    $tokens = @{}

    if ($arch -eq "x86") {
        $tokens["x64Directives"] = ""
        
        if ($beta) {
            $appId = "VidCoder-Beta-x86"
        } else {
            $appId = "VidCoder"
        }
    } else {
        $tokens["x64Directives"] = "ArchitecturesAllowed=x64`r`nArchitecturesInstallIn64BitMode=x64"

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
        $tokens["folderName"] = "VidCoder-Beta"
        $tokens["outputBaseFileName"] = "VidCoder-" + $version + "-Beta-" + $arch
        $tokens["appVerName"] = "VidCoder " + $version + " Beta (" + $arch + ")"
    } else {
        $tokens["appName"] = "VidCoder"
        $tokens["folderName"] = "VidCoder"
        $tokens["outputBaseFileName"] = "VidCoder-" + $version + "-" + $arch
        $tokens["appVerName"] = "VidCoder " + $version + " (" + $arch + ")"
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

function CopyBoth($fileName) {
    $dest86 = ".\Installer\Files\x86\"
    $dest64 = ".\Installer\Files\x64\"

    $source86 = ".\VidCoder\bin\x86\Release\"
    $source64 = ".\VidCoder\bin\x64\Release\"

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

function CopyCommon($fileName) {
    $dest86 = ".\Installer\Files\x86"
    $dest64 = ".\Installer\Files\x64"

    copy $fileName $dest86; ExitIfFailed
    copy $fileName $dest64; ExitIfFailed
}

function CopyLanguage($language) {
    $dest86 = ".\Installer\Files\x86\"
    $dest64 = ".\Installer\Files\x64\"

    $source86 = ".\VidCoder\bin\x86\Release\"
    $source64 = ".\VidCoder\bin\x64\Release\"

    copy ($source86 + $language) ($dest86 + $language) -recurse; ExitIfFailed
    copy ($source64 + $language) ($dest64 + $language) -recurse; ExitIfFailed
}

# Master switch for if this branch is beta
$beta = $false

if ($beta) {
    $configuration = "Release-Beta"
} else {
    $configuration = "Release"
}

# Get master version number
$versionShort = Get-Content "version.txt"
$versionLong = $versionShort + ".0"

# Put version numbers into AssemblyInfo.cs files
UpdateAssemblyInfo "VidCoder\Properties\AssemblyInfo.cs" $versionLong
UpdateAssemblyInfo "VidCoderWorker\Properties\AssemblyInfo.cs" $versionLong

# Build VidCoder.sln
& $DevEnv11Exe VidCoder.sln /Rebuild ($configuration + "|x86"); ExitIfFailed
& $DevEnv11Exe VidCoder.sln /Rebuild ($configuration + "|x64"); ExitIfFailed

# Run sgen to create *.XmlSerializers.dll
& ($NetToolsFolder + "\sgen.exe") /f /a:"Lib\x86\HandBrakeInterop.dll"; ExitIfFailed
& ($NetToolsFolder + "\x64\sgen.exe") /f /a:"Lib\x64\HandBrakeInterop.dll"; ExitIfFailed
& ($NetToolsFolder + "\sgen.exe") /f /a:"VidCoder\bin\x86\Release\VidCoder.exe"; ExitIfFailed
& ($NetToolsFolder + "\x64\sgen.exe") /f /a:"VidCoder\bin\x64\Release\VidCoder.exe"; ExitIfFailed


# Copy install files to staging folder
$dest86 = ".\Installer\Files\x86"
$dest64 = ".\Installer\Files\x64"

ClearFolder $dest86; ExitIfFailed
ClearFolder $dest64; ExitIfFailed

$source86 = ".\VidCoder\bin\x86\Release\"
$source64 = ".\VidCoder\bin\x64\Release\"

# Architecture-specific app files
CopyBoth "VidCoder.exe"
CopyBoth "VidCoder.pdb"
CopyBoth "VidCoder.exe.config"
CopyBoth "VidCoderWorker.exe"
CopyBoth "VidCoderWorker.exe.config"
CopyBoth "VidCoderWorker.pdb"
CopyBoth "VidCoder.XmlSerializers.dll"
CopyBoth "Omu.ValueInjecter.dll"

# Architecture-specific files from Lib folder
CopyLibBoth "hb.dll"
CopyLibBoth "System.Data.SQLite.dll"
CopyLibBoth "HandBrakeInterop.dll"
CopyLibBoth "HandBrakeInterop.pdb"
CopyLibBoth "HandBrakeInterop.XmlSerializers.dll"

# Common files
CopyCommon ".\Lib\Ookii.Dialogs.Wpf.dll"
CopyCommon ".\Lib\Ookii.Dialogs.Wpf.pdb"
CopyCommon ".\Lib\Microsoft.Practices.Unity.dll"
CopyCommon ".\Lib\Hardcodet.Wpf.TaskbarNotification.dll"
CopyCommon ".\Lib\GalaSoft.MvvmLight.WPF4.dll"
CopyCommon ".\VidCoder\BuiltInPresets.xml"
CopyCommon ".\VidCoder\Encode_Complete.wav"
CopyCommon ".\License.txt"

# Languages
CopyLanguage "hu"
CopyLanguage "es"
CopyLanguage "eu"
CopyLanguage "pt"
CopyLanguage "fr"
CopyLanguage "de"


# Create portable installer

if ($beta) {
    $betaNameSection = "-Beta"
} else {
    $betaNameSection = ""
}

$portableExeWithoutExtension86 = ".\Installer\BuiltInstallers\VidCoder-$versionShort$betaNameSection-x86-Portable"
$portableExeWithoutExtension64 = ".\Installer\BuiltInstallers\VidCoder-$versionShort$betaNameSection-x64-Portable"

DeleteFileIfExists ($portableExeWithoutExtension86 + ".exe")
DeleteFileIfExists ($portableExeWithoutExtension64 + ".exe")

$winRarExe = "c:\Program Files\WinRar\WinRAR.exe"

& $winRarExe a -sfx -z".\Installer\VidCoderRar.conf" -iicon".\VidCoder\VidCoder_icon.ico" -r -ep1 $portableExeWithoutExtension86 .\Installer\Files\x86\** | Out-Null
ExitIfFailed

& $winRarExe a -sfx -z".\Installer\VidCoderRar.conf" -iicon".\VidCoder\VidCoder_icon.ico" -r -ep1 $portableExeWithoutExtension64 .\Installer\Files\x64\** | Out-Null
ExitIfFailed

#$archiveFolder = ".\Installer\Archive\"
#ClearFolder $archiveFolder
#$archive86 = $archiveFolder + "VidCoder-x86.7z"
#$archive64 = $archiveFolder + "VidCoder-x64.7z"
#.\Lib\7z\7za.exe a $archive86 .\Installer\Files\x86\**; ExitIfFailed
#.\Lib\7z\7za.exe a $archive64 .\Installer\Files\x64\**; ExitIfFailed
#cmd /c copy /b .\Lib\7z\7zS.sfx + .\Installer\PortableInstallConfig.txt + $archive86 .\Installer\BuiltInstallers\VidCoder-$versionShort$betaNameSection-x86-Portable.exe; ExitIfFailed
#cmd /c copy /b .\Lib\7z\7zS.sfx + .\Installer\PortableInstallConfig.txt + $archive64 .\Installer\BuiltInstallers\VidCoder-$versionShort$betaNameSection-x64-Portable.exe; ExitIfFailed

# Update latest.xml files with version
if ($beta)
{
    $latestFile = "Installer\latest-beta2.xml"
}
else
{
    $latestFile = "Installer\latest.xml"
}

$fileContent = Get-Content $latestFile
$fileContent = $fileContent -replace "<Latest>[\d.]+</Latest>", ("<Latest>" + $versionShort + "</Latest>")
$fileContent = $fileContent -replace "(VidCoder-)[\d.]+((?:-Beta)?-x\d{2})", ("`${1}" + $versionShort + "`${2}")
Set-Content $latestFile $fileContent

# Create x86/x64 .iss files in the correct configuration
CreateIssFile $versionShort $beta "x86"
CreateIssFile $versionShort $beta "x64"

# Build the installers
& $InnoSetupExe Installer\VidCoder-x86-gen.iss; ExitIfFailed
& $InnoSetupExe Installer\VidCoder-x64-gen.iss; ExitIfFailed


WriteSuccess

Write-Host
Pause