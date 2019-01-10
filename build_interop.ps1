# Builds HandBrakeInterop from the HandBrake source tree and copying the DLLs
# to the VidCoder lib folder.

. ./build_common.ps1

# Change this to the folder containing HandBrake.sln
$HandBrakeFolder = "..\HandBrake\win\CS"

$HandBrakeSolution = $HandBrakeFolder + "\HandBrake.sln"
$HandBrakeInteropFolder = $HandBrakeFolder + "\HandBrake.Interop"
$HandBrakeInteropProject = $HandBrakeInteropFolder + "\HandBrake.Interop.csproj"

& $MsBuildExe $HandBrakeInteropProject /t:rebuild "/p:Configuration=Release;Platform=x64"; ExitIfFailed
copy ($HandBrakeInteropFolder + "\bin\Release\HandBrake.Interop.dll") Lib -force
copy ($HandBrakeInteropFolder + "\bin\Release\HandBrake.Interop.pdb") Lib -force
"Files copied."

WriteSuccess

Write-Host
Pause