# MEŞRUİYET — CO-OP + AI + WEB (ikinci prompt)

**Bunu tek kişilik oyun çalıştıktan SONRA ver.** Birinci prompt (`PROMPT-CITY.md`) bitip proje
F5 ile hatasız açıldıktan sonra bu ikinci geçişi uygula. İkisini birleştirip tek promptta
vermeye çalışmak neredeyse kesin yarım kalmış bir build üretir.

---

You are adding **online co-op, AI players, and a web build** to the existing, working Godot 4.4 project "MEŞRUİYET" (2D, Compatibility renderer). The single-player game is complete and running. Do not rewrite it — extend it. Output every new file in full and, for every existing file you touch, output the complete modified file rather than a diff. Do not ask me questions; ship the whole feature in one pass.

## 1. THE CO-OP PREMISE

**2–6 players. One VALİ (governor), up to five BAKAN (ministers): `MALİYE`, `TARIM`, `GÜVENLİK`, `İMAR`, `HALK`.**

The single-player game already models ministers who distort the numbers they report. **In co-op, human players replace that formula.** Nothing about the architecture changes: `Reporting.report_for(domain)` already funnels every minister report through one function. In co-op the source of that dictionary is a player's submitted report instead of the bias formula. **Implement it exactly that way — swap the source, keep the plumbing.**

- A **minister** sees the **true values of their own domain only**, plus whatever the Vali chooses to tell them. Not the treasury, not the axes, not another domain.
- The **Vali** sees only what the five ministers report, decides everything alone, and holds every instrument: build, zone, decrees, laws, budget, council, appointments, elections.

Nobody is forced to lie. Everybody has a small reason to.

## 2. AI SEATS — every single role can be played by the computer

This must be a first-class feature, not a fallback, because it is how the game gets tested and how someone plays alone.

**Any of the six seats — including the Vali — can be AI.** A lobby is valid with zero humans (a full AI demo game the player can watch) or with one. The two configurations that must work perfectly:
- **SOLO VALİ:** you are the Vali, five AI ministers report to you. This is the default single-player experience and the main test path.
- **SOLO BAKAN:** you are one minister, an AI Vali governs and the other four ministries are AI. **The AI Vali acts on the numbers you report** — so if you lie, it makes decisions based on your lie, and you watch the city bend. This is how you test that the deception loop actually closes.

**AI ministers are characters, not noise.** Each AI minister gets:
- a **distortion profile** driven by the existing single-player bias formula, plus a personality: `ŞİŞİRİCİ` (inflates output), `SAKLAYICI` (reports healthy totals over a blocked chain stage), `ALARMCI` (inflates threats to win funding), `DÜRÜST AMA BECERİKSİZ` (accurate numbers, bad forecasts), `YALAKA` (reports whatever last pleased the Vali).
- **a secret objective it actually pursues** (§3). An AI whose objective is "SANAYİ nüfusu üçe katlansın" will lobby for factories in its telegrams, overstate industrial returns, and understate pollution.
- **telegrams it writes every turn**, in character, built from the stock phrase pool plus its personality's own lines. Without this, solo play is a spreadsheet and tests nothing. Write at least 10 telegram templates per personality.
- **reactions to being dismissed.** An AI minister who is sacked complains to the council; its replacement is drawn as SADIK or UZMAN by the same rules a human faces.

**AI difficulty, exposed in the lobby:**
- `DÜRÜST` — zero distortion. For debugging the simulation without the fog. Say clearly in the UI that this is a test setting.
- `NORMAL` — the single-player bias formula as-is.
- `SİYASETÇİ` — heavy distortion, actively lobbies the Vali, coordinates with other AI ministers when their objectives align (a visible private channel appears, contents hidden, exactly as between humans).

**The AI Vali** uses a simple, readable utility heuristic — never a hidden oracle. It must decide *from the reported values*, never from the true ones. This is a correctness requirement: if the AI Vali reads `GameState` directly instead of `Reporting`, the entire deception system is silently bypassed. Assert it in code.

