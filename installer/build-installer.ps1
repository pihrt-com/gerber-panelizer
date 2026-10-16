[CmdletBinding()]
param(
    [string]$Version = '0.1.7',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\installer'),
    [switch]$CleanPublish
)

$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Version must use three numeric components, for example 0.1.5.'
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$publishDirectory = Join-Path $repositoryRoot "publish\win-x64-v$Version-ready"
$applicationProject = Join-Path $repositoryRoot 'src\GerberPanelizer.App\GerberPanelizer.App.csproj'
$installerProject = Join-Path $PSScriptRoot 'GerberPanelizer.Installer.wixproj'
$licenseSource = Join-Path $repositoryRoot 'LICENSE'
$generatedDirectory = Join-Path $PSScriptRoot 'obj\generated'
$licenseRtfPath = Join-Path $generatedDirectory 'GPL-3.0.rtf'

function ConvertTo-RtfText {
    param([Parameter(Mandatory = $true)][string]$Text)

    $builder = [System.Text.StringBuilder]::new()
    foreach ($character in $Text.ToCharArray()) {
        $codePoint = [int][char]$character
        switch ($codePoint) {
            13 { continue }
            10 { [void]$builder.Append('\\par').Append("`r`n"); continue }
            92 { [void]$builder.Append('\\\\'); continue }
            123 { [void]$builder.Append('\\{'); continue }
            125 { [void]$builder.Append('\\}'); continue }
            default {
                if ($codePoint -gt 127) {
                    [void]$builder.Append('\\u').Append($codePoint).Append('?')
                }
                else {
                    [void]$builder.Append($character)
                }
            }
        }
    }

    return $builder.ToString()
}

if (-not (Test-Path -LiteralPath $licenseSource -PathType Leaf)) {
    throw "The GPL license source was not found at '$licenseSource'."
}

New-Item -ItemType Directory -Path $generatedDirectory -Force | Out-Null
$licenseText = [System.IO.File]::ReadAllText($licenseSource)
$licenseRtf = '{\rtf1\ansi\deff0{\fonttbl{\f0\fnil Segoe UI;}}\viewkind4\uc1\pard\f0\fs18 ' + (ConvertTo-RtfText -Text $licenseText) + '}'
[System.IO.File]::WriteAllText($licenseRtfPath, $licenseRtf, [System.Text.Encoding]::ASCII)

if (Test-Path -LiteralPath $publishDirectory) {
    if (-not $CleanPublish) {
        throw "Publish directory '$publishDirectory' already exists. Use -CleanPublish only when it contains a disposable build output."
    }

    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputDirectoryPath = (Resolve-Path -LiteralPath $OutputDirectory).Path

dotnet publish $applicationProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version -p:AssemblyVersion="$Version.0" -p:FileVersion="$Version.0" -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw 'Publishing the application failed.'
}

dotnet build $installerProject -t:Rebuild -c Release -p:Version=$Version -p:PublishDirectory=$publishDirectory -p:LicenseRtfPath=$licenseRtfPath -p:OutputPath=$outputDirectoryPath
if ($LASTEXITCODE -ne 0) {
    throw 'Building the MSI installer failed.'
}

$msi = Join-Path $outputDirectoryPath "GerberPanelizer-$Version-win-x64.msi"
if (-not (Test-Path -LiteralPath $msi -PathType Leaf)) {
    throw "The installer was not created at '$msi'."
}

Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $publishDirectory 'GerberPanelizer.App.exe')
Get-FileHash -Algorithm SHA256 -LiteralPath $msi
