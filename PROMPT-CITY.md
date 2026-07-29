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

You are the founder-governor of a new city on a river delta: 400 settlers, a small treasury, a blank grid. **One term, 60 turns, one season per turn — fifteen years.** You will not survive by being kind and you will not survive by being cruel. You will survive, if you survive, by knowing exactly how far you already went. At turn 60 you do not hand the city to an heir. You stand in it.

**Genre:** Turn-based city builder fused with a political-drift simulator. *SimCity* placement, *Frostpunk* law-book escalation, *Democracy*'s faction arithmetic — but the subject is **ideological drift under pressure**.

**Tone: comic characters, ice-cold consequences.** The framing is bureaucratic — a governor's desk, ledgers, stamped decrees, a newspaper, telegrams — but the *people* are funny. Ministers are venal, vain, petty and gloriously incompetent, each with a running gag (the Maliye minister who keeps proposing a new tax named after himself; the Güvenlik minister who sees Mersa's agents in the bakery). Events are absurd on the surface: a prize bull loose in the market, a rival city's ambassador who will not leave, a prophet forecasting the end of the world next Tuesday, a shipment of 4000 unwanted hats.

**But the consequences are never funny.** Hunger is hunger, the coup is a coup, and the ledger never winks. The comedy lives entirely in voices, names, flavour text and event setups; the simulation underneath stays merciless and completely straight-faced. This is the RimWorld contract: laugh at the setup, get destroyed by the outcome. If a single mechanical effect is played for laughs — a crisis that resolves itself as a gag, a punishment that isn't real — the drift theme dies and the game becomes a toy. Hold the line: **funny mouths, honest math.**

**Target session:** a full run is 90–120 minutes. Size all content and pacing to that.

---

## 2. DESIGN PILLARS (obey these — they are the whole game)

1. **Placement is a political act.** Every building has a functional effect AND a political effect that depends on **which district it is placed in**. The map is where politics is played, not a menu bolted onto a builder.
2. **The player must never choose "become a dictator."** Every crisis has a *clean* response — slow and expensive — and a *fast* response that shifts an axis. Under pressure players take the fast one. Twelve reasonable emergency measures later there are checkpoints on the streets. **Drift is emergent, never selected.** Never label an option as extreme, evil, or authoritarian; label it by what it does.
3. **The safety margin is the real currency.** See §7. Foresight — hoarded stock, treasury, spare labour — is the only thing that lets you afford the clean option. A player who spends every surplus on growth grows faster and arrives at the first crisis with nothing. **The cause of drift is greed, not malice.**
4. **Each extreme fails through its own mechanic, not a lose screen.** See §9.
5. **Extremes are powerful, not stupid.** Radical bands unlock tools that genuinely solve otherwise-unsolvable crises. The cost is fragility, not weakness. If hugging the centre can win, the game is broken — the centre must be too slow to survive the mid-game alone.
6. **Truth is a resource with a political price.** See §5.

---

## 3. THE CITY LAYER

**Map:** `48 × 32` tiles, 32 px each, top-down. Camera pans with middle-drag/WASD, zooms 0.5×–2.5×. River along one edge, fertile soil near it, ore in the hills, marsh that must be drained.

**Districts — 6, pre-drawn and named**, each with its own `grievance`, `wealth`, `population` and faction affinity:
**LİMAN** (docks, workers → İşçiler) · **TEPE** (wealthy hill → Tüccarlar) · **ESKİ ŞEHİR** (old town, market, temple → Gelenek) · **SANAYİ** (industry, smoke → İşçiler) · **ÜNİVERSİTE** (schools, press, clinics → Aydınlar) · **KIŞLA** (garrison, depots → Ordu).

A district can riot, strike, or fall out of your control **independently**. Losing one removes its production and its tiles from your build range.

**Resources:** show stockpile AND per-turn flow for `Para (₺)`, `Yiyecek`, `Su`, `Enerji`, `Malzeme`, `İşgücü`. Plus city indices 0–100: `Sağlık`, `Eğitim`, `Güvenlik`, `Kültür`, `Kirlilik`.

