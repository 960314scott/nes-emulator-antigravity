# NES Emulator PowerShell Build Script
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  NES Emulator - PowerShell Build Script" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    Write-Host "[ERROR] C# Compiler (csc.exe) not found at: $csc" -ForegroundColor Red
    exit 1
}

Write-Host "Compiling NesEmulator.exe..." -ForegroundColor Yellow
$compileArgs = @(
    "/target:winexe",
    "/optimize+",
    "/r:System.Windows.Forms.dll",
    "/r:System.Drawing.dll",
    "/out:NesEmulator.exe",
    "/recurse:src\*.cs"
)

$process = Start-Process -FilePath $csc -ArgumentList $compileArgs -Wait -NoNewWindow -PassThru

if ($process.ExitCode -eq 0) {
    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host "  [SUCCESS] NesEmulator.exe built successfully!" -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host "Location: $PSScriptRoot\NesEmulator.exe" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[ERROR] Compilation failed with exit code $($process.ExitCode)" -ForegroundColor Red
}
