param(
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$service = Get-Service -Name MongoDB -ErrorAction SilentlyContinue

if (-not $service -and $Install) {
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw 'winget is required for automatic installation. Install MongoDB Community Server manually from mongodb.com.'
    }

    Write-Host 'Installing MongoDB Community Server...' -ForegroundColor Cyan
    & $winget.Source install `
        --id MongoDB.Server `
        --exact `
        --silent `
        --accept-package-agreements `
        --accept-source-agreements
    if ($LASTEXITCODE -ne 0) { throw 'MongoDB installation failed.' }
    $service = Get-Service -Name MongoDB -ErrorAction SilentlyContinue
}

if (-not $service) {
    throw 'MongoDB Community Server is not installed. Run this script again with -Install.'
}

if ($service.Status -ne 'Running') {
    try {
        Start-Service -Name MongoDB
    }
    catch {
        throw 'MongoDB is installed but could not be started. Run PowerShell as Administrator and try again.'
    }
}

$deadline = [DateTime]::UtcNow.AddSeconds(15)
do {
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $client.Connect('127.0.0.1', 27017)
        Write-Host 'MongoDB is ready at mongodb://127.0.0.1:27017.' -ForegroundColor Green
        exit 0
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
    finally {
        $client.Dispose()
    }
} while ([DateTime]::UtcNow -lt $deadline)

throw 'MongoDB service is running, but port 27017 did not become ready.'