**Light supply chains — exactly two, no more.** Everything else is a district-level pool.
- **Food:** `Tarla → Değirmen → Fırın → Ekmek`. Each stage has throughput and a small stock. A blocked middle stage starves the city while the granary total still looks fine — and a lying minister (§5) will report the total, not the blockage. This is deliberate.
- **Materials:** `Ocak → Kereste/Taş → İnşaat Deposu`. Construction draws from the depot, not from an abstract pool, so a cut ore supply freezes building three turns later.

Both chains consume **İşgücü**, which is a single shared pool. This matters — see §8.

**Buildings — at least 30**, each with cost, upkeep, footprint, worker demand, output, adjacency rules, and **a district-dependent political effect**. Follow this pattern throughout:

| Building | Function | Political effect |
|---|---|---|
| Tahıl Ambarı | +Yiyecek buffer | none — one of the few politically neutral buildings |
| Dokuma Atölyesi | +₺, needs 40 işgücü | in SANAYİ: +Tüccar. In LİMAN: +₺ bonus (cheap labour) but +grievance, +Kirlilik |
| Karakol | +Güvenlik in radius | in LİMAN: −İşçi, +Otorite. In TEPE: +Tüccar, no axis shift |
| Matbaa | +Eğitim, **+Şeffaflık** | +Aydın, −Otorite, surfaces scandals |
| Park | +Mutluluk in radius | in TEPE: +Tüccar. In LİMAN: +İşçi, −Tüccar ("neden onlara?") |
| Anıt | +Meşruiyet, −₺₺ | +Otorite strongly, −Aydın |
| Tapınak | +Kültür, −grievance | +Gelenek, −Aydın |
| Tayınlama Deposu | food to all regardless of ₺ | +Eşitlik, −Tüccar |
| Serbest Borsa | +₺₺₺ | +Sermaye, +Tüccar, housing costs rise → +LİMAN grievance |
| Toplu Konut | cheap dense housing | +Eşitlik, −Tüccar, −TEPE land value |
| Kontrol Noktası | −smuggling, +Güvenlik | +Otorite, +grievance in its own district |
| Kışla | +Garnizon capacity | +Ordu, enables conscription — see §8 |

Zoning: paint `KONUT` (3 density tiers), `TİCARET`, `SANAYİ`, `TARIM`, then place service buildings. Roads/water/power must connect (flood-fill check; mark unserviced buildings with a red icon).

**Needs & grievance:** every 100 population demands food, water, housing, a job and a service basket. Unmet needs add grievance **to that district**, weighted by local expectations — TEPE tolerates less discomfort, LİMAN tolerates less injustice. Grievance decays slowly when needs are met. `>70` for 3 consecutive turns → unrest event. `>90` → the district acts on its own.

---

## 4. THE GOVERNANCE LAYER

**Two axes, each `-100 … +100`:** **OTORİTE ↔ ÖZGÜRLÜK** (`axis_order`) and **SERMAYE ↔ EŞİTLİK** (`axis_economy`).

Render each as a horizontal meter with a marker and, behind it, **a faint trail of the last 15 turns** so the player can see the drift they never noticed. Build this trail — it is the only place the game's subject becomes visible.

**Bands (absolute value):**
- `0–39` **PRAGMATİK** — no modifiers, no unlocks. Safe, and too slow to survive the mid-game alone.
- `40–69` **KARARLI** — unlocks that side's strong laws and buildings. Mild opposing-faction penalty.
- `70–89` **RADİKAL** — unlocks that side's decisive crisis tools. Opposing factions turn hostile; that quadrant's failure mechanic switches on at partial strength.
- `90–100` **DÖNÜŞSÜZ** — the strongest tools in the game. A one-time warning event fires, a visible **8-turn `GERİ DÖNÜŞ` countdown** begins, and the failure mechanic runs at full strength. Pulling back below 80 is possible but demands a real sacrifice: repeal your keystone law, lose 30 Meşruiyet, or hand a district's administration to a faction. Countdown reaching 0 while still ≥90 fires that collapse ending.

