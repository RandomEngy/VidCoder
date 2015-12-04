# Extract files from Crowdin zip
if (Test-Path .\ResourcesImport) {
    Remove-Item .\ResourcesImport\* -recurse
}

Add-Type -assembly "system.io.compression.filesystem"
[io.compression.zipfile]::ExtractToDirectory("vidcoder.zip", "ResourcesImport")

# Copy files from holding directory to project directory
$copiedFiles = New-Object System.Collections.Generic.List[System.String]

function CopyLanguage($languageDir, $language) {
    $fileEntries = [IO.Directory]::GetFiles(".\ResourcesImport\" + $languageDir)
    foreach($fullFileName in $fileEntries) 
    {
        $lastSlash = $fullFileName.LastIndexOf("\")
        $sourceFileName = $fullFileName.Substring($lastSlash + 1)

        if ($languageDir.Contains("-")) {
            $destFileName = $sourceFileName.Replace($languageDir, $language)
        } else {
            $destFileName = $sourceFileName
        }

        $sourcePath = ".\ResourcesImport\" + $languageDir + "\" + $sourceFileName
        $destPath = ".\VidCoder\Resources\Translations\" + $destFileName
        copy $sourcePath $destPath
        Write-Host "Copied $sourcePath to $destPath"

        $copiedFiles.Add($destFileName)
    } 
}

# List of language codes and names: http://msdn.microsoft.com/en-us/goglobal/bb896001.aspx

CopyLanguage "es-ES" "es"
CopyLanguage "fr" "fr"
CopyLanguage "hu" "hu"
CopyLanguage "pt-PT" "pt"
CopyLanguage "pt-BR" "pt-BR"
CopyLanguage "eu" "eu"
CopyLanguage "de" "de"
CopyLanguage "zh-TW" "zh-Hant"
CopyLanguage "zh-CN" "zh"
CopyLanguage "it" "it"
CopyLanguage "ja" "ja"
CopyLanguage "cs" "cs"
CopyLanguage "ru" "ru"
CopyLanguage "pl" "pl"
CopyLanguage "tr" "tr"