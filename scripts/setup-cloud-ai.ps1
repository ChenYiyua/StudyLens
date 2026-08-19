param(
    [ValidateSet('Gemini', 'OpenAI')]
    [string]$Provider = 'Gemini'
)

$ErrorActionPreference = 'Stop'
$variableName = if ($Provider -eq 'Gemini') { 'GEMINI_API_KEY' } else { 'OPENAI_API_KEY' }
$secureKey = Read-Host "Paste the $Provider API key (input is hidden)" -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)

try {
    $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ([string]::IsNullOrWhiteSpace($plainKey)) {
        throw 'No API key was entered.'
    }

    [Environment]::SetEnvironmentVariable($variableName, $plainKey, 'User')
    Set-Item -LiteralPath "Env:$variableName" -Value $plainKey
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $plainKey = $null
}

Write-Host "$Provider is configured for StudyLens." -ForegroundColor Green
Write-Host 'Restart StudyLens so the new model becomes selectable.' -ForegroundColor DarkGray