**Meşruiyet (0–100, starts 60):** spent to force laws through the council and to absorb scandals; gained by meeting needs, winning crises, holding elections. At 0 you are finished regardless of everything else.

**Factions — 5, loyalty 0–100, all start 50:** `TÜCCARLAR`, `İŞÇİLER`, `ORDU`, `GELENEK`, `AYDINLAR`. Each grants a passive gift at ≥65 (Tüccarlar −15% build cost · İşçiler +10% işgücü · Ordu +Güvenlik and coup immunity · Gelenek faster grievance decay · Aydınlar +Şeffaflık) and a threat at ≤25 (sabotage, strike, coup clock, schism, leak). **Two hostile factions at once = a compound crisis.**

**Faction loyalty is never shown as a number.** It is shown as a qualitative mood — `hoşnut / temkinli / kaygılı / öfkeli / düşman` — and these moods are **always honest**, because you meet these people face to face. This is the player's one reliable anchor; the fog must never be total or the game becomes unlearnable.

**With exactly one exception: ORDU.** The army's mood reaches you only through your Güvenlik minister, so it is subject to that minister's distortion (§5). The player therefore has a single deadly blind spot rather than general blindness — and it is precisely the faction that can end them. A loyalist Güvenlik minister reports "ordu memnun" until the morning of the coup.

