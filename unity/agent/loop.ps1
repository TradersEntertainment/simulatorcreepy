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
#
# It has to mirror, not merely copy: `Copy-Item` never removes anything, so a file renamed or
# moved in the source left its old self behind in the working copy and the build failed with
# "already defines a member" from a class that exists once on disk. Scripts and shaders are
# mirrored; Unity's own Library/ and the .meta files it regenerates are left alone.
$src = Join-Path $root "Assets"
if (Test-Path $src) {
    $dst = Join-Path $game "Assets"
    $stale = Get-ChildItem -Recurse -File -Path $dst -Include *.cs,*.shader,*.hlsl -ErrorAction SilentlyContinue |
             Where-Object { -not (Test-Path (Join-Path $src $_.FullName.Substring($dst.Length + 1))) }
    foreach ($f in $stale) {
        Write-Host "  [senkron] kaynakta yok, siliniyor: $($f.FullName.Substring($dst.Length + 1))" -ForegroundColor DarkYellow
        Remove-Item $f.FullName -Force
        if (Test-Path "$($f.FullName).meta") { Remove-Item "$($f.FullName).meta" -Force }
    }

    Copy-Item -Recurse -Force (Join-Path $src "*") $dst
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

# The game now opens on a title screen. The agent starts a term the way a player does — by
# pressing the button — rather than through a back door, so every scenario below begins in the
# same place a human would. The `menus` scenario is the exception: it tests the screen itself.
if ($Scenario -ne "menus") {
    Send-Cmd '{"cmd":"click","id":"btn_new_term"}' | Out-Null
    Start-Sleep -Milliseconds 700
}

# ---------------------------------------------------------------- scenarios
switch ($Scenario) {
    "smoke" {
        Shot "01-acilis.png"
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":3}' | Out-Null
        Wait-Turn ($t + 3) 40 | Out-Null
        Shot "02-3tur.png"

        # An ordinary quarter, close up. Everything about the roofs, doors, stalls, people and
        # cars is invisible from the default framing, and "it renders" is not the same claim as
        # "it looks like a town", so the loop takes a picture that can settle the second one.
        foreach ($i in 1..8) { Send-Cmd '{"cmd":"press","key":"zoomin"}' | Out-Null }
        Start-Sleep -Milliseconds 900
        Shot "03-yakin.png"

        # The same block with the housing shut down. An unworked building is drawn cold, and
        # every part of it has to go cold together — the roof is the largest surface on a house,
        # so a roof that ignored the shutdown left a bright lid hanging over invisible walls and
        # the whole row looked like it was floating. This picture is how that stays fixed.
        Send-Cmd '{"cmd":"block","id":"konut","n":1}' | Out-Null
        Start-Sleep -Milliseconds 900
        Shot "04-kapali-konut.png"
        Send-Cmd '{"cmd":"block","id":"konut","n":0}' | Out-Null

        # Right down onto a single block, where a wall either meets the ground or it does not.
        foreach ($i in 1..8) { Send-Cmd '{"cmd":"press","key":"zoomin"}' | Out-Null }
        Start-Sleep -Milliseconds 900
        Shot "05-cok-yakin.png"

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

    # The counterweight. With every ministry rounding in the governor's favour, the council is
    # the only place the truth still arrives — and "Meclisi Tatil Et" is the button that ends
    # that. Drift to it by ordinary governing rather than by setting a variable: this scenario
    # never once picks "become authoritarian", it just answers each turn's problem quickly.
    "meclis" {
        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        function Reported([string] $json, [string] $key) {
            if ($json -match "`"reported`":\{[^}]*`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        Shot "01-acilis.png"

        Write-Host "[loop] sadık bakan + duran değirmenler..." -ForegroundColor Cyan
        Send-Cmd '{"cmd":"appoint","id":"tarim","n":1}' | Out-Null
        Send-Cmd '{"cmd":"block","id":"degirmen","n":1}' | Out-Null
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":4}' | Out-Null
        Wait-Turn ($t + 4) 90 | Out-Null
        Shot "02-meclis-uyariyor.png"
        $fogged = Send-Cmd '{"cmd":"state"}'

        Write-Host "`n[loop] MECLİS KONTROLÜ:" -ForegroundColor Cyan
        Check (((Reported $fogged "yiyecek") - (Field $fogged "yiyecek")) -gt 1) `
              "bakanlık rakamı şişiriyor"
        Check ($fogged -match '"murmurs":\[[^\]]*ekmek') `
              "meclis ekmeği konuşuyor — gerçek hâlâ ulaşıyor"

        Write-Host "`n[loop] YÖNETİM ARAÇLARI:" -ForegroundColor Cyan
        $o0 = Field $fogged "axisOrder"
        Send-Cmd '{"cmd":"decree","id":"sokaga_cikma"}' | Out-Null
        $s = Send-Cmd '{"cmd":"state"}'
        Check ((Field $s "axisOrder") -gt $o0) "kararname ekseni oynattı"

        Send-Cmd '{"cmd":"law","id":"kontrol_noktalari","n":1}' | Out-Null
        $s = Send-Cmd '{"cmd":"state"}'
        Check ($s -match '"laws":\[[^\]]*kontrol_noktalari') "kanun yasa kitabına girdi"

        # Twelve reasonable emergency measures. Nothing here is labelled authoritarian.
        Write-Host "`n[loop] her turun sorununa hızlı cevap veriliyor..." -ForegroundColor Cyan
        foreach ($i in 1..7) {
            Send-Cmd '{"cmd":"decree","id":"sokaga_cikma"}' | Out-Null
            Send-Cmd '{"cmd":"decree","id":"basin_talimatnamesi"}' | Out-Null
            $t = Get-Turn
            Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
            Wait-Turn ($t + 1) 30 | Out-Null
        }
        Send-Cmd '{"cmd":"block","id":"degirmen","n":0}' | Out-Null
        $drifted = Send-Cmd '{"cmd":"state"}'
        Shot "03-suruklenme.png"

        $order = Field $drifted "axisOrder"
        $transparency = Field $drifted "transparency"
        Write-Host ("  otorite ekseni : {0,6:N0}   ({1})" -f $order, `
                    ($(if ($drifted -match '"bandOrder":"([^"]+)"') { $Matches[1] } else { "?" })))
        Write-Host ("  şeffaflık      : {0,6:N2}" -f $transparency)
        Check ($order -ge 75) "sıradan yönetimle DÖNÜŞSÜZ eşiğine sürüklenildi"
        Check ($transparency -lt 0.2) "sis kalınlaştı"

        Write-Host "`n[loop] meclis tatil ediliyor..." -ForegroundColor Cyan
        Send-Cmd '{"cmd":"council","n":1}' | Out-Null
        $silent = Send-Cmd '{"cmd":"state"}'
        Shot "04-meclis-tatilde.png"
        Check ($silent -match '"suspended":true') "meclis tatilde"
        Check ($silent -notmatch '"murmurs":\[[^\]]*ekmek') "son dürüst kanal da sustu"

        Write-Host "`n[loop] seçim..." -ForegroundColor Cyan
        $t = Get-Turn
        $target = 12
        if ($t -lt $target) {
            Send-Cmd ('{"cmd":"endturn","n":' + ($target - $t) + '}') | Out-Null
            Wait-Turn $target 120 | Out-Null
        }
        $atElection = Send-Cmd '{"cmd":"state"}'
        Check ($atElection -match '"electionPending":true') "seçim sandığı kuruldu"
        $reply = Send-Cmd '{"cmd":"election","id":"yap"}'
        Write-Host "  $reply"
        Check ($reply -match '"ok":true') "seçim sonuçlandı"
        Shot "05-secim-sonrasi.png"

        if (-not $ok) { $chainBroken = $true }
        $state = Send-Cmd '{"cmd":"state"}'
    }

    # The outside world, and the causal loop the whole game turns on. Nothing here picks an
    # ideology either: it borrows because the treasury is short, and it conscripts because
    # Mersa is at the door. The axis moves anyway.
    "dis" {
        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        Shot "01-acilis.png"

        # ---- 1. the loan whose collateral is a law slot
        Write-Host "`n[loop] BORÇ VE YASA YUVASI:" -ForegroundColor Cyan
        $before = Send-Cmd '{"cmd":"state"}'
        $reply = Send-Cmd '{"cmd":"loan","id":"yabanci_konsorsiyum","n":1}'
        $after = Send-Cmd '{"cmd":"state"}'
        Write-Host "  $reply"
        Check ((Field $after "para") -gt (Field $before "para")) "para hazineye girdi"
        Check ($after -match '"laws":\[[^\]]*grev_yasagi') "alacaklının kanunu yuvaya kondu"
        Check ((Field $after "sealedSlots") -ge 1) "yuva mühürlendi"

        # The rule that makes debt matter: you cannot take that law back.
        $repeal = Send-Cmd '{"cmd":"law","id":"grev_yasagi","n":0}'
        Check ($repeal -match '"ok":false') "mühürlü kanun kaldırılamadı"
        Write-Host "  $repeal"
        Shot "02-borclu.png"

        # ---- 2. the causal loop the whole game turns on:
        #        Mersa'nın tehdidi ↑ → askere alma → tarlalarda işgücü ↓ → yiyecek açığı
        Write-Host "`n[loop] TEHDİT → ASKERE ALMA → TARLA:" -ForegroundColor Cyan
        Send-Cmd '{"cmd":"threat","n":60}' | Out-Null

        # Get past the learning turns so the card is legal, answering anything that shows up.
        $t = Get-Turn
        while ($t -lt 14) {
            $s = Send-Cmd '{"cmd":"state"}'
            if ($s -notmatch '"pendingEvent":""') { Send-Cmd '{"cmd":"event","n":0}' | Out-Null }
            Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
            Wait-Turn ($t + 1) 40 | Out-Null
            $t = Get-Turn
        }
        $s = Send-Cmd '{"cmd":"state"}'
        if ($s -notmatch '"pendingEvent":""') { Send-Cmd '{"cmd":"event","n":0}' | Out-Null }

        $peace = Send-Cmd '{"cmd":"state"}'
        $poolBefore = Field $peace "labourPool"
        $farmBefore = 0
        if ($peace -match '"name":"Tarla","stock":[\d.]+,"capacity":[\d.]+,"throughput":([\d.]+)') {
            $farmBefore = [double]$Matches[1]
        }

        # Mersa is at the door and the ministry wants a battalion. Take the direct answer.
        Send-Cmd '{"cmd":"card","id":"askere_alma"}' | Out-Null
        $r = Send-Cmd '{"cmd":"event","n":0}'
        Write-Host "  [olay] askere_alma -> $r"
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 40 | Out-Null

        $armed = Send-Cmd '{"cmd":"state"}'
        Shot "03-garnizon.png"
        $conscripts = Field $armed "conscripts"
        $garrison = Field $armed "garrison"
        $poolAfter = Field $armed "labourPool"
        Write-Host ("  garnizon {0:N0} · asker {1:N0} · işgücü havuzu {2:N0} -> {3:N0}" -f `
                    $garrison, $conscripts, $poolBefore, $poolAfter)
        Check ($garrison -gt 22) "garnizon büyüdü"
        Check ($conscripts -gt 0) "asker alındı"
        Check ($poolAfter -lt $poolBefore) "askere alınanlar işgücü havuzundan düştü"

        # ---- 4. the Kadra squeeze
        Write-Host "`n[loop] KADRA:" -ForegroundColor Cyan
        $t = Get-Turn
        while ($t -lt 30) {
            $s = Send-Cmd '{"cmd":"state"}'
            if ($s -match '"pendingEvent":"[^"]+"' -and $s -notmatch '"pendingEvent":""') {
                Send-Cmd '{"cmd":"event","n":0}' | Out-Null
            }
            if ($s -match '"electionPending":true') { Send-Cmd '{"cmd":"election","id":"yap"}' | Out-Null }
            $t = Get-Turn
            Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
            Wait-Turn ($t + 1) 40 | Out-Null
            $t = Get-Turn
        }
        $atKadra = Send-Cmd '{"cmd":"state"}'
        Check ($atKadra -match '"pendingEvent":"kadra"') "30. turda Kadra düştü"
        Shot "04-kadra.png"

        $threatBefore = Field $atKadra "threat"
        $r = Send-Cmd '{"cmd":"event","n":0}'
        Write-Host "  $r"
        $afterKadra = Send-Cmd '{"cmd":"state"}'
        Check ((Field $afterKadra "threat") -gt $threatBefore) "Mersa bir komşusundan kurtuldu"
        Check ((Field $afterKadra "population") -gt (Field $atKadra "population")) "mülteci kolu içeri alındı"
        Shot "05-kadra-sonrasi.png"

        Write-Host ("`n  eksen otorite {0:N0} · şeffaflık {1:N2} · tampon {2:N1}" -f `
                    (Field $afterKadra "axisOrder"), (Field $afterKadra "transparency"), `
                    (Field $afterKadra "bufferTurns"))

        if (-not $ok) { $chainBroken = $true }
        $state = Send-Cmd '{"cmd":"state"}'
    }

    # The readability channel. The design's target is explicit: a player should be able to
    # diagnose a district by watching it for five seconds without opening a panel. So drive the
    # city into three distinct states and check the crowd actually changes shape.
    "kalabalik" {
        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        function Crowd([string] $json, [string] $key) {
            if ($json -match "`"crowd`":\{[^}]*`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        # ---- 1. an ordinary working city
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":2}' | Out-Null
        Wait-Turn ($t + 2) 40 | Out-Null
        Start-Sleep -Milliseconds 800
        Shot "01-calisan-sehir.png"
        $calm = Send-Cmd '{"cmd":"state"}'
        Write-Host "`n[loop] KALABALIK KONTROLÜ:" -ForegroundColor Cyan
        Write-Host ("  çalışan şehir : yürüyen {0:N0} · duran {1:N0} · yürüyüşte {2:N0} · sokakta yok {3:N0}" -f `
                    (Crowd $calm "walking"), (Crowd $calm "idle"), (Crowd $calm "marching"), (Crowd $calm "hidden"))
        Check ((Crowd $calm "live") -gt 0) "kalabalık var"
        Check ((Crowd $calm "walking") -gt 0) "sokaklar dolu"
        Check ((Crowd $calm "marching") -eq 0) "kimse yürüyüşte değil"

        # ---- 2. a struck quarter empties
        # Every workplace in SANAYİ, or the quarter has not actually stopped. Blocking the farms
        # hits LİMAN too, but LİMAN keeps its mills and wells and so keeps working — which is
        # the comparison that makes the empty quarter mean something.
        Write-Host "`n[loop] SANAYİ'de iş bırakma..." -ForegroundColor Cyan
        foreach ($id in @("dokuma","santral","tarla")) {
            Send-Cmd ('{"cmd":"block","id":"' + $id + '","n":1}') | Out-Null
        }
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 40 | Out-Null
        Start-Sleep -Milliseconds 800
        Shot "02-grev.png"
        $struck = Send-Cmd '{"cmd":"state"}'
        Write-Host ("  grevde        : yürüyen {0:N0} · duran {1:N0} · yürüyüşte {2:N0} · sokakta yok {3:N0}" -f `
                    (Crowd $struck "walking"), (Crowd $struck "idle"), (Crowd $struck "marching"), (Crowd $struck "hidden"))
        Check ((Crowd $struck "hidden") -gt (Crowd $calm "hidden")) "duran mahalle sokaktan çekildi"

        # ---- 3. anger brings them to the square with banners
        Write-Host "`n[loop] hoşnutsuzluk tırmandırılıyor..." -ForegroundColor Cyan
        $guard = 0
        while ($guard -lt 12) {
            Send-Cmd '{"cmd":"decree","id":"serbest_fiyat"}' | Out-Null
            Send-Cmd '{"cmd":"decree","id":"imar_affi"}' | Out-Null
            $t = Get-Turn
            $s = Send-Cmd '{"cmd":"state"}'
            if ($s -notmatch '"pendingEvent":""') { Send-Cmd '{"cmd":"event","n":2}' | Out-Null }
            if ($s -match '"electionPending":true') { Send-Cmd '{"cmd":"election","id":"ertele"}' | Out-Null }
            Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
            Wait-Turn ($t + 1) 40 | Out-Null
            $s = Send-Cmd '{"cmd":"state"}'
            if ((Crowd $s "marching") -gt 0) { break }
            $guard++
        }
        Start-Sleep -Milliseconds 900
        Shot "03-yuruyus.png"
        $angry = Send-Cmd '{"cmd":"state"}'
        Write-Host ("  öfkeli        : yürüyen {0:N0} · duran {1:N0} · yürüyüşte {2:N0} · sokakta yok {3:N0}" -f `
                    (Crowd $angry "walking"), (Crowd $angry "idle"), (Crowd $angry "marching"), (Crowd $angry "hidden"))
        Check ((Crowd $angry "marching") -gt 0) "kalabalık meydana yürüdü, pankart taşıyor"

        # ---- 4. the crisis ring, and who decides whether you can see it.
        #        The city is identical either side of this appointment.
        Write-Host "`n[loop] HARİTADAKİ KRİZ:" -ForegroundColor Cyan
        $trueWorst = 0
        if ($angry -match '"grievance":([\d.]+)') { }
        foreach ($m in ([regex]::Matches($angry, '"grievance":([\d.]+)'))) {
            $v = [double]$m.Groups[1].Value
            if ($v -gt $trueWorst) { $trueWorst = $v }
        }
        $saidLoyal = Field $angry "enYuksekHosnutsuzluk"
        Write-Host ("  sadık bakanla : gerçek {0:N0} · bildirilen {1:N0}" -f $trueWorst, $saidLoyal)
        Check ($trueWorst -ge 70) "mahalle gerçekten ayaklanma eşiğinde"
        Check ($saidLoyal -lt 70) "sadık bakan eşiğin altında bildiriyor — haritada halka yok"

        Send-Cmd '{"cmd":"appoint","id":"halk","n":0}' | Out-Null
        Start-Sleep -Milliseconds 800
        Shot "04-kriz-halkasi.png"
        $honest = Send-Cmd '{"cmd":"state"}'
        $saidHonest = Field $honest "enYuksekHosnutsuzluk"
        Write-Host ("  uzman bakanla : bildirilen {0:N0}" -f $saidHonest)
        Check ($saidHonest -ge 70) "uzman bakan gerçeği bildiriyor — halka haritada belirir"

        # ---- 5. the city looks like its politics — verified by eye, close up
        foreach ($i in 1..7) { Send-Cmd '{"cmd":"press","key":"zoomin"}' | Out-Null }
        Start-Sleep -Milliseconds 900
        Shot "05-yakin-ideoloji.png"

        Write-Host "`n[loop] İDEOLOJİ PROPLARI:" -ForegroundColor Cyan
        Write-Host ("  otorite {0:N0} · ekonomi {1:N0}" -f `
                    (Field $angry "axisOrder"), (Field $angry "axisEconomy"))
        Check ([math]::Abs((Field $angry "axisEconomy")) -ge 25) "eksen prop eşiğini geçti (duvarlarda görünmeli)"

        if (-not $ok) { $chainBroken = $true }
        $state = Send-Cmd '{"cmd":"state"}'
    }

    # The end of a term, and the session that reads it back. Drift to DÖNÜŞSÜZ by answering
    # each turn quickly, watch the eight-turn countdown start, and let it run out.
    "son" {
        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        Write-Host "`n[loop] SÜRÜKLENME:" -ForegroundColor Cyan
        $countdownSeen = $false
        $guard = 0
        while ($guard -lt 40) {
            $s = Send-Cmd '{"cmd":"state"}'
            if ($s -match '"isOver":true') { break }
            if ((Field $s "noReturnCountdown") -gt 0 -and -not $countdownSeen) {
                $countdownSeen = $true
                Write-Host ("  geri dönüş sayacı başladı · otorite {0:N0}" -f (Field $s "axisOrder"))
                Shot "01-geri-donus.png"
            }
            if ($s -notmatch '"pendingEvent":""') { Send-Cmd '{"cmd":"event","n":0}' | Out-Null }
            if ($s -match '"electionPending":true') { Send-Cmd '{"cmd":"election","id":"ertele"}' | Out-Null }

            Send-Cmd '{"cmd":"decree","id":"sokaga_cikma"}' | Out-Null
            Send-Cmd '{"cmd":"decree","id":"basin_talimatnamesi"}' | Out-Null

            $t = Get-Turn
            Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
            Wait-Turn ($t + 1) 40 | Out-Null
            $guard++
        }

        $final = Send-Cmd '{"cmd":"state"}'
        Start-Sleep -Milliseconds 1200
        Shot "02-hesap-verme.png"

        $ending = ""
        if ($final -match '"ending":"([^"]*)"') { $ending = $Matches[1] }
        Write-Host "`n[loop] SON:" -ForegroundColor Cyan
        Write-Host ("  {0} · {1}. tur" -f $ending, (Field $final "turn"))
        Write-Host ("  otorite {0:N0} · yetkinlik {1:N2} · kaybedilen mahalle {2:N0}" -f `
                    (Field $final "axisOrder"), (Field $final "competence"), (Field $final "lostDistricts"))

        Check $countdownSeen "geri dönüş sayacı göründü"
        Check ($final -match '"isOver":true') "görev sona erdi"
        Check ($ending -ne "") "bir son kartı seçildi"
        Check ((Field $final "historyRows") -gt 5) "tur tur kayıt tutuldu — oturum okuyabilir"
        Check ((Field $final "competence") -lt 1) "tasfiyeler yetkinliği kalıcı olarak aşındırdı"

        # A finished term must not keep ticking.
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Start-Sleep -Seconds 2
        Check ((Get-Turn) -eq $t) "biten görev tur ilerletmiyor"

        if (-not $ok) { $chainBroken = $true }
        $state = $final
    }

    # The two levers the governor holds from turn one: the charter that decides what they will
    # never be allowed to do, and the budget that decides what the city can afford this year.
    "butce" {
        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        # ---- 1. the charter arrives on turn five and blocks the turn until it is written
        Write-Host "`n[loop] ANAYASA:" -ForegroundColor Cyan
        $t = Get-Turn
        Send-Cmd ('{"cmd":"endturn","n":' + (5 - $t) + '}') | Out-Null
        Wait-Turn 5 60 | Out-Null
        Start-Sleep -Milliseconds 700
        $atCharter = Send-Cmd '{"cmd":"state"}'
        Shot "01-anayasa.png"
        Check ($atCharter -match '"charterPending":true') "5. turda anayasa açıldı"

        # "Söz Serbesttir" is the interesting one: it caps authority for the whole run.
        Send-Cmd '{"cmd":"clause","id":"soz_serbest"}'  | Out-Null
        Send-Cmd '{"cmd":"clause","id":"herkese_ekmek"}' | Out-Null
        Send-Cmd '{"cmd":"clause","id":"meclis_ustundur"}' | Out-Null
        $written = Send-Cmd '{"cmd":"state"}'
        Write-Host ("  otorite tavanı {0:N0} · yasa yuvası {1:N0}" -f `
                    (Field $written "orderCeiling"), (Field $written "lawSlots"))
        Check ($written -notmatch '"charterPending":true') "üç madde yazıldı"
        Check ((Field $written "orderCeiling") -le 55) "anayasa otoriteye tavan koydu"
        Check ((Field $written "lawSlots") -eq 5) "meclis maddesi bir yuva daha açtı"

        # ---- 2. the wall holds against everything
        Write-Host "`n[loop] DUVAR:" -ForegroundColor Cyan
        foreach ($i in 1..10) {
            Send-Cmd '{"cmd":"decree","id":"sokaga_cikma"}' | Out-Null
            Send-Cmd '{"cmd":"decree","id":"basin_talimatnamesi"}' | Out-Null
            $t = Get-Turn
            $s = Send-Cmd '{"cmd":"state"}'
            if ($s -notmatch '"pendingEvent":""') { Send-Cmd '{"cmd":"event","n":0}' | Out-Null }
            if ($s -match '"electionPending":true') { Send-Cmd '{"cmd":"election","id":"yap"}' | Out-Null }
            Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
            Wait-Turn ($t + 1) 40 | Out-Null
        }
        $walled = Send-Cmd '{"cmd":"state"}'
        Shot "02-duvar.png"
        Write-Host ("  yirmi kararname sonrası otorite {0:N0} (tavan {1:N0})" -f `
                    (Field $walled "axisOrder"), (Field $walled "orderCeiling"))
        Check ((Field $walled "axisOrder") -le (Field $walled "orderCeiling")) `
              "eksen anayasal tavanı aşamadı"

        # ---- 3. the budget is a real lever in both directions
        Write-Host "`n[loop] BÜTÇE:" -ForegroundColor Cyan
        $before = Send-Cmd '{"cmd":"state"}'
        Send-Cmd '{"cmd":"tax","n":50}' | Out-Null
        $high = Send-Cmd '{"cmd":"state"}'
        Write-Host ("  vergi oranı %{0:N0} -> %{1:N0} · ödenek gideri {2:N0} ₺/tur" -f `
                    ((Field $before "taxRate") * 100), ((Field $high "taxRate") * 100), `
                    (Field $high "fundingCost"))
        Check ((Field $high "taxRate") -gt 0.45) "vergi oranı ayarlandı"

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":3}' | Out-Null
        Wait-Turn ($t + 3) 60 | Out-Null
        $taxed = Send-Cmd '{"cmd":"state"}'
        Shot "03-vergi.png"

        $worst = 0
        foreach ($m in ([regex]::Matches($taxed, '"grievance":([\d.]+)'))) {
            $v = [double]$m.Groups[1].Value
            if ($v -gt $worst) { $worst = $v }
        }
        $worstBefore = 0
        foreach ($m in ([regex]::Matches($before, '"grievance":([\d.]+)'))) {
            $v = [double]$m.Groups[1].Value
            if ($v -gt $worstBefore) { $worstBefore = $v }
        }
        Write-Host ("  en yüksek hoşnutsuzluk {0:N0} -> {1:N0}" -f $worstBefore, $worst)
        Check ($worst -gt $worstBefore) "yüksek vergi her mahallede hissedildi"

        if (-not $ok) { $chainBroken = $true }
        $state = $taxed
    }

    # Traffic. The design asks that a jam be a symptom of an unserviced district rather than a
    # separate stat, so the test is whether laying roads actually fixes one.
    "trafik" {
        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        function District([string] $json, [string] $name, [string] $key) {
            if ($json -match "`"name`":`"$name`"[^}]*?`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":2}' | Out-Null
        Wait-Turn ($t + 2) 40 | Out-Null
        Start-Sleep -Milliseconds 900
        Shot "01-trafik.png"

        $before = Send-Cmd '{"cmd":"state"}'
        Write-Host "`n[loop] TRAFİK:" -ForegroundColor Cyan
        foreach ($d in @("LİMAN","SANAYİ","TEPE")) {
            Write-Host ("  {0,-11} yol {1,3:N0} · sıkışıklık {2:N2}" -f `
                        $d, (District $before $d "roadTiles"), (District $before $d "congestion"))
        }
        Check ((District $before "LİMAN" "roadTiles") -gt 0) "mahalleler yol karolarını sayıyor"

        # Crowd the docks: houses without streets is exactly the unserviced case.
        Write-Host "`n[loop] LİMAN'a yol açmadan konut ekleniyor..." -ForegroundColor Cyan
        foreach ($xy in @(@(7,7),@(8,7),@(9,7),@(10,7),@(7,9),@(8,9),@(9,9),@(10,9))) {
            Build-At "konut" $xy[0] $xy[1] | Out-Null
        }
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":3}' | Out-Null
        Wait-Turn ($t + 3) 60 | Out-Null
        $crowded = Send-Cmd '{"cmd":"state"}'
        Shot "02-sikisiklik.png"
        Write-Host ("  LİMAN sıkışıklık {0:N2} -> {1:N2}" -f `
                    (District $before "LİMAN" "congestion"), (District $crowded "LİMAN" "congestion"))
        Check ((District $crowded "LİMAN" "congestion") -gt (District $before "LİMAN" "congestion")) `
              "yolsuz büyüme sokakları tıkadı"

        # Now lay the streets the quarter needed and watch it clear.
        Write-Host "`n[loop] yol açılıyor..." -ForegroundColor Cyan
        # A row between the two blocks of houses, and a column beside them. Tiles the houses
        # already sit on are refused, which is correct and not worth fighting.
        foreach ($x in 6..11) { Build-At "yol" $x 8  | Out-Null }
        foreach ($y in 9..11) { Build-At "yol" 6 $y  | Out-Null }
        foreach ($x in 6..11) { Build-At "yol" $x 11 | Out-Null }
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":2}' | Out-Null
        Wait-Turn ($t + 2) 60 | Out-Null
        $paved = Send-Cmd '{"cmd":"state"}'
        Shot "03-yol-acildi.png"
        Write-Host ("  LİMAN yol {0:N0} -> {1:N0} · sıkışıklık {2:N2} -> {3:N2}" -f `
                    (District $crowded "LİMAN" "roadTiles"), (District $paved "LİMAN" "roadTiles"), `
                    (District $crowded "LİMAN" "congestion"), (District $paved "LİMAN" "congestion"))
        Check ((District $paved "LİMAN" "roadTiles") -gt (District $crowded "LİMAN" "roadTiles")) `
              "yol karoları arttı"
        Check ((District $paved "LİMAN" "congestion") -lt (District $crowded "LİMAN" "congestion")) `
              "yol açmak tıkanıklığı çözdü"

        # Let them drive for a while, then ask where they are. A car that turns before reaching
        # the junction cuts the corner diagonally and ends up on the grass, and the error carries
        # into every leg after it — so after twelve seconds of driving, none of them should be
        # off the carriageway.
        Write-Host "`n[loop] arabalar on iki saniye sürüyor..." -ForegroundColor Cyan
        Start-Sleep -Seconds 12
        $driven = Send-Cmd '{"cmd":"state"}'
        $off = 0
        if ($driven -match '"yoldisi":(\d+)') { $off = [int]$Matches[1] }
        Write-Host ("  yol dışında kalan araç: {0}" -f $off)
        Check ($off -eq 0) "arabalar yolda kalıyor, köşe kesmiyor"

        # The charter opens on turn five and this scenario runs past it, so clear it or the
        # picture is a picture of a modal.
        if ($driven -match '"charterPending":true') {
            Send-Cmd '{"cmd":"clause","id":"herkese_ekmek"}'   | Out-Null
            Send-Cmd '{"cmd":"clause","id":"soz_serbest"}'     | Out-Null
            Send-Cmd '{"cmd":"clause","id":"meclis_ustundur"}' | Out-Null
        }
        foreach ($i in 1..2) { Send-Cmd '{"cmd":"press","key":"zoomin"}' | Out-Null }
        Start-Sleep -Milliseconds 900
        Shot "04-araclar.png"

        if (-not $ok) { $chainBroken = $true }
        $state = $paved
    }

    "turns40" {
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":40}' | Out-Null
        Wait-Turn ($t + 40) 300 | Out-Null
        Shot "40tur.png"
        $state = Send-Cmd '{"cmd":"state"}'
    }

    # Does the deck actually deal a varied game? A card table can look full on paper and still
    # hand out the same four crises all run, because the conditions that gate the interesting
    # cards are the conditions that stay true. Only a full term shows it.
    # The pass conditions, measured. (a) every building's bounds.min.y within 0 ± 0.02 of its
    # floor; (b) every car's drawn forward · road direction > 0.99; (c) every pedestrian's drawn
    # lowest point within 0 ± 0.02. These are aggregates over ALL entities — the printed samples
    # are for reading, the aggregate is what passes or fails.
    "probe" {
        Write-Host "`n[loop] PROBE:" -ForegroundColor Cyan

        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        # Let the city actually run so the cars have driven and the crowd has moved.
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":2}' | Out-Null
        Wait-Turn ($t + 2) 60 | Out-Null
        Start-Sleep -Seconds 6

        $bina = Send-Cmd '{"cmd":"probe","id":"bina"}'
        $araba = Send-Cmd '{"cmd":"probe","id":"araba"}'
        $yaya = Send-Cmd '{"cmd":"probe","id":"yaya"}'

        $binaKotu = Field $bina "enKotuMinY"
        $arabaKotu = Field $araba "enKotuDot"
        $yayaKotu = Field $yaya "enKotuMinY"
        $binaKim = ""; if ($bina -match '"enKotuYapi":"([^"]*)"') { $binaKim = $Matches[1] }

        Write-Host ("  bina : {0} yapı · en kötü min.y {1:N3} ({2})" -f (Field $bina "toplam"), $binaKotu, $binaKim)
        Write-Host ("  araba: en kötü dot {0:N3}" -f $arabaKotu)
        Write-Host ("  yaya : en kötü min.y {0:N3}" -f $yayaKotu)

        Check ([math]::Abs($binaKotu) -le 0.02) "her binanın bounds.min.y 0 ± 0.02"
        Check ($arabaKotu -gt 0.99) "her aracın forward · yol dot > 0.99"
        Check ([math]::Abs($yayaKotu) -le 0.02) "her yayanın min.y 0 ± 0.02"

        # The skinned characters: wait for the async glTF load, then hold it to its budget.
        $deadline = (Get-Date).AddSeconds(20)
        $sk = Send-Cmd '{"cmd":"state"}'
        while ((Get-Date) -lt $deadline -and $sk -notmatch '"hazir":true') {
            Start-Sleep -Milliseconds 800
            $sk = Send-Cmd '{"cmd":"state"}'
        }
        $aktif = Field $sk "aktif"; $tavan = Field $sk "tavan"
        $iskKotu = 0.0
        if ($sk -match '"iskelet":\{[^}]*"enKotuMinY":(-?[\d.]+)') { $iskKotu = [double]$Matches[1] }
        Write-Host ("  iskelet: hazır {0} · aktif {1:N0}/{2:N0} · asker {3:N0} · ölçek {4:N3} · en kötü min.y {5:N3}" -f `
                    ($sk -match '"hazir":true'), $aktif, $tavan, (Field $sk "asker"), (Field $sk "olcek"), $iskKotu)
        Check ($sk -match '"hazir":true') "glTF modelleri yüklendi"
        Check ($aktif -gt 0) "kamera yakınındakiler gerçek modelle çiziliyor"
        Check ($aktif -le $tavan) "iskeletli sayısı tavanın altında"
        Check ([math]::Abs($iskKotu) -le 0.05) "modellerin ayakları yerde (± 0.05)"

        # The sample rows, so a failure is diagnosable without another run.
        Write-Host "`n  --- bina örnekleri ---" -ForegroundColor DarkGray
        Write-Host "  $bina"
        Write-Host "`n  --- araba örnekleri ---" -ForegroundColor DarkGray
        Write-Host "  $araba"
        Write-Host "`n  --- yaya örnekleri ---" -ForegroundColor DarkGray
        Write-Host "  $yaya"

        foreach ($i in 1..5) { Send-Cmd '{"cmd":"press","key":"zoomin"}' | Out-Null }
        Start-Sleep -Milliseconds 900
        Shot "probe-yakin.png"

        $state = Send-Cmd '{"cmd":"state"}'
        if (-not $ok) { $chainBroken = $true }
    }

    # Every building has its own silhouette, and every part of every one of them stands on
    # something that reaches the ground. The second half is measured rather than looked at: the
    # renderer records each building's lowest vertex against the tile it stands on.
    "siluet" {
        Write-Host "`n[loop] SİLUET:" -ForegroundColor Cyan

        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        # Put one of everything on the map, so every form is exercised, not just the founding six.
        Send-Cmd '{"cmd":"grant","n":40000}' | Out-Null
        $ids = @("konut","toplukonut","tarla","degirmen","firin","ambar","tayinlama",
                 "ocak","islik","depo","pazar","borsa","dokuma",
                 "kuyu","sukemeri","aritma","santral","yol",
                 "klinik","hastane","okul","kutuphane","hamam","park","tapinak","matbaa","anit",
                 "karakol","kontrol","kisla","tersane")
        $placed = 0
        foreach ($id in $ids) {
            $done = $false
            foreach ($x in 4..42) {
                if ($done) { break }
                foreach ($y in 3..27) {
                    if ((Send-Cmd ('{"cmd":"build","id":"' + $id + '","x":' + $x + ',"y":' + $y + '}')) -match '"ok":true') {
                        $done = $true; $placed++; break
                    }
                }
            }
        }

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 60 | Out-Null
        Start-Sleep -Milliseconds 800
        $s = Send-Cmd '{"cmd":"state"}'

        $havada = [int](Field $s "havada")
        $lift   = Field $s "enKotuKalkis"
        $who    = ""
        if ($s -match '"enKotuYapi":"([^"]*)"') { $who = $Matches[1] }

        Write-Host ("  {0}/{1} yapı kondu · havada duran {2} · en kötü kalkış {3:N3} ({4})" -f `
                    $placed, $ids.Count, $havada, $lift, $who)

        Check ($placed -eq $ids.Count) "her yapı haritaya kondu"
        Check ($havada -eq 0) "hiçbir yapı havada durmuyor"
        Check ($lift -le 0.01) "en kötü kalkış bile zeminde"

        Send-Cmd '{"cmd":"press","key":"zoomin"}' | Out-Null
        Start-Sleep -Milliseconds 900
        Shot "siluet.png"

        $state = $s
        if (-not $ok) { $chainBroken = $true }
    }

    # The uncertainty system. A minister who shades a figure a little should state it precisely;
    # one who shades it a lot should give a range whose ends actually differ. The failure this
    # catches is a range printed as "~1911–1911" — a widget that looks broken rather than unsure.
    "belirsizlik" {
        Write-Host "`n[loop] BELİRSİZLİK:" -ForegroundColor Cyan

        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }
        function Report([string] $json, [string] $tag) {
            Write-Host ("  {0,-22} alt {1,8:N0} · üst {2,8:N0} · yayılım {3,6:N1} · aralıklı {4}" -f `
                        $tag, (Field $json "paraAlt"), (Field $json "paraUst"),
                        (Field $json "paraYayilim"), ($json -match '"paraAralikli":true'))
        }

        # ---- 1. an honest minister: no range, no badge, at all.
        Send-Cmd '{"cmd":"appoint","id":"maliye","n":0}' | Out-Null
        Start-Sleep -Milliseconds 500
        $honest = Send-Cmd '{"cmd":"state"}'
        Report $honest "uzman bakan"
        Check ((Field $honest "paraYayilim") -lt 0.001) "dürüst bakanda gürültü sıfır"
        Check ($honest -notmatch '"paraAralikli":true') "gürültü sıfırken aralık gösterilmiyor"

        # ---- 2. a loyalist: a range, and its two ends must genuinely differ.
        Send-Cmd '{"cmd":"appoint","id":"maliye","n":1}' | Out-Null
        Start-Sleep -Milliseconds 500
        $loyal = Send-Cmd '{"cmd":"state"}'
        Report $loyal "sadık bakan"
        $low  = Field $loyal "paraAlt"
        $high = Field $loyal "paraUst"

        Check ($loyal -match '"paraAralikli":true') "sadık bakanda aralık gösteriliyor"
        Check ((Field $loyal "paraYayilim") -gt 0) "gürültü sıfırdan büyük"
        Check ([math]::Round($high) -gt [math]::Round($low)) "aralığın iki ucu gerçekten farklı"

        Shot "belirsizlik.png"
        $state = $loyal
        if (-not $ok) { $chainBroken = $true }
    }

    # A dark, hard-edged wedge was reported over the map. It is either something the sun is doing
    # or something in the scene, and a single screenshot cannot tell those apart — so take two,
    # identical but for the shadows.
    "golge" {
        Write-Host "`n[loop] GÖLGE TEŞHİSİ:" -ForegroundColor Cyan

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 40 | Out-Null
        Start-Sleep -Milliseconds 800

        # At the zoom where the artefact is visible — a diagnostic at the wrong framing spends
        # a build cycle answering a different question.
        foreach ($i in 1..5) { Send-Cmd '{"cmd":"press","key":"zoomin"}' | Out-Null }
        Start-Sleep -Milliseconds 900

        Shot "golge-01-acik.png"
        Send-Cmd '{"cmd":"shadows","n":0}' | Out-Null
        Start-Sleep -Milliseconds 800
        Shot "golge-02-kapali.png"

        # Second question, and it separates the two remaining suspects on its own: rotate the
        # map ninety degrees. Anything in world space turns with it; anything in screen space —
        # a UI panel, a post-processing effect — stays exactly where it was.
        Send-Cmd '{"cmd":"press","key":"q"}' | Out-Null
        Start-Sleep -Milliseconds 1200
        Shot "golge-03-donuk.png"
        Send-Cmd '{"cmd":"press","key":"e"}' | Out-Null

        # Third question: with the interface gone as well. Anything still there is in the render.
        Send-Cmd '{"cmd":"ui","n":0}' | Out-Null
        Start-Sleep -Milliseconds 800
        Shot "golge-04-arayuzsuz.png"
        Send-Cmd '{"cmd":"ui","n":1}' | Out-Null
        Send-Cmd '{"cmd":"shadows","n":1}' | Out-Null

        Write-Host "  dört kare: açık / gölgesiz / döndürülmüş / arayüzsüz" -ForegroundColor DarkGray

        Write-Host "`n[loop] HARİTAYI ÖRTEN ARAYÜZ ÖĞELERİ:" -ForegroundColor Cyan
        $overlay = Send-Cmd '{"cmd":"overlay"}'
        if ($overlay -match '"v":"(.*)"\}\}$') {
            ($Matches[1] -replace '\\n', "`n") -split "`n" | ForEach-Object { if ($_) { Write-Host "  $_" } }
        } else {
            Write-Host "  $overlay"
        }
        $state = Send-Cmd '{"cmd":"state"}'
    }

    # The menus. The game used to boot straight into a running city with no way to pause, restart
    # or leave; this walks the whole surface a player touches before and around a term.
    "menus" {
        Write-Host "`n[loop] MENÜLER:" -ForegroundColor Cyan

        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }
        function Menu {
            $s = Send-Cmd '{"cmd":"state"}'
            if ($s -match '"menu":"([^"]*)"') { return $Matches[1] }
            return "?"
        }

        Shot "menu-01-baslangic.png"
        Check ((Menu) -eq "baslangic") "oyun başlangıç ekranıyla açılıyor"

        # Settings, from the title.
        Send-Cmd '{"cmd":"click","id":"btn_title_settings"}' | Out-Null
        Start-Sleep -Milliseconds 500
        Shot "menu-02-ayarlar.png"
        Check ((Menu) -eq "ayarlar") "ayarlar açılıyor"

        Send-Cmd '{"cmd":"click","id":"btn_mute"}' | Out-Null
        Start-Sleep -Milliseconds 300
        Check ((Send-Cmd '{"cmd":"state"}') -match '"muted":true') "ayarlardan susturma çalışıyor"
        Send-Cmd '{"cmd":"click","id":"btn_mute"}' | Out-Null
        Send-Cmd '{"cmd":"click","id":"btn_settings_close"}' | Out-Null
        Start-Sleep -Milliseconds 400
        Check ((Menu) -eq "baslangic") "ayarlardan geldiği yere dönüyor"

        # Into the term.
        Send-Cmd '{"cmd":"click","id":"btn_new_term"}' | Out-Null
        Start-Sleep -Milliseconds 700
        Shot "menu-03-donem-basladi.png"
        Check ((Menu) -eq "") "YENİ DÖNEM oyunu başlatıyor"

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":2}' | Out-Null
        Wait-Turn ($t + 2) 40 | Out-Null
        Check ((Get-Turn) -ge ($t + 2)) "dönem başladıktan sonra turlar işliyor"

        # Pause on top of a running city.
        Send-Cmd '{"cmd":"key","id":"escape"}' | Out-Null
        Start-Sleep -Milliseconds 500
        Shot "menu-04-duraklatildi.png"
        Check ((Menu) -eq "duraklatildi") "ESC duraklatma menüsünü açıyor"

        Send-Cmd '{"cmd":"click","id":"btn_resume"}' | Out-Null
        Start-Sleep -Milliseconds 400
        Check ((Menu) -eq "") "DEVAM oyuna döndürüyor"

        # Restarting reloads the scene, and the scene ships empty — the world is assembled at
        # runtime. If Bootstrap does not re-run, this comes back to a black frame, which is
        # exactly the kind of break nobody notices until they press the button.
        Send-Cmd '{"cmd":"key","id":"escape"}' | Out-Null
        Start-Sleep -Milliseconds 400
        Send-Cmd '{"cmd":"click","id":"btn_restart"}' | Out-Null
        Start-Sleep -Seconds 3
        Shot "menu-05-yeniden.png"
        $fresh = Send-Cmd '{"cmd":"state"}'

        Check ($fresh -match '"buildings":(\d+)' -and [int]$Matches[1] -gt 20) "yeniden kurulan şehir gerçekten var"
        Check ($fresh -match '"turn":1\b') "yeniden kurmak dönemi başa aldı"
        Check ((Menu) -eq "") "yeniden kurmak doğrudan yeni döneme giriyor"

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 40 | Out-Null
        Check ((Get-Turn) -ge ($t + 1)) "yeniden kurulan şehirde turlar işliyor"

        # The build dock: categories replaced the 31-tile wall. A tab has to open, its tiles
        # have to arm placement, ESC has to disarm, and clicking the tab again has to close it.
        function DockField([string] $key) {
            $s = Send-Cmd '{"cmd":"state"}'
            if ($s -match "`"$key`":`"([^`"]*)`"") { return $Matches[1] }
            return "?"
        }
        Check ((DockField "acikKategori") -eq "") "dock kategorileri kapalı başlıyor"
        Send-Cmd '{"cmd":"click","id":"btn_cat_kamu"}' | Out-Null
        Start-Sleep -Milliseconds 400
        Check ((DockField "acikKategori") -eq "kamu") "KAMU sekmesi açılıyor"
        Send-Cmd '{"cmd":"click","id":"btn_build_klinik"}' | Out-Null
        Start-Sleep -Milliseconds 300
        Check ((DockField "secili") -eq "klinik") "sekmedeki karo yerleştirmeyi kuruyor"
        $armed = Send-Cmd '{"cmd":"state"}'
        Check ($armed -match '"oneri":(\d+)' -and [int]$Matches[1] -ge 1) "danışman önerilen kareleri gösteriyor"
        # The frame is taken with the clinic armed, so the blinking hint tiles are in it.
        Shot "menu-06-kategori-acik.png"

        # And a close-up on the advisor's first suggestion, because at default framing a
        # translucent tile is four pixels and "the hints render" cannot be settled by eye.
        $hx = -1; $hy = -1
        if ($armed -match '"oneriIlkX":(-?\d+)') { $hx = [int]$Matches[1] }
        if ($armed -match '"oneriIlkY":(-?\d+)') { $hy = [int]$Matches[1] }
        if ($hx -ge 0) {
            Send-Cmd ('{{"cmd":"focus","x":{0},"y":{1}}}' -f $hx, $hy) | Out-Null
            foreach ($i in 1..6) { Send-Cmd '{"cmd":"press","key":"zoomin"}' | Out-Null }
            Start-Sleep -Milliseconds 900
            Shot "menu-06b-oneri-yakin.png"
        }
        Send-Cmd '{"cmd":"key","id":"escape"}' | Out-Null
        Start-Sleep -Milliseconds 300
        Check ((DockField "secili") -eq "") "ESC kurulu yerleştirmeyi bırakıyor"
        Check ((Menu) -eq "") "yerleştirme kuruluyken ESC menü açmıyor"
        Send-Cmd '{"cmd":"click","id":"btn_cat_kamu"}' | Out-Null
        Start-Sleep -Milliseconds 300
        Check ((DockField "acikKategori") -eq "") "sekmeye ikinci tıklama kapatıyor"

        # The rail folds: Dış Dünya starts folded, a header click opens it.
        Check ((DockField "disKarti") -eq "kapali") "Dış Dünya kartı katlı başlıyor"
        Send-Cmd '{"cmd":"click","id":"btn_fold_dis"}' | Out-Null
        Start-Sleep -Milliseconds 300
        Shot "menu-07-dis-acildi.png"
        Check ((DockField "disKarti") -eq "acik") "başlığa tıklamak kartı açıyor"

        $state = Send-Cmd '{"cmd":"state"}'
        if (-not $ok) { $chainBroken = $true }
    }

    # Delegation: hand a district to a minister and watch them actually build in it. This is
    # the "şu bölge sende" loop — the game has to be playable without placing every building
    # by hand, and that claim is only true if a delegated district visibly grows on its own.
    "vekalet" {
        Write-Host "`n[loop] VEKÂLET:" -ForegroundColor Cyan

        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        $before = Send-Cmd '{"cmd":"state"}'
        $baseline = 0
        if ($before -match '"buildings":(\d+)') { $baseline = [int]$Matches[1] }
        Check ($before -match '"vekalet":\[\]') "başlangıçta hiçbir bölge devredilmemiş"

        # Four clicks on TEPE's cycle button: VALİDE → MALİYE → TARIM → GÜVENLİK → İMAR.
        foreach ($i in 1..4) {
            Send-Cmd '{"cmd":"click","id":"btn_bolge_tepe"}' | Out-Null
            Start-Sleep -Milliseconds 250
        }
        $assigned = Send-Cmd '{"cmd":"state"}'
        Check ($assigned -match '"vekalet":\["tepe:imar"\]') "TEPE, İMAR bakanına devredildi"
        Shot "vekalet-01-atama.png"

        # Eight turns under delegation. The minister must have built at least once, and the
        # note must have gone out as a telegram in the turn it happened.
        $built = 0
        foreach ($i in 1..8) {
            $t = Get-Turn
            Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
            Wait-Turn ($t + 1) 40 | Out-Null
            $s = Send-Cmd '{"cmd":"state"}'
            if ($s -match '"vekaletKurdu":(\d+)' -and [int]$Matches[1] -gt 0) { $built += [int]$Matches[1] }
        }
        $after = Send-Cmd '{"cmd":"state"}'
        $now = 0
        if ($after -match '"buildings":(\d+)') { $now = [int]$Matches[1] }

        Write-Host ("  yapı {0} → {1} · bakanın kurdukları {2}" -f $baseline, $now, $built)
        Check ($built -ge 1) "bakan en az bir yapı kurdu"
        Check ($now -gt $baseline) "şehir kendi kendine büyüdü"
        Shot "vekalet-02-sekiz-tur.png"

        # Taking the district back stops the building.
        foreach ($i in 1..2) {
            Send-Cmd '{"cmd":"click","id":"btn_bolge_tepe"}' | Out-Null
            Start-Sleep -Milliseconds 250
        }
        $back = Send-Cmd '{"cmd":"state"}'
        Check ($back -match '"vekalet":\[\]') "bölge geri alınabiliyor"

        $state = Send-Cmd '{"cmd":"state"}'
        if (-not $ok) { $chainBroken = $true }
    }

    # The bot cabinet: each of the five personalities has a measurable signature, and the
    # switch back to the formula must restore the baseline exactly. This is co-op slice 2 —
    # still one player, still no network, but the ministers now have character.
    "bot" {
        Write-Host "`n[loop] BOT KABİNESİ:" -ForegroundColor Cyan

        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }
        function Lean([string] $json, [string] $domain) {
            if ($json -match "`"domain`":`"$domain`"[^}]*`"egilim`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        function Rep([string] $json, [string] $key) {
            if ($json -match "`"reported`":\{[^}]*`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":2}' | Out-Null
        Wait-Turn ($t + 2) 40 | Out-Null

        $base = Send-Cmd '{"cmd":"state"}'
        Check ($base -match '"kaynak":"formul"') "varsayılan kaynak formül"
        $baseMaliye = Lean $base "MALİYE"

        Send-Cmd '{"cmd":"kaynak","id":"bot"}' | Out-Null
        Start-Sleep -Milliseconds 300
        $bot = Send-Cmd '{"cmd":"state"}'
        Check ($bot -match '"kaynak":"bot"') "bot kabinesi devrede"

        # ŞİŞİRİCİ (Nazif, MALİYE): the lean grows half again.
        $m = Lean $bot "MALİYE"
        Write-Host ("  MALİYE eğilim {0:N3} → {1:N3}" -f $baseMaliye, $m)
        Check ($m -gt $baseMaliye * 1.3) "ŞİŞİRİCİ şişiriyor"

        # ALARMCI (Kadri, GÜVENLİK): the lean flips negative — everything is direr.
        $gv = Lean $bot "GÜVENLİK"
        Write-Host ("  GÜVENLİK eğilim {0:N3}" -f $gv)
        Check ($gv -lt 0) "ALARMCI karartıyor"

        # YALAKA (Cevat, HALK): flattery has a floor no transparency law reaches.
        $h = Lean $bot "HALK"
        Write-Host ("  HALK eğilim {0:N3}" -f $h)
        Check ($h -ge 0.30) "YALAKA parlatıyor"

        # DÜRÜST AMA BECERİKSİZ (Müzeyyen, TARIM): wrong number, clean look — no range.
        $trueFood = Field $bot "yiyecek"
        $repFood = Rep $bot "yiyecek"
        Write-Host ("  TARIM gerçek {0:N1} · rapor {1:N1}" -f $trueFood, $repFood)
        Check ([math]::Abs($repFood - $trueFood) -ge 1) "BECERİKSİZ yanlış sayı veriyor"
        Check ($bot -match '"yiyecekAralikli":false') "BECERİKSİZ'in yanlışı temiz görünüyor (rozet yok)"

        # SAKLAYICI (Rıza, TARIM'a atanır): stop the mill, drain the bakery, and watch the
        # claimed bread stay fat while the true bread empties.
        Send-Cmd '{"cmd":"appoint","id":"tarim","n":1}' | Out-Null
        Send-Cmd '{"cmd":"block","id":"degirmen","n":1}' | Out-Null
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":4}' | Out-Null
        Wait-Turn ($t + 4) 60 | Out-Null
        $hidden = Send-Cmd '{"cmd":"state"}'
        $trueBread = Field $hidden "bread"
        $repBread = Rep $hidden "ekmek"
        Write-Host ("  SAKLAYICI: gerçek ekmek {0:N1} · bildirilen {1:N1}" -f $trueBread, $repBread)
        Check ($trueBread -lt 30) "değirmen durunca fırın gerçekten boşalıyor"
        Check ($repBread -gt ($trueBread + 25)) "SAKLAYICI tıkanıklığı toplamın arkasına saklıyor"
        Send-Cmd '{"cmd":"block","id":"degirmen","n":0}' | Out-Null
        Shot "bot-01-saklayici.png"

        # And back: the formula baseline must return exactly.
        Send-Cmd '{"cmd":"kaynak","id":"formul"}' | Out-Null
        Start-Sleep -Milliseconds 300
        $back = Send-Cmd '{"cmd":"state"}'
        Check ($back -match '"kaynak":"formul"') "formüle dönülebiliyor"
        $mb = Lean $back "MALİYE"
        Write-Host ("  MALİYE eğilim (dönüş) {0:N3}" -f $mb)
        Check (([math]::Abs($mb - $baseMaliye) -lt 0.1) -and ($mb -lt $m)) "dönüşte eğilim formül tabanına indi"

        $state = Send-Cmd '{"cmd":"state"}'
        if (-not $ok) { $chainBroken = $true }
    }

    # Hot-seat: a human takes the Tarım desk, lies by hand, and the governor's screen shows
    # the lie with a clean face. The turn gate matters as much as the lie: no human report,
    # no turn. This is co-op slice 3 — the mode's whole idea, before any network exists.
    "hotseat" {
        Write-Host "`n[loop] HOT-SEAT:" -ForegroundColor Cyan

        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }
        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        function Rep([string] $json, [string] $key) {
            if ($json -match "`"reported`":\{[^}]*`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 40 | Out-Null

        # Seat a human at Tarım (seat 2, per COOP.md).
        Send-Cmd '{"cmd":"koltuk","seat":2,"id":"insan"}' | Out-Null
        Start-Sleep -Milliseconds 300
        $s = Send-Cmd '{"cmd":"state"}'
        Check ($s -match '"koltuklar":\["maliye:formul","tarim:insan"') "Tarım koltuğu insanda"
        Check ((Field $s "raporBekleyen") -eq 1) "rapor bekleniyor"

        # The gate: no report, no turn.
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Start-Sleep -Seconds 3
        Check ((Get-Turn) -eq $t) "rapor yokken tur bitmiyor"

        # The human lies: half again the granary, a comfortable buffer.
        $trueFood = Field $s "yiyecek"
        $claim = [math]::Round($trueFood * 1.5)
        Send-Cmd ('{{"cmd":"report","seat":2,"line":"tahil","value":{0}}}' -f $claim) | Out-Null
        Send-Cmd '{"cmd":"report","seat":2,"line":"tampon","value":9}' | Out-Null
        Send-Cmd '{"cmd":"submit","seat":2}' | Out-Null
        Start-Sleep -Milliseconds 300

        $lied = Send-Cmd '{"cmd":"state"}'
        Check ((Field $lied "raporBekleyen") -eq 0) "rapor gönderildi"
        $repFood = Rep $lied "yiyecek"
        Write-Host ("  ambar gerçek {0:N0} · insan bildirdi {1:N0} · vali görüyor {2:N0}" -f $trueFood, $claim, $repFood)
        Check ([math]::Abs($repFood - $claim) -le [math]::Max(1, $claim * 0.02)) "valinin gördüğü, insanın yazdığı"
        Check ($lied -match '"yiyecekAralikli":false') "insan yalanı temiz yüzle geliyor (rozet yok)"

        # The gate opens: the turn resolves, and next turn the report is void again.
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 40 | Out-Null
        Check ((Get-Turn) -eq ($t + 1)) "rapor sonrası tur işliyor"
        $fresh = Send-Cmd '{"cmd":"state"}'
        Check ((Field $fresh "raporBekleyen") -eq 1) "yeni tur yeni rapor istiyor"

        # The desk screen itself: open it from the portrait, photograph it, close it.
        Send-Cmd '{"cmd":"click","id":"btn_minister_tarim"}' | Out-Null
        Start-Sleep -Milliseconds 500
        Shot "hotseat-01-bakan-ekrani.png"
        Send-Cmd '{"cmd":"click","id":"btn_rapor_gonder"}' | Out-Null
        Start-Sleep -Milliseconds 300
        $desk = Send-Cmd '{"cmd":"state"}'
        Check ((Field $desk "raporBekleyen") -eq 0) "ekrandan GÖNDER de raporu mühürlüyor"

        # Seat back to the formula: single player must return exactly.
        Send-Cmd '{"cmd":"koltuk","seat":2,"id":"formul"}' | Out-Null
        Start-Sleep -Milliseconds 300
        $back = Send-Cmd '{"cmd":"state"}'
        Check ($back -match '"koltuklar":\["maliye:formul","tarim:formul"') "koltuk formüle döndü"
        Check ((Field $back "raporBekleyen") -eq 0) "bekleyen rapor kalmadı"

        $state = Send-Cmd '{"cmd":"state"}'
        if (-not $ok) { $chainBroken = $true }
    }

    # The telegraph office and the secret objectives: co-op slice 4. Objectives are dealt to
    # every desk, telegrams are sealed into a permanent archive, a human's own words reach
    # the governor's card verbatim, the ferman is one a turn, and bot desks find each other
    # in back rooms the governor can only count.
    "telgraf" {
        Write-Host "`n[loop] TELGRAF + HEDEFLER:" -ForegroundColor Cyan

        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }
        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }

        $s = Send-Cmd '{"cmd":"state"}'

        # Objectives: five desks, five distinct, all from the pool.
        $ids = [regex]::Matches($s, '"hedef":"([a-z0-9]+)"') | ForEach-Object { $_.Groups[1].Value }
        Write-Host ("  hedefler: {0}" -f ($ids -join " · "))
        Check ($ids.Count -eq 5) "beş koltuğun beşine de hedef dağıtıldı"
        Check (($ids | Select-Object -Unique).Count -eq 5) "hedefler birbirinden farklı"

        # The founding cabinet's first words are already on record.
        $arsiv0 = Field $s "telgrafArsivi"
        Check ($arsiv0 -ge 5) "kuruluş telgrafları mühürlendi"

        # A human minister's own words reach the governor verbatim.
        Send-Cmd '{"cmd":"koltuk","seat":2,"id":"insan"}' | Out-Null
        Send-Cmd '{"cmd":"report","seat":2,"line":"tahil","value":400}' | Out-Null
        Send-Cmd '{"cmd":"submit","seat":2}' | Out-Null
        Send-Cmd '{"cmd":"telgraf","seat":2,"path":"Ambar doludur. Endişeye mahal yoktur."}' | Out-Null
        Start-Sleep -Milliseconds 300
        $t1 = Send-Cmd '{"cmd":"state"}'
        Check ($t1 -match 'Ambar doludur\. Endişeye mahal yoktur\.') "insanın telgrafı valinin kartında, kelimesi kelimesine"
        Check ((Field $t1 "telgrafArsivi") -gt $arsiv0) "telgraf arşive mühürlendi"

        # The ferman: once a turn, no more.
        Send-Cmd '{"cmd":"ferman","path":"Fırınlar gece de çalışacaktır."}' | Out-Null
        Start-Sleep -Milliseconds 200
        $f = Send-Cmd '{"cmd":"state"}'
        Check ((Field $f "fermanTuru") -eq (Get-Turn)) "ferman yayınlandı"
        $second = Send-Cmd '{"cmd":"ferman","path":"İkinci ferman denemesi."}'
        Check ($second -match '"ok":false') "tur başına tek ferman"

        # Bot desks find each other; the governor can only count the doors.
        Send-Cmd '{"cmd":"koltuk","seat":3,"id":"bot"}' | Out-Null
        Send-Cmd '{"cmd":"koltuk","seat":4,"id":"bot"}' | Out-Null
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 40 | Out-Null
        $ch = Send-Cmd '{"cmd":"state"}'
        Write-Host ("  özel kanal: {0}" -f (Field $ch "ozelKanal"))
        Check ((Field $ch "ozelKanal") -ge 1) "bot masaları özel kanal açtı"

        # The archive keeps growing every turn. The human desk re-seals first — a new turn
        # voided the old report, and the gate is doing its job.
        $arsiv1 = Field $ch "telgrafArsivi"
        Send-Cmd '{"cmd":"submit","seat":2}' | Out-Null
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 40 | Out-Null
        $ch2 = Send-Cmd '{"cmd":"state"}'
        Check ((Field $ch2 "telgrafArsivi") -gt $arsiv1) "arşiv her tur büyüyor"

        # The desk screen carries the secret objective; photograph it.
        Send-Cmd '{"cmd":"click","id":"btn_minister_tarim"}' | Out-Null
        Start-Sleep -Milliseconds 500
        Shot "telgraf-01-bakan-masasi.png"
        Send-Cmd '{"cmd":"click","id":"btn_rapor_kapat"}' | Out-Null

        # Back to a plain cabinet.
        Send-Cmd '{"cmd":"koltuk","seat":2,"id":"formul"}' | Out-Null
        Send-Cmd '{"cmd":"koltuk","seat":3,"id":"formul"}' | Out-Null
        Send-Cmd '{"cmd":"koltuk","seat":4,"id":"formul"}' | Out-Null
        Start-Sleep -Milliseconds 200
        $back = Send-Cmd '{"cmd":"state"}'
        Check ((Field $back "raporBekleyen") -eq 0) "koltuklar formüle döndü"

        $state = Send-Cmd '{"cmd":"state"}'
        if (-not $ok) { $chainBroken = $true }
    }

    # The player's own 3D models: konut.glb and tapinak.glb from StreamingAssets must load,
    # replace their procedural forms, stand exactly on the ground, and keep their textures
    # (a real shader, not a stripped one). Close-up frames are taken for the eye check.
    "modeller" {
        Write-Host "`n[loop] DIŞ MODELLER:" -ForegroundColor Cyan

        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }
        function Model([string] $json, [string] $key) {
            if ($json -match "`"modeller`":\{[^}]*`"$key`":(-?[\d.]+|true|false|`"[^`"]*`")") { return $Matches[1] }
            return ""
        }

        # Loading is async; give it up to 20 s like the crowd skins get. The bar scales with
        # delivery: every glb in the folder must load, however many there are.
        $expected = (Get-ChildItem (Join-Path $PSScriptRoot "..\Assets\StreamingAssets\Models\buildings") -Filter *.glb).Count
        $loaded = $false
        foreach ($i in 1..20) {
            $s = Send-Cmd '{"cmd":"state"}'
            if ($s -match "`"modeller`":\{`"yuklu`":$expected") { $loaded = $true; break }
            Start-Sleep -Seconds 1
        }
        Check $loaded "klasördeki $expected modelin hepsi yüklendi"
        Check ($s -match '"kapsanan":\[[^\]]*"konut"') "konut modeli devrede"
        Check ($s -match '"kapsanan":\[[^\]]*"tapinak"') "tapınak modeli devrede"
        Check ($s -match '"kapsanan":\[[^\]]*"toplukonut"') "toplu konut modeli devrede"
        Check ($s -match '"kapsanan":\[[^\]]*"degirmen"') "değirmen modeli devrede"

        $shader = Model $s "shader"
        Write-Host ("  shader: {0}" -f $shader)
        Check ($shader -ne '""' -and $shader.Length -gt 2) "modeller gerçek bir shader ile çiziliyor"

        $minY = [double](Model $s "enKotuMinY")
        Write-Host ("  en kötü zemin sapması: {0:N3}" -f $minY)
        Check ([math]::Abs($minY) -le 0.05) "modellerin tabanı yerde (± 0.05)"

        # The probe bar still holds for the whole city, models included.
        Check ($s -match '"zemin":\{"havada":0') "hiçbir yapı havada değil"

        # A temple placed fresh, then both photographed up close — the eye check.
        Send-Cmd '{"cmd":"build","id":"tapinak","x":24,"y":14}' | Out-Null
        Start-Sleep -Milliseconds 800
        Send-Cmd '{"cmd":"focus","x":24,"y":14}' | Out-Null
        foreach ($i in 1..7) { Send-Cmd '{"cmd":"press","key":"zoomin"}' | Out-Null }
        Start-Sleep -Milliseconds 900
        Shot "model-01-tapinak.png"

        Send-Cmd '{"cmd":"focus","x":28,"y":16}' | Out-Null
        Start-Sleep -Milliseconds 700
        Shot "model-02-konutlar.png"

        # The mill by the fields, and a freshly-placed apartment block.
        Send-Cmd '{"cmd":"focus","x":13,"y":8}' | Out-Null
        Start-Sleep -Milliseconds 700
        Shot "model-03-degirmen.png"
        Send-Cmd '{"cmd":"build","id":"toplukonut","x":26,"y":15}' | Out-Null
        Start-Sleep -Milliseconds 800
        Send-Cmd '{"cmd":"focus","x":26,"y":15}' | Out-Null
        Start-Sleep -Milliseconds 700
        Shot "model-04-toplukonut.png"

        # The food chain in the player's own models: fields, the bakery, and a granary
        # placed beside a rationing depot.
        Send-Cmd '{"cmd":"focus","x":8,"y":4}' | Out-Null
        Start-Sleep -Milliseconds 700
        Shot "model-05-tarlalar.png"
        Send-Cmd '{"cmd":"focus","x":20,"y":15}' | Out-Null
        Start-Sleep -Milliseconds 700
        Shot "model-06-firin.png"
        Send-Cmd '{"cmd":"grant","n":500}' | Out-Null
        Send-Cmd '{"cmd":"build","id":"ambar","x":22,"y":16}' | Out-Null
        Send-Cmd '{"cmd":"build","id":"tayinlama","x":24,"y":16}' | Out-Null
        Start-Sleep -Milliseconds 800
        Send-Cmd '{"cmd":"focus","x":23,"y":16}' | Out-Null
        Start-Sleep -Milliseconds 700
        Shot "model-07-ambar-tayin.png"

        # Civic row: the market, the bath house and the police station.
        Send-Cmd '{"cmd":"grant","n":900}' | Out-Null
        Send-Cmd '{"cmd":"build","id":"pazar","x":20,"y":16}' | Out-Null
        Send-Cmd '{"cmd":"build","id":"hamam","x":26,"y":16}' | Out-Null
        Send-Cmd '{"cmd":"build","id":"karakol","x":27,"y":17}' | Out-Null
        Start-Sleep -Milliseconds 800
        Send-Cmd '{"cmd":"focus","x":24,"y":16}' | Out-Null
        Start-Sleep -Milliseconds 700
        Shot "model-09-kamu.png"

        # Industry: the weaving mill, the warehouse and the power station side by side.
        Send-Cmd '{"cmd":"grant","n":800}' | Out-Null
        Send-Cmd '{"cmd":"build","id":"dokuma","x":21,"y":17}' | Out-Null
        Send-Cmd '{"cmd":"build","id":"depo","x":23,"y":17}' | Out-Null
        Send-Cmd '{"cmd":"build","id":"santral","x":25,"y":17}' | Out-Null
        Start-Sleep -Milliseconds 800
        Send-Cmd '{"cmd":"focus","x":23,"y":17}' | Out-Null
        Start-Sleep -Milliseconds 700
        Shot "model-08-sanayi.png"

        $state = Send-Cmd '{"cmd":"state"}'
        if (-not $ok) { $chainBroken = $true }
    }

    # Co-op over a real wire: the local Node lobby server, the Unity client as governor and
    # a Node client as a minister, one full phase round trip. No Cloudflare involved —
    # exactly the point of the three-layer split.
    "coop" {
        Write-Host "`n[loop] CO-OP AĞ:" -ForegroundColor Cyan

        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }
        function Lobi([string] $json, [string] $key) {
            if ($json -match "`"lobi`":\{[^}]*`"$key`":(-?[\d.]+|true|false|`"[^`"]*`")") { return $Matches[1] }
            return ""
        }

        $webRoot = Join-Path $PSScriptRoot "..\.."
        $server = Start-Process node -ArgumentList "web/dev-server.mjs" -WorkingDirectory $webRoot `
                  -PassThru -WindowStyle Hidden -Environment @{ PORT = "8492" }
        Start-Sleep -Seconds 2

        try {
            Send-Cmd '{"cmd":"baglan","path":"ws://127.0.0.1:8492","id":"TEST42"}' | Out-Null
            Start-Sleep -Seconds 2
            $s = Send-Cmd '{"cmd":"state"}'
            Check ((Lobi $s "bagli") -eq "true") "Unity lobiye bağlandı"
            Check ((Lobi $s "faz") -eq '"lobi"') "faz: lobi"

            Send-Cmd '{"cmd":"lobikoltuk","n":0}' | Out-Null
            Start-Sleep -Milliseconds 800
            $s = Send-Cmd '{"cmd":"state"}'
            Check ((Lobi $s "koltuk") -eq "0") "vali koltuğu alındı"

            $client = Start-Process node -ArgumentList "web/test-client.mjs","ws://127.0.0.1:8492","TEST42","2","6000" `
                      -WorkingDirectory $webRoot -PassThru -WindowStyle Hidden
            Start-Sleep -Seconds 2
            $s = Send-Cmd '{"cmd":"state"}'
            Check ((Lobi $s "oyuncu") -eq "2") "ikinci oyuncu (Node) odada"

            Send-Cmd '{"cmd":"lobibaslat"}' | Out-Null
            Start-Sleep -Seconds 2
            $s = Send-Cmd '{"cmd":"state"}'
            Check ((Lobi $s "faz") -eq '"bakan"') "vali başlattı, faz: bakan"

            # The Node minister reports on its own; the server advances the phase and BOTH
            # clients hear it — this is the whole loop, over real sockets.
            $deadline = (Get-Date).AddSeconds(15)
            $vali = $false
            while ((Get-Date) -lt $deadline) {
                Start-Sleep -Milliseconds 800
                $s = Send-Cmd '{"cmd":"state"}'
                if ((Lobi $s "faz") -eq '"vali"') { $vali = $true; break }
            }
            Check $vali "bakan raporlayınca faz valiye döndü"

            $clientDone = $client.WaitForExit(10000)
            Check ($clientDone -and $client.ExitCode -eq 0) "Node bakanı turunu temiz kapattı"
        }
        finally {
            if ($client -and -not $client.HasExited) { Stop-Process -Id $client.Id -Force -ErrorAction SilentlyContinue }
            if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue }
        }

        $state = Send-Cmd '{"cmd":"state"}'
        if (-not $ok) { $chainBroken = $true }
    }

    # Audio ships with no files: every clip is synthesized at startup. An unattended run cannot
    # listen, so the bus reports itself — how many clips exist, and whether the two ambient
    # voices actually track the city. A drone wired to nothing sounds exactly like a drone.
    "ses" {
        Write-Host "`n[loop] SES:" -ForegroundColor Cyan

        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        function Audio([string] $json, [string] $key) {
            if ($json -match "`"audio`":\{[^}]*`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":2}' | Out-Null
        Wait-Turn ($t + 2) 40 | Out-Null
        Start-Sleep -Milliseconds 1200
        $calm = Send-Cmd '{"cmd":"state"}'

        Write-Host ("  sakin şehir : klip {0:N0} · uğultu {1:N2} · kalabalık {2:N2} · hoşnutsuzluk {3:N1}" -f `
                    (Audio $calm "clips"), (Audio $calm "drone"), (Audio $calm "murmur"), `
                    (Field $calm "enYuksekHosnutsuzluk"))
        Check ((Audio $calm "clips") -ge 9) "bütün sesler çalışma anında sentezlendi"
        Check ((Audio $calm "drone") -gt 0) "uğultu çalıyor"
        Check ((Audio $calm "murmur") -lt 0.05) "sakin şehirde kalabalık sesi yok"

        # Same escalation the crowd scenario uses: a struck quarter plus decrees nobody likes.
        Write-Host "`n[loop] hoşnutsuzluk tırmandırılıyor..." -ForegroundColor Cyan
        foreach ($id in @("dokuma","santral","tarla","kuyu")) {
            Send-Cmd ('{"cmd":"block","id":"' + $id + '","n":1}') | Out-Null
        }
        $guard = 0
        while ($guard -lt 14) {
            Send-Cmd '{"cmd":"decree","id":"serbest_fiyat"}' | Out-Null
            Send-Cmd '{"cmd":"decree","id":"imar_affi"}' | Out-Null
            $t = Get-Turn
            $s = Send-Cmd '{"cmd":"state"}'
            if ($s -notmatch '"pendingEvent":""') { Send-Cmd '{"cmd":"event","n":2}' | Out-Null }
            if ($s -match '"electionPending":true') { Send-Cmd '{"cmd":"election","id":"ertele"}' | Out-Null }
            if ($s -match '"charterPending":true') {
                Send-Cmd '{"cmd":"clause","id":"herkese_ekmek"}'   | Out-Null
                Send-Cmd '{"cmd":"clause","id":"soz_serbest"}'     | Out-Null
                Send-Cmd '{"cmd":"clause","id":"meclis_ustundur"}' | Out-Null
            }
            Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
            Wait-Turn ($t + 1) 40 | Out-Null
            if ((Audio (Send-Cmd '{"cmd":"state"}') "murmur") -gt 0.05) { break }
            $guard++
        }

        Start-Sleep -Milliseconds 1500
        $angry = Send-Cmd '{"cmd":"state"}'
        Write-Host ("  öfkeli şehir: uğultu {0:N2} · kalabalık {1:N2} · hoşnutsuzluk {2:N1}" -f `
                    (Audio $angry "drone"), (Audio $angry "murmur"), (Field $angry "enYuksekHosnutsuzluk"))

        Check ((Audio $angry "drone") -gt (Audio $calm "drone")) "uğultu hoşnutsuzlukla kalınlaştı"
        Check ((Audio $angry "murmur") -gt (Audio $calm "murmur")) "kalabalık sesi ayaklanmadan önce kabardı"

        Send-Cmd '{"cmd":"mute","n":1}' | Out-Null
        Start-Sleep -Milliseconds 300
        $muted = Send-Cmd '{"cmd":"state"}'
        Check ($muted -match '"muted":true') "susturma çalışıyor"
        Send-Cmd '{"cmd":"mute","n":0}' | Out-Null

        Shot "ses.png"
        $state = $angry
        if (-not $ok) { $chainBroken = $true }
    }

    # Every building must actually be placeable. A table entry that no tile in the city can
    # satisfy is a dead hotbar tile, and the only way to find one is to try to build it.
    "yapilar" {
        Write-Host "`n[loop] YAPILAR:" -ForegroundColor Cyan

        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        # The city indices are recomputed on the tick, not on placement, so the baseline has to be
        # read after a turn has actually run — otherwise both readings are the untouched default.
        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 40 | Out-Null

        # Fund the experiment: everything at once costs far more than a founding treasury holds,
        # and this scenario is asking whether a parcel is legal, not whether the city is rich.
        Send-Cmd '{"cmd":"grant","n":40000}' | Out-Null
        $before = Send-Cmd '{"cmd":"state"}'
        $healthBefore = Field $before "saglik"

        $ids = @("konut","toplukonut","tarla","degirmen","firin","ambar","tayinlama",
                 "ocak","islik","depo","pazar","borsa","dokuma",
                 "kuyu","sukemeri","aritma","santral","yol",
                 "klinik","hastane","okul","kutuphane","hamam","park","tapinak","matbaa","anit",
                 "karakol","kontrol","kisla","tersane")

        # Sweep the map for each one rather than guessing a tile: a refusal only counts if every
        # parcel refuses it.
        $script:placed = 0
        $failed = @()
        foreach ($id in $ids) {
            $done = $false
            foreach ($x in 4..40) {
                if ($done) { break }
                foreach ($y in 3..26) {
                    $reply = Send-Cmd ('{"cmd":"build","id":"' + $id + '","x":' + $x + ',"y":' + $y + '}')
                    if ($reply -match '"ok":true') { $done = $true; $script:placed++; break }
                }
            }
            if (-not $done) { $failed += $id }
        }

        $t = Get-Turn
        Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
        Wait-Turn ($t + 1) 60 | Out-Null

        $after = Send-Cmd '{"cmd":"state"}'
        $healthAfter = Field $after "saglik"

        Write-Host ("  {0}/{1} yapı haritaya kondu" -f $script:placed, $ids.Count)
        if ($failed.Count) {
            Write-Host ("  kurulamayan: {0}" -f ($failed -join ', ')) -ForegroundColor Red
        }
        Write-Host ("  sağlık {0:N1} -> {1:N1}" -f $healthBefore, $healthAfter)

        # §11 asks for roads that repave as land value rises. Land value is not a stat here, so
        # paving is founding wealth plus what has been built since — which means building has to
        # move it, or the road surface is decoration pretending to be a signal.
        function Paving([string] $json, [string] $name) {
            if ($json -match "`"name`":`"$name`"[^}]*?`"paving`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $pavedBefore = Paving $before "LİMAN"
        $pavedAfter  = Paving $after  "LİMAN"
        Write-Host ("  LİMAN kaplama {0:N2} -> {1:N2}" -f $pavedBefore, $pavedAfter)

        Check ($ids.Count -ge 30) "yapı tablosu tasarım çıtasında (en az 30)"
        Check ($failed.Count -eq 0) "her yapının kurulabileceği bir parsel var"
        Check ($healthAfter -gt $healthBefore + 5) "sağlık binaları endeksi gerçekten yükseltti"
        Check ($pavedAfter -gt $pavedBefore) "mahalleye inşa etmek yollarını yeniledi"

        Shot "yapilar.png"
        $state = $after
        if (-not $ok) { $chainBroken = $true }
    }

    "deste" {
        Write-Host "`n[loop] DESTE — 55 tur oynanıyor, her kart cevaplanıyor:" -ForegroundColor Cyan

        function Field([string] $json, [string] $key) {
            if ($json -match "`"$key`":(-?[\d.]+)") { return [double]$Matches[1] }
            return [double]::NaN
        }
        $ok = $true
        function Check([bool] $pass, [string] $label) {
            if ($pass) { Write-Host "  ✔ $label" -ForegroundColor Green }
            else { Write-Host "  ✘ $label" -ForegroundColor Red; $script:ok = $false }
        }

        $t = Get-Turn
        $script:answered = 0
        $ended = ""
        while ($t -lt 55) {
            $s = Send-Cmd '{"cmd":"state"}'

            # Rotating the answers walks the axes hard, so this run can genuinely collapse before
            # turn 55. That is the game working; the deck is still measurable from what it dealt.
            if ($s -match '"isOver":true') {
                if ($s -match '"ending":"([^"]*)"') { $ended = $Matches[1] }
                Write-Host ("  koşu {0}. turda bitti: {1}" -f $t, $ended) -ForegroundColor DarkYellow
                break
            }

            if ($s -match '"charterPending":true') {
                Send-Cmd '{"cmd":"clause","id":"herkese_ekmek"}'   | Out-Null
                Send-Cmd '{"cmd":"clause","id":"soz_serbest"}'     | Out-Null
                Send-Cmd '{"cmd":"clause","id":"meclis_ustundur"}' | Out-Null
            }
            if ($s -match '"electionPending":true') { Send-Cmd '{"cmd":"election","id":"yap"}' | Out-Null }
            if ($s -notmatch '"pendingEvent":""') {
                Send-Cmd ('{"cmd":"event","n":' + ($script:answered % 3) + '}') | Out-Null
                $script:answered++
            }

            Send-Cmd '{"cmd":"endturn","n":1}' | Out-Null
            Wait-Turn ($t + 1) 40 | Out-Null
            $t = Get-Turn
        }

        $state = Send-Cmd '{"cmd":"state"}'
        Shot "deste.png"

        $deck = [int](Field $state "deckSize")
        $fired = @()
        if ($state -match '"firedEvents":\[(.*?)\]') {
            $fired = ($Matches[1] -split ',') | Where-Object { $_ -ne '' }
        }
        $distinct = $fired.Count

        Write-Host ("  deste {0} kart · bir dönemde {1} ayrı kart geldi · {2} kart cevaplandı" `
                    -f $deck, $distinct, $script:answered)
        Write-Host ("  gelenler: {0}" -f (($fired -replace '"','') -join ', ')) -ForegroundColor DarkGray

        Check ($deck -ge 50) "deste tasarım çıtasında (en az 50 kart)"
        Check ($script:answered -ge 10) "bir dönem boyunca kriz akışı var"
        Check ($distinct -ge 12) "deste kendini tekrar etmiyor"

        if (-not $ok) { $chainBroken = $true }
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
# An explicit success code: without it $LASTEXITCODE keeps whatever the last native command
# returned (robocopy says 1 for "files copied"), and a suite loop reads a clean run as failed.
exit 0








