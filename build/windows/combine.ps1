$srcDir = "$PSScriptRoot\src"
$target = "$PSScriptRoot\ConnectingApp.cs"

$files = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | Select-Object -ExpandProperty FullName

$usings = New-Object System.Collections.Generic.HashSet[string]
$codeBlocks = @()

foreach ($file in $files) {
    $rawText = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
    $lines = $rawText -split "\r?\n"
    $body = @()
    foreach ($line in $lines) {
        if ($line -match "^using\s+[\w\.]+;") {
            $u = $line.Trim()
            if (-not ($u.StartsWith("using Conecting"))) {
                $null = $usings.Add($u)
            }
        } else {
            $body += $line
        }
    }
    $codeBlocks += ($body -join "`n")
}

$header = ($usings | Sort-Object) -join "`n"
$combined = $header + "`n`n" + ($codeBlocks -join "`n`n")
$combined = $combined.Replace("labs.connecting.gaiatech.com.py", "your-relay-server.com")

[System.IO.File]::WriteAllText($target, $combined, [System.Text.Encoding]::UTF8)
Write-Host "Successfully generated build/windows/ConnectingApp.cs"
