param(
    [string]$DocsRoot = "docs/architecture",
    [int]$MaxAgeDays = 60
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $DocsRoot)) {
    throw "Docs root not found: $DocsRoot"
}

$today = Get-Date
$failures = New-Object System.Collections.Generic.List[string]

Get-ChildItem -LiteralPath $DocsRoot -Filter "*.md" -File | ForEach-Object {
    if ($_.BaseName -cmatch "^[A-Z0-9_]+$") {
        Write-Verbose "Skipping legacy architecture doc: $($_.Name)"
        return
    }

    $path = $_.FullName
    $relative = Resolve-Path -LiteralPath $path -Relative
    $text = Get-Content -LiteralPath $path -Raw

    if (-not $text.StartsWith("---")) {
        $failures.Add("${relative}: missing YAML frontmatter")
        return
    }

    $end = $text.IndexOf("`n---", 3)
    if ($end -lt 0) {
        $failures.Add("${relative}: unterminated YAML frontmatter")
        return
    }

    $frontmatter = $text.Substring(3, $end - 3)
    foreach ($field in @("title", "scope", "status", "last_verified")) {
        if ($frontmatter -notmatch "(?m)^${field}:\s*.+$") {
            $failures.Add("${relative}: missing frontmatter field '$field'")
        }
    }

    $match = [regex]::Match($frontmatter, "(?m)^last_verified:\s*(\d{4}-\d{2}-\d{2})\s*$")
    if ($match.Success) {
        $verified = [datetime]::ParseExact($match.Groups[1].Value, "yyyy-MM-dd", $null)
        $age = ($today.Date - $verified.Date).Days
        if ($age -gt $MaxAgeDays -and $frontmatter -notmatch "(?m)^status:\s*.*stale") {
            $failures.Add("${relative}: last_verified is $age days old; mark stale or reverify")
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Architecture docs frontmatter check passed."
