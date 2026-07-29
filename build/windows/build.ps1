# =========================================================================
# Connecting Remote Desktop - Windows Open Source Build Script
# =========================================================================

$ErrorActionPreference = "Stop"

$CscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$SourceFile = "ConnectingApp.cs"
$OutputExe = "Connecting.exe"
$ManifestFile = "Connecting.manifest"
$IconFile = "icon.ico"
$RcFile = "Connecting.rc"
$ResFile = "Connecting.res"

Write-Host ""
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  CONNECTING REMOTE DESKTOP - WINDOWS BUILD" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $CscPath)) {
    Write-Host "[ERROR] csc.exe not found at $CscPath" -ForegroundColor Red
    Write-Host "        .NET Framework 4.8 is required." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $SourceFile)) {
    Write-Host "[ERROR] Source file $SourceFile not found in current directory." -ForegroundColor Red
    exit 1
}

$RcExePath = $null
$SdkBinPath = "C:\Program Files (x86)\Windows Kits\10\bin"
if (Test-Path $SdkBinPath) {
    $versions = Get-ChildItem $SdkBinPath -Directory | Sort-Object Name -Descending
    foreach ($ver in $versions) {
        $candidate = Join-Path $ver.FullName "x64\rc.exe"
        if (Test-Path $candidate) {
            $RcExePath = $candidate
            break
        }
    }
}

$useNativeRes = $false

if ($RcExePath -and (Test-Path $RcFile)) {
    Write-Host "[+] Found Windows SDK Resource Compiler: $RcExePath" -ForegroundColor Green
    Write-Host "[+] Compiling $RcFile -> $ResFile ..." -ForegroundColor Yellow
    
    & $RcExePath /fo $ResFile $RcFile
    
    if ($LASTEXITCODE -eq 0 -and (Test-Path $ResFile)) {
        Write-Host "[+] Resource file compiled successfully." -ForegroundColor Green
        $useNativeRes = $true
    } else {
        Write-Host "[!] rc.exe failed. Falling back to manifest-only mode." -ForegroundColor Yellow
        $useNativeRes = $false
    }
} else {
    Write-Host "[!] Windows SDK (rc.exe) not found. Using manifest-only mode." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[+] Compiling $SourceFile -> $OutputExe ..." -ForegroundColor Yellow

if ($useNativeRes) {
    & $CscPath /target:winexe /out:$OutputExe /win32res:$ResFile /unsafe `
        /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll `
        $SourceFile
} else {
    $cscArgs = @("/target:winexe", "/out:$OutputExe", "/unsafe")
    
    if (Test-Path $ManifestFile) {
        $cscArgs += "/win32manifest:$ManifestFile"
        Write-Host "[+] Embedding UAC manifest: $ManifestFile" -ForegroundColor Green
    }
    
    if (Test-Path $IconFile) {
        $cscArgs += "/win32icon:$IconFile"
        Write-Host "[+] Embedding icon: $IconFile" -ForegroundColor Green
    }
    
    $cscArgs += "/r:System.dll,System.Drawing.dll,System.Windows.Forms.dll"
    $cscArgs += $SourceFile
    
    & $CscPath @cscArgs
}

Write-Host ""

if ($LASTEXITCODE -eq 0 -and (Test-Path $OutputExe)) {
    $fileInfo = Get-Item $OutputExe
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "  [OK] BUILD SUCCESSFUL" -ForegroundColor Green
    Write-Host "  Output: $($fileInfo.FullName)" -ForegroundColor Green
    Write-Host "  Size:   $([math]::Round($fileInfo.Length / 1KB, 1)) KB" -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green

    # ---- CODE SIGNING ----
    $SigningPfx = Join-Path $PSScriptRoot "certs\ConnectingSigning.pfx"
    $GenCertScript = Join-Path $PSScriptRoot "generate-certs.ps1"
    $SignerPassword = "ConnectingSigner2026"

    if (-not (Test-Path $SigningPfx) -and (Test-Path $GenCertScript)) {
        Write-Host ""
        Write-Host "[+] Generating local code signing certificate..." -ForegroundColor Yellow
        & $GenCertScript
    }

    if (Test-Path $SigningPfx) {
        Write-Host ""
        Write-Host "[+] Signing $OutputExe with local certificate..." -ForegroundColor Yellow

        $ThumbPath = Join-Path $PSScriptRoot "certs\signer_thumbprint.txt"
        $cert = $null
        if (Test-Path $ThumbPath) {
            $tp = (Get-Content $ThumbPath).Trim()
            if (Test-Path "Cert:\CurrentUser\My\$tp") {
                $cert = Get-Item "Cert:\CurrentUser\My\$tp"
            } elseif (Test-Path "Cert:\CurrentUser\Root\$tp") {
                $cert = Get-Item "Cert:\CurrentUser\Root\$tp"
            }
        }

        if (-not $cert) {
            $pfxPass = ConvertTo-SecureString -String $SignerPassword -Force -AsPlainText
            $cert = Import-PfxCertificate -FilePath $SigningPfx -CertStoreLocation "Cert:\CurrentUser\My" -Password $pfxPass
        }

        if ($cert) {
            $result = Set-AuthenticodeSignature -FilePath $OutputExe -Certificate $cert -TimestampServer "http://timestamp.digicert.com" -HashAlgorithm SHA256
            Write-Host ""
            if ($result.Status -eq "Valid") {
                Write-Host "=================================================" -ForegroundColor Green
                Write-Host "  [OK] EXECUTABLE SIGNED SUCCESSFULLY (VALID)" -ForegroundColor Green
                Write-Host "  Signer: $($cert.Subject)" -ForegroundColor Green
                Write-Host "=================================================" -ForegroundColor Green
            } else {
                Write-Host "=================================================" -ForegroundColor Green
                Write-Host "  [OK] EXECUTABLE SIGNED SUCCESSFULLY (Self-Signed)" -ForegroundColor Green
                Write-Host "  Signer: $($cert.Subject)" -ForegroundColor Green
                Write-Host "  Digital signature & DigiCert timestamp embedded." -ForegroundColor Gray
                Write-Host "=================================================" -ForegroundColor Green
            }
        } else {
            Write-Host "[!] Unable to load signing certificate." -ForegroundColor Red
        }
    }
} else {
    Write-Host "[ERROR] Compilation failed." -ForegroundColor Red
    exit 1
}
