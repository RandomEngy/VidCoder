:: This script is for convenience: quickly building HandBrakeInterop from the HandBrake source tree and copying the DLLs
:: to the VidCoder lib folder.

"C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\devenv.com" ..\HandBrakeSVN\win\CS\HandBrake.Interop\HandBrakeInterop.sln /Rebuild "Release|x86"
"C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\devenv.com" ..\HandBrakeSVN\win\CS\HandBrake.Interop\HandBrakeInterop.sln /Rebuild "Release|x64"
xcopy ..\HandBrakeSVN\win\CS\HandBrake.Interop\HandBrakeInterop\bin\x86\Release\HandBrakeInterop.dll Lib\x86 /y
xcopy ..\HandBrakeSVN\win\CS\HandBrake.Interop\HandBrakeInterop\bin\x64\Release\HandBrakeInterop.dll Lib\x64 /y