param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9][a-z0-9-]{1,63}$')]
    [string]$CourseId,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$CourseName,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$SourcePath,

    [switch]$MakeDefault
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$requirementsPath = Join-Path $repositoryRoot 'tools\requirements.txt'
$indexScriptPath = Join-Path $repositoryRoot 'tools\build_course_index.py'
$indexFileName = "$CourseId-course-index.json"
$indexPath = Join-Path $repositoryRoot "backend\StudyLens.Api\App_Data\$indexFileName"
$localSettingsPath = Join-Path $repositoryRoot 'backend\StudyLens.Api\appsettings.Local.json'

if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
    throw "Course material folder not found: $SourcePath"
}

$pythonLauncher = Get-Command py -ErrorAction SilentlyContinue
if (-not $pythonLauncher) {
    throw 'Python Launcher was not found. Install Python 3.12 and try again.'
}

& $pythonLauncher.Source -3.12 -m pip install -r $requirementsPath
if ($LASTEXITCODE -ne 0) { throw 'Python dependency installation failed.' }

& $pythonLauncher.Source -3.12 $indexScriptPath `
    --source $SourcePath `
    --output $indexPath `
    --course-id $CourseId `
    --course-name $CourseName
if ($LASTEXITCODE -ne 0) { throw 'Course material indexing failed.' }

$resolvedSourcePath = (Resolve-Path -LiteralPath $SourcePath).Path
$settings = @{}
if (Test-Path -LiteralPath $localSettingsPath) {
    $existing = Get-Content -Raw -LiteralPath $localSettingsPath | ConvertFrom-Json
    if ($existing) {
        foreach ($property in $existing.PSObject.Properties) {
            $settings[$property.Name] = $property.Value
        }
    }
}

$existingCourses = @()
if ($settings.ContainsKey('CourseCatalog') -and $settings['CourseCatalog'].Courses) {
    $existingCourses = @($settings['CourseCatalog'].Courses) |
        Where-Object { $_.Id -ne $CourseId }
}

$course = [ordered]@{
    Id = $CourseId
    IndexPath = "App_Data/$indexFileName"
    SourceRoot = $resolvedSourcePath
}
$allCourses = @($existingCourses) + @($course)
$defaultCourseId = if ($MakeDefault -or -not $settings.ContainsKey('CourseCatalog')) {
    $CourseId
}
else {
    $settings['CourseCatalog'].DefaultCourseId
}

$settings['CourseCatalog'] = [ordered]@{
    DefaultCourseId = $defaultCourseId
    Courses = $allCourses
}
$settings.Remove('CourseCorpus')
$settings | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $localSettingsPath -Encoding utf8

Write-Host "Course '$CourseName' is ready in StudyLens." -ForegroundColor Green
Write-Host "Index: $indexPath" -ForegroundColor DarkGray
