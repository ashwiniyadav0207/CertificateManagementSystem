<#
.SYNOPSIS
    Convenience wrapper to run the Certificate Authority (CaTool) generator.
.DESCRIPTION
    Runs the built-in CertificateEngine.CaTool to generate root-ca.pem, root-ca.pfx, and signing.pfx.
.EXAMPLE
    .\tools\generate-ca.ps1 -Organization "My NGO" -Force
#>
param(
    [string]$Organization = "My Organization",
    [string]$OutDir = "data/ca",
    [int]$RootYears = 20,
    [int]$SigningYears = 2,
    [string]$RootPassword = "",
    [string]$SigningPassword = "",
    [switch]$Force
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$caToolExe = Join-Path $scriptDir "CertificateEngine.CaTool\bin\Release\net8.0\win-x64\CertificateEngine.CaTool.exe"

if (-not (Test-Path $caToolExe)) {
    Write-Host "Compiling / checking CaTool..." -ForegroundColor Cyan
    # Fallback to dotnet run if pre-built exe is missing
    $cmd = "dotnet run --project `"$scriptDir\CertificateEngine.CaTool`" -- init --organization `"$Organization`" --out `"$OutDir`" --root-years $RootYears --signing-years $SigningYears"
    if ($RootPassword) { $cmd += " --root-password `"$RootPassword`"" }
    if ($SigningPassword) { $cmd += " --signing-password `"$SigningPassword`"" }
    if ($Force) { $cmd += " --force" }
    Invoke-Expression $cmd
    return
}

$argsList = @("init", "--organization", $Organization, "--out", $OutDir, "--root-years", $RootYears, "--signing-years", $SigningYears)
if ($RootPassword) { $argsList += @("--root-password", $RootPassword) }
if ($SigningPassword) { $argsList += @("--signing-password", $SigningPassword) }
if ($Force) { $argsList += "--force" }

Write-Host "Running CaTool: $caToolExe $argsList" -ForegroundColor Cyan
& $caToolExe $argsList
