[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Stop-PackageValidation {
    param([Parameter(Mandatory = $true)][string] $Message)
    throw "Package validation failed: $Message"
}

function Get-BytesHash {
    param([Parameter(Mandatory = $true)][byte[]] $Bytes)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.BitConverter]::ToString($sha256.ComputeHash($Bytes)).Replace('-', '')
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-EntryBytes {
    param([Parameter(Mandatory = $true)][System.IO.Compression.ZipArchiveEntry] $Entry)

    $entryStream = $Entry.Open()
    $memory = [System.IO.MemoryStream]::new()
    try {
        $entryStream.CopyTo($memory)
        return $memory.ToArray()
    }
    finally {
        $entryStream.Dispose()
        $memory.Dispose()
    }
}

function Test-BlockingLeakPatterns {
    param(
        [Parameter(Mandatory = $true)][byte[]] $Bytes,
        [Parameter(Mandatory = $true)][string] $FileLabel,
        [Parameter(Mandatory = $true)][string] $ResolvedRepositoryRoot
    )

    $representations = @(
        [System.Text.Encoding]::ASCII.GetString($Bytes),
        [System.Text.Encoding]::Unicode.GetString($Bytes)
    )

    $blockingPatterns = [ordered]@{
        'CodeView RSDS signature' = 'RSDS'
        'embedded PDB reference' = '(?i)\.pdb(?:\x00|$|[^A-Za-z0-9])'
        'Windows user-profile path' = '(?i)[A-Z]:\\Users\\[^\\\x00\r\n]+\\'
        'Windows AppData path' = '(?i)[A-Z]:\\[^\x00\r\n]{1,160}\\AppData\\(?:Local|Roaming)\\'
        'GitHub runner workspace path' = '(?i)[A-Z]:\\a\\[^\\\x00\r\n]+\\'
        'local source or build root' = '(?i)[A-Z]:\\(?:agent|build|repos|src|source|workspace|work)\\[^\\\x00\r\n]+\\'
        'Game Routines repository checkout path' = '(?i)[A-Z]:\\[^\x00\r\n]{0,200}\\playnite-gameroutines-plugin(?:\\|/)'
    }

    $escapedRoot = [regex]::Escape($ResolvedRepositoryRoot)
    if (-not [string]::IsNullOrWhiteSpace($escapedRoot)) {
        $blockingPatterns['exact current repository path'] = "(?i)$escapedRoot"
    }

    foreach ($pattern in $blockingPatterns.GetEnumerator()) {
        foreach ($representation in $representations) {
            if ([regex]::IsMatch($representation, $pattern.Value)) {
                Stop-PackageValidation "$($pattern.Key) detected in $FileLabel."
            }
        }
    }

    $informationalPatterns = [ordered]@{
        'generic rooted drive path' = '(?i)[A-Z]:\\[A-Za-z0-9_. -]+\\'
        'generic UNC-like path' = '(?i)\\\\[A-Za-z0-9_.-]+\\[A-Za-z0-9$_.-]+\\'
    }
    foreach ($pattern in $informationalPatterns.GetEnumerator()) {
        $matched = $false
        foreach ($representation in $representations) {
            if ([regex]::IsMatch($representation, $pattern.Value)) {
                $matched = $true
                break
            }
        }
        if ($matched) {
            Write-Host "::warning title=Ambiguous path-like string::$($pattern.Key) detected in $FileLabel; review if unexpected."
        }
    }
}

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$package = (Resolve-Path -LiteralPath $PackagePath).Path
if ([System.IO.Path]::GetExtension($package) -cne '.pext') {
    Stop-PackageValidation "Expected a .pext file: $package"
}

$extensionManifestPath = Join-Path $root 'source\extension.yaml'
$manifestContent = [System.IO.File]::ReadAllText($extensionManifestPath)
$versionMatches = [regex]::Matches($manifestContent, '(?m)^Version:\s*(\S+)\s*$')
if ($versionMatches.Count -ne 1) {
    Stop-PackageValidation "Expected one extension Version, found $($versionMatches.Count)."
}
$extensionVersion = $versionMatches[0].Groups[1].Value
$expectedAssemblyVersion = "$extensionVersion.0"

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Reflection.Metadata

$expectedEntries = @('extension.yaml', 'GameRoutines.dll', 'icon.png', 'Localization/en_US.xaml')
$entryBytes = @{}
$archive = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    $seenEntries = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $archive.Entries) {
        $normalizedName = $entry.FullName.Replace('\', '/')
        if ([string]::IsNullOrWhiteSpace($normalizedName) -or
            $normalizedName.StartsWith('/', [System.StringComparison]::Ordinal) -or
            $normalizedName -match '^[A-Za-z]:' -or
            $normalizedName.Split('/') -contains '..') {
            Stop-PackageValidation "Unsafe or rooted archive path: $normalizedName"
        }
        if (-not $seenEntries.Add($normalizedName)) {
            Stop-PackageValidation "Duplicate archive path, including case-only duplicates: $normalizedName"
        }
        if ($expectedEntries -cnotcontains $normalizedName) {
            $reason = switch -Regex ($normalizedName) {
                '(?i)\.pdb$' { 'debug symbols are forbidden'; break }
                '(?i)^Playnite\.SDK\.(dll|xml)$' { 'Playnite SDK runtime or documentation is forbidden'; break }
                '(?i)\.dll$' { 'only GameRoutines.dll is allowed'; break }
                '(?i)(^|/)(Integrations|FusionX)(/|$)' { 'FusionX reference integration files are forbidden'; break }
                '(?i)\.(cs|csproj|sln|md|txt)$' { 'source, project, or repository documentation is forbidden'; break }
                '(?i)(^|/)(bin|obj|packages|\.git|\.github|assets)(/|$)' { 'repository or build output is forbidden'; break }
                default { 'the path is not in the release allowlist' }
            }
            Stop-PackageValidation "Unexpected package entry '$normalizedName': $reason."
        }
        if ($entry.Length -eq 0) {
            Stop-PackageValidation "Required package entry is empty: $normalizedName"
        }
        $entryBytes[$normalizedName] = Get-EntryBytes -Entry $entry
    }
}
finally {
    $archive.Dispose()
}

