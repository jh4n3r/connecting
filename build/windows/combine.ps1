$srcDir = "C:\Users\gea\Desktop\Desk\Proyectos\conecting\local-nogit\windows\src"
$target = "C:\Users\gea\Desktop\Desk\Proyectos\conecting\build\windows\ConnectingApp.cs"

$files = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | Select-Object -ExpandProperty FullName

$usings = New-Object System.Collections.Generic.HashSet[string]
$codeBlocks = @()

foreach ($file in $files) {
    $lines = Get-Content -Path $file
    $body = @()
    foreach ($line in $lines) {
        if ($line -match "^using\s+[\w\.]+;") {
            $null = $usings.Add($line.Trim())
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