**Mixed and hot-swapped:** humans and AI in any combination. A human who disconnects is covered by AI for the remainder of that turn and gets the seat back on reconnect. A human can hand their seat to AI mid-game from the pause menu.

## 3. SECRET OBJECTIVES

Deal each minister — human or AI — **one hidden objective** from a pool of at least 14: "kendi fraksiyonun 60 üzerinde bitsin", "SANAYİ nüfusu üçe katlansın", "hiçbir bakan görevden alınmasın", "hazine 60. turda 5000 ₺ üzerinde olsun", "en az iki seçim ertelenmiş olsun", "kendi mahallende hiç ayaklanma çıkmasın", "şehirde hiç kontrol noktası kurulmasın", "kendi bütçe kalemin hiç kesilmesin".

**If the city collapses, everybody loses, objectives included.** There is no traitor and no saboteur. Everyone genuinely wants the city to survive; everyone also wants their own thing; so everyone shades their reports a little. That is real politics and it is funnier than a hidden villain. Never imply sabotage anywhere in the UI or text.

At the end, score each player: survival is the shared prize, objectives are personal bragging rights, and **lifetime reporting accuracy is displayed whether they like it or not** (§7).

## 4. NETWORKING — WebSocket, not ENet

Use **`WebSocketMultiplayerPeer`** with Godot's high-level multiplayer API. **Do not use `ENetMultiplayerPeer`** — browsers cannot open UDP sockets, so ENet would make the web build impossible. WebSocket behaves identically on desktop and in the browser, and this game is turn-based so TCP latency is irrelevant.

- **Host/join** with a 5-character lobby code: a "Sunucu Aç" screen (desktop only) and a "Katıl" screen taking `wss://host/lobby/CODE`.
- **Host-authoritative.** The host owns `GameState`; true values never go out wholesale. Each peer receives **only its own role's view**, enforced when the message is constructed — not by hiding fields in the UI. A minister's client must never hold data it isn't entitled to, because anyone can open the debugger.
- Traffic is a few hundred bytes per turn. Send whole role-views as dictionaries; do not build a delta system.
- **Reconnect** by lobby code into the same seat; AI covers the gap (§2).
- Support `--headless --server` so the host can be a dedicated process rather than a player (§9).

## 5. TURN FLOW IN CO-OP

1. **BAKAN FAZI** (simultaneous, 90-second timer, host-adjustable). Each minister sees their own true numbers and composes a report. The report UI has two columns — **GERÇEK**, visible only to them, and **RAPOR**, what the Vali will see — with direct entry or a truth↔flattery slider per figure, plus one free-text note. Early submit allowed; on timeout the current state is sent. AI ministers submit instantly but with a randomized 5–40 s delay so the phase feels populated.
2. **VALİ FAZI.** Five telegrams arrive. The Vali builds, governs, appoints, ends the turn. Ministers watch a restricted view and may send telegrams but cannot act.
3. **ÇÖZÜMLEME.** The simulation ticks and everyone watches the same animated resolution. This shared moment is where the table finds out something was wrong.
4. **OLAY.** Event cards resolve; the Vali chooses, ministers may advise.

## 6. COMMUNICATION — the funniest system in the game

- **Resmî Telgraf** — the only public channel. A composer offering stock bureaucratic phrases ("durum tatminkârdır", "tedbir alınmıştır", "mesele tetkik edilmektedir", "zât-ı âlinizi meşgul etmeye değmez") plus free text. Every telegram is stamped, archived, and **quoted back verbatim at the accountability session.** Write at least 30 stock phrases.
- **Özel Kanal** — any two ministers may open a private channel. Two people quietly agreeing on a story is the best thing that can happen in this game. The Vali sees that a channel exists as a small icon, never its contents. AI ministers use these too when objectives align.
- **Vali Fermanı** — one public broadcast per turn. Ministers cannot reply publicly, only by telegram next turn.

