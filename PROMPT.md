# 7-11 SIMULATOR: GECE VARDİYASI — Tek Prompt (Godot 4 / Summer Engine)

Boş bir Godot 4 projesi oluştur (İşleyici: **Uyumluluk**), sonra aşağıdaki metnin tamamını
tek seferde AI'ya yapıştır.

---

You are generating a COMPLETE, FINISHED, playable game inside an existing, EMPTY **Godot 4.4** project (Summer Engine, a Godot fork). Renderer is **Compatibility**, the game is **2D only**.

Output every file I need with its **exact `res://` path** and its **full contents** — `.gd` scripts, `.tscn` scenes in Godot's text scene format, `.tres` resources, and the exact lines to add to `project.godot` (autoloads, input map, display settings). Do not stub anything, do not leave TODOs, do not ask me questions, do not summarize — ship the entire game in one pass.

**Hard constraints:**
- **Zero external assets.** No `.png`, `.wav`, `.ogg`, no downloaded fonts. Every visual is drawn in code (`_draw()`, `Polygon2D`, `Line2D`, `NinePatchRect`-free `ColorRect`/`StyleBoxFlat`). Every sound is synthesized at runtime by filling an `AudioStreamWAV` buffer or feeding an `AudioStreamGenerator`. Use Godot's built-in default font.
- All in-game text is in **Turkish**. All code identifiers, comments and file names in English.
- Static typing everywhere (`var hp: int = 3`, `func f(x: float) -> void:`). No untyped `var`.
- Must run with zero errors and zero warnings in the Godot output panel on first F5.

## GAME

**Title:** "SEVEN-ELEVEN: GECE VARDİYASI"

**Genre:** First-person convenience-store clerk simulator + social-deduction horror-comedy. *Papers, Please* meets a late-night snack run, where half your customers are monsters doing a hilariously bad job of pretending to be human.

**Premise:** You are the lone night clerk at a 7-Eleven on the edge of town, 23:00 to 06:00. Ordinary humans come in. So do monsters wearing human suits. Company policy: humans get served, monsters get politely refused and reported. Serve a monster and someone gets eaten. Refuse a human and you get a complaint and a pay cut. The monsters are not scary — they are *deeply committed to the bit* and terrible at it. The horror is atmospheric; the comedy is in the details.

**Tone:** Deadpan, dry, absurd. Buzzing fluorescent lights, a broken slushie machine, rain outside. Monsters say things like "SELAMLAR, MAAŞLI İNSAN YOLDAŞ. ET TÜPÜ İSTİYORUM." Never gory, never mean-spirited — creepy-cute.

## CORE LOOP (per customer)

