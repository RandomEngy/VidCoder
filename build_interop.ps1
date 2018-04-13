# Builds HandBrakeInterop from the HandBrake source tree and copying the DLLs
# to the VidCoder lib folder.

. ./build_common.ps1

# Change this to the folder containing HandBrake.sln
$HandBrakeFolder = "..\HandBrake\win\CS"

$HandBrakeSolution = $HandBrakeFolder + "\HandBrake.sln"
$HandBrakeAppServicesFolder = $HandBrakeFolder + "\HandBrake.ApplicationServices"
$HandBrakeAppServiceProject = $HandBrakeAppServicesFolder + "\HandBrake.ApplicationServices.csproj"

& $MsBuildExe $HandBrakeAppServiceProject /t:rebuild "/p:Configuration=Release;Platform=x64"; ExitIfFailed
copy ($HandBrakeAppServicesFolder + "\bin\Release\HandBrake.ApplicationServices.dll") Lib -force
copy ($HandBrakeAppServicesFolder + "\bin\Release\HandBrake.ApplicationServices.pdb") Lib -force
"Files copied."

WriteSuccess

Write-Host
Pause