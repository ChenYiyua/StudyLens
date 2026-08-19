$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$bundledDotnet = Join-Path $repositoryRoot '..\.tools\dotnet\dotnet.exe'
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dashboardPath = Join-Path $repositoryRoot 'frontend\dashboard'
$applicationUrl = 'http://127.0.0.1:5080'
$requirementsPath = Join-Path $repositoryRoot 'tools\requirements.txt'
$localSettingsPath = Join-Path $repositoryRoot 'backend\StudyLens.Api\appsettings.Local.json'
$studyDataProvider = 'MongoDb'
if (Test-Path -LiteralPath $localSettingsPath) {
    $localSettings = Get-Content -Raw -LiteralPath $localSettingsPath | ConvertFrom-Json
    if ($localSettings.StudyData.Provider) {
        $studyDataProvider = $localSettings.StudyData.Provider
    }
}

if ($studyDataProvider -eq 'MongoDb') {
    $mongoService = Get-Service -Name MongoDB -ErrorAction SilentlyContinue
    if (-not $mongoService -or $mongoService.Status -ne 'Running') {
        throw 'MongoDB is required and is not running. Run scripts\setup-mongodb.ps1 (use -Install if MongoDB is missing).'
    }
}

if (Test-Path -LiteralPath $bundledDotnet) {
    $dotnet = $bundledDotnet
}
elseif ($dotnetCommand) {
    $dotnet = $dotnetCommand.Source
}
else {
    throw '.NET 10 SDK was not found.'
}

$pythonLauncher = Get-Command py -ErrorAction SilentlyContinue
if (-not $pythonLauncher) {
    throw 'Python Launcher was not found. Install Python 3.12 and try again.'
}
& $pythonLauncher.Source -3.12 -c 'import pypdf; import pypdfium2; import PIL' 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Installing the local PDF indexing and page-preview tools...' -ForegroundColor Yellow
    & $pythonLauncher.Source -3.12 -m pip install -r $requirementsPath
    if ($LASTEXITCODE -ne 0) { throw 'Python dependency installation failed.' }
}

Push-Location $dashboardPath
try {
    if (-not (Test-Path -LiteralPath 'node_modules')) {
        npm ci
        if ($LASTEXITCODE -ne 0) { throw 'Dashboard dependency installation failed.' }
    }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Dashboard build failed.' }
}
finally {
    Pop-Location
}

$browserJob = Start-Job -ScriptBlock {
    param($Url)
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            Invoke-WebRequest -UseBasicParsing "$Url/health" -TimeoutSec 1 | Out-Null
            Start-Process $Url
            return
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }
} -ArgumentList $applicationUrl

Write-Host "Starting StudyLens at $applicationUrl" -ForegroundColor Green
Write-Host 'Keep this window open. Press Ctrl+C to stop the app.' -ForegroundColor DarkGray

Push-Location $repositoryRoot
try {
    & $dotnet run --project backend\StudyLens.Api
}
finally {
    Pop-Location
    Stop-Job $browserJob -ErrorAction SilentlyContinue
    Remove-Job $browserJob -Force -ErrorAction SilentlyContinue
}