**Instruments:**
- **KARARNAME** — immediate, 2 per turn, shift axes 3–10. ~25 of them.
- **YASA** — permanent and **slotted: 4 slots, expandable to 7**. Adopting a law means repealing one. This forces the city to have an identity instead of collecting every good idea. ~40 laws that shift axes persistently and modify formulas (tax yield, density caps, conscription, price controls, censorship, land rights).
- **BÜTÇE** — tax rate 0–60% and funding sliders for Sağlık / Eğitim / Güvenlik / Kültür / Altyapı.
- **MECLİS** — 21 seats derived from faction loyalty and district population. Laws need a majority; short of one, spend Meşruiyet. **The council debate is your best early-warning system** — members voice grievances your ministers are hiding. At `axis_order ≥ 75` you unlock "Meclisi Tatil Et": laws pass instantly and free, and you permanently lose the debate, i.e. your last honest channel. State the trade in the tooltip and make it devastating in practice.
- **ANAYASA** — on turn 5, pick **3 founding clauses from a pool of 12** ("Mülkiyet Kutsaldır", "Herkese Ekmek", "Şehir Kendini Savunur", "Söz Serbesttir", …). Each sets run-long multipliers and one locked axis floor or ceiling. This is the main replayability lever.
- **SEÇİM — the one channel that cannot lie.** Elections fall on turns **12, 24, 36, 48 and 60**. The result is computed from **true** district grievance and **cannot be distorted by any minister** — this is the only moment the player sees their real city, because the number comes from the population rather than from an official. Winning grants +Meşruiyet and a temporary faction goodwill bump; a heavy loss forces a concession (repeal a law, fund a district, or dismiss a minister of the winner's choosing). Three options each time:
  - **YAP** — hold it. Honest, and if the city is worse than you were told, you find out here, publicly, at full cost.
  - **ERTELE** — postpone. +12 `axis_order`, −15 Meşruiyet, every faction's mood drops one step. Cheap the first time, ruinous by the third.
  - **HİLE YAP** — rig it. You get the win and no immediate cost — **but the true result is recorded**, and if the press is free or an auditor is active, the scandal surfaces within 1–4 turns for −35 Meşruiyet and a permanent Aydınlar and İşçiler penalty.
  
  Design intent, and make it land: the authoritarian player is pushed to cancel the exact instrument that would have cured their blindness. Rigging feels free precisely because the only witness is the press they already muzzled.

---

## 5. MINISTERS & THE INFORMATION SYSTEM (the thesis — build it fully)

Maintain **a true value and a displayed value for every stat.** Every UI read goes through the `Reporting` autoload; nothing reads `GameState` directly. That single rule is what makes this system real rather than cosmetic.

**Five named ministers**, one per domain: `MALİYE` (₺, tax), `TARIM` (food chain), `GÜVENLİK` (Güvenlik, garrison, threat), `İMAR` (materials, construction, housing), `HALK` (grievance, population, health). Each has a name, a portrait drawn from procedural shapes, a personal agenda, and **a distortion style applied only to their own domain**:

```
displayed = true * (1.0 + bias)
bias = minister.style_bias * (1.0 - transparency) * authority_pressure
# bias is always OPTIMISTIC for a loyalist; a hawk inflates threats instead
```

Maliye inflates revenue. Güvenlik inflates the external threat and understates unrest. Tarım hides a blocked chain stage behind a healthy total. Halk rounds grievance down. Distortion scales with `axis_order` and with censorship laws — under a free press and an open council the numbers are nearly clean.

**Dismissal is the central repeated choice.** Sack a minister and two candidates appear:
- **SADIK** — reports what you want to hear (high distortion), obeys without friction, −25% decree cost, no council complaints.
- **UZMAN** — reports the truth, +20% output in their domain, and reports *you* to the council, costing Meşruiyet whenever you act against their advice.

The same question recurs on every purge. The whole road to dictatorship is compressed into one choice the player will make eight times, and the loyalist is the correct short-term answer nearly every time. **That is the trap and it must be a fair one.**

**The interlock that matters most (implement it explicitly):** a loyalist Tarım minister reports the granary full while it drains. The player consumes their safety margin without knowing. They arrive at the crisis with no buffer and must take the fast option. **Lying ministers destroy the safety margin silently** — the drift happened three turns earlier, at an appointment.

Three purchasable, politically expensive sources of truth: an independent auditor, an opposition newspaper, an open council. A free press keeps numbers accurate but surfaces scandals costing 5–15 Meşruiyet each. Above a noise threshold, display ranges ("Tahıl: ~%40–70") with a small `?` glyph, and when reality finally surfaces, deliver it as a full-screen "GERÇEK RAKAMLAR" report.

---

## 6. THE OUTSIDE WORLD (three pressures, one squeeze)

**a) External threat — KOMŞU: MERSA.** A single `tehdit_seviyesi` 0–100 rising on its own schedule and with your weakness. It makes tribute demands, stages border raids, and eventually invades. **No war map** — all combat resolves as event cards against `Garnizon Gücü` (§8).

**b) Foreign debt — creditors demand LAWS, not money.** This is the key rule: a loan's collateral is **a law slot**. Accepting a credit line forces a creditor law ("Özel Mülkiyet Dokunulmazlığı", "Liman İmtiyazı", "Grev Yasağı") into one of your 4–7 slots, **and it cannot be repealed while the debt is outstanding**. So foreign money does not buy you out of politics — it shrinks your capacity to govern and drags `axis_economy` toward Sermaye. Three loans and half your law book belongs to someone else. Escaping a crisis is impossible; only choosing which axis you escape along.

**c) Refugees.** Columns arrive at the gate, mostly after Mersa presses a smaller neighbour. Accepting costs food, water and housing from your buffer and grants population and İşgücü. Refusing costs Aydınlar and Gelenek loyalty. Accepting without a buffer means starvation and grievance — or requisition, and +Otorite. **Refugees are the buffer test made into a moral choice.**

**The mid-game squeeze — design it deliberately.** Around turn 30 the neighbouring city of **KADRA** collapses. One event, three simultaneous consequences: a large refugee column at your gate, Mersa's threat level jumps because Kadra's fall empowers it, and your creditors reprice risk and raise interest. Telegraph it **5 turns in advance** through minister reports and council murmurs — a player with reliable information and a buffer can prepare; a player with loyalist ministers will not see it coming at all.

