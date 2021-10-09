# Builds HandBrakeInterop from the HandBrake source tree and copying the DLLs
# to the VidCoder lib folder.

. ./build_common.ps1

# Change this to the folder containing HandBrake.sln
$HandBrakeFolder = "..\HandBrake\win\CS"

$HandBrakeSolution = $HandBrakeFolder + "\HandBrake.sln"
$HandBrakeInteropFolder = $HandBrakeFolder + "\HandBrake.Interop"
$HandBrakeInteropProject = $HandBrakeInteropFolder + "\HandBrake.Interop.csproj"
$HandBrakeInteropBinFolder = $HandBrakeInteropFolder + "\bin\Any CPU\Release";

& $MsBuildExe $HandBrakeInteropProject /t:rebuild "/p:Configuration=Release;Platform=Any CPU"; ExitIfFailed
copy ($HandBrakeInteropBinFolder + "\HandBrake.Interop.dll") Lib -force
copy ($HandBrakeInteropBinFolder + "\HandBrake.Interop.pdb") Lib -force
"Files copied."

WriteSuccess

Write-Host
Pause