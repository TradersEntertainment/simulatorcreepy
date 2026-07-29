# 7-11 SIMULATOR: NIGHT SHIFT — Tek Prompt

Aşağıdaki metnin tamamını kopyalayıp tek seferde oyun motoruna / AI kod üreticisine yapıştır.

---

Build a COMPLETE, FINISHED, playable game in a SINGLE self-contained `index.html` file. No external files, no CDN links, no image/audio assets — everything is inline HTML/CSS/JS, all graphics drawn with CSS/SVG/Canvas, all sound synthesized with the Web Audio API. It must run by double-clicking the file. Do not stub anything, do not leave TODOs, do not ask me questions — ship the whole thing in one pass.

## GAME

**Title:** "SEVEN-ELEVEN: GECE VARDİYASI" (7-Eleven: Night Shift)

**Genre:** First-person convenience-store clerk simulator + social-deduction horror-comedy. Think *Papers, Please* meets a late-night snack run, where half your customers are monsters doing a hilariously bad job of pretending to be human.

**Premise:** You are the lone night clerk at a 7-Eleven on the edge of town, 23:00 to 06:00. Ordinary humans come in. So do monsters wearing human suits. Company policy: humans get served, monsters get politely refused and reported. Serving a monster gets someone eaten. Refusing a human gets you a complaint and a pay cut. The monsters are not scary — they are *deeply committed to the bit* and terrible at it. The horror is atmospheric; the comedy is in the details.

**Tone:** Deadpan, dry, absurd. Buzzing fluorescent lights, a broken slushie machine, rain outside. The monsters say things like "GREETINGS, FELLOW WAGE EARNER. I REQUIRE THE MEAT TUBE." Never mean-spirited, never gory — creepy-cute. All in-game text is in **Turkish**; keep code identifiers/comments in English.

## CORE LOOP (per customer)

1. Door chime. A customer walks up to the counter and is rendered as a procedurally drawn portrait (see ART).
2. They say a greeting line and place 1–4 items on the counter.
3. **INVESTIGATION PHASE** — you have limited time and limited tool uses. Gather evidence with the tools below.
4. **DECISION** — press one of two big buttons:
   - **[SAT] (SERVE)** — scan the items, take payment, they leave.
   - **[REDDET] (REFUSE)** — hit the counter buzzer, they get escorted out.
5. Result screen: were you right? Money/reputation/suspicion updates. Next customer.

A shift = 8–12 customers. Between shifts: a night-summary screen (earnings, rent due, accuracy) plus a short newspaper clipping that reacts to your mistakes ("YEREL ADAM SOSİSLİ SANDVİÇ ALIRKEN YENDİ").

## THE DETECTION SYSTEM (this is the heart of the game — make it deep)

Every customer is generated from a hidden `isMonster` flag plus a set of **tells**. A human has 0 tells. A monster has 1–4 tells drawn from the pool below. Crucially: **some tells are only visible through a specific tool**, so the player must actively investigate rather than eyeball.

Implement **at least 14 distinct tells**, including:

**Visual (naked eye, if you look closely):**
- Wrong finger count (6, 7, or 3 fingers) — visible in the portrait's hands
- Pupils are vertical slits, or there are three eyes, or the eyes don't blink for 30+ seconds
- Skin has a faint scale/tile texture
- Too many teeth, or the smile is wider than the face allows
- Reflection in the counter glass doesn't match the body

**Tool-gated:**
- **UV EL FENERİ (UV flashlight)** — press-and-hold; monsters glow with slime patches / suit seams / a zipper down the back
- **GÜVENLİK KAMERASI (CCTV monitor)** — the on-screen feed shows their TRUE form (a tentacle mass in a hoodie) while the counter view looks human. Some monsters are camera-safe, so it's not a universal oracle.
- **TERMOMETRE (infrared thermometer)** — humans read 36–37.5 °C; monsters read 4 °C or 61 °C or "ERROR: NaN"
- **KİMLİK OKUYUCU (ID scanner)** — check the ID card: birth year 1804, expiry date "YARIN", height "3 metre", photo doesn't match, name is "İNSAN OĞLU İNSAN", blood type "HEPSİ"
- **BARKOD (barcode scanner)** — some items ring up as "EVCİL HAYVAN MAMASI ×40" or "ÇİĞ ET (İNSAN?)" — the *shopping basket itself* is a tell

