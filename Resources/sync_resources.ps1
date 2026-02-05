# 경로 설정
$baseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$enPath = Join-Path $baseDir "Strings.en.resx"
$jaPath = Join-Path $baseDir "Strings.ja.resx"
$outputPath = Join-Path $baseDir "missing_ja.txt"

if (-not (Test-Path $enPath)) { Write-Error "en.resx not found"; exit }
if (-not (Test-Path $jaPath)) { Write-Error "ja.resx not found"; exit }

Write-Host "Comparing: en vs ja..." -ForegroundColor Cyan

# UTF-8로 읽기
$enContent = Get-Content $enPath -Raw -Encoding UTF8
$jaContent = Get-Content $jaPath -Raw -Encoding UTF8

# 모든 키 추출
$enKeys = [regex]::Matches($enContent, '<data name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$jaKeys = [regex]::Matches($jaContent, '<data name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }

# 누락된 키 찾기
$missingKeys = $enKeys | Where-Object { $jaKeys -notcontains $_ }

if ($null -eq $missingKeys -or $missingKeys.Count -eq 0) {
    Write-Host "All resources are in sync!" -ForegroundColor Green
    exit
}

Write-Host "Found $($missingKeys.Count) missing resources." -ForegroundColor Yellow

# 각 누락된 키에 대해 전체 <data> 블록 추출
$results = @()
foreach ($key in $missingKeys) {
    # 해당 키의 <data> 블록 전체를 찾기 (멀티라인 지원)
    $escapedKey = [regex]::Escape($key)
    $pattern = "(?ms)^(\s*<data name=`"$escapedKey`".*?</data>)"
    
    if ($enContent -match $pattern) {
        $results += $Matches[1]
    }
}

# 결과를 파일에 저장 (UTF8, BOM 없음)
$results -join "`r`n" | Out-File -FilePath $outputPath -Encoding utf8 -NoNewline
Write-Host "Successfully created: $outputPath" -ForegroundColor Green
Write-Host "------------------------------"
foreach ($k in $missingKeys) { Write-Host " [+] $k" }
