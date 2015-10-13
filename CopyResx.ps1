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

$projectPath = ".\VidCoder\VidCoder.csproj"
$projectXml = $xml = [xml](Get-Content $projectPath)

$resourceElements = $projectXml.Project.ItemGroup.EmbeddedResource | where {$_.Include -and $_.Include.StartsWith("Resources\Translations\")}

$itemGroup = $resourceElements[0].ParentNode

foreach($resourceElement in $resourceElements) {
    $resourceElement.ParentNode.RemoveChild($resourceElement) | Out-Null
}

foreach($copiedFile in $copiedFiles) {
    $newResourceElement = $xml.CreateElement("EmbeddedResource", $xml.Project.NamespaceURI)
    $newResourceElement.SetAttribute("Include", "Resources\Translations\" + $copiedFile)

    $manifestElement = $xml.CreateElement("ManifestResourceName", $xml.Project.NamespaceURI)
    $manifestElement.InnerText = '$(TargetName).Resources.%(Filename)'
    $newResourceElement.AppendChild($manifestElement) | Out-Null

    $itemGroup.AppendChild($newResourceElement) | Out-Null
}

$xml.Save($projectPath)