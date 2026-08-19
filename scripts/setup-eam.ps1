param(
    [string]$SourcePath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $courseFolderName = 'Enterprise Architecture Management and Reference Models (INHN0017)'
    $SourcePath = Get-ChildItem -LiteralPath 'D:\' -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName $courseFolderName } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Container } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    throw 'EAM material folder was not found. Pass it with -SourcePath.'
}

& (Join-Path $PSScriptRoot 'setup-course.ps1') `
    -CourseId 'tum-inhn0017-eam' `
    -CourseName 'Enterprise Architecture Management and Reference Models' `
    -SourcePath $SourcePath `
    -MakeDefault