---

## 7. THE SAFETY MARGIN (pillar 3, mechanically)

Every crisis response is priced so that the clean option costs roughly **1.5× one turn of surplus**. It is affordable only out of accumulated stock, treasury or spare labour — never out of current income. Growth spending and buffer holding draw from the same surplus, so the player chooses between compounding and surviving, every single turn.

Make the buffer legible: a persistent HUD strip showing `TAMPON: N tur` — how many turns the city could absorb a shock. Watching that number fall from 6 to 1 while you build is the game's core tension. And a loyalist Tarım minister makes that number a lie, which is exactly the point.

---

## 8. GARRISON, CONSCRIPTION, AND THE COUP (one causal chain)

`Garnizon Gücü` grows through Kışla buildings and conscription laws. Conscription draws bodies from the **shared İşgücü pool**, i.e. straight out of the farms and mills of the food chain.

**The intended causal loop, and you must make it reachable in a normal run:**
`Mersa'nın tehdidi ↑ → askere alma → tarlalarda işgücü ↓ → yiyecek açığı → yiyecek krizi → hızlı çözüm: el koyma → axis_order ↑ → bilgi bozulur → sonraki kriz körlemesine karşılanır`

Arming against an external threat is what makes you authoritarian. Nothing in the UI ever says so.

**And the other end:** `coup_risk = f(Garnizon Gücü, 1 - Ordu loyalty, 1 - Meşruiyet)`. The army large enough to save you is the army large enough to depose you. Ordu ≤25 with Meşruiyet ≤35 starts a 5-turn coup countdown — **announced only if your information is reliable**. With a loyalist Güvenlik minister it arrives with no warning at all.

---

## 9. THE FOUR COLLAPSES (distinct mechanics, visible 10+ turns ahead)

