# MEŞRUİYET — 2D Şehir Kurma & Yönetme Oyunu (Godot 4 / Summer Engine)

Godot 4, 2D, İşleyici: **Uyumluluk**. Aşağıdaki metnin tamamını tek seferde AI'ya yapıştır.

---

You are generating a COMPLETE, FINISHED, playable **2D city-building and political-management game** inside an existing, EMPTY **Godot 4.4** project (Summer Engine, a Godot fork). Renderer is **Compatibility**, the game is **2D only**.

Output every file with its **exact `res://` path** and its **full contents** — `.gd` scripts, `.tscn` scenes in Godot's text scene format, `.gdshader` files, and the exact lines to add to `project.godot`. Do not stub anything, do not leave TODOs, do not ask me questions, do not summarize — ship the entire game in one pass. If you run out of room, stop mid-file and I will say "continue"; never skip a file or replace it with a comment.

**Hard constraints**
- **Zero external assets.** No `.png`, `.wav`, `.ogg`, no downloaded fonts. Every visual is drawn in code (`_draw()`, `Polygon2D`, `Line2D`, `ColorRect`, `StyleBoxFlat`) or generated (`NoiseTexture2D`, `GradientTexture2D`, shaders). Every sound is synthesized at runtime into an `AudioStreamWAV`. Use Godot's built-in default font.
- All in-game text is in **Turkish**. All code identifiers, comments and file names in English.
- Static typing everywhere. No untyped `var`. No `get_node("../../..")` chains — communicate with signals.
- Runs with zero errors and zero warnings in the Godot output panel on first F5.

---

## 1. THE GAME

**Title:** "MEŞRUİYET" — subtitle "Bir Şehir Kurma ve Kaybetme Simülasyonu"

You are the founder-governor of a new city on a river delta, starting with 400 settlers, a treasury, and a blank grid. You must grow it into a functioning city over **60 turns (each turn = one season, 15 years)**. You will not survive by being nice and you will not survive by being cruel. You will survive — if you survive — by knowing exactly how far you already went.

**Genre:** Turn-based city builder fused with a political-drift simulator. Think *SimCity* placement, *Frostpunk* law-book escalation, and *Democracy*'s faction arithmetic, but the core subject is **ideological drift under pressure**.

**Tone:** Dry, bureaucratic, quietly ominous. The UI is a governor's desk: ledgers, stamped decrees, a newspaper, telegrams from ministers. Text is terse and official — humor comes from euphemism ("Gönüllü Yeniden Yerleşim Programı"), never from jokes.

---

## 2. DESIGN PILLARS (obey these — they are the whole game)

1. **Placement is a political act.** Every building has a *functional* effect AND a *political* effect that depends on **which district it is placed in**. The map is where politics is played, not a second menu bolted onto a builder.
2. **The player must never choose "become a dictator."** Nobody picks that from a menu. Instead: every crisis has a *clean* response that is slow and expensive, and a *fast* response that shifts an axis. Under time pressure the player takes the fast one. Twelve reasonable emergency measures later there are checkpoints on the streets. **Drift must be emergent, never selected.** Never label an option as extreme, evil, or authoritarian — label it by what it does ("Tahıl El Koyma Kararnamesi").
3. **Each extreme fails through a different mechanic, not a lose screen.** See §6.
4. **Extremes are powerful, not stupid.** Radical zones unlock tools that genuinely solve otherwise-unsolvable crises. The cost is fragility, not weakness. If a player can win by hugging the center, the game is broken — the center must be too slow to survive the mid-game crises alone.
5. **Truth is a resource with a political price.** See §5. This is the standout system; build it fully.

---

## 3. THE CITY LAYER

**Map:** a `48 × 32` tile grid, 32 px tiles, top-down. Camera pans with middle-drag/WASD and zooms with the wheel (0.5×–2.5×). River along one edge, fertile soil near it, ore in the hills, marsh that must be drained.

**Districts:** the map is partitioned into **6 named districts**, each with its own `grievance: float`, `wealth: float`, `population: int` and **faction affinity**:
- **LİMAN** — docks and workers. Affinity: İşçiler.
- **TEPE** — the wealthy hill. Affinity: Tüccarlar.
- **ESKİ ŞEHİR** — old town, market, temple. Affinity: Gelenek.
- **SANAYİ** — industry, smoke. Affinity: İşçiler.
- **ÜNİVERSİTE** — schools, press, clinics. Affinity: Aydınlar.
- **KIŞLA** — garrison and depots. Affinity: Ordu.

Districts start mostly empty and grow as you zone them. A district can riot, strike, or fall out of your control **independently** — losing a district removes its production and its tiles from your build range.

