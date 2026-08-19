$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$bundledDotnet = Join-Path $repositoryRoot '..\.tools\dotnet\dotnet.exe'
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $bundledDotnet) {
    $dotnet = $bundledDotnet
}
elseif ($dotnetCommand) {
    $dotnet = $dotnetCommand.Source
}
else {
    throw '.NET 10 SDK was not found. Install it or place it in the workspace .tools/dotnet folder.'
}

Push-Location $repositoryRoot
try {
    $pythonLauncher = Get-Command py -ErrorAction SilentlyContinue
    if (-not $pythonLauncher) { throw 'Python Launcher was not found.' }
    & $pythonLauncher.Source -3.12 -m unittest tools.test_build_course_index
    if ($LASTEXITCODE -ne 0) { throw 'Course indexer tests failed.' }

    & $dotnet test StudyLens.sln --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }

    & $dotnet format StudyLens.sln --verify-no-changes
    if ($LASTEXITCODE -ne 0) { throw 'Backend formatting verification failed.' }

    Push-Location 'frontend/dashboard'
    try {
        if (-not (Test-Path -LiteralPath 'node_modules\.bin\oxlint.cmd')) {
            npm ci
            if ($LASTEXITCODE -ne 0) { throw 'Dashboard dependency installation failed.' }
        }
        npm run lint
        if ($LASTEXITCODE -ne 0) { throw 'Dashboard lint failed.' }
        npm test
        if ($LASTEXITCODE -ne 0) { throw 'Dashboard tests failed.' }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Dashboard build failed.' }
    }
    finally {
        Pop-Location
    }

    Push-Location 'frontend/extension'
    try {
        if (-not (Test-Path -LiteralPath 'node_modules\.bin\oxlint.cmd')) {
            npm ci
            if ($LASTEXITCODE -ne 0) { throw 'Extension dependency installation failed.' }
        }
        npm run lint
        if ($LASTEXITCODE -ne 0) { throw 'Extension lint failed.' }
        npm test
        if ($LASTEXITCODE -ne 0) { throw 'Extension tests failed.' }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Extension build failed.' }
    }
    finally {
        Pop-Location
    }

    Write-Host 'StudyLens verification passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