- **OTORİTE / Diktatörlük:** information degrades until you govern a fiction; each purge trades competence for compliance (+Güvenlik, −Eğitim, one building's output permanently reduced); ends in a coup by the army you built.
- **ÖZGÜRLÜK / Anarşi:** every build order needs consent — construction times double then triple, permits deadlock, services decay, districts stop remitting taxes and pass to local strongmen one by one until you govern one district.
- **SERMAYE / Plütokrasi:** the treasury is enormous and the city is hollow. Land values price workers out, LİMAN and SANAYİ depopulate, the buildings stand unstaffed; ends in a general strike no amount of money can end.
- **EŞİTLİK / Kolektif:** everyone housed and fed, nothing accumulates. Capital never forms, research stalls, shortages turn chronic, an untaxable black market forms; ends in brain drain and famine.

No sudden deaths. Every collapse is legible in the city long before it lands.

---

## 10. TURN STRUCTURE, EVENTS, ENDINGS

1. **RAPOR** — newspaper front page reacting to last turn, ledger, minister telegrams, faction moods, council murmurs. Displayed values only.
2. **İNŞA** — zone and place, spend ₺ and Malzeme from the depot.
3. **YÖNETİM** — decrees, laws, budget, council votes, minister appointments.
4. **OLAY** — simulation tick, then 1–2 event cards resolve.

**Events — at least 50 cards**, each with 3–4 responses on different axes plus one slow clean option: drought, epidemic, dock strike, refugee column, granary fire, creditor ultimatum, Mersa's tribute demand, border raid, assassination attempt, corruption scandal, bumper harvest, ore strike, riot, religious revival, student protest, army pay demand, Kadra's collapse, a minister asking to speak with you privately.

**Pacing:** turns 1–10 no crises, learn the systems. 11–30 escalating single crises. 31–50 compound crises where the clean option genuinely cannot pay in time unless you built a buffer — this is where drift happens. 51–60 endgame; whatever you have become, you face it.

**ONBOARDING — there are no tutorial screens.** Teaching is entirely diegetic: during turns 1–10 the five ministers introduce themselves and their domain by telegram, in character, one or two per turn. "Sayın Vali, ambarı ben takip ediyorum, zât-ı âliniz meşgul olmasın." Each telegram teaches one system and one piece of UI. Write ~14 of these. The design reason matters: this establishes the ministers as your information channel and teaches you to rely on them **before** you ever learn they distort. The tutorial and the trap are the same content.

**THE FINAL TURN — HESAP VERME OTURUMU.** If the player reaches turn 60 alive, do not cut straight to an ending card. Hold a public accountability session:
1. **Gerçek rakamlar.** Every true value is laid out beside what the player was told, turn by turn, as a scrolling ledger. Highlight the largest lifetime divergence per domain ("TARIM: 40 tur boyunca ortalama %31 şişirilmiş rapor").
2. **Bakanların ifadesi.** Each of the five ministers speaks in turn — loyalists blame each other and the circumstances, experts state plainly what they told you and when.
3. **Fraksiyonların ifadesi.** Each faction delivers one verdict paragraph on your term, shaped by its final mood.
4. **The axis trail** is redrawn full-screen as a single line from turn 1 to turn 60, with the moment of each irreversible decision marked and labelled with the decree that caused it.
5. Only then, the ending card.

This is the payoff for the entire information system: the first time the player sees their own city truthfully is the moment they can no longer do anything about it. Write it to land hard and without a single word of moralising.

**Endings — at least 11**, each a full screen with an epilogue paragraph, a final map snapshot and run statistics:
the four collapses · `DARBE` (your own garrison) · `İŞGAL` (Mersa overruns you) · `BORÇLU ŞEHİR` (every law slot creditor-owned — you govern someone else's city) · `İFLAS` · `TERK EDİLMİŞ ŞEHİR` (population < 150) · `SÜRDÜRÜLEBİLİR ŞEHİR` (60 turns, both axes < 70, no district lost — the best ending, and it must be hard) · `DAYANIKLI ŞEHİR` (60 turns having reached RADİKAL and pulled back — the most interesting ending; say so on the screen).

---

## 11. PRESENTATION (2D, procedural)

- **The city looks like your politics.** As axes shift, buildings gain code-drawn overlays: `otorite` → banners, checkpoints, your statues, shuttered windows. `özgürlük` → graffiti, improvised extensions, awnings. `sermaye` → billboards, tall thin towers, private walls. `eşitlik` → murals, shared courtyards, laundry lines. Make it obvious enough that a player who never reads the meters can *see* what they became. Highest-payoff feature in the game.
- Flat-color building geometry with clean silhouettes and a 1 px darker outline; roads pave as land value rises; seasons tint the palette; smoke particles scale with Kirlilik; tiny citizen dots walk the roads with density and speed reflecting employment.
- UI is a governor's desk: parchment `StyleBoxFlat` panels, stamped decree cards, a newspaper overlay, a law book with physical slots (creditor-held slots visibly sealed with a foreign wax stamp), five minister portraits with a small `?` badge when their reports are unreliable. Both axis meters with the 15-turn drift trail, always visible. `TAMPON: N tur` strip always visible.
- Full-screen paper-grain + vignette shader on the UI `CanvasLayer`. `1920×1080`, `canvas_items` stretch, aspect `expand`.

## 12. AUDIO (synthesized, no files)

Stamp thud for decrees, coin clink, construction taps, a low string drone whose dissonance rises with total grievance, a crowd murmur bed swelling before unrest, newspaper rustle, a distant bell on crisis, a muffled drum during the coup countdown, a dry telegraph tick for minister reports. Build each as an `AudioStreamWAV` in `AudioBus.gd` at startup. `Master`/`SFX`/`Ambience` buses, mute toggle.

## 13. ARCHITECTURE

```
res://project.godot
res://scenes/Main.tscn                    # screen router
res://scenes/TitleScreen.tscn
res://scenes/CityView.tscn                # grid, camera, placement
res://scenes/ui/HUD.tscn                  # ledger, axis meters, TAMPON strip, turn button
res://scenes/ui/NewspaperPanel.tscn
res://scenes/ui/LawBookPanel.tscn         # slots, creditor seals
res://scenes/ui/CouncilPanel.tscn
res://scenes/ui/BudgetPanel.tscn
res://scenes/ui/MinisterPanel.tscn        # portraits, reliability, SADIK/UZMAN choice
res://scenes/ui/CreditorPanel.tscn        # loans and the laws they demand
res://scenes/ui/EventCard.tscn
res://scenes/ui/ConstitutionScreen.tscn
res://scenes/ui/EndingScreen.tscn
res://shaders/paper_grain.gdshader
res://scripts/autoload/GameState.gd       # TRUE values, save/load
res://scripts/autoload/Reporting.gd       # true -> displayed, per-minister bias
res://scripts/autoload/AudioBus.gd
res://scripts/sim/TurnResolver.gd         # the tick: chains, needs, grievance, factions
res://scripts/sim/GridMap2D.gd
res://scripts/sim/SupplyChain.gd          # food and materials chains
res://scripts/sim/DistrictManager.gd
res://scripts/sim/FactionManager.gd
res://scripts/sim/MinisterManager.gd      # appointment, distortion, complaints
res://scripts/sim/ElectionManager.gd      # turns 12/24/36/48/60, hold/postpone/rig
res://scripts/sim/DebtManager.gd          # creditors, demanded laws, sealed slots
res://scripts/sim/ThreatManager.gd        # Mersa, garrison, conscription, coup risk
res://scripts/sim/BufferTracker.gd        # TAMPON in turns, true and displayed
res://scripts/sim/AxisTracker.gd          # bands, drift trail, DÖNÜŞSÜZ countdown
res://scripts/sim/CollapseWatcher.gd      # the four failure mechanics
res://scripts/data/BuildingDef.gd         # class_name BuildingDef extends Resource
res://scripts/data/LawDef.gd
res://scripts/data/EventDef.gd
res://scripts/data/MinisterDef.gd
res://scripts/content/Buildings.gd        # all 30+ as data
res://scripts/content/Laws.gd             # all 40 as data
res://scripts/content/Decrees.gd
res://scripts/content/Events.gd           # all 50 as data
res://scripts/content/Ministers.gd
res://scripts/content/Creditors.gd
res://scripts/content/Headlines.gd
res://scripts/content/Endings.gd
```

- `GameState`, `Reporting`, `AudioBus` are **autoloads**. **Every UI read goes through `Reporting`.** Enforce it — no exceptions anywhere in the codebase.
- Funnel all minister reporting through **one** function, `Reporting.report_for(domain: String) -> Dictionary`, and let the bias formula be its only current implementation. A later co-op mode will replace the *source* of that dictionary with a human player's submitted report; keep the plumbing indifferent to where the numbers came from.
- Buildings, laws, decrees, events, ministers and creditors are **pure data tables**. New content must be a one-line data change, never new branching logic.
- Save to `user://save.cfg` via `ConfigFile`, including the axis trail and each minister's identity and bias.

## 14. QUALITY BAR

Clean, commented, statically typed GDScript. No dead code, no placeholder content, no missing-resource errors. The simulation must be **legible**: every ledger number needs a tooltip decomposing it into its terms ("Yiyecek −40: nüfus 1200 (−48), fırınlar (+30), değirmen tıkanıklığı (−18), kayıp %12 (−4)"). A player who cannot see why a number moved cannot learn the game, and this game is only worth playing if it can be learned.

Balance targets: a first run reaches roughly turn 35 before collapsing. `SÜRDÜRÜLEBİLİR ŞEHİR` takes several runs. And verify this explicitly — **if a player can survive to turn 60 while keeping both axes under 40, the crisis costs are too low; raise them until the centre alone cannot pay.**

Deliver every file in full.

---
