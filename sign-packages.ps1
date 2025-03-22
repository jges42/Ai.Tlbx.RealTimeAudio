# Script to sign NuGet packages
# This script requires:
# 1. A code signing certificate (*.pfx file)
# 2. NuGet.exe to be installed and available in PATH

param(
    [Parameter(Mandatory=$true)]
    [string]$CertificatePath,
    
    [Parameter(Mandatory=$true)]
    [string]$CertificatePassword,
    
    [Parameter(Mandatory=$false)]
    [string]$TimestampServer = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

# Find NuGet packages
$nupkgDir = Join-Path $PSScriptRoot "nupkg"
if (-not (Test-Path $nupkgDir)) 
{
    Write-Error "NuGet package directory not found at $nupkgDir. Run publish-nuget.ps1 first."
    exit 1
}

$packages = Get-ChildItem -Path $nupkgDir -Filter "*.nupkg"
if ($packages.Count -eq 0) 
{
    Write-Error "No NuGet packages found in $nupkgDir"
    exit 1
}

Write-Host "Found $($packages.Count) packages to sign"

# Check if nuget.exe is available
try 
{
    $nugetVersion = & nuget help | Select-String "NuGet Version" | ForEach-Object { $_.ToString().Trim() }
    Write-Host "Using $nugetVersion"
}
catch 
{
    Write-Error "nuget.exe not found in PATH. Please install NuGet CLI from https://www.nuget.org/downloads"
    exit 1
}

# Sign each package
foreach ($package in $packages) 
{
    Write-Host "Signing package: $($package.Name)"
    
    try 
    {
        # Sign the package using nuget sign
        & nuget sign $package.FullName -CertificatePath $CertificatePath -CertificatePassword $CertificatePassword -Timestamper $TimestampServer
        
        if ($LASTEXITCODE -eq 0) 
        {
            Write-Host "Successfully signed $($package.Name)" -ForegroundColor Green
        }
        else 
        {
            Write-Error "Failed to sign $($package.Name)"
        }
    }
    catch 
    {
        Write-Error "Exception while signing $($package.Name): $_"
    }
}

Write-Host "Package signing complete."
Write-Host "To publish signed packages, run publish-nuget.ps1" 