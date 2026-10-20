<#
.SYNOPSIS
    Build a Unity .unitypackage from a UPM package folder.

.DESCRIPTION
    A .unitypackage is a gzipped tar archive. Each asset gets a GUID-named folder
    that contains:
        asset       - the file content (files only, folders have no asset file)
        asset.meta  - the content of the matching .meta file
        pathname    - the path inside the project, for example
                      Assets/SSRichText/Runtime/Scripts/RichText.cs
    Folder entries only carry asset.meta and pathname, which keeps the folder GUIDs
    stable when the package is imported.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools~/BuildUnityPackage.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Tools~/BuildUnityPackage.ps1 `
        -PackageRoot Assets/SSRichText `
        -OutputPath dist/SSRichText.unitypackage

.NOTES
    The Tools~ folder is ignored by Unity, so this script is never imported
    into the project.
#>
[CmdletBinding()]
param(
    [string]$PackageRoot = 'Assets/SSRichText',
    [string]$OutputPath = 'dist/SSRichText.unitypackage'
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Get-Location).Path
$rootPath = (Resolve-Path -LiteralPath $PackageRoot).Path

function Get-PathUnderRoot {
    param([string]$BasePath, [string]$FullPath)

    # Windows PowerShell 5.1 does not have [System.IO.Path]::GetRelativePath
    $baseUri = [Uri](([Uri]$BasePath).AbsoluteUri.TrimEnd('/') + '/')
    $targetUri = [Uri]$FullPath
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString())
}

function Get-GuidFromMeta {
    param([string]$MetaPath)

    if (-not (Test-Path -LiteralPath $MetaPath)) {
        throw "Missing meta file: $MetaPath"
    }

    $text = [System.IO.File]::ReadAllText($MetaPath)
    $match = [regex]::Match($text, '(?m)^guid:\s*([0-9a-fA-F]{32})\s*$')
    if (-not $match.Success) {
        throw "Cannot parse a GUID from: $MetaPath"
    }

    return $match.Groups[1].Value
}

$relativeRoot = (Get-PathUnderRoot -BasePath $projectRoot -FullPath $rootPath).Replace('\', '/')
if (-not $relativeRoot.StartsWith('Assets/')) {
    throw "PackageRoot must live under Assets/, got '$relativeRoot'"
}

# Collect folders first (including the package root), then files.
$entries = [System.Collections.Generic.List[object]]::new()

$directories = @($rootPath) + @(Get-ChildItem -LiteralPath $rootPath -Recurse -Directory | ForEach-Object { $_.FullName })
foreach ($directory in ($directories | Sort-Object)) {
    $entries.Add([pscustomobject]@{
            IsFolder   = $true
            SourcePath = $directory
            MetaPath   = "$directory.meta"
            PathName   = (Get-PathUnderRoot -BasePath $projectRoot -FullPath $directory).Replace('\', '/')
            Guid       = Get-GuidFromMeta -MetaPath "$directory.meta"
        })
}

$files = Get-ChildItem -LiteralPath $rootPath -Recurse -File |
    Where-Object { $_.Extension -ne '.meta' -and $_.Name -notin @('.DS_Store', 'Thumbs.db') } |
    Sort-Object FullName

foreach ($file in $files) {
    $entries.Add([pscustomobject]@{
            IsFolder   = $false
            SourcePath = $file.FullName
            MetaPath   = "$($file.FullName).meta"
            PathName   = (Get-PathUnderRoot -BasePath $projectRoot -FullPath $file.FullName).Replace('\', '/')
            Guid       = Get-GuidFromMeta -MetaPath "$($file.FullName).meta"
        })
}

$duplicated = $entries | Group-Object Guid | Where-Object Count -gt 1
if ($duplicated) {
    throw "Duplicated GUIDs: $($duplicated.Name -join ', ')"
}

$outputFullPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
}
else {
    Join-Path $projectRoot $OutputPath
}

$outputDirectory = Split-Path -Parent $outputFullPath
if ($outputDirectory -and -not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Force
}

$staging = Join-Path ([System.IO.Path]::GetTempPath()) ('ssrt-package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null

try {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    foreach ($entry in $entries) {
        $entryDirectory = Join-Path $staging $entry.Guid
        New-Item -ItemType Directory -Path $entryDirectory | Out-Null

        if (-not $entry.IsFolder) {
            Copy-Item -LiteralPath $entry.SourcePath -Destination (Join-Path $entryDirectory 'asset')
        }

        Copy-Item -LiteralPath $entry.MetaPath -Destination (Join-Path $entryDirectory 'asset.meta')
        [System.IO.File]::WriteAllText((Join-Path $entryDirectory 'pathname'), $entry.PathName, $utf8NoBom)
    }

    $tarArguments = @('-czf', $outputFullPath, '-C', $staging) + ($entries.Guid | Sort-Object)
    & tar @tarArguments
    if ($LASTEXITCODE -ne 0) {
        throw "tar failed with exit code $LASTEXITCODE"
    }
}
finally {
    Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
}

$outputFile = Get-Item -LiteralPath $outputFullPath
$hash = (Get-FileHash -LiteralPath $outputFullPath -Algorithm SHA256).Hash
$folderCount = @($entries | Where-Object { $_.IsFolder }).Count
$fileCount = @($entries | Where-Object { -not $_.IsFolder }).Count

Write-Host "Package built: $($outputFile.FullName)"
Write-Host "Entries: $($entries.Count) (folders: $folderCount, files: $fileCount)"
Write-Host "Size: $($outputFile.Length) bytes"
Write-Host "SHA256: $hash"
