param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot "HearthCodex.csproj"
[xml]$project = Get-Content -LiteralPath $projectFile
$version = [string]$project.Project.PropertyGroup.Version
$packageSource = Join-Path $projectRoot "package"
$dist = Join-Path $projectRoot "dist"
$stage = Join-Path $dist "package\HearthCodex"
$archive = Join-Path $dist ("Blackhearthx-HearthCodex-" + $version + ".zip")

dotnet build $projectFile -t:Rebuild -c $Configuration --no-restore "-p:MOD_DEPLOYPATH=$dist" -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw "HearthCodex build failed." }

$manifest = Get-Content -LiteralPath (Join-Path $packageSource "manifest.json") -Raw | ConvertFrom-Json
if ($manifest.version_number -ne $version) {
    throw "Manifest version $($manifest.version_number) does not match project version $version."
}

if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item -LiteralPath (Join-Path $dist "HearthCodex.dll") -Destination $stage
Copy-Item -LiteralPath (Join-Path $packageSource "manifest.json") -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot "CHANGELOG.md") -Destination $stage
Copy-Item -LiteralPath (Join-Path $packageSource "icon.png") -Destination $stage

if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $archive -CompressionLevel Optimal
Write-Host "Created $archive"
