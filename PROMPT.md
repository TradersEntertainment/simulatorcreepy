# 7-11 SIMULATOR: GECE VARDİYASI — Tek Prompt (Godot 4 / Summer Engine, 3D)

Godot 4 projesi oluştur, İşleyici: **İleri+ (Forward+)**.
Sonra aşağıdaki metnin tamamını tek seferde AI'ya yapıştır.

---

You are generating a COMPLETE, FINISHED, playable **3D** game inside an existing, EMPTY **Godot 4.4** project (Summer Engine, a Godot fork). Renderer is **Forward+**.

Output every file with its **exact `res://` path** and its **full contents** — `.gd` scripts, `.tscn` scenes in Godot's text scene format, `.gdshader` files, and the exact lines to add to `project.godot` (autoloads, input map, rendering and display settings). Do not stub anything, do not leave TODOs, do not ask me questions, do not summarize — ship the entire game in one pass.

**Hard constraints:**
- **Zero external assets.** No `.glb`, `.obj`, `.png`, `.wav`, no downloaded fonts. Every mesh is built from Godot primitives (`BoxMesh`, `CylinderMesh`, `SphereMesh`, `CapsuleMesh`, `PrismMesh`, `TorusMesh`) or `CSGShape3D`, assembled in code. Every texture is a `NoiseTexture2D`/`GradientTexture2D` or a shader. Every sound is synthesized at runtime into an `AudioStreamWAV`. Use Godot's built-in default font.
- **Art direction: chunky low-poly.** Characters and props are readable silhouettes made of stacked primitives with flat `StandardMaterial3D` colors — deliberately stylized, like a PS1-era toy set. This is a style choice, not a limitation; commit to it hard and it will look intentional.
- All in-game text is in **Turkish**. All code identifiers, comments and file names in English.
- Static typing everywhere (`var hp: int = 3`, `func f(x: float) -> void:`). No untyped `var`.
- Must run with zero errors and zero warnings in the Godot output panel on first F5.

## GAME

**Title:** "SEVEN-ELEVEN: GECE VARDİYASI"

**Genre:** First-person 3D convenience-store clerk simulator + social-deduction horror-comedy. *Papers, Please* meets a late-night snack run, where half your customers are monsters doing a hilariously bad job of pretending to be human.

**Premise:** You are the lone night clerk at a 7-Eleven on the edge of town, 23:00 to 06:00. Ordinary humans come in. So do monsters wearing human suits. Company policy: humans get served, monsters get politely refused and reported. Serve a monster and someone gets eaten. Refuse a human and you get a complaint and a pay cut. The monsters are not scary — they are *deeply committed to the bit* and terrible at it. The horror is atmospheric; the comedy is in the details.

**Tone:** Deadpan, dry, absurd. Buzzing fluorescent lights, a broken slushie machine, rain against the window. Monsters say things like "SELAMLAR, MAAŞLI İNSAN YOLDAŞ. ET TÜPÜ İSTİYORUM." Never gory, never mean-spirited — creepy-cute.

## SPACE & PLAYER

The whole game takes place in one small 3D store interior, built from CSG boxes: a serving counter with a register, a wall of shelves, a drinks cooler that hums and glows, a hotdog roller, a slushie machine with a "BOZUK" sign taped to it, a rain-streaked front window, and a doorway with a bell. Behind the counter is the clerk's nook: a **CCTV monitor**, a **mirror** angled at the customer, a policy fax machine, and a rack holding the handheld tools.

The player is a first-person `CharacterBody3D` confined behind the counter — free mouse-look, `WASD` to step along the counter and turn to the monitor/mirror/fax. Never let them leave the nook. A `RayCast3D` from the camera drives a "bak / kullan" interaction prompt (**E**) for the monitor, the fax, the ID card on the counter, and the tool rack.

## CORE LOOP (per customer)

