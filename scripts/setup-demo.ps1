$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourcePath = Join-Path $repositoryRoot 'samples\demo-course'

& (Join-Path $PSScriptRoot 'setup-course.ps1') `
    -CourseId 'studylens-demo' `
    -CourseName 'AI-Assisted Learning Demo' `
    -SourcePath $sourcePath

if ($LASTEXITCODE -ne 0) {
    throw 'Demo course setup failed.'
}