1. Door chime. A customer walks up to the counter, drawn by the procedural portrait generator (see ART).
2. They speak a greeting line and place 1–4 items on the counter.
3. **INVESTIGATION PHASE** — limited time, limited tool uses. Gather evidence.
4. **DECISION** — two big buttons: **[SAT]** (serve: scan items, take payment, they leave) or **[REDDET]** (refuse: hit the buzzer, they're escorted out).
5. Result panel: right or wrong? Money / reputation / suspicion update. Next customer.

A shift = 8–12 customers. Between shifts: a night-summary screen (earnings, rent due, accuracy) plus a newspaper clipping that reacts to your actual mistakes ("YEREL ADAM SOSİSLİ SANDVİÇ ALIRKEN YENDİ").

## THE DETECTION SYSTEM (the heart of the game — make it deep)

Every customer is generated from a hidden `is_monster: bool` plus a set of **tells**. A human has 0 tells; a monster has 1–4. Crucially, **some tells are only visible through a specific tool**, so the player must actively investigate instead of eyeballing.

Implement **at least 14 distinct tells**:

**Visual (naked eye, if you look closely):**
- Wrong finger count (6, 7 or 3) — actually rendered on the portrait's hands
- Vertical slit pupils, a third eye, or eyes that haven't blinked in 30+ seconds
- Faint scale/tile texture on the skin
- Too many teeth, or a smile wider than the face allows
- The reflection in the counter glass doesn't match the body

**Tool-gated:**
- **UV EL FENERİ** — hold to sweep a UV cone over the customer; monsters glow with slime patches, suit seams, or a zipper down the back
- **GÜVENLİK KAMERASI** — a CRT panel showing the TRUE form (tentacle mass in a hoodie) while the counter view looks human. Some monsters are camera-safe, so it is not a universal oracle.
- **TERMOMETRE** — humans read 36–37.5 °C; monsters read 4 °C, 61 °C, or "HATA: NaN"
- **KİMLİK OKUYUCU** — inspect the ID card: birth year 1804, expiry "YARIN", height "3 metre", photo mismatch, name "İNSAN OĞLU İNSAN", blood type "HEPSİ"
- **BARKOD OKUYUCU** — items ring up as "EVCİL HAYVAN MAMASI ×40" or "ÇİĞ ET (İNSAN?)" — the basket itself is a tell

**Behavioral / dialogue:**
- Says something no human would: "BU BEDENDE 400 YILDIR YAŞIYORUM", "PARA. EVET. BEN DE PARAYI SEVERİM."
- Pays with wet cash, a live beetle, teeth, or a coin minted in 1623
- Asks where the "yumuşak insan" (previous clerk) went
- Refers to itself in the plural: "BİZ SİGARA İSTİYORUZ"

Tells must be **combinable and sometimes ambiguous**. Include at least three **false-positive traps** (genuinely human customers who look wrong — a bloodshot night-shift trucker with a damp roll of cash) and at least two **perfect mimics** (monsters with exactly one very subtle tell).

**Rule escalation:** each night a policy fax adds a rule that reshapes the deduction — Night 2: "reflections may now lag"; Night 3: "one monster species is licensed and MUST be served (green ID sticker)"; Night 4: "the CCTV is compromised and lies 20% of the time"; Night 5: "a monster is wearing the manager's face." A rulebook screen reopenable at any time with **R**.

## SYSTEMS

- **PARA:** each correct serve earns ₺; each night has rent plus a random expense. Below ₺0 = game over (evicted).
- **ŞÜPHE (0–100):** serving a monster spikes it. At 100 the store is "marked" and the final night triggers early.
- **İTİBAR:** refusing humans tanks it; at 0 you're fired.
- **SÜRE:** per-customer countdown; running out is an auto-refuse and annoys everyone. Generous on Night 1, tighter every night.
- **TOOL LIMITS:** per-customer uses (UV ×2, thermometer ×1, CCTV unlimited but costs 5 seconds of the timer each look). This forces real decisions.
- **COMBO:** consecutive correct calls build a bonus multiplier with a small on-screen flourish.
- **5 NIGHTS + FINALE:** Night 5 ends with a boss — three smaller monsters in a trench coat — identified by catching the *seam* with the UV light while it distracts you with small talk. Three distinct endings: fired, eaten, survived-until-sunrise.
- **SAVE:** progress, money and best score persisted to `user://save.cfg` via `ConfigFile`.

## GODOT ARCHITECTURE (follow this structure)

```
res://project.godot
res://scenes/Main.tscn              # root, swaps between screens
res://scenes/TitleScreen.tscn
res://scenes/StoreScene.tscn        # the counter view, gameplay
res://scenes/NightSummary.tscn
res://scenes/Rulebook.tscn
res://scenes/CustomerPortrait.tscn  # procedural, driven by CustomerData
res://scenes/tools/UVLight.tscn
res://scenes/tools/CCTVPanel.tscn
res://scenes/tools/IDCard.tscn
res://scripts/autoload/GameState.gd # singleton: money, rep, suspicion, night, save/load
res://scripts/autoload/AudioBus.gd  # singleton: synthesized SFX
res://scripts/data/CustomerData.gd  # class_name CustomerData extends Resource
res://scripts/data/ItemData.gd      # class_name ItemData extends Resource
res://scripts/data/TellDefs.gd      # the tell catalogue + which tool reveals each
res://scripts/CustomerGenerator.gd  # builds CustomerData for a given night
res://scripts/PortraitRenderer.gd   # _draw() based, reads CustomerData
res://scripts/ShiftManager.gd       # queue, timer, decision resolution, scoring
res://scripts/content/Dialogue.gd   # all Turkish lines as const arrays
res://scripts/content/Catalog.gd    # store items, prices, barcodes
res://scripts/content/Headlines.gd  # newspaper clippings
```

- Register `GameState` and `AudioBus` as **autoloads** in `project.godot`.
- Communicate between nodes with **signals**, not `get_node("../../..")` chains.
- Tell definitions live in one data table (`TellDefs.gd`) mapping tell id → display name, revealing tool, portrait effect, and rarity. Adding a tell must be a one-line data change, not new branching logic.
- `CustomerGenerator` takes the night number and returns a `CustomerData`; the portrait, dialogue, basket and tool readouts all derive from that single object. Never store the answer in two places.

## ART (procedural — zero image files)

- **Store:** `StoreScene` draws the counter in the foreground, shelves and a rain-streaked window behind, a flickering fluorescent light (animate a `CanvasModulate` or a `Light2D` energy curve via `Tween`/`AnimationPlayer`), a hotdog roller, and a slushie machine with a "BOZUK" sign.
- **Customers:** `PortraitRenderer` is layered and parametric — head shape, skin tone, eyes (count / shape / pupil), mouth (teeth count), hair or hat, clothing, hands (finger count). Monster traits are just extra layers, so the same generator produces humans and monsters and every tell is literally rendered geometry, not a text label.
- **Palette:** sodium-yellow store light, cold blue night outside, sickly green CRT for the camera feed.
- Add a full-screen CRT scanline + vignette + film-grain overlay as a **custom `ShaderMaterial` on a `ColorRect`** (write the full `.gdshader` inline as a file).
- Display: viewport `1920×1080`, stretch mode `canvas_items`, aspect `expand`.

## AUDIO (synthesized at runtime, no files)

Door chime (two-tone bell), barcode beep, fluorescent hum loop (filtered noise), rain (filtered noise), refuse buzzer, a sub-bass sting when a monster is served, register cha-ching, and a faint wet squelch whose volume scales with how many tells the current customer has — a subconscious hint. Build each as an `AudioStreamWAV` generated in `AudioBus.gd` at startup. Separate `Master` / `SFX` / `Ambience` audio buses, plus a mute toggle.

## INPUT MAP (add these actions to `project.godot`)

`tool_uv`=1, `tool_temp`=2, `tool_cctv`=3, `tool_id`=4, `tool_barcode`=5, `decide_serve`=Space, `decide_refuse`=X, `open_rulebook`=R, `mute`=M, `pause`=Escape. Everything is also clickable with the mouse.

A **NOT DEFTERİ** panel logs each tell as the player discovers it, so they can reason before deciding.

## CONTENT VOLUME (do not skimp — this is what makes it feel finished)

- ≥ 30 unique customer archetypes (mixed humans and monsters)
- ≥ 60 distinct Turkish dialogue lines, all funny, none repeating within a night
- ≥ 25 store items with names, prices and barcode strings
- ≥ 10 newspaper headlines

## QUALITY BAR

Clean, commented, statically typed GDScript. No `get_node` spaghetti, no dead code, no placeholder art. Difficulty curve balanced: Night 1 winnable by a first-timer, Night 5 genuinely hard. Deliver every file in full.

---