**Resources (stockpiles + per-turn flow, both shown):** `Para (₺)`, `Yiyecek`, `Su`, `Enerji`, `Malzeme`, `İşgücü`. Plus city-wide indices 0–100: `Sağlık`, `Eğitim`, `Güvenlik`, `Kültür`, `Kirlilik`.

**Buildings — implement at least 30**, each with: cost, upkeep, tile footprint, worker demand, output, adjacency rules, and **a political effect that varies by district**. Examples of the pattern you must follow throughout:

| Building | Function | Political effect |
|---|---|---|
| Tahıl Ambarı | +Yiyecek buffer | none — a rare politically neutral building |
| Dokuma Atölyesi | +₺, needs 40 işgücü | in SANAYİ: +Tüccar loyalty. In LİMAN: +₺ bonus (cheap labor) but +grievance and +Kirlilik |
| Karakol | +Güvenlik in radius | in LİMAN: −İşçi loyalty, +Otorite. In TEPE: +Tüccar loyalty, no axis shift |
| Matbaa (press) | +Eğitim, **+Şeffaflık** | +Aydın loyalty, −Otorite, surfaces scandals |
| Park | +Mutluluk in radius | in TEPE: +Tüccar. In LİMAN: +İşçi, −Tüccar ("neden onlara?") |
| Anıt (your statue) | +Meşruiyet, −₺₺ | +Otorite strongly, −Aydın |
| Tapınak | +Kültür, −grievance | +Gelenek loyalty, −Aydın, −İlerleme |
| Tayınlama Deposu | food to everyone regardless of ₺ | +Eşitlik, −Tüccar |
| Serbest Borsa | +₺₺₺ | +Sermaye, +Tüccar, housing costs rise → +LİMAN grievance |
| Toplu Konut | cheap housing, high density | +Eşitlik, −Tüccar, −TEPE property value |
| Kontrol Noktası | −smuggling, +Güvenlik | +Otorite, +grievance in its own district |

Zoning: the player paints `KONUT` (3 density tiers), `TİCARET`, `SANAYİ`, `TARIM` and then places specific service buildings. Housing tiers unlock by law and by land value. Roads/water/power must connect (simple flood-fill connectivity check — show unserviced buildings with a red icon).

**Needs & grievance:** every 100 population demands food, water, housing, a job, and a service basket. Each unmet need adds grievance **to that district**, weighted by the district's own expectations (TEPE tolerates less discomfort than LİMAN; LİMAN tolerates less injustice than TEPE). Grievance decays slowly when needs are met. District grievance > 70 for 3 consecutive turns → unrest event. > 90 → the district acts on its own.

---

## 4. THE GOVERNANCE LAYER

### Two ideological axes, each `-100 … +100`

- **OTORİTE ↔ ÖZGÜRLÜK** (`axis_order`)
- **SERMAYE ↔ EŞİTLİK** (`axis_economy`)

Show them as two horizontal meters with a marker and, behind it, a faint trail of the last 15 turns so the player can *see the drift they didn't notice*. This trail is important — build it.