if ($entryBytes.Count -ne $expectedEntries.Count) {
    Stop-PackageValidation "Expected exactly $($expectedEntries.Count) files, found $($entryBytes.Count)."
}
foreach ($expectedEntry in $expectedEntries) {
    if (-not $entryBytes.ContainsKey($expectedEntry)) {
        Stop-PackageValidation "Required package entry is missing or has unexpected casing: $expectedEntry"
    }
}

$releaseOutput = Join-Path $root 'source\bin\Release'
foreach ($expectedEntry in $expectedEntries) {
    $buildPath = Join-Path $releaseOutput $expectedEntry
    if (-not (Test-Path -LiteralPath $buildPath -PathType Leaf)) {
        Stop-PackageValidation "Release output is missing while comparing package contents: $expectedEntry"
    }
    $buildHash = (Get-FileHash -LiteralPath $buildPath -Algorithm SHA256).Hash
    $packageHash = Get-BytesHash -Bytes $entryBytes[$expectedEntry]
    if ($buildHash -cne $packageHash) {
        Stop-PackageValidation "Packaged file differs from the validated Release output: $expectedEntry"
    }
}

$temporaryDll = Join-Path ([System.IO.Path]::GetTempPath()) ("GameRoutines-CI-" + [guid]::NewGuid().ToString('N') + '.dll')
[System.IO.File]::WriteAllBytes($temporaryDll, $entryBytes['GameRoutines.dll'])
try {
    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($temporaryDll)
    if ($assemblyName.Name -cne 'GameRoutines') {
        Stop-PackageValidation "Assembly name is $($assemblyName.Name), expected GameRoutines."
    }
    if ($assemblyName.Version.ToString() -cne $expectedAssemblyVersion) {
        Stop-PackageValidation "Assembly version is $($assemblyName.Version), expected $expectedAssemblyVersion."
    }

    $dllStream = [System.IO.MemoryStream]::new($entryBytes['GameRoutines.dll'], $false)
    $peReader = [System.Reflection.PortableExecutable.PEReader]::new($dllStream)
    try {
        foreach ($debugEntry in $peReader.ReadDebugDirectory()) {
            if ($debugEntry.Type -eq [System.Reflection.PortableExecutable.DebugDirectoryEntryType]::CodeView) {
                Stop-PackageValidation 'CodeView debug-directory data is present in GameRoutines.dll.'
            }
        }
    }
    finally {
        $peReader.Dispose()
        $dllStream.Dispose()
    }
}
finally {
    Remove-Item -LiteralPath $temporaryDll -Force -ErrorAction SilentlyContinue
}

Test-BlockingLeakPatterns -Bytes $entryBytes['GameRoutines.dll'] -FileLabel 'GameRoutines.dll' -ResolvedRepositoryRoot $root
Test-BlockingLeakPatterns -Bytes $entryBytes['extension.yaml'] -FileLabel 'extension.yaml' -ResolvedRepositoryRoot $root
Test-BlockingLeakPatterns -Bytes $entryBytes['Localization/en_US.xaml'] -FileLabel 'Localization/en_US.xaml' -ResolvedRepositoryRoot $root

Write-Host "Package validation passed: exact four-file inventory, GameRoutines $expectedAssemblyVersion."
