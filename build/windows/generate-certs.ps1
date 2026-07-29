# =========================================================================
# Connecting Remote Desktop - Open Source Code Signing Certificate Generator
# =========================================================================

$ErrorActionPreference = "Stop"

$CertDir = Join-Path $PSScriptRoot "certs"
$SignerName = "Connecting Remote Desktop"
$SignerPassword = "ConnectingSigner2026"
$ValidYears = 5

Write-Host ""
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  CONNECTING - CODE SIGNING CERTIFICATE SETUP" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $CertDir)) {
    New-Item -ItemType Directory -Path $CertDir -Force | Out-Null
}

Write-Host "[1/3] Creating Direct Code Signing certificate..." -ForegroundColor Yellow

$signerCert = New-SelfSignedCertificate `
    -Subject "CN=$SignerName, O=Connecting Open Source Project" `
    -Type CodeSigningCert `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears($ValidYears) `
    -FriendlyName $SignerName

Write-Host "    Thumbprint: $($signerCert.Thumbprint)" -ForegroundColor Gray

Write-Host "[2/3] Installing Certificate into Personal Certificate Store..." -ForegroundColor Yellow
$cerPath = Join-Path $CertDir "ConnectingCS.cer"
Export-Certificate -Cert $signerCert -FilePath $cerPath | Out-Null
Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\My" -ErrorAction SilentlyContinue | Out-Null

Write-Host "[3/3] Exporting PFX for build script..." -ForegroundColor Yellow
$signerPfxPath = Join-Path $CertDir "ConnectingSigning.pfx"
Export-PfxCertificate -Cert $signerCert -FilePath $signerPfxPath `
    -Password (ConvertTo-SecureString -String $SignerPassword -Force -AsPlainText) | Out-Null

$thumbprintPath = Join-Path $CertDir "signer_thumbprint.txt"
$signerCert.Thumbprint | Out-File -FilePath $thumbprintPath -Encoding ASCII -NoNewline

Write-Host ""
Write-Host "=================================================================" -ForegroundColor Green
Write-Host "  [OK] CERTIFICATE INSTALLED & READY" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
Write-Host ""
