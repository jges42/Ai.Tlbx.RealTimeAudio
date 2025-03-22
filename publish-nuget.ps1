# Script to build NuGet packages with auto-incremented version numbers
# This script will:
# 1. Extract the current git hash
# 2. Increment the patch version number (1.0.x)
# 3. Build the packages with the version 1.0.x-g{githash}
# 4. If NUGET_API_KEY environment variable exists, upload packages to NuGet.org

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Create output directory if it doesn't exist
$nupkgDir = Join-Path $PSScriptRoot "nupkg"
if (-not (Test-Path $nupkgDir)) 
{
    New-Item -ItemType Directory -Path $nupkgDir | Out-Null
    Write-Host "Created nupkg directory: $nupkgDir"
}

# Get git hash
$gitHash = git rev-parse --short HEAD
if (-not $gitHash) 
{
    Write-Error "Could not get git hash. Make sure git is installed and this is a git repository."
    exit 1
}
Write-Host "Current git hash: $gitHash"

# Version file path
$versionFilePath = Join-Path $PSScriptRoot "version.txt"

# Initialize or read current version
if (Test-Path $versionFilePath) 
{
    $versionContent = Get-Content $versionFilePath
    $versionParts = $versionContent -split '\.'
    $major = [int]$versionParts[0]
    $minor = [int]$versionParts[1]
    $patch = [int]$versionParts[2]
    
    # Increment patch version
    $patch++
    
    $newVersion = "$major.$minor.$patch"
} 
else 
{
    # Default starting version
    $newVersion = "1.0.1"
}

# Format version with git hash as a prerelease identifier (SemVer compatible)
# Adding 'g' prefix to ensure the prerelease identifier doesn't start with a number
$fullVersion = "$newVersion-g$gitHash"

# Save the new version (without git hash)
$newVersion | Out-File -FilePath $versionFilePath -NoNewline
Write-Host "New version: $fullVersion"

# Check for NuGet API key in environment
$apiKey = $env:NUGET_API_KEY
if ($apiKey) 
{
    Write-Host "NUGET_API_KEY environment variable found. Will attempt to publish packages."
    $willPublish = $true
}
else 
{
    Write-Host "NUGET_API_KEY environment variable not found. Packages will be built but not published."
    $willPublish = $false
}

# Projects to build
$projects = @(
    "Provider\Ai.Tlbx.RealTimeAudio.OpenAi\Ai.Tlbx.RealTimeAudio.OpenAi.csproj",
    "Hardware\Ai.Tlbx.RealTimeAudio.Hardware.Windows\Ai.Tlbx.RealTimeAudio.Hardware.Windows.csproj"
)

$allPackagesSuccessful = $true
$publishedPackages = @()

# Build each project
foreach ($project in $projects) 
{
    $projectPath = Join-Path $PSScriptRoot $project
    $projectName = Split-Path $project -Leaf
    $projectName = $projectName -replace "\.csproj$", ""
    
    Write-Host "Building $projectName in $Configuration configuration..."
    
    # Clean and build the project
    dotnet clean $projectPath -c $Configuration
    
    # Update the version in the project file
    $projectContent = Get-Content $projectPath
    $updatedContent = $projectContent -replace "<Version>.*?</Version>", "<Version>$fullVersion</Version>"
    $updatedContent | Set-Content $projectPath
    
    # Make sure the project is restored first
    dotnet restore $projectPath
    
    # Build and pack the project
    dotnet build $projectPath -c $Configuration
    
    Write-Host "Packing $projectName..."
    dotnet pack $projectPath -c $Configuration --no-build
    
    # Find the generated package
    $packagePattern = Join-Path $nupkgDir "$projectName.*.nupkg"
    $packageFiles = Get-ChildItem -Path $packagePattern | Sort-Object LastWriteTime -Descending
    
    if ($packageFiles.Count -eq 0) 
    {
        Write-Error "No package found for $projectName"
        $allPackagesSuccessful = $false
        continue
    }
    
    $package = $packageFiles[0]
    Write-Host "Package created: $($package.Name)"
    
    # Publish the package if API key is available
    if ($willPublish) 
    {
        Write-Host "Publishing $($package.Name) to NuGet.org..."
        try 
        {
            # Add --skip-duplicate to avoid errors when re-publishing the same version
            # Removing the default behavior of unlisted packages by NOT using --no-service-endpoint
            $pushResult = dotnet nuget push $package.FullName --api-key $apiKey --source https://api.nuget.org/v3/index.json --skip-duplicate
            
            # Check if the push was successful
            if ($LASTEXITCODE -eq 0) 
            {
                Write-Host "Package $($package.Name) published successfully" -ForegroundColor Green
                $publishedPackages += $package.Name
            } 
            else 
            {
                Write-Host "Failed to publish package $($package.Name)" -ForegroundColor Red
                $allPackagesSuccessful = $false
            }
        }
        catch 
        {
            Write-Host "Exception while publishing package $($package.Name): $_" -ForegroundColor Red
            $allPackagesSuccessful = $false
        }
    }
    
    Write-Host ""
}

Write-Host "Package building completed."
Write-Host "NuGet packages are available in: $nupkgDir"

if ($willPublish) 
{
    if ($publishedPackages.Count -gt 0) 
    {
        Write-Host "Successfully published packages:" -ForegroundColor Green
        foreach ($pkgName in $publishedPackages) 
        {
            Write-Host "  - $pkgName" -ForegroundColor Green
        }
    }
    
    if (-not $allPackagesSuccessful) 
    {
        Write-Host "Some packages were not published successfully. See errors above." -ForegroundColor Red
        exit 1
    }
    else 
    {
        Write-Host "All packages were published successfully." -ForegroundColor Green
    }
} 