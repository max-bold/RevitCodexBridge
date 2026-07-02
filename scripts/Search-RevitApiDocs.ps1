[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string[]] $Query,

    [int] $MaxResults = 25,

    [int] $Context = 0,

    [switch] $CaseSensitive,

    [switch] $Regex,

    [switch] $OpenFirst
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$docsRoot = Join-Path $repoRoot 'APIdocs'
$htmlRoot = Join-Path $docsRoot 'html'

if (-not (Test-Path -LiteralPath $htmlRoot)) {
    throw "Revit API HTML docs were not found at '$htmlRoot'. Extract RevitAPI.chm into '$docsRoot' first."
}

$pattern = ($Query -join ' ').Trim()
if ([string]::IsNullOrWhiteSpace($pattern)) {
    throw 'Query must not be empty.'
}

function ConvertFrom-HtmlSnippet {
    param([string] $Text)

    $withoutTags = [regex]::Replace($Text, '<[^>]+>', ' ')
    $decoded = [System.Net.WebUtility]::HtmlDecode($withoutTags)
    [regex]::Replace($decoded, '\s+', ' ').Trim()
}

function Get-HtmlMetaContent {
    param(
        [string] $Html,
        [string] $Name
    )

    $escapedName = [regex]::Escape($Name)
    $pattern = '<meta\s+[^>]*name="' + $escapedName + '"[^>]*content="([^"]*)"[^>]*>'
    $match = [regex]::Match($Html, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($match.Success) {
        return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value).Trim()
    }

    $pattern = '<meta\s+[^>]*content="([^"]*)"[^>]*name="' + $escapedName + '"[^>]*>'
    $match = [regex]::Match($Html, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($match.Success) {
        return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value).Trim()
    }

    $null
}

$metadataCache = @{}
function Get-RevitApiTopicMetadata {
    param([string] $Path)

    if ($metadataCache.ContainsKey($Path)) {
        return $metadataCache[$Path]
    }

    $html = Get-Content -LiteralPath $Path -Raw
    $title = $null
    $titleMatch = [regex]::Match($html, '<title>(.*?)</title>', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($titleMatch.Success) {
        $title = ConvertFrom-HtmlSnippet $titleMatch.Groups[1].Value
    }

    $metadata = [pscustomobject]@{
        Title = $title
        ApiId = Get-HtmlMetaContent -Html $html -Name 'Microsoft.Help.Id'
        Description = Get-HtmlMetaContent -Html $html -Name 'Description'
    }
    $metadataCache[$Path] = $metadata
    $metadata
}

$rg = Get-Command rg -ErrorAction SilentlyContinue
$results = New-Object System.Collections.Generic.List[object]

if ($rg) {
    $rgArgs = @(
        '--json',
        '--color', 'never',
        '--glob', '*.htm',
        '--glob', '*.html',
        '--max-count', '3'
    )

    if (-not $CaseSensitive) {
        $rgArgs += '--ignore-case'
    }

    if (-not $Regex) {
        $rgArgs += '--fixed-strings'
    }

    if ($Context -gt 0) {
        $rgArgs += @('--context', $Context)
    }

    $rgArgs += @($pattern, $htmlRoot)

    & $rg.Source @rgArgs | ForEach-Object {
        if ($results.Count -ge $MaxResults) {
            return
        }

        $event = $_ | ConvertFrom-Json
        if ($event.type -ne 'match' -and $event.type -ne 'context') {
            return
        }

        $path = $event.data.path.text
        $meta = Get-RevitApiTopicMetadata -Path $path
        $lineText = $event.data.lines.text
        $results.Add([pscustomobject]@{
            Kind = $event.type
            Title = $meta.Title
            ApiId = $meta.ApiId
            Description = $meta.Description
            Line = $event.data.line_number
            Path = $path
            Excerpt = ConvertFrom-HtmlSnippet $lineText
        }) | Out-Null
    }
}
else {
    $selectArgs = @{
        Path = Join-Path $htmlRoot '*'
        Pattern = $pattern
        Recurse = $true
        CaseSensitive = $CaseSensitive.IsPresent
    }
    if (-not $Regex) {
        $selectArgs['SimpleMatch'] = $true
    }

    Select-String @selectArgs | Select-Object -First $MaxResults | ForEach-Object {
        $meta = Get-RevitApiTopicMetadata -Path $_.Path
        $results.Add([pscustomobject]@{
            Kind = 'match'
            Title = $meta.Title
            ApiId = $meta.ApiId
            Description = $meta.Description
            Line = $_.LineNumber
            Path = $_.Path
            Excerpt = ConvertFrom-HtmlSnippet $_.Line
        }) | Out-Null
    }
}

if ($OpenFirst -and $results.Count -gt 0) {
    Start-Process $results[0].Path
}

$results | Select-Object -First $MaxResults