1. The door bell rings. A customer walks in on a `Path3D`, stops at the counter, and turns to face you.
2. They speak a greeting line (typewriter subtitle) and place 1–4 physical item meshes on the counter.
3. **INVESTIGATION PHASE** — limited time, limited tool uses. Look, lean, and use tools to gather evidence.
4. **DECISION** — **[SAT]** (serve: scan each item with the barcode gun, take payment, they leave) or **[REDDET]** (refuse: slap the counter buzzer, they're escorted out).
5. Result panel: right or wrong? Money / reputation / suspicion update. Next customer walks in.

A shift = 8–12 customers. Between shifts: a night-summary screen (earnings, rent due, accuracy) plus a newspaper clipping that reacts to your actual mistakes ("YEREL ADAM SOSİSLİ SANDVİÇ ALIRKEN YENDİ").

## THE DETECTION SYSTEM (the heart of the game — make it deep)

Every customer is generated from a hidden `is_monster: bool` plus a set of **tells**. A human has 0 tells; a monster has 1–4. Crucially, **some tells are only visible through a specific tool or viewpoint**, so the player must actively investigate instead of eyeballing.

### The render-layer trick (implement exactly this — it is what makes 3D worth it)

Every customer node holds **two meshes in the same skeleton pose**: the `DisguiseBody` (human) and the `TrueForm` (tentacles, extra limbs, a wet mass in a hoodie). Control who sees which with visual layers and camera `cull_mask`s:

- **Layer 1 — Disguise.** The player camera sees only this. Always the human-looking body.
- **Layer 2 — True form.** Rendered by the **CCTV `SubViewport` camera** and the **mirror `SubViewport` camera** only. A camera-vulnerable monster puts its `TrueForm` on layer 2, so the monitor shows a tentacle mass while the thing in front of you smiles politely.
- **Layer 3 — Shadow only.** A copy of the `TrueForm` with `cast_shadow = SHADOWS_ONLY`, invisible to every camera but still casting. The fluorescent light throws a **wrong shadow** on the floor — a tell you can only catch by looking down.
- **UV reveal.** The `UV EL FENERİ` is a `SpotLight3D` on its own light cull layer plus a `uv_amount` shader uniform; monster materials have hidden emissive slime patches, suit seams and a back zipper that only light up inside the cone.

Each monster picks which of these channels betray it, so no single tool is a universal oracle.

### Tells (implement at least 16)

**Naked eye, if you look closely:**
- Wrong finger count (6, 7 or 3) — actually modeled on the hand
- Vertical slit pupils, a third eye, or eyes that haven't blinked in 30+ seconds
- Scale texture faintly tiling across the neck
- Too many teeth, or a jaw that opens wider than a jaw should
- Joints that bend the wrong way when they reach for their wallet
- Doesn't cast the shadow a body that shape would cast

**Tool / viewpoint gated:**
- **UV EL FENERİ** — slime, seams, the zipper down the back
- **GÜVENLİK KAMERASI** — true form on the CRT while the counter view stays human
- **AYNA** — the reflection lags, or reflects something else entirely
- **TERMOMETRE** — humans read 36–37.5 °C; monsters read 4 °C, 61 °C, or "HATA: NaN"
- **KİMLİK OKUYUCU** — pick the card up and rotate it in hand: birth year 1804, expiry "YARIN", height "3 metre", photo mismatch, name "İNSAN OĞLU İNSAN", blood type "HEPSİ"
- **BARKOD OKUYUCU** — items ring up as "EVCİL HAYVAN MAMASI ×40" or "ÇİĞ ET (İNSAN?)" — the basket itself is a tell

**Behavioral / dialogue:**
- Says something no human would: "BU BEDENDE 400 YILDIR YAŞIYORUM", "PARA. EVET. BEN DE PARAYI SEVERİM."
- Pays with wet cash, a live beetle, teeth, or a coin minted in 1623
- Asks where the "yumuşak insan" (the previous clerk) went
- Refers to itself in the plural: "BİZ SİGARA İSTİYORUZ"

Tells must be **combinable and sometimes ambiguous**. Include at least three **false-positive traps** (genuinely human customers who look wrong — a bloodshot night-shift trucker with a damp roll of cash) and at least two **perfect mimics** (monsters with exactly one very subtle tell).

**Rule escalation:** each night the fax machine prints a rule that reshapes the deduction — Night 2: "reflections may now lag legally"; Night 3: "one monster species is licensed and MUST be served (green ID sticker)"; Night 4: "the CCTV is compromised and lies 20% of the time"; Night 5: "a monster is wearing the manager's face." A rulebook openable any time with **R**.

## SYSTEMS

- **PARA:** each correct serve earns ₺; each night has rent plus a random expense. Below ₺0 = game over (evicted).
- **ŞÜPHE (0–100):** serving a monster spikes it. At 100 the store is "marked" and the final night triggers early.
- **İTİBAR:** refusing humans tanks it; at 0 you're fired.
- **SÜRE:** per-customer countdown; running out is an auto-refuse and annoys everyone. Generous on Night 1, tighter every night.
- **TOOL LIMITS:** per-customer uses — UV ×2, thermometer ×1, ID card once, CCTV unlimited but every look costs 5 seconds of the timer and you cannot watch the customer while you watch the screen. This is the central tension: **looking away is expensive**.
- **COMBO:** consecutive correct calls build a bonus multiplier.
- **5 NIGHTS + FINALE:** Night 5 ends with a boss — three smaller monsters stacked in a trench coat — identified by catching the *seam* with the UV light while it distracts you with small talk. Three distinct endings: fired, eaten, survived-until-sunrise.
- **SAVE:** progress, money and best score persisted to `user://save.cfg` via `ConfigFile`.

## GODOT ARCHITECTURE (follow this structure)

```
res://project.godot
res://scenes/Main.tscn                  # root, swaps between screens
res://scenes/TitleScreen.tscn
res://scenes/Store.tscn                 # the 3D interior, lights, player
res://scenes/Player.tscn                # CharacterBody3D + Camera3D + RayCast3D
res://scenes/Customer.tscn              # DisguiseBody + TrueForm + ShadowProxy
res://scenes/props/CCTVMonitor.tscn     # SubViewport + CRT shader on a quad
res://scenes/props/Mirror.tscn          # SubViewport + reflection camera
res://scenes/props/FaxMachine.tscn
res://scenes/tools/UVFlashlight.tscn    # SpotLight3D on its own light layer
res://scenes/tools/Thermometer.tscn
res://scenes/tools/BarcodeGun.tscn
res://scenes/tools/IDCard.tscn          # held, rotatable, two-sided
res://scenes/ui/HUD.tscn                # money, rep, suspicion, timer, notepad
res://scenes/ui/NightSummary.tscn
res://scenes/ui/Rulebook.tscn
res://shaders/crt.gdshader              # scanlines, curvature, chroma bleed
res://shaders/uv_reveal.gdshader        # emissive under the UV cone only
res://shaders/rain_window.gdshader
res://scripts/autoload/GameState.gd     # money, rep, suspicion, night, save/load
res://scripts/autoload/AudioBus.gd      # synthesized SFX
res://scripts/data/CustomerData.gd      # class_name CustomerData extends Resource
res://scripts/data/ItemData.gd
res://scripts/data/TellDefs.gd          # tell catalogue + revealing channel
res://scripts/CustomerGenerator.gd      # CustomerData for a given night
res://scripts/BodyBuilder.gd            # assembles primitive meshes from CustomerData
res://scripts/ShiftManager.gd           # queue, timer, decision resolution, scoring
res://scripts/content/Dialogue.gd       # all Turkish lines as const arrays
res://scripts/content/Catalog.gd        # store items, prices, barcodes
res://scripts/content/Headlines.gd
```

- Register `GameState` and `AudioBus` as **autoloads** in `project.godot`.
- Communicate with **signals**, not `get_node("../../..")` chains.
- `TellDefs.gd` is one data table mapping tell id → Turkish display name, revealing channel (eye / uv / cctv / mirror / shadow / thermo / id / barcode / dialogue), the mesh or material change it causes, and rarity. Adding a tell must be a one-line data change, not new branching logic.
- `CustomerGenerator` takes the night number and returns a `CustomerData`; `BodyBuilder` turns that into the actual node tree. Body, dialogue, basket, ID card and every tool readout derive from that single object. **Never store the answer in two places.**

## LIGHTING & ATMOSPHERE (Forward+ — use it)

- One flickering fluorescent `OmniLight3D` bank over the counter, driven by a noise-based energy curve, plus a cold blue `DirectionalLight3D` outside the window.
- **Volumetric fog** at low density so the light shafts read, and a slight `WorldEnvironment` glow on the cooler and the register display.
- SSAO on, SDFGI off (too heavy for this scale), tonemap Filmic, and a subtle vignette + film grain as a full-screen shader on the HUD `CanvasLayer`.
- Palette: sodium-yellow store light, cold blue night outside, sickly green CRT.
- Display: `1920×1080`, `canvas_items` stretch, aspect `expand`.

## AUDIO (synthesized at runtime, no files)

Door bell (two-tone chime), barcode beep, fluorescent hum loop (filtered noise), rain (filtered noise, positioned at the window as `AudioStreamPlayer3D`), cooler compressor drone, refuse buzzer, register cha-ching, a sub-bass sting when a monster is served, and a faint wet squelch positioned **on the customer** whose volume scales with how many tells they have — a subconscious spatial hint. Build each as an `AudioStreamWAV` generated in `AudioBus.gd` at startup. Separate `Master` / `SFX` / `Ambience` buses, plus a mute toggle.

## INPUT MAP (add these actions to `project.godot`)

`move_forward/back/left/right`=WASD, `look`=mouse, `interact`=E, `tool_uv`=1, `tool_temp`=2, `tool_barcode`=3, `decide_serve`=Space, `decide_refuse`=X, `open_rulebook`=R, `mute`=M, `pause`=Escape (releases mouse capture).

A **NOT DEFTERİ** HUD panel logs each tell as the player discovers it, so they can reason before deciding.

## CONTENT VOLUME (do not skimp — this is what makes it feel finished)

- ≥ 30 unique customer archetypes (mixed humans and monsters), each a distinct primitive-mesh build
- ≥ 60 distinct Turkish dialogue lines, all funny, none repeating within a night
- ≥ 25 store items with names, prices, barcode strings and a simple mesh each
- ≥ 10 newspaper headlines

## QUALITY BAR

Clean, commented, statically typed GDScript. No dead code, no placeholder meshes, no missing-resource errors. Stable 60 FPS — the store is one small room, keep draw calls low and reuse materials. Difficulty curve balanced: Night 1 winnable by a first-timer, Night 5 genuinely hard. Deliver every file in full.

---

## Notlar

- 3D versiyon 2D'den belirgin şekilde daha büyük bir çıktı. AI yarıda kesilirse:
  `"Continue exactly where you stopped. Do not repeat any file you already emitted."`
- `.tscn` yapıştırmak zahmetliyse prompt'un başına ekle:
  `"Build every scene tree in code from _ready(). Only Main.tscn should exist as a file: a single Node3D with Main.gd attached."`
