$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendDir = Join-Path $Root 'backend\AiDailyExpense.Api'
$ConstantsFile = Join-Path $Root 'mobile\AiDailyExpense.Mobile\src\utils\constants.ts'
$LogDir = Join-Path $Root '.runtime'
$ApiPort = 5000
$BackendUrl = "http://127.0.0.1:$ApiPort"
$BackendLog = Join-Path $LogDir 'backend.log'
$BackendErr = Join-Path $LogDir 'backend.err.log'
$TunnelLog = Join-Path $LogDir 'tunnel.log'
$TunnelErr = Join-Path $LogDir 'tunnel.err.log'
$BundledCloudflared = Join-Path $LogDir 'cloudflared.exe'

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
Remove-Item -LiteralPath $TunnelLog, $TunnelErr -Force -ErrorAction SilentlyContinue

function Test-PortOpen {
  param([int] $Port)

  $line = netstat -ano | Select-String -Pattern ":$Port\s+.*LISTENING" | Select-Object -First 1
  return $null -ne $line
}

function Wait-ForBackend {
  for ($i = 0; $i -lt 45; $i++) {
    if (Test-PortOpen -Port $ApiPort) {
      return
    }
    Start-Sleep -Seconds 1
  }

  throw "Backend chưa mở cổng $ApiPort. Xem log: $BackendErr"
}

function Wait-ForTunnelUrl {
  param(
    [string] $Pattern,
    [string] $Name
  )

  for ($i = 0; $i -lt 60; $i++) {
    $text = ''
    if (Test-Path $TunnelLog) { $text += Get-Content -Raw $TunnelLog -ErrorAction SilentlyContinue }
    if (Test-Path $TunnelErr) { $text += "`n" + (Get-Content -Raw $TunnelErr -ErrorAction SilentlyContinue) }

    $match = [regex]::Match($text, $Pattern)
    if ($match.Success) {
      return $match.Value.TrimEnd('/')
    }

    Start-Sleep -Seconds 1
  }

  throw "Không lấy được link $Name. Xem log: $TunnelLog và $TunnelErr"
}

function Update-MobileApiUrl {
  param([string] $PublicBaseUrl)

  $apiUrl = "$PublicBaseUrl/api"
  $content = Get-Content -Raw $ConstantsFile
  $updated = [regex]::Replace(
    $content,
    "const LOCAL_API_URL = '.*?';",
    "const LOCAL_API_URL = '$apiUrl';"
  )
  Set-Content -LiteralPath $ConstantsFile -Value $updated -Encoding UTF8
  return $apiUrl
}

Write-Host ''
Write-Host '=== AiDailyExpense public API ===' -ForegroundColor Cyan
Write-Host "Backend local: $BackendUrl"

if (-not (Test-PortOpen -Port $ApiPort)) {
  Write-Host 'Đang bật backend...' -ForegroundColor Yellow
  Start-Process `
    -WindowStyle Hidden `
    -FilePath 'dotnet' `
    -ArgumentList @('run', '--urls', "http://0.0.0.0:$ApiPort") `
    -WorkingDirectory $BackendDir `
    -RedirectStandardOutput $BackendLog `
    -RedirectStandardError $BackendErr
} else {
  Write-Host "Backend đã chạy ở cổng $ApiPort." -ForegroundColor Green
}

Wait-ForBackend

$cloudflaredCommand = Get-Command cloudflared -ErrorAction SilentlyContinue
$localCloudflared = 'C:\tmp\cloudflared.exe'
$tunnelProcess = $null
$publicBaseUrl = $null

if ($cloudflaredCommand -or (Test-Path $BundledCloudflared) -or (Test-Path $localCloudflared)) {
  $cloudflaredPath = if ($cloudflaredCommand) { $cloudflaredCommand.Source } elseif (Test-Path $BundledCloudflared) { $BundledCloudflared } else { $localCloudflared }
  Write-Host 'Đang tạo Cloudflare Tunnel...' -ForegroundColor Yellow
  $tunnelProcess = Start-Process `
    -WindowStyle Hidden `
    -FilePath $cloudflaredPath `
    -ArgumentList @('tunnel', '--url', $BackendUrl) `
    -RedirectStandardOutput $TunnelLog `
    -RedirectStandardError $TunnelErr `
    -PassThru
  $publicBaseUrl = Wait-ForTunnelUrl -Pattern 'https://[a-zA-Z0-9-]+\.trycloudflare\.com' -Name 'Cloudflare'
} else {
  Write-Host 'Chưa có cloudflared, dùng Serveo qua SSH...' -ForegroundColor Yellow
  $tunnelProcess = Start-Process `
    -WindowStyle Hidden `
    -FilePath 'ssh' `
    -ArgumentList @('-o', 'StrictHostKeyChecking=no', '-R', "80:127.0.0.1:$ApiPort", 'serveo.net') `
    -RedirectStandardOutput $TunnelLog `
    -RedirectStandardError $TunnelErr `
    -PassThru
  $publicBaseUrl = Wait-ForTunnelUrl -Pattern 'https://[a-zA-Z0-9-]+\.serveousercontent\.com' -Name 'Serveo'
}

$apiUrl = Update-MobileApiUrl -PublicBaseUrl $publicBaseUrl

Write-Host ''
Write-Host 'Link API đã được cập nhật cho app mobile:' -ForegroundColor Green
Write-Host $apiUrl -ForegroundColor Green
Write-Host ''
Write-Host "File đã đổi: $ConstantsFile"
Write-Host 'Giữ cửa sổ này mở để đường dẫn tiếp tục hoạt động. Nhấn Ctrl+C để tắt tunnel.' -ForegroundColor Yellow
Write-Host ''

Wait-Process -Id $tunnelProcess.Id
