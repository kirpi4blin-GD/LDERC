$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceFile = Join-Path $projectRoot "LocalDollarExchangeRateChecker.cs"
$iconFile = Join-Path $projectRoot "LocalDollarExchangeRateChecker.ico"
$outputFile = Join-Path $projectRoot "LocalDollarExchangeRateChecker.exe"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $compiler)) {
    $compiler = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $compiler)) {
    throw "C# compiler was not found."
}

& $compiler `
    /nologo `
    /codepage:65001 `
    /target:winexe `
    /out:$outputFile `
    /win32icon:$iconFile `
    /r:System.dll `
    /r:System.Drawing.dll `
    /r:System.Net.Http.dll `
    /r:System.Web.Extensions.dll `
    /r:System.Windows.Forms.dll `
    $sourceFile

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Write-Host "Built $outputFile"
