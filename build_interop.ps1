# Builds HandBrakeInterop from the HandBrake source tree and copying the DLLs
# to the VidCoder lib folder.

. ./build_common.ps1

# Change this to the folder containing HandBrake10.sln
$HandBrakeFolder = "..\HandBrakeSVN\win\CS"

$HandBrakeSolution = $HandBrakeFolder + "\HandBrake10.sln"
$HandBrakeAppServicesFolder = $HandBrakeFolder + "\HandBrake.ApplicationServices"

& $DevEnv12Exe $HandBrakeSolution /Rebuild "Release|x86" /project "HandBrake.ApplicationServices"; ExitIfFailed
copy ($HandBrakeAppServicesFolder + "\bin\Release\HandBrake.ApplicationServices.dll") Lib\x86 -force
copy ($HandBrakeAppServicesFolder + "\bin\Release\HandBrake.ApplicationServices.pdb") Lib\x86 -force
"Files copied."

& $DevEnv12Exe $HandBrakeSolution /Rebuild "Release|x64" /project "HandBrake.ApplicationServices"; ExitIfFailed
copy ($HandBrakeAppServicesFolder + "\bin\Release\HandBrake.ApplicationServices.dll") Lib\x64 -force
copy ($HandBrakeAppServicesFolder + "\bin\Release\HandBrake.ApplicationServices.pdb") Lib\x64 -force
"Files copied."

WriteSuccess

Write-Host
Pause