:: Builds VidCoder, serialization assemblies and installers.

call build_common.cmd

"C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\devenv.com" VidCoder.sln /Rebuild "Release|x86"
"C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\devenv.com" VidCoder.sln /Rebuild "Release|x64"

"%NetToolsFolder%\sgen.exe" /f /a:"Lib\x86\HandBrakeInterop.dll"
"%NetToolsFolder%\x64\sgen.exe" /f /a:"Lib\x64\HandBrakeInterop.dll"
"%NetToolsFolder%\sgen.exe" /f /a:"VidCoder\bin\x86\Release\VidCoder.exe"
"%NetToolsFolder%\x64\sgen.exe" /f /a:"VidCoder\bin\x64\Release\VidCoder.exe"

"%InnoSetupExe%" Installer\VidCoder-x86.iss
"%InnoSetupExe%" Installer\VidCoder-x64.iss