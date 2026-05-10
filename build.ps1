[CmdletBinding()]
Param(
  [Parameter(Mandatory=$True)]
  [string]$versionShort,
  [switch]$beta,
  [switch]$debugBuild = $false
)

. ./build_common.ps1

# Get master version number
$version4Part = $versionShort + ".0.0"

if ($debugBuild) {
    $buildFlavor = "Debug"
} else {
    $buildFlavor = "Release"
}

$branch = &git rev-parse --abbrev-ref HEAD
if ($beta -and ($branch -eq "master")) {
    ExitWithError "Current branch is master but build calls for beta"
}

if (!$beta -and ($branch -eq "beta")) {
    ExitWithError "Current branch is beta but build calls for stable"
}

if ($beta) {
    $configuration = $buildFlavor + "-Beta"
    $productName = "VidCoder Beta"
} else {
    $configuration = $buildFlavor
    $productName = "VidCoder"
}

function GetPublishFolderPath($projectName, $folderNameSuffix) {
	".\$projectName\bin\publish-$folderNameSuffix"
}

function PlatformToBuildPlatform($platform) {
    if ($platform -ieq "x64") { return "x64" }
    elseif ($platform -ieq "arm64") { return "ARM64" }
    else { ExitWithError "Unknown platform: $platform" }
}

# Basic clear and publish for a specific project and publish profile
function Publish($projectName, $platform, $installerType, $publishProfileName) {
	$folderNameSuffix = "$installerType-$platform"
	$buildPlatform = PlatformToBuildPlatform $platform
	
	PublishRaw $projectName $buildPlatform $folderNameSuffix $publishProfileName
}

function PublishRaw($projectName, $buildPlatform, $folderNameSuffix, $publishProfileName) {
	Write-Host "Publishing $folderNameSuffix on project $projectName for $buildPlatform..."
    $publishFolderPath = GetPublishFolderPath $projectName $folderNameSuffix
    if (Test-Path -Path $publishFolderPath) {
        Get-ChildItem -Path $publishFolderPath -Include * -File -Recurse | foreach { $_.Delete()}
    }

	# We have to explicitly pass in Platform to make sure the conditional hb.dll link resolves correctly
	& dotnet publish .\$projectName\$projectName.csproj `
        /p:PublishProfile=$publishProfileName `
        /p:Platform=$buildPlatform `
        /p:Version=$version4part `
        "/p:Product=$productName" `
        -c $configuration
}

# Publish an installer and copy extra files for it
function PublishInstaller($platform, $publishProfileName) {
	Publish "VidCoder" $platform "installer" $publishProfileName
	CopyInstallerExtraFiles $platform
}

function CopyInstallerExtraFiles($platform) {
	$extraFiles = @(
        ".\VidCoder\Icons\File\VidCoderPreset.ico",
        ".\VidCoder\Icons\File\VidCoderQueue.ico")

	$publishFolderPath = GetPublishFolderPath "VidCoder" "installer" $platform

    foreach ($extraFile in $extraFiles) {
        copy $extraFile $publishFolderPath; ExitIfFailed
    }
}


function SignExe($filePath) {
    & signtool sign /a /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $filePath
}



# Publish the files
PublishInstaller "x64" "InstallerX64Profile"
PublishInstaller "arm64" "InstallerArm64Profile"

Publish "VidCoder" "x64" "portable" "PortableX64Profile"
Publish "VidCoder" "arm64" "portable" "PortableArm64Profile"

PublishRaw "VidCoderWorker" "AnyCPU" "portable" "PortableProfile"

# We need to copy some files from the Worker publish over to the main publish output, because the main publish output doesn't properly set the Worker to self-contained mode
copy ".\VidCoderWorker\bin\publish-portable\VidCoderWorker*" ".\VidCoder\bin\publish-portable-x64"
copy ".\VidCoderWorker\bin\publish-portable\VidCoderWorker*" ".\VidCoder\bin\publish-portable-arm64"

# Create portable exes

if ($beta) {
    $betaNameSection = "-Beta"
} else {
    $betaNameSection = ""
}

$binaryNameBase = "VidCoder-$versionShort$betaNameSection"

if ($debugBuild) {
    $builtInstallerFolder = "Installer\BuiltInstallers\Test"
} else {
    $builtInstallerFolder = "Installer\BuiltInstallers"
}

$winRarExe = "c:\Program Files\WinRar\WinRAR.exe"

function BuildPortable($platform) {
	Write-Host "Creating portable exe for $platform..."
	New-Item -ItemType Directory -Force -Path ".\$builtInstallerFolder"

	$portableExeWithoutExtension = ".\$builtInstallerFolder\$binaryNameBase-$platform-Portable"
	$portableExeWithExtension = $portableExeWithoutExtension + ".exe"

	DeleteFileIfExists $portableExeWithExtension

	& $winRarExe a -sfx -z".\Installer\VidCoderRar.conf" -iicon".\VidCoder\VidCoder_icon.ico" -r -ep1 $portableExeWithoutExtension .\VidCoder\bin\publish-portable-$platform\** | Out-Null
	ExitIfFailed

	SignExe $portableExeWithExtension; ExitIfFailed
}

BuildPortable "x64"
BuildPortable "arm64"

function BuildZip($platform) {
	# Sign executables in publish-installer, for inclusion in the .zip Release
	$publishedExes = Get-ChildItem -Path .\VidCoder\bin\publish-installer-$platform\ -Filter *.exe
	foreach ($exeFile in $publishedExes) {
		SignExe $exeFile.FullName; ExitIfFailed
	}

	# Create zip file with binaries
	$zipFilePath = ".\Installer\BuiltInstallers\$binaryNameBase-$platform.zip"
	DeleteFileIfExists $zipFilePath

	& $winRarExe a -afzip -ep1 -r $zipFilePath .\VidCoder\bin\publish-installer-$platform\
	ExitIfFailed
}

BuildZip "x64"
BuildZip "arm64"

# Build Velopack installer
if ($beta) {
    $packId = "VidCoder.Beta"
    $releaseDirSuffix = "Beta"
} else {
    $packId = "VidCoder.Stable"
    $releaseDirSuffix = "Stable"
}

$releaseDir = ".\Installer\Releases-$releaseDirSuffix"

function Velopack($packId, $channel, $releaseDir, $platform) {
	Write-Host "Building Velopack for $platform"
	vpk pack `
		-x `
		-y `
		--packId $packId `
		--packTitle "$productName" `
		--packVersion ($versionShort + ".0") `
		--packAuthors RandomEngy `
		--packDir .\VidCoder\bin\publish-installer-$platform `
		--channel $channel `
		--mainExe VidCoder.exe `
		--icon .\Installer\VidCoder_Setup.ico `
		--outputDir $releaseDir `
		--splashImage .\Installer\InstallerSplash.png `
		--signParams "/a /fd SHA256 /tr http://timestamp.digicert.com /td SHA256" `
		--framework net10.0.2-$platform
	
	Copy-Item -Path ("$releaseDir\$packId-$channel-Setup.exe") -Destination ".\Installer\BuiltInstallers\$binaryNameBase-$platform.exe" -Force
	ExitIfFailed
}

Velopack $packId "win" $releaseDir "x64"
Velopack $packId "win-arm64" $releaseDir "arm64"

WriteSuccess

Write-Host