**Behavioral / dialogue:**
- Says something a human wouldn't: "BU BEDENDE 400 YILDIR YAŞIYORUM", "PARA. EVET. BEN DE PARAYI SEVERİM."
- Pays with wet cash, a live beetle, teeth, or a coin minted in 1623
- Asks where the "yumuşak insan" (the previous clerk) went
- Correct grammar failure: refers to itself in plural ("BİZ SİGARA İSTİYORUZ")

Make tells **combinable and sometimes ambiguous**: a human night-shift trucker with a weird cash bundle and bloodshot eyes should be a real trap. There must be at least three "false-positive traps" (odd but genuinely human customers) and at least two "perfect mimics" (monsters with exactly one, very subtle tell).

**Rule escalation:** each night the policy fax adds a new rule that reshapes the deduction — e.g. Night 2: "reflections may now lag"; Night 3: "one monster species is licensed and MUST be served (green ID sticker)"; Night 4: "the CCTV has been compromised — it lies 20% of the time"; Night 5: "a monster is wearing the manager's face." Show the new rules on a rulebook screen the player can reopen with **[R]** at any time.

## SYSTEMS

- **PARA (Money):** each correct serve earns ₺; each night has rent + a random expense. Below ₺0 = game over (evicted).
- **ŞÜPHE (Suspicion meter, 0–100):** serving a monster spikes it. At 100, the store is "marked" and the final night triggers early.
- **İTİBAR (Reputation):** refusing humans tanks it; at 0 you're fired.
- **SÜRE (Timer):** each customer has a countdown; letting it run out counts as an auto-refuse and annoys everyone. The timer is generous on Night 1 and tightens each night.
- **TOOL LIMITS:** each tool has per-customer uses (e.g. UV ×2, thermometer ×1, CCTV unlimited but slow — it costs 5 seconds of the timer). This forces real decisions.
- **COMBO/STREAK:** consecutive correct calls build a bonus multiplier and a small on-screen "BURN THE SLUSHIE MACHINE" style flourish.
- **5 NIGHTS + FINALE:** Night 5 ends with a boss customer — a monster made of three smaller monsters in a trench coat that you must identify by catching the *seam* with the UV light while it distracts you with small talk. Write three distinct endings: fired, eaten, and survived-until-sunrise.
- **SAVE:** persist progress, money, and best score to `localStorage`.

## ART (all procedural — zero image files)

- The store is drawn with CSS: counter in the foreground, shelves and a rain-streaked window behind, a flickering fluorescent light (CSS animation), a hotdog roller, a slushie machine with a "BOZUK" sign.
- Customers are built from a **layered SVG/CSS portrait generator**: head shape, skin tone, eyes (count/shape/pupil), mouth (teeth count), hair/hat, clothing, hands (finger count). Monster traits are just extra layers, so the same generator makes both humans and monsters and the tells are literally rendered geometry, not text labels.
- Palette: sodium-yellow store light, cold blue night outside, sickly green CRT for the CCTV feed. Add a subtle CRT scanline + vignette overlay and a slight film-grain animation.
- Everything responsive down to 900 px wide; the UI is a fixed 16:9 stage that scales.

## AUDIO (Web Audio API, synthesized)

Door chime (two-tone bell), barcode beep, fluorescent hum loop (filtered noise), rain (filtered noise), buzzer for refuse, a low sub-bass "wrong" sting when a monster is served, register cha-ching, and a tiny wet squelch when a monster is on screen (volume scales with how many tells it has — a subtle subconscious hint). Add a mute button.

## UI / CONTROLS

- Mouse-driven, with keyboard shortcuts: `1`=UV, `2`=Thermometer, `3`=CCTV, `4`=ID scanner, `5`=Barcode, `Space`=Serve, `X`=Refuse, `R`=Rulebook, `M`=Mute.
- A "NOT DEFTERİ" (notepad) panel where discovered tells for the current customer get logged as you find them, so the player can reason before deciding.
- Start screen with title, blurb, controls, and a [BAŞLA] button. Pause with `Esc`.

## CONTENT VOLUME (do not skimp — this is what makes it feel finished)

- ≥ 30 unique customer archetypes (mix of humans and monsters)
- ≥ 60 distinct dialogue lines, all funny, none repeated within a night
- ≥ 25 store items with names, prices, and barcode strings
- ≥ 10 newspaper headlines for the between-night screens

## QUALITY BAR

Clean, organized, commented JavaScript in clearly separated modules (state, generator, render, audio, ui, game loop). No runtime errors in console. No dead code. Balanced difficulty curve — Night 1 must be winnable by a first-timer, Night 5 must be genuinely hard. Deliver the complete `index.html` and nothing else.

---