## 7. THE FINALE — HESAP VERME OTURUMU, CO-OP VERSION

The single-player accountability session already reveals every true value beside what the player was told. In co-op it becomes the whole point:

1. **Sapma tablosu** — per player, per turn: reported vs true, with a lifetime average. "MALİYE — Ortalama şişirme: %34. En büyük tek yalan: 41. tur, hazine 2200 bildirildi, gerçek 310."
2. **Telgraf alıntıları** — their own worst telegram quoted on screen, in their own words, beside the true figure at that moment.
3. **Gizli hedefler açılır** — everyone learns what everyone was quietly steering toward.
4. **Özel kanallar açılır** — private channel contents published in full. Announce this at game start so people know the record will surface. It will change how they write and it will not stop them.
5. **The axis trail** full-screen, each irreversible turn marked with the decree that caused it **and which minister's report the Vali was acting on.**
6. Then the ending card and a per-player scorecard.

Play the roast for comedy; let the last screen go completely cold.

## 8. DEBUG & TEST TOOLS (build these — the game cannot be balanced without them)

Gate all of it behind `OS.is_debug_build()` and a `--test` flag so it never ships enabled.

- **`F9` — GERÇEK toggle.** Overlays every displayed number with its true value in red. The single most useful tool for verifying the reporting layer.
- **`F10` — turn skipper.** Advance N turns with AI filling every seat, no animation. Reaching turn 40 must take seconds, not an hour.
- **Seeded RNG.** A seed field on the title screen; the same seed reproduces an identical run including event order and AI personalities. Print the seed on every ending screen.
- **Event injector.** A searchable list of all 50+ event cards; fire any of them immediately.
- **State jumper.** Set money, grievance, axis positions, threat level, garrison and debt directly, so any crisis or collapse can be reached in one action instead of forty turns.
- **`--autoplay`** — a full AI-vs-AI game to completion with a summary line per turn to stdout. Run it a hundred times to check the balance target from §14 of the first prompt (a naive run collapses around turn 35; both axes under 40 must not reach turn 60).
- **Divergence log.** Write every reported-vs-true pair to `user://divergence.csv` for offline analysis.

## 9. WEB EXPORT + CLOUDFLARE (get this exactly right — it is where projects die)

**Export:** Godot 4 Web preset, Compatibility renderer (already the project's renderer), threads enabled.

**Required HTTP headers.** A threaded Godot 4 web build needs `SharedArrayBuffer`, which requires a secure context plus two headers. Without them the game boots to a blank page with a cryptic console error. Commit a `_headers` file at the Pages output root:

```
/*
  Cross-Origin-Opener-Policy: same-origin
  Cross-Origin-Embedder-Policy: require-corp
```

Confirm `.wasm` is served as `application/wasm` and that `.pck` is not rewritten.

**Cloudflare Pages is static and cannot host a WebSocket server.** Generate a `DEPLOY.md` documenting both paths with exact commands:

**Path A — playtesting today.** Run the game headless locally and tunnel it:
```
godot --headless --server --port 8910
cloudflared tunnel --url ws://localhost:8910
```
This prints a public `wss://…trycloudflare.com` address. Players open the Pages URL and paste it into "Katıl". Zero infrastructure code, works in minutes, the correct choice while iterating.

**Path B — persistent lobby server.** A **Cloudflare Worker with one Durable Object per lobby**, using the WebSocket Hibernation API so idle lobbies cost nothing. Write it in full:
- `worker.ts` (~150 lines): `fetch` handler upgrading `/lobby/:code` to a WebSocket and routing it into the `Lobby` Durable Object; the DO keeps the peer list, assigns peer ids, forwards framed messages, and evicts empty lobbies.
- **The Worker is a dumb relay.** It holds no game state and knows no rules; authority stays on the host client. This keeps the game logic in exactly one language — do not reimplement any simulation in TypeScript.
- `wrangler.toml` with the Durable Object binding and migration block.
- Deploy commands: `npx wrangler deploy` for the Worker, `npx wrangler pages deploy ./build/web --project-name mesruiyet` for the client, and the optional custom-domain step.
- Note in `DEPLOY.md` that Durable Objects free-tier limits should be checked in the dashboard before relying on them.

**Web build caveats to handle in code:** `user://` is IndexedDB-backed, so flush saves explicitly; the audio context needs a user gesture, so gate `AudioBus` behind the title-screen click; `OS.get_name() == "Web"` must disable "Sunucu Aç" because a browser cannot listen for connections; and the canvas must resize with the window.

## 10. COMEDY LAYER (the tone is comic — commit to it)

Ministers are comic characters with names, portraits and running gags: the Maliye minister who proposes a tax named after himself every eight turns, the Güvenlik minister who sees Mersa's agents in the bakery, the İmar minister whose every estimate is exactly half the real cost, the Tarım minister who reports harvests in units nobody recognises, the Halk minister who has never once said anyone was unhappy. Write at least 8 recurring bits and fire them on a schedule so the table starts anticipating them.

Events are absurd on the surface: a prize bull loose in the market, an ambassador who will not leave, a prophet dating the apocalypse to next Tuesday, a shipment of 4000 unwanted hats. **But every mechanical consequence is played dead straight.** Hunger is hunger. The coup is a coup. If a crisis ever resolves itself as a gag, or a punishment isn't real, the drift theme dies and the game becomes a toy. **Funny mouths, honest math.**

## 11. NEW FILES

```
res://scripts/net/NetManager.gd            # WebSocketMultiplayerPeer, lobby codes, reconnect
res://scripts/net/RoleView.gd              # per-role dictionary; the security boundary
res://scripts/net/CoopTurnController.gd    # minister phase, vali phase, timers
res://scripts/ai/AIMinister.gd             # personality, distortion, telegrams, lobbying
res://scripts/ai/AIGovernor.gd             # utility heuristic reading ONLY reported values
res://scripts/ai/AIPersonalities.gd        # 5 profiles + 10 telegram templates each
res://scripts/sim/ObjectiveManager.gd      # secret objectives, scoring
res://scripts/debug/DebugTools.gd          # F9/F10, injector, state jumper, autoplay
res://scripts/content/Objectives.gd        # 14+ objectives as data
res://scripts/content/TelegramPhrases.gd   # 30+ stock bureaucratic phrases
res://scenes/net/LobbyScreen.tscn          # host/join, seat picking, AI toggles, difficulty
res://scenes/net/MinisterScreen.tscn       # GERÇEK vs RAPOR composer, free-text note
res://scenes/net/TelegramPanel.tscn        # public telegrams + private channels
res://scenes/ui/AccountabilityScreen.tscn  # deviation table, quotes, reveals
res://web/_headers                         # COOP/COEP
res://web/wrangler.toml                    # Worker + Durable Object config
res://web/worker.ts                        # Durable Object relay, full implementation
res://DEPLOY.md                            # both hosting paths, exact commands
```

- Extend `Reporting` so a submitted report — human or AI — overrides the bias formula for that domain and turn, with the formula as fallback. **One code path, three sources: human, AI, formula.**
- `RoleView.gd` is a security boundary, not a UI convenience. Assert in code that a minister peer's received dictionary contains no key outside its own domain.

## 12. QUALITY BAR

Typed GDScript, signals not node paths, no warnings. The web build must load from a cold Cloudflare Pages deploy and reach the title screen in under 10 seconds on a normal connection. A 6-seat lobby — any mix of humans and AI — must complete a full 60-turn game without a desync; if any figure can disagree between host and client, it is a bug, because the host is the only source of truth. `--autoplay` must run 100 games without a crash. Deliver every file in full, including `DEPLOY.md`, `worker.ts` and `wrangler.toml`.

---
