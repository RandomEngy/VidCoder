# Common settings/functions for scripts

function ExitIfFailed()
{
    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "An error occurred. Stopping build." -foregroundcolor "red"
        exit
    }
}

function WriteSuccess()
{
    Write-Host "Build succeeded." -foregroundcolor "green"
}

function ClearFolder($folderName)
{
    DeleteFolderIfExists $folderName
    New-Item $folderName -type directory
}

function DeleteFolderIfExists($folderName)
{
    if (Test-Path $folderName) { Remove-Item -r $folderName }
}

function DeleteFileIfExists($fileName)
{
    if (Test-Path $fileName) { Remove-Item $fileName }
}

$DevEnv10Exe = "C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\devenv.com"
$DevEnv11Exe = "C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\devenv.com"
$NetToolsFolder = "C:\Program Files\Microsoft SDKs\Windows\v7.1\Bin\NETFX 4.0 Tools"
$InnoSetupExe = "C:\Program Files (x86)\Inno Setup 5\ISCC.exe"