# Builds HandBrakeInterop from the HandBrake source tree and copying the DLLs
# to the VidCoder lib folder.

. ./build_common.ps1

# Change this to the folder containing HandBrake10.sln
$HandBrakeFolder = "..\HandBrakeGit\win\CS"

$HandBrakeSolution = $HandBrakeFolder + "\HandBrake.sln"
$HandBrakeAppServicesFolder = $HandBrakeFolder + "\HandBrake.ApplicationServices"

& $DevEnvExe $HandBrakeSolution /Rebuild "Release|x64" /project "HandBrake.ApplicationServices"; ExitIfFailed
copy ($HandBrakeAppServicesFolder + "\bin\Release\HandBrake.ApplicationServices.dll") Lib -force
copy ($HandBrakeAppServicesFolder + "\bin\Release\HandBrake.ApplicationServices.pdb") Lib -force
"Files copied."

WriteSuccess

Write-Host
Pause