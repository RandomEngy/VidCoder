# Download Crowdin zip
Invoke-WebRequest -Uri "https://crowdin.com/backend/download/project/vidcoder.zip" -OutFile ".\Import\VidCoderResources.zip"

# Extract files from Crowdin zip
if (Test-Path .\Import\Resources) {
    Remove-Item .\Import\Resources\* -recurse
}

Add-Type -assembly "system.io.compression.filesystem"
[io.compression.zipfile]::ExtractToDirectory(".\Import\VidCoderResources.zip", "Import\Resources")

# Copy files from holding directory to project directory
$copiedFiles = New-Object System.Collections.Generic.List[System.String]

function CopyLanguage($languageDir, $language) {
    $fileEntries = [IO.Directory]::GetFiles(".\Import\Resources\" + $languageDir)
    foreach($fullFileName in $fileEntries) 
    {
        $lastSlash = $fullFileName.LastIndexOf("\")
        $sourceFileName = $fullFileName.Substring($lastSlash + 1)

        if ($languageDir.Contains("-")) {
            $destFileName = $sourceFileName.Replace($languageDir, $language)
        } else {
            $destFileName = $sourceFileName
        }

        $sourcePath = ".\Import\Resources\" + $languageDir + "\" + $sourceFileName
        $destPath = ".\VidCoder\Resources\Translations\" + $destFileName
        copy $sourcePath $destPath
        Write-Host "Copied $sourcePath to $destPath"

        $copiedFiles.Add($destFileName)
    } 
}

# List of language codes and names: http://msdn.microsoft.com/en-us/goglobal/bb896001.aspx

CopyLanguage "ar" "ar"
CopyLanguage "bs" "bs"
CopyLanguage "ca" "ca"
CopyLanguage "cs" "cs"
CopyLanguage "de" "de"
CopyLanguage "el" "el"
CopyLanguage "es-ES" "es"
CopyLanguage "eu" "eu"
CopyLanguage "fr" "fr"
CopyLanguage "hr" "hr"
CopyLanguage "hu" "hu"
CopyLanguage "id" "id"
CopyLanguage "it" "it"
CopyLanguage "ja" "ja"
CopyLanguage "ka" "ka"
CopyLanguage "ko" "ko"
CopyLanguage "nl" "nl"
CopyLanguage "pl" "pl"
CopyLanguage "pt-BR" "pt-BR"
CopyLanguage "pt-PT" "pt"
CopyLanguage "ru" "ru"
CopyLanguage "tr" "tr"
CopyLanguage "vi" "vi"
CopyLanguage "zh-TW" "zh-Hant"
CopyLanguage "zh-CN" "zh"