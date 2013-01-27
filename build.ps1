. ./build_common.ps1

function UpdateIssFile($fileName, $version, $beta)
{
    $newString = "`${1}" + $version + "`${2}"
    
    if ($beta)
    {
        $versionNameReplacement = "`${1}" + $version + " Beta (`${2})"
        $fileNameReplacement = "`${1}" + $version + "-Beta-`${2}"
    }
    else
    {
        $versionNameReplacement = "`${1}" + $version + " (`${2})"
        $fileNameReplacement = "`${1}" + $version + "-`${2}"
    }

    
    $fileContent = Get-Content $fileName
    $fileContent = $fileContent -replace "(VidCoder )[\d.]+(?: Beta)? \((x\d{2})\)", $versionNameReplacement
    $fileContent = $fileContent -replace "(VidCoder-)[\d.]+(?:-Beta)?-(x\d{2})", $fileNameReplacement
    $fileContent = $fileContent -replace "AppVersion=[\d.]+", ("AppVersion=" + $version)
    Set-Content $fileName $fileContent
}

function UpdateAssemblyInfo($fileName, $version)
{
    $newVersionText = 'AssemblyVersion("' + $version + '")';
    $newFileVersionText = 'AssemblyFileVersion("' + $version + '")';

    $tmpFile = $fileName + ".tmp"

    Get-Content $fileName | 
    %{$_ -replace 'AssemblyVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)', $newVersionText } |
    %{$_ -replace 'AssemblyFileVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)', $newFileVersionText } > $tmpFile

    Move-Item $tmpFile $fileName -force
}

# Master switch for if this branch is beta
$beta = $true

if ($beta)
{
    $configuration = "Release-Beta"
}
else
{
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

# Get the version of the built executable
#$fileVersion = (Get-Command VidCoder\bin\x64\Release\VidCoder.exe).FileVersionInfo.FileVersion
#$fileVersion = $fileVersion.Substring(0, $fileVersion.LastIndexOf("."))

# Update installer files with version
UpdateIssFile "Installer\VidCoder-x86.iss" $versionShort $beta
UpdateIssFile "Installer\VidCoder-x64.iss" $versionShort $beta

if ($beta)
{
    $latestFile = "Installer\latest-beta.xml"
}
else
{
    $latestFile = "Installer\latest.xml"
}

$fileContent = Get-Content $latestFile
$fileContent = $fileContent -replace "<Latest>[\d.]+</Latest>", ("<Latest>" + $versionShort + "</Latest>")
$fileContent = $fileContent -replace "(VidCoder-)[\d.]+((?:-Beta)?-x\d{2})", ("`${1}" + $versionShort + "`${2}")
Set-Content $latestFile $fileContent

# Build installers
& $InnoSetupExe Installer\VidCoder-x86.iss; ExitIfFailed
& $InnoSetupExe Installer\VidCoder-x64.iss; ExitIfFailed


WriteSuccess

Write-Host
powershell.exe -noexit -nologo