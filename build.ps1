. ./build_common.ps1

function UpdateIssFile($fileName, $version)
{
    $newString = "`${1}" + $version + "`${2}"
    
    $fileContent = Get-Content $fileName
    $fileContent = $fileContent -replace "(VidCoder )[\d.]+( \(x\d{2}\))", $newString
    $fileContent = $fileContent -replace "(VidCoder-)[\d.]+(-x\d{2})", $newString
    Set-Content $fileName $fileContent
}

& $DevEnvExe VidCoder.sln /Rebuild "Release|x86"
& $DevEnvExe VidCoder.sln /Rebuild "Release|x64"

& ($NetToolsFolder + "\sgen.exe") /f /a:"Lib\x86\HandBrakeInterop.dll"
& ($NetToolsFolder + "\x64\sgen.exe") /f /a:"Lib\x64\HandBrakeInterop.dll"
& ($NetToolsFolder + "\sgen.exe") /f /a:"VidCoder\bin\x86\Release\VidCoder.exe"
& ($NetToolsFolder + "\x64\sgen.exe") /f /a:"VidCoder\bin\x64\Release\VidCoder.exe"

$fileVersion = (Get-Command VidCoder\bin\x64\Release\VidCoder.exe).FileVersionInfo.FileVersion
$fileVersion = $fileVersion.Substring(0, $fileVersion.LastIndexOf("."))

UpdateIssFile "Installer\VidCoder-x86.iss" $fileVersion
UpdateIssFile "Installer\VidCoder-x64.iss" $fileVersion

& $InnoSetupExe Installer\VidCoder-x86.iss
& $InnoSetupExe Installer\VidCoder-x64.iss

Write-Host
powershell.exe -noexit 