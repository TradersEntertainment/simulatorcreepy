# loop.ps1 — build, launch, play, capture. The agent runs this; the human never has to.
#
#   .\agent\loop.ps1 -Scenario smoke
#   .\agent\loop.ps1 -Scenario turns40 -SkipBuild
#
# Everything lands under agent/: logs/, shots/, build/. Exit code is non-zero if the build
# or the scenario failed, so the agent can react without reading prose.

param(
    [string] $Scenario = "smoke",
    [switch] $SkipBuild,
    [int]    $Port = 8787
)

$ErrorActionPreference = "Stop"
$root  = Split-Path -Parent $PSScriptRoot
$agent = Join-Path $root "agent"
$game  = Join-Path $root "Game"

# Adjust if your Unity version differs — Hub installs under Editor\<version>\Editor\Unity.exe
$UnityExe = (Get-ChildItem "C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe" |
             Sort-Object FullName -Descending | Select-Object -First 1).FullName
if (-not $UnityExe) { throw "Unity bulunamadı. loop.ps1 içindeki `$UnityExe yolunu elle ayarla." }

New-Item -ItemType Directory -Force -Path "$agent\logs","$agent\shots" | Out-Null

# ---------------------------------------------------------------- build
if (-not $SkipBuild) {
    Write-Host "[loop] derleniyor..." -ForegroundColor Cyan
    $buildLog = "$agent\logs\build.log"
    & $UnityExe -batchmode -quit -nographics `
        -projectPath $game `
        -executeMethod Mesruiyet.EditorTools.BuildScript.Windows `
        -logFile $buildLog
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[loop] DERLEME HATASI — son satırlar:" -ForegroundColor Red
        Get-Content $buildLog -Tail 40
        exit 1
    }
}

$exe = "$agent\build\StandaloneWindows64\Mesruiyet.exe"
if (-not (Test-Path $exe)) { throw "Build çıktısı yok: $exe" }

# ---------------------------------------------------------------- launch
Write-Host "[loop] oyun başlatılıyor..." -ForegroundColor Cyan
$playerLog = "$agent\logs\player.log"
$proc = Start-Process $exe -PassThru -ArgumentList @(
    "-logFile", $playerLog, "-screen-width", "1600", "-screen-height", "900", "-screen-fullscreen", "0"
)

function Send-Cmd([string] $json) {
    $client = New-Object System.Net.Sockets.TcpClient
    $client.Connect("127.0.0.1", $Port)
    $stream = $client.GetStream()
    $writer = New-Object System.IO.StreamWriter($stream); $writer.AutoFlush = $true
    $reader = New-Object System.IO.StreamReader($stream)
    $writer.WriteLine($json)
    $reply = $reader.ReadLine()
    $client.Close()
    return $reply
}

# wait for AgentBridge to come up
$ready = $false
foreach ($i in 1..40) {
    Start-Sleep -Milliseconds 500
    try { if ((Send-Cmd '{"cmd":"ping"}') -match '"ok":true') { $ready = $true; break } } catch { }
}
if (-not $ready) {
    Write-Host "[loop] AgentBridge cevap vermedi — player.log son satırlar:" -ForegroundColor Red
    if (Test-Path $playerLog) { Get-Content $playerLog -Tail 40 }
    try { $proc.Kill() } catch { }
    exit 2
}
Write-Host "[loop] köprü hazır" -ForegroundColor Green

# ---------------------------------------------------------------- scenarios
switch ($Scenario) {
    "smoke" {
        Send-Cmd '{"cmd":"shot","path":"agent/shots/01-acilis.png"}' | Out-Null
        Start-Sleep -Seconds 1
        Send-Cmd '{"cmd":"endturn","n":3}' | Out-Null
        Start-Sleep -Seconds 3
        Send-Cmd '{"cmd":"shot","path":"agent/shots/02-3tur.png"}' | Out-Null
        $state = Send-Cmd '{"cmd":"state"}'
    }
    "turns40" {
        Send-Cmd '{"cmd":"endturn","n":40}' | Out-Null
        Start-Sleep -Seconds 30
        Send-Cmd '{"cmd":"shot","path":"agent/shots/40tur.png"}' | Out-Null
        $state = Send-Cmd '{"cmd":"state"}'
    }
    default { throw "Bilinmeyen senaryo: $Scenario" }
}

Start-Sleep -Seconds 1
Send-Cmd '{"cmd":"quit"}' | Out-Null
Start-Sleep -Seconds 2
if (-not $proc.HasExited) { $proc.Kill() }

# ---------------------------------------------------------------- report
$state | Out-File "$agent\logs\state.json" -Encoding utf8
Write-Host "`n[loop] DURUM:" -ForegroundColor Cyan
Write-Host $state

$errors = Select-String -Path $playerLog -Pattern "Exception|NullReference|Error" -ErrorAction SilentlyContinue
if ($errors) {
    Write-Host "`n[loop] ÇALIŞMA ZAMANI HATALARI:" -ForegroundColor Red
    $errors | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line }
    exit 3
}

Write-Host "`n[loop] temiz. Görüntüler: agent\shots\" -ForegroundColor Green
