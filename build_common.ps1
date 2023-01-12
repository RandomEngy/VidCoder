# Common settings/functions for scripts

Function Pause ($Message = "Press any key to continue . . . ") {
    If ($psISE) {
        # The "ReadKey" functionality is not supported in Windows PowerShell ISE.
 
        $Shell = New-Object -ComObject "WScript.Shell"
        $Button = $Shell.Popup("Click OK to continue.", 0, "Script Paused", 0)
 
        Return
    }
 
    Write-Host -NoNewline $Message
 
    $Ignore =
        16,  # Shift (left or right)
        17,  # Ctrl (left or right)
        18,  # Alt (left or right)
        20,  # Caps lock
        91,  # Windows key (left)
        92,  # Windows key (right)
        93,  # Menu key
        144, # Num lock
        145, # Scroll lock
        166, # Back
        167, # Forward
        168, # Refresh
        169, # Stop
        170, # Search
        171, # Favorites
        172, # Start/Home
        173, # Mute
        174, # Volume Down
        175, # Volume Up
        176, # Next Track
        177, # Previous Track
        178, # Stop Media
        179, # Play
        180, # Mail
        181, # Select Media
        182, # Application 1
        183  # Application 2
 
    While ($KeyInfo.VirtualKeyCode -Eq $Null -Or $Ignore -Contains $KeyInfo.VirtualKeyCode) {
        $KeyInfo = $Host.UI.RawUI.ReadKey("NoEcho, IncludeKeyDown")
    }
 
    Write-Host
}

function ExitWithError($message)
{
    Write-Host $message -foregroundcolor "red"
    Pause
    exit
}

function ExitIfFailed()
{
    if ($LASTEXITCODE -ne 0)
    {
        ExitWithError "An error occurred. Stopping build."
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

# vswhere.exe is documented to have a consistent install location, no matter the version or edition of VS that is installed.
$MSBuildPath = (&"${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe) | Out-String
Set-Alias msbuild $MSBuildPath.Trim()