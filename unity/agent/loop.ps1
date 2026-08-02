# loop.ps1 — build, launch, play, capture. The agent runs this; the human never has to.
#
#   .\agent\loop.ps1 -Scenario smoke
#   .\agent\loop.ps1 -Scenario play -SkipBuild
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

# Find the Unity project by looking for Assets + ProjectSettings, so the folder can be
# called Game, cityGame or anything else without editing this script.
$game = (Get-ChildItem -Path $root -Directory |
         Where-Object { (Test-Path (Join-Path $_.FullName "Assets")) -and
                        (Test-Path (Join-Path $_.FullName "ProjectSettings")) } |
         Select-Object -First 1).FullName
if (-not $game) {
    throw "Unity projesi bulunamadı. $root altında Assets ve ProjectSettings içeren bir klasör olmalı."
}
Write-Host "[loop] proje: $game" -ForegroundColor DarkGray

# Use the editor the project was made with. Opening it with a different one triggers a full
# reimport and, worse, silently rewrites ProjectVersion.txt. Do not sort version folders as
# strings either — "6000.3.6f1" sorts above "6000.3.21f1" and you get the wrong editor.
$hub = "C:\Program Files\Unity\Hub\Editor"
$wanted = (Get-Content (Join-Path $game "ProjectSettings\ProjectVersion.txt") |
           Select-String '^m_EditorVersion:\s*(.+)$').Matches.Groups[1].Value.Trim()

$UnityExe = Join-Path $hub "$wanted\Editor\Unity.exe"
if (-not (Test-Path $UnityExe)) {
    $available = (Get-ChildItem $hub -Directory -ErrorAction SilentlyContinue).Name -join ", "
    throw "Projenin istediği Unity $wanted kurulu değil. Kurulu sürümler: $available"
}
Write-Host "[loop] editör: $wanted" -ForegroundColor DarkGray

New-Item -ItemType Directory -Force -Path "$agent\logs","$agent\shots" | Out-Null

# unity/Assets is the tracked source; the project folder holds a working copy. Syncing here
# rather than by hand removes the worst failure in this loop: building the previous edit and
# spending ten minutes debugging a bug you already fixed.
$src = Join-Path $root "Assets"
if (Test-Path $src) {
    Copy-Item -Recurse -Force (Join-Path $src "*") (Join-Path $game "Assets")
    Write-Host "[loop] kaynaklar senkronlandı" -ForegroundColor DarkGray
}

# The editor holds an exclusive lock on the project, so a batchmode build silently refuses to
# run while it is open. Catch it here instead of ten minutes later in a confusing log.
if (-not $SkipBuild) {
    $editor = Get-Process Unity -ErrorAction SilentlyContinue |
              Where-Object { $_.MainWindowTitle -like "*$(Split-Path $game -Leaf)*" }
    if ($editor) {
        Write-Host "[loop] Unity Editor bu projeyi açık tutuyor — batchmode build çalışamaz." -ForegroundColor Red
        Write-Host "       Editörü kapatın, sonra tekrar deneyin." -ForegroundColor Red
        exit 4
    }
}

# ---------------------------------------------------------------- build
if (-not $SkipBuild) {
    Write-Host "[loop] derleniyor..." -ForegroundColor Cyan
    $buildLog = "$agent\logs\build.log"
    Remove-Item $buildLog -ErrorAction SilentlyContinue

    # Two traps here, both of which cost an hour the first time.
    #
    # `& $UnityExe` does not block at all: Unity.exe is a GUI-subsystem binary, so the call
    # operator returns immediately and leaves $LASTEXITCODE empty, and you end up reading the
    # previous run's log.
    #
    # `Start-Process -Wait` overshoots the other way: it waits for the process *and all its
    # descendants*, and Unity leaves a shared Roslyn compiler server (dotnet.exe) running on
    # purpose so the next build is faster. The build finishes, the log says so, and the script
    # sits there for as long as that server lives. WaitForExit waits for Unity and nothing else.
    $unity = Start-Process -FilePath $UnityExe -PassThru -ArgumentList @(
        "-batchmode", "-quit", "-nographics",
        "-projectPath", $game,
        "-executeMethod", "Mesruiyet.EditorTools.BuildScript.Windows",
        "-logFile", $buildLog)
    $unity.WaitForExit()
    $code = $unity.ExitCode

    if (-not (Test-Path $buildLog)) {
        Write-Host "[loop] Unity hiç log yazmadı — editör kapanırken kilidi bırakmamış olabilir." -ForegroundColor Red
        exit 1
    }

    # Unity exits 0 when it refuses to open a locked project, so verify the log too.
    $refused = Select-String -Path $buildLog -Pattern "Multiple Unity instances" -Quiet -ErrorAction SilentlyContinue
    if ($code -ne 0 -or $refused) {
        Write-Host "[loop] DERLEME HATASI — son satırlar:" -ForegroundColor Red
        Get-Content $buildLog -Tail 40
        exit 1
    }

    $compileErrors = Select-String -Path $buildLog -Pattern "error CS\d+" -ErrorAction SilentlyContinue
    if ($compileErrors) {
        Write-Host "[loop] DERLEYİCİ HATALARI:" -ForegroundColor Red
        $compileErrors | Select-Object -First 30 | ForEach-Object { Write-Host $_.Line }
        exit 1
    }
}

$exe = "$agent\build\StandaloneWindows64\Mesruiyet.exe"
if (-not (Test-Path $exe)) { throw "Build çıktısı yok: $exe" }

# ---------------------------------------------------------------- launch
Write-Host "[loop] oyun başlatılıyor..." -ForegroundColor Cyan
$playerLog = "$agent\logs\player.log"
Remove-Item $playerLog -ErrorAction SilentlyContinue
$proc = Start-Process $exe -PassThru -WorkingDirectory $agent -ArgumentList @(
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

# Screenshot paths must be absolute: the player resolves relative paths against its own
# working directory, which is not something the caller should have to reason about.
function Shot([string] $name) {
    $path = (Join-Path "$agent\shots" $name) -replace '\\', '/'
    Send-Cmd ('{"cmd":"shot","path":"' + $path + '"}') | Out-Null
    Start-Sleep -Milliseconds 900
}

function Build-At([string] $id, [int] $x, [int] $y) {
    $reply = Send-Cmd ('{"cmd":"build","id":"' + $id + '","x":' + $x + ',"y":' + $y + '}')
    if ($reply -notmatch '"ok":true') { Write-Host "  [inşa reddedildi] $id ($x,$y): $reply" -ForegroundColor DarkYellow }
    return $reply
}

function Get-Turn {
    $s = Send-Cmd '{"cmd":"state"}'
    if ($s -match '"turn":(\d+)') { return [int]$Matches[1] }
    return -1
}

# Waiting for turnIdle alone is not enough: it is true *between* turns, so a ten-turn request
# would appear finished after the first tick. Wait for the turn counter to actually arrive.
function Wait-Turn([int] $target, [int] $seconds = 90) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 400
        $s = Send-Cmd '{"cmd":"state"}'
        if ($s -match '"turn":(\d+)' -and [int]$Matches[1] -ge $target -and $s -match '"turnIdle":true') {
            return $true
        }
    }
    Write-Host "  [uyarı] $target. tura ulaşılamadı ($seconds sn)" -ForegroundColor DarkYellow
    return $false
}

# wait for AgentBridge to come up
$ready = $false
foreach ($i in 1..60) {
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
Start-Sleep -Seconds 1

# ---------------------------------------------------------------- scenarios
switch ($Scenario) {
    "smoke" {
        Shot "01-acilis.png"
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":3}' | Out-Null
        Wait-Turn ($t + 3) 40 | Out-Null
        Shot "02-3tur.png"
        $state = Send-Cmd '{"cmd":"state"}'
    }

    # A real play session: look around, build in two districts, run the clock.
    "play" {
        Shot "01-acilis.png"

        Write-Host "[loop] inşa ediliyor..." -ForegroundColor Cyan
        Build-At "tarla"   9  4 | Out-Null
        Build-At "tarla"  10  4 | Out-Null
        Build-At "kuyu"   14 16 | Out-Null
        Build-At "konut"  20 16 | Out-Null
        # The same workshop in two districts — the political effect must differ.
        Build-At "dokuma"  6  8 | Out-Null
        Build-At "dokuma" 26  9 | Out-Null
        Build-At "park"   36 20 | Out-Null
        Shot "02-insa.png"

        Send-Cmd '{"cmd":"press","key":"e"}' | Out-Null
        Start-Sleep -Milliseconds 700
        Shot "03-donmus-kamera.png"
        Send-Cmd '{"cmd":"press","key":"q"}' | Out-Null
        Start-Sleep -Milliseconds 700

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":10}' | Out-Null
        Wait-Turn ($t + 10) 120 | Out-Null
        Shot "04-10tur.png"
        $state = Send-Cmd '{"cmd":"state"}'
    }

    # The point of the whole slice: stop the mills and watch the ledger stay calm while the
    # city runs out of bread. If Yiyecek does not rise while ekmek falls, the trap is broken.
    "chain" {
        Shot "01-acilis.png"
        $before = Send-Cmd '{"cmd":"state"}'

        Write-Host "[loop] değirmenler durduruluyor..." -ForegroundColor Cyan
        Send-Cmd '{"cmd":"block","id":"degirmen","n":1}' | Out-Null

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":6}' | Out-Null
        Wait-Turn ($t + 6) 90 | Out-Null
        Shot "02-degirmen-durdu.png"
        $blocked = Send-Cmd '{"cmd":"state"}'

        Write-Host "[loop] değirmenler yeniden çalışıyor..." -ForegroundColor Cyan
        Send-Cmd '{"cmd":"block","id":"degirmen","n":0}' | Out-Null
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":5}' | Out-Null
        Wait-Turn ($t + 5) 90 | Out-Null
        Shot "03-toparlanma.png"

        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }

        Write-Host "`n[loop] TUZAK KONTROLÜ:" -ForegroundColor Cyan
        $y0 = Field $before  "yiyecek"; $b0 = Field $before  "bread"
        $y1 = Field $blocked "yiyecek"; $b1 = Field $blocked "bread"
        Write-Host ("  defterdeki Yiyecek : {0,7:N0}  ->  {1,7:N0}" -f $y0, $y1)
        Write-Host ("  gerçekte ekmek     : {0,7:N0}  ->  {1,7:N0}" -f $b0, $b1)
        if ($y1 -ge $y0 -and $b1 -lt $b0) {
            Write-Host "  ✔ toplam yükselirken ekmek tükendi — tuzak çalışıyor" -ForegroundColor Green
        } else {
            Write-Host "  ✘ tuzak çalışmıyor: toplam düşmemeli, ekmek düşmeli" -ForegroundColor Red
            $chainBroken = $true
        }

        $state = Send-Cmd '{"cmd":"state"}'
    }

    # The thesis, made testable. Run the exact same famine twice: once with an honest Tarım
    # minister, once with a loyalist. The city suffers identically. What the governor is told
    # must not be identical — and the loyalist's version must look *better* than the truth.
    "bakan" {
        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        function Reported([string] $json, [string] $key) {
            if ($json -match "`"reported`":\{[^}]*`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }

        Shot "01-uzman-bakan.png"

        Write-Host "[loop] UZMAN bakanla kıtlık..." -ForegroundColor Cyan
        Send-Cmd '{"cmd":"appoint","id":"tarim","n":0}' | Out-Null
        Send-Cmd '{"cmd":"block","id":"degirmen","n":1}' | Out-Null
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":6}' | Out-Null
        Wait-Turn ($t + 6) 90 | Out-Null
        Shot "02-uzman-kitlik.png"
        $honest = Send-Cmd '{"cmd":"state"}'

        Write-Host "[loop] aynı şehir, SADIK bakanla..." -ForegroundColor Cyan
        Send-Cmd '{"cmd":"appoint","id":"tarim","n":1}' | Out-Null
        Start-Sleep -Milliseconds 600
        Shot "03-sadik-kitlik.png"
        $loyal = Send-Cmd '{"cmd":"state"}'

        Send-Cmd '{"cmd":"block","id":"degirmen","n":0}' | Out-Null

        $trueFood   = Field $honest "yiyecek"
        $honestSaid = Reported $honest "yiyecek"
        $loyalSaid  = Reported $loyal  "yiyecek"
        $honestBuf  = Reported $honest "bufferTurns"
        $loyalBuf   = Reported $loyal  "bufferTurns"
        $trueBread  = Field $honest "bread"

        Write-Host "`n[loop] BİLGİ KATMANI KONTROLÜ:" -ForegroundColor Cyan
        Write-Host ("  gerçek yiyecek toplamı : {0,8:N0}   (fırında ekmek {1:N0})" -f $trueFood, $trueBread)
        Write-Host ("  UZMAN ne diyor         : {0,8:N0}" -f $honestSaid)
        Write-Host ("  SADIK ne diyor         : {0,8:N0}" -f $loyalSaid)
        Write-Host ("  UZMAN'ın tamponu       : {0,8:N1} tur" -f $honestBuf)
        Write-Host ("  SADIK'ın tamponu       : {0,8:N1} tur" -f $loyalBuf)

        $ok = $true
        if ([math]::Abs($honestSaid - $trueFood) -gt 1) {
            Write-Host "  ✘ uzman bakan gerçeği bildirmedi" -ForegroundColor Red; $ok = $false
        }
        if ($loyalSaid -le $honestSaid) {
            Write-Host "  ✘ sadık bakan rakamı şişirmedi" -ForegroundColor Red; $ok = $false
        }
        if ($loyalBuf -le $honestBuf) {
            Write-Host "  ✘ sadık bakan güvenlik payını şişirmedi — tampon yalana dönüşmeli" -ForegroundColor Red; $ok = $false
        }
        if ($loyal -notmatch '"yiyecekTeshis":"bildirilmedi"') {
            Write-Host "  ✘ sadık bakan tıkanmayı gizlemedi" -ForegroundColor Red; $ok = $false
        }
        if ($ok) {
            Write-Host "  ✔ aynı kıtlık, iki farklı gerçeklik — bilgi katmanı çalışıyor" -ForegroundColor Green
        } else {
            $chainBroken = $true
        }

        $state = Send-Cmd '{"cmd":"state"}'
    }

    "turns40" {
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":40}' | Out-Null
        Wait-Turn ($t + 40) 300 | Out-Null
        Shot "40tur.png"
        $state = Send-Cmd '{"cmd":"state"}'
    }

    default { throw "Bilinmeyen senaryo: $Scenario" }
}

Start-Sleep -Seconds 1
Send-Cmd '{"cmd":"quit"}' | Out-Null
Start-Sleep -Seconds 3
if (-not $proc.HasExited) { $proc.Kill() }

# ---------------------------------------------------------------- report
$state | Out-File "$agent\logs\state.json" -Encoding utf8
Write-Host "`n[loop] DURUM:" -ForegroundColor Cyan
Write-Host $state

Write-Host "`n[loop] görüntüler:" -ForegroundColor DarkGray
Get-ChildItem "$agent\shots" -Filter *.png | ForEach-Object {
    Write-Host ("  {0}  {1:N0} KB" -f $_.Name, ($_.Length / 1KB))
}

# Only real failures: Unity prints plenty of lines containing the word "error" that are not.
$errors = Select-String -Path $playerLog `
          -Pattern "Exception|NullReference|error CS|Shader error|\[Bootstrap\] .*(bulunamadı|kurulamadı)|\[CityGrid\] .*yerleştirilemedi" `
          -ErrorAction SilentlyContinue
if ($errors) {
    Write-Host "`n[loop] ÇALIŞMA ZAMANI HATALARI:" -ForegroundColor Red
    $errors | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line }
    exit 3
}

if ($chainBroken) {
    Write-Host "`n[loop] SENARYO BAŞARISIZ: tedarik zinciri tuzağı beklendiği gibi davranmadı." -ForegroundColor Red
    exit 5
}

Write-Host "`n[loop] temiz. Görüntüler: agent\shots\" -ForegroundColor Green

