# GOLFIN Architecture Audit Generator (PowerShell)
# Run at the start of each session to give Claude (architect) current project state.
# Usage: powershell -File Docs/generate_audit.ps1 > Docs/ARCHITECTURE_AUDIT.md

$ScriptsDir = "Assets/Scripts"
$DataDir = "Assets/Data"

Write-Output "# Architecture Audit"
Write-Output ""
Write-Output "> Auto-generated $(Get-Date -Format 'yyyy-MM-dd HH:mm'). Do not edit manually."
Write-Output ""

# --- File Tree (Scripts) ---
Write-Output "## File Tree (Scripts)"
Write-Output ""
Write-Output '```'
Get-ChildItem -Path $ScriptsDir -Filter "*.cs" -Recurse | Sort-Object FullName | ForEach-Object {
    $_.FullName.Replace((Get-Location).Path + "\", "").Replace("\", "/")
}
Write-Output '```'
Write-Output ""

# --- File Tree (Data) ---
Write-Output "## File Tree (Data)"
Write-Output ""
Write-Output '```'
if (Test-Path $DataDir) {
    Get-ChildItem -Path $DataDir -File -Recurse | Sort-Object FullName | ForEach-Object {
        $_.FullName.Replace((Get-Location).Path + "\", "").Replace("\", "/")
    }
}
Write-Output '```'
Write-Output ""

# --- MonoBehaviours ---
Write-Output "## MonoBehaviours"
Write-Output ""
Write-Output "| Class | File | Singleton | Key Interfaces |"
Write-Output "|---|---|---|---|"

Get-ChildItem -Path $ScriptsDir -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    $content = Get-Content $file.FullName -Raw
    $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "").Replace("\", "/")

    # Find classes that inherit from MonoBehaviour
    $matches = [regex]::Matches($content, 'class\s+(\w+)\s*:\s*([^{]+)')
    foreach ($match in $matches) {
        $className = $match.Groups[1].Value
        $inheritance = $match.Groups[2].Value.Trim()

        if ($inheritance -match "MonoBehaviour") {
            $isSingleton = if ($content -match "Instance") { "Yes" } else { "" }
            $interfaces = ($inheritance -replace "MonoBehaviour", "" -replace ",\s*", ", ").Trim().Trim(",").Trim()
            Write-Output "| $className | $relativePath | $isSingleton | $interfaces |"
        }
    }
}
Write-Output ""

# --- Singletons ---
Write-Output "## Singletons"
Write-Output ""

Get-ChildItem -Path $ScriptsDir -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    $content = Get-Content $file.FullName -Raw
    $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "").Replace("\", "/")

    if ($content -match "static\s+\w+\s+Instance") {
        $classMatch = [regex]::Match($content, 'class\s+(\w+)')
        if ($classMatch.Success) {
            $className = $classMatch.Groups[1].Value
            $dontDestroy = if ($content -match "DontDestroyOnLoad") { "(DontDestroyOnLoad)" } else { "" }
            Write-Output "- **$className** ($relativePath) $dontDestroy"
        }
    }
}
Write-Output ""

# --- Events ---
Write-Output "## Events (Action delegates)"
Write-Output ""
Write-Output "| Class | Event |"
Write-Output "|---|---|"

Get-ChildItem -Path $ScriptsDir -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    $lines = Get-Content $file.FullName
    $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "").Replace("\", "/")
    $content = Get-Content $file.FullName -Raw
    $classMatch = [regex]::Match($content, 'class\s+(\w+)')
    $className = if ($classMatch.Success) { $classMatch.Groups[1].Value } else { "Unknown" }

    foreach ($line in $lines) {
        if ($line -match "event\s+.*Action") {
            $eventDecl = $line.Trim()
            Write-Output "| $className | ``$eventDecl`` |"
        }
    }
}
Write-Output ""

# --- SerializeField counts ---
Write-Output "## Serialized Fields Summary"
Write-Output ""
Write-Output "| Class | File | SerializeField Count |"
Write-Output "|---|---|---|"

Get-ChildItem -Path $ScriptsDir -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    $content = Get-Content $file.FullName -Raw
    $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "").Replace("\", "/")
    $count = ([regex]::Matches($content, "SerializeField")).Count

    if ($count -gt 0) {
        $classMatch = [regex]::Match($content, 'class\s+(\w+)')
        $className = if ($classMatch.Success) { $classMatch.Groups[1].Value } else { "Unknown" }
        Write-Output "| $className | $relativePath | $count |"
    }
}
Write-Output ""

# --- CSV Structure ---
Write-Output "## CSV Data Files"
Write-Output ""

if (Test-Path $DataDir) {
    Get-ChildItem -Path $DataDir -Filter "*.csv" -Recurse | ForEach-Object {
        $csvFile = $_
        Write-Output "### $($csvFile.Name)"
        Write-Output '```'
        Get-Content $csvFile.FullName -TotalCount 2
        Write-Output '```'
        $lineCount = (Get-Content $csvFile.FullName).Count
        Write-Output "($lineCount rows)"
        Write-Output ""
    }
}

# --- Quick Health Check ---
Write-Output "## Quick Health"
Write-Output ""
Write-Output "### Potential Missing Methods on CharacterManager"
Write-Output '```'

# Find all methods called on CharacterManager.Instance
$calledMethods = @()
Get-ChildItem -Path $ScriptsDir -Filter "*.cs" -Recurse | ForEach-Object {
    $lines = Get-Content $_.FullName
    foreach ($line in $lines) {
        $methodMatches = [regex]::Matches($line, 'CharacterManager\.Instance\.(\w+)')
        foreach ($m in $methodMatches) {
            $calledMethods += $m.Groups[1].Value
        }
    }
}
$calledMethods = $calledMethods | Sort-Object -Unique

# Find all public methods defined in CharacterManager
$definedMethods = @()
Get-ChildItem -Path $ScriptsDir -Filter "CharacterManager.cs" -Recurse | ForEach-Object {
    $lines = Get-Content $_.FullName
    foreach ($line in $lines) {
        $defMatch = [regex]::Match($line, 'public\s+\S+\s+(\w+)\s*\(')
        if ($defMatch.Success) {
            $definedMethods += $defMatch.Groups[1].Value
        }
    }
}

# Compare
foreach ($method in $calledMethods) {
    if ($method -notin $definedMethods) {
        Write-Output "WARNING: CharacterManager.$method() called but not found as public method"
    }
}

$missingCount = ($calledMethods | Where-Object { $_ -notin $definedMethods }).Count
if ($missingCount -eq 0) {
    Write-Output 'All clear - no missing methods detected.'
}

Write-Output '```'
Write-Output ''
Write-Output '---'
Write-Output 'End of audit.'