**Zone bands (absolute value):**
- `0–39` **PRAGMATİK** — no modifiers, no special unlocks. Safe, and too slow to survive the mid-game alone.
- `40–69` **KARARLI** — unlocks that side's strong laws and buildings. Mild penalty from the opposing faction.
- `70–89` **RADİKAL** — unlocks that side's decisive crisis tools. Opposing factions turn hostile; the failure mechanic for that quadrant switches on at partial strength.
- `90–100` **DÖNÜŞSÜZ** — the most powerful tools in the game are available. A one-time warning event fires, a visible `GERİ DÖNÜŞ` countdown of 8 turns begins, and the quadrant failure mechanic runs at full strength. Pulling back below 80 is possible but requires a genuine sacrifice (repeal your keystone law, lose 30 legitimacy, or hand a district's administration to a faction). If the countdown reaches 0 while still ≥90, the corresponding collapse ending fires.

### Legitimacy (`Meşruiyet`, 0–100, starts 60)
Spent to pass laws against council opposition and to survive scandals. Gained by meeting needs, winning crises, and holding elections. At 0 you are finished regardless of anything else.

### Factions — 5, loyalty 0–100, all start at 50
`TÜCCARLAR`, `İŞÇİLER`, `ORDU`, `GELENEK`, `AYDINLAR`.
Each has a **passive gift while ≥65** (Tüccarlar: −15% build cost. İşçiler: +10% işgücü. Ordu: +Güvenlik, coup immunity. Gelenek: grievance decays faster. Aydınlar: +research, +Şeffaflık) and **a threat while ≤25** (sabotage, strike, coup clock, schism, leak). **Two hostile factions simultaneously = a compound crisis.** Ordu ≤25 while Meşruiyet ≤35 starts a 5-turn coup countdown, announced only if your information is reliable (§5) — otherwise it hits without warning.

### Instruments of government
- **KARARNAME (decrees):** immediate, cheap, per-turn limited (2/turn). Shift axes by 3–10. ~25 of them.
- **YASA (laws):** permanent, and **slotted — you have 4 law slots, expandable to 7**. To adopt a new law you must repeal an old one. This forces the city to have an actual identity instead of accumulating every good idea. ~40 laws, each shifting axes persistently and modifying formulas (tax yield, housing density caps, conscription, price controls, censorship, term limits, land rights).
- **BÜTÇE (budget):** sliders for tax rate (0–60%) and funding for Sağlık / Eğitim / Güvenlik / Kültür / Altyapı. The soft layer between hard laws.
- **MECLİS (council):** 21 seats, composition derived from faction loyalty and district populations. Laws need a majority; short of one you spend Meşruiyet to force it. **The council's debate is your best early-warning system** — members voice the grievances your ministers are hiding. At `axis_order ≥ 75` you unlock "Meclisi Tatil Et": laws pass instantly and free — and you lose the debate, i.e. you lose your last honest information channel. Make this trade explicit in the tooltip and devastating in practice.
- **ANAYASA (constitution):** on turn 5 the player writes **3 founding clauses** from a pool of 12 (e.g. "Mülkiyet Kutsaldır", "Herkese Ekmek", "Şehir Kendini Savunur", "Söz Serbesttir"). Each sets run-long multipliers and one locked axis floor/ceiling. This is the main replayability lever — the same map plays very differently.

---

## 5. THE INFORMATION SYSTEM (build this fully — it is the thesis)

Maintain, for every stat, **a true value and a displayed value.**

```
transparency = f(press buildings, censorship laws, axis_order, council active)
report_error = base_noise * (1.0 - transparency) * authority_pressure
displayed = true * (1.0 + biased_noise)   # bias is OPTIMISTIC, never pessimistic
```

- High `axis_order` + censorship → officials report what you want to hear. Granary shows 60% when it holds 20%. Grievance shows 30 when it is 75. **The punishment for authoritarianism is not a score penalty — it is being unable to govern.**
- Above a noise threshold, show numbers as ranges ("Tahıl: ~%40–70") and mark them with a small `?` glyph. When the truth finally surfaces it should land as a shock: "GERÇEK RAKAMLAR" report on a scandal or after a purge.
- A free press keeps numbers accurate but periodically surfaces scandals that cost 5–15 Meşruiyet each.
- Independent auditors, an opposition newspaper, and an open council are three distinct, purchasable, politically expensive sources of truth.

---

## 6. THE FOUR COLLAPSES (distinct mechanics, not lose screens)

- **OTORİTE (Diktatörlük):** information degrades (§5) until you are managing a fiction; purges buy compliance and destroy competence (each purge: +Güvenlik, −Eğitim, −a random building's output permanently); ends in a **coup** by the army you built.
- **ÖZGÜRLÜK (Anarşi):** every build order now needs consent — construction times double, then triple; permits deadlock; services decay; districts stop remitting taxes and pass to local strongmen one by one until you govern one district.
- **SERMAYE (Plütokrasi):** the treasury is enormous and the city is hollow. Land values price workers out; LİMAN and SANAYİ depopulate; the buildings still stand but nobody staffs them; ends in a **general strike** that no amount of money can end.
- **EŞİTLİK (Kolektif):** everyone is housed and fed and nothing accumulates; capital never forms, research stalls, shortages become chronic, a black market forms that you cannot tax; ends in **brain drain and famine**.

Each collapse should be *visible in the city* for 10+ turns before it lands. No sudden deaths.

---

## 7. TURN STRUCTURE

1. **RAPOR** — newspaper front page (headline reacts to the previous turn), ledger, faction moods, council murmurs. Displayed values only.
2. **İNŞA** — zone and place buildings, spend ₺/Malzeme.
3. **YÖNETİM** — decrees, laws, budget, council votes.
4. **OLAY** — simulation tick, then 1–2 event cards resolve.

**Events — at least 50 cards**, each with 3–4 responses mapped to different axes plus one slow/expensive clean option: drought, epidemic, dock strike, refugee column at the gate, granary fire, foreign creditor's ultimatum, assassination attempt, corruption scandal, bumper harvest, ore strike, riot, religious revival, student protest, army pay demand, neighbouring city's collapse, a minister who wants to tell you something in private.

**Pacing:** turns 1–10 no crises (learn the systems). 11–30 escalating. 31–50 compound crises where the clean option genuinely cannot pay in time — this is where drift happens. 51–60 endgame; whatever you have become, you face it.

**Endings — write at least 8**, each a full screen with an epilogue paragraph, a final map snapshot, and statistics: the four collapses, `İFLAS`, `TERK EDİLMİŞ ŞEHİR` (population < 150), `SÜRDÜRÜLEBİLİR ŞEHİR` (survive 60 turns, both axes < 70, no district lost — the best ending and it should be hard), and `DAYANIKLI ŞEHİR` (survived 60 turns having touched RADİKAL and pulled back — the most interesting ending, and say so).

---

## 8. PRESENTATION (2D, procedural)

- **The city looks like your politics.** As axes shift, buildings gain overlay sprites drawn in code: `axis_order` → banners, checkpoints, your statues, shuttered windows. `özgürlük` → graffiti, improvised extensions, awnings. `sermaye` → billboards, tall thin towers, private walls. `eşitlik` → murals, shared courtyards, laundry lines. This is the single highest-payoff visual feature — make it obvious enough that a player who never reads the meters can *see* what they became.
- Buildings are flat-color geometry with clean readable silhouettes and a 1 px darker outline; roads darken and pave as land value rises. Seasons tint the palette; smoke particles from industry scale with Kirlilik; tiny citizen dots walk the roads, and their density/speed reflects employment.
- UI is a governor's desk: parchment panels with `StyleBoxFlat`, stamped decree cards, a newspaper overlay, a law book with 4–7 physical slots. Two axis meters with the 15-turn drift trail, always visible.
- A full-screen paper-grain + vignette shader on the UI `CanvasLayer`. Display `1920×1080`, `canvas_items` stretch, aspect `expand`.

## 9. AUDIO (synthesized, no files)

Stamp thud for decrees, coin clink, construction taps, a low string drone whose dissonance rises with total grievance, a crowd murmur bed that swells before unrest, newspaper rustle, a distant bell on crisis, a muffled drum in the coup countdown. Build each as an `AudioStreamWAV` in `AudioBus.gd` at startup. `Master`/`SFX`/`Ambience` buses, mute toggle.

## 10. ARCHITECTURE

```
res://project.godot
res://scenes/Main.tscn                    # screen router
res://scenes/TitleScreen.tscn
res://scenes/CityView.tscn                # grid, camera, placement
res://scenes/ui/HUD.tscn                  # ledger, axis meters, turn button
res://scenes/ui/NewspaperPanel.tscn
res://scenes/ui/LawBookPanel.tscn
res://scenes/ui/CouncilPanel.tscn
res://scenes/ui/BudgetPanel.tscn
res://scenes/ui/EventCard.tscn
res://scenes/ui/ConstitutionScreen.tscn
res://scenes/ui/EndingScreen.tscn
res://shaders/paper_grain.gdshader
res://scripts/autoload/GameState.gd       # true values, save/load
res://scripts/autoload/Reporting.gd       # true -> displayed, transparency
res://scripts/autoload/AudioBus.gd
res://scripts/sim/TurnResolver.gd         # the tick: needs, flows, grievance, factions
res://scripts/sim/GridMap2D.gd
res://scripts/sim/DistrictManager.gd
res://scripts/sim/FactionManager.gd
res://scripts/sim/AxisTracker.gd          # bands, drift trail, DÖNÜŞSÜZ countdown
res://scripts/sim/CollapseWatcher.gd      # the four failure mechanics
res://scripts/data/BuildingDef.gd         # class_name BuildingDef extends Resource
res://scripts/data/LawDef.gd
res://scripts/data/EventDef.gd
res://scripts/content/Buildings.gd        # all 30+ as data
res://scripts/content/Laws.gd             # all 40 as data
res://scripts/content/Decrees.gd
res://scripts/content/Events.gd           # all 50 as data
res://scripts/content/Headlines.gd
res://scripts/content/Endings.gd
```

- `GameState` and `Reporting` and `AudioBus` are **autoloads**. **Every UI read goes through `Reporting`, never straight to `GameState`** — that single rule is what makes the information system real instead of cosmetic.
- Buildings, laws, decrees and events are **pure data tables**. Adding content must be a one-line data change, never new branching logic.
- Save to `user://save.cfg` via `ConfigFile`, including the axis trail.

## 11. QUALITY BAR

Clean, commented, statically typed GDScript. No dead code, no placeholder content, no missing-resource errors. The simulation must be **legible**: every number in the ledger needs a tooltip that decomposes it into its contributing terms ("Yiyecek −40: nüfus 1200 (−48), çiftlikler (+30), kayıp %12 (−4)"). A player who cannot see why a number moved cannot learn the game, and this game is only interesting if it can be learned. Balance so that a first run reaches roughly turn 35 before collapsing, and `SÜRDÜRÜLEBİLİR ŞEHİR` demands several runs. Deliver every file in full.

---
