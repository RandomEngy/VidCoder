:: Builds HandBrakeInterop from the HandBrake source tree and copying the DLLs
:: to the VidCoder lib folder.

call build_common.cmd

:: Change this to the folder containing HandBrakeInterop.sln
set HandBrakeInteropFolder=..\HandBrakeSVN\win\CS\HandBrake.Interop

"%DevEnvExe%" %HandBrakeInteropFolder%\HandBrakeInterop.sln /Rebuild "Release|x86"
"%DevEnvExe%" %HandBrakeInteropFolder%\HandBrakeInterop.sln /Rebuild "Release|x64"
xcopy %HandBrakeInteropFolder%\HandBrakeInterop\bin\x86\Release\HandBrakeInterop.dll Lib\x86 /y
xcopy %HandBrakeInteropFolder%\HandBrakeInterop\bin\x86\Release\HandBrakeInterop.pdb Lib\x86 /y
xcopy %HandBrakeInteropFolder%\HandBrakeInterop\bin\x64\Release\HandBrakeInterop.dll Lib\x64 /y
xcopy %HandBrakeInteropFolder%\HandBrakeInterop\bin\x64\Release\HandBrakeInterop.pdb Lib\x64 /y