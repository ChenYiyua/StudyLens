param(
    [string]$Model = 'qwen3.5:4b',
    [string]$ModelRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ModelRoot)) {
    $ModelRoot = if (Test-Path -LiteralPath 'D:\') {
        'D:\AIModels\Ollama'
    }
    else {
        Join-Path $env:LOCALAPPDATA 'OllamaModels'
    }
}
New-Item -ItemType Directory -Force -Path $modelRoot | Out-Null
[Environment]::SetEnvironmentVariable('OLLAMA_MODELS', $modelRoot, 'User')
$env:OLLAMA_MODELS = $modelRoot

$ollamaCommand = Get-Command ollama -ErrorAction SilentlyContinue
if (-not $ollamaCommand) {
    $knownLocations = @(
        'D:\Programs\Ollama\ollama.exe',
        (Join-Path $env:LOCALAPPDATA 'Programs\Ollama\ollama.exe')
    )
    $ollamaPath = $knownLocations | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $ollamaPath) {
        throw 'Ollama is not installed. Install it from https://ollama.com/download/windows first.'
    }
}
else {
    $ollamaPath = $ollamaCommand.Source
}

try {
    Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:11434/api/version' -TimeoutSec 2 | Out-Null
}
catch {
    Start-Process -FilePath $ollamaPath -ArgumentList 'serve' -WindowStyle Hidden
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Milliseconds 500
        try {
            Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:11434/api/version' -TimeoutSec 2 | Out-Null
            break
        }
        catch {
            if ($attempt -eq 29) { throw 'Ollama did not start.' }
        }
    }
}

Write-Host "Downloading local model $Model to $modelRoot ..." -ForegroundColor Cyan
& $ollamaPath pull $Model
if ($LASTEXITCODE -ne 0) { throw "Model download failed: $Model" }

$testBody = @{
    model = $Model
    stream = $false
    think = $false
    messages = @(@{ role = 'user'; content = 'Reply with exactly: StudyLens ready' })
} | ConvertTo-Json -Depth 5
$test = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:11434/api/chat' `
    -ContentType 'application/json' -Body $testBody -TimeoutSec 300

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$localSettingsPath = Join-Path $repositoryRoot 'backend\StudyLens.Api\appsettings.Local.json'
$settings = @{}
if (Test-Path -LiteralPath $localSettingsPath) {
    $existing = Get-Content -Raw -LiteralPath $localSettingsPath | ConvertFrom-Json
    if ($existing) {
        foreach ($property in $existing.PSObject.Properties) {
            $settings[$property.Name] = $property.Value
        }
    }
}
$settings['TutorAi'] = [ordered]@{
    Endpoint = 'http://127.0.0.1:11434'
    Model = $Model
    TimeoutSeconds = 300
    ContextLength = 4096
}
$settings | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $localSettingsPath -Encoding utf8

Write-Host "Local AI is ready: $($test.message.content)" -ForegroundColor Green
