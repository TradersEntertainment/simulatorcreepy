# MEŞRUİYET — CO-OP + WEB (ikinci prompt)

**Bunu tek kişilik oyun çalıştıktan SONRA ver.** Birinci prompt (`PROMPT-CITY.md`) bitip
projeyi F5 ile hatasız açtıktan sonra bu ikinci geçişi uygula. İkisini birleştirip tek
promptta vermeye çalışmak neredeyse kesin yarım kalmış bir build üretir.

---

You are adding **online co-op and a web build** to the existing, working Godot 4.4 Godot project "MEŞRUİYET" (2D, Compatibility renderer). The single-player game is complete and running. Do not rewrite it — extend it. Output every new file in full and, for every existing file you touch, output the complete modified file rather than a diff. Do not ask me questions; ship the whole feature in one pass.

## 1. THE CO-OP PREMISE

**2–6 players. One VALİ (governor), up to five BAKAN (ministers): `MALİYE`, `TARIM`, `GÜVENLİK`, `İMAR`, `HALK`.**

The single-player game already models ministers who distort the numbers they report. **In co-op, human players replace that formula.** Nothing about the architecture changes: `Reporting.report_for(domain)` already funnels every minister report through one function. In co-op, the source of that dictionary is a player's submitted report instead of the bias formula. **Implement it exactly that way — swap the source, keep the plumbing.**

- A **minister** sees the **true values of their own domain only**, plus whatever the Vali chooses to tell them. Nothing else. They do not see the other domains' truth, the treasury, or the axes.
- The **Vali** sees only what the five ministers report, makes every decision alone, and holds every instrument: build, zone, decrees, laws, budget, council, appointments, elections.
- **Empty ministries are filled by the AI** using the existing bias formula, so the game is fully playable with 2 players (Vali + one minister) all the way to 6.

Nobody is forced to lie. Everybody has a small reason to.

## 2. SECRET OBJECTIVES

Deal each minister **one hidden objective** from a pool of at least 14, e.g. "kendi fraksiyonun 60 üzerinde bitsin", "SANAYİ nüfusu üçe katlansın", "hiçbir bakan görevden alınmasın", "hazine 60. turda 5000 ₺ üzerinde olsun", "en az iki seçim ertelenmiş olsun", "kendi mahallende hiç ayaklanma çıkmasın".

**If the city collapses, everybody loses, objectives included.** There is no traitor and no saboteur. Everyone genuinely wants the city to survive; everyone also wants their own thing; so everyone shades their reports a little. That is real politics and it is much funnier than a hidden villain. Never use the words "hain" or "görev" in a way that implies sabotage.

At the end, score each player: survival is the shared prize, objectives are personal bragging rights, and **accuracy of their lifetime reporting is displayed whether they like it or not** (§6).

## 3. NETWORKING — WebSocket, not ENet

Use **`WebSocketMultiplayerPeer`** with Godot's high-level multiplayer API. **Do not use `ENetMultiplayerPeer`** — browsers cannot open UDP sockets, so ENet would make the web build impossible. WebSocket runs identically on desktop and in the browser, and this game is turn-based so TCP latency is irrelevant.

- **Host/join by URL** plus a 5-character lobby code. A "Sunucu Aç" screen (desktop only) and a "Katıl" screen taking `wss://host/lobby/CODE`.
- **Host-authoritative.** The host client owns `GameState`; all true values live there and are never broadcast wholesale. Each peer receives **only its own role's view** — enforce this server-side in the message construction, not by hiding things in the UI. A minister's client must never hold data it isn't entitled to, because anyone can open the debugger.
- Traffic is a few hundred bytes per turn. Send whole role-views as dictionaries; do not build a delta system.
- **Reconnect:** a dropped peer can rejoin with the same lobby code and role for the rest of the turn; if the turn ends first, the AI covers that ministry for that turn and hands it back on reconnect.
- Run headless with `--headless --server` so the host can be a dedicated process instead of a player (§7).

## 4. TURN FLOW IN CO-OP

1. **BAKAN FAZI (simultaneous, 90-second timer, host-adjustable).** Each minister sees their own true numbers and composes a report. The report UI shows two columns: **GERÇEK** (only they can see it) and **RAPOR** (what the Vali will see), with a slider or direct entry per figure, plus one free-text note. Submitting early is allowed. On timeout, the last state is sent.
2. **VALİ FAZI.** The Vali receives five telegrams, builds, governs, appoints, and ends the turn. Ministers watch a limited view and may send telegrams (§5) but cannot act.
3. **ÇÖZÜMLEME.** The simulation ticks. Everyone watches the same animated resolution — this shared moment is where the table finds out something was wrong.
4. **Event cards** resolve; the Vali chooses, ministers may advise.

## 5. COMMUNICATION — the funniest system in the game

- **Resmî Telgraf:** the only public channel. A composer with a set of stock bureaucratic phrases ("durum tatminkârdır", "tedbir alınmıştır", "mesele tetkik edilmektedir", "zât-ı âlinizi meşgul etmeye değmez") plus free text. Every telegram is stamped, archived, and **quoted back verbatim at the accountability session.** Write at least 30 stock phrases.
- **Özel Kanal:** any two ministers may open a private channel. Two people quietly agreeing on a story is the best thing that can happen in this game. The Vali can see that a private channel exists — a small icon — but never its contents.
- **Vali Fermanı:** the Vali can broadcast one public statement per turn. Ministers cannot reply to it publicly, only by telegram next turn.

## 6. THE FINALE — HESAP VERME OTURUMU, CO-OP VERSION

The single-player accountability session already reveals every true value beside what the player was told. In co-op it becomes the whole point:

1. **Sapma tablosu.** Per player, per turn: reported vs true, with a lifetime average. "MALİYE — Ortalama şişirme: %34. En büyük tek yalan: 41. tur, hazine 2200 bildirildi, gerçek 310."
2. **Telgraf alıntıları.** Their own worst telegram is quoted on screen, in their own words, beside the true figure at that moment.
3. **Gizli hedefler açılır.** Everyone learns what everyone else was quietly steering toward all game.
4. **Özel kanallar açılır** — the private channels' contents are published in full. Announce this at the start of the game so people know the record will surface. It will change how they write, and it will not stop them.
5. **The axis trail** full-screen, with each irreversible turn marked by the decree that caused it and **which minister's report the Vali was acting on at the time.**
6. Then the ending card, and a per-player scorecard.

Play the roast for comedy and let the last screen go completely cold.

## 7. WEB EXPORT + HOSTING (get this exactly right — it is where projects die)

**Export:** Godot 4 Web export preset, Compatibility renderer (already the project's renderer). Keep threads enabled.

**Required HTTP headers.** A threaded Godot 4 web build needs `SharedArrayBuffer`, which requires a secure context and two headers. Without them the game boots to a blank page. On **Cloudflare Pages**, commit a `_headers` file at the output root:

```
/*
  Cross-Origin-Opener-Policy: same-origin
  Cross-Origin-Embedder-Policy: require-corp
```

Also emit `_routes.json` if needed so Pages serves `.wasm` and `.pck` untouched, and confirm the `.wasm` MIME type is `application/wasm`.

**The server.** Cloudflare Pages is static and cannot host a WebSocket server, so provide **both** paths and document them in a `DEPLOY.md` you generate:

- **Path A — testing today.** Run the game headless on a local machine (`godot --headless --server --port 8910`) and expose it with `cloudflared tunnel --url ws://localhost:8910`, giving a public `wss://` address. Players open the Cloudflare Pages URL and paste that address. Zero infrastructure code, works immediately, correct choice for playtesting.
- **Path B — persistent.** A **Cloudflare Worker with a Durable Object per lobby**, using the WebSocket Hibernation API. Critically, the Worker is a **dumb relay**: it holds the peer list and forwards framed messages, and knows nothing about game rules. Authority stays on the host client, so the game logic exists in exactly one language. Write the full Worker in TypeScript (~150 lines) plus its `wrangler.toml`. Verify the current Durable Objects free-tier limits before relying on them — Cloudflare has changed this recently.

**Web build caveats to handle in code:** no filesystem beyond `user://` (already used, backed by IndexedDB — flush explicitly), audio context needs a user gesture before the first sound (gate `AudioBus` behind the title-screen click), gamepad and window-resize behave differently, and `OS.get_name() == "Web"` must disable the "Sunucu Aç" button since a browser cannot listen for connections.

## 8. COMEDY LAYER (the tone is now comic — commit to it)

Ministers are comic characters with names, portraits and running gags: the Maliye minister who proposes a tax named after himself every eight turns, the Güvenlik minister who sees Mersa's agents in the bakery, the İmar minister whose every estimate is exactly half the real cost, the Tarım minister who reports harvests in units nobody recognises, the Halk minister who has never once said anyone was unhappy. Write at least 8 recurring bits and let them fire on a schedule so the table starts anticipating them.

Events are absurd on the surface: a prize bull loose in the market, an ambassador who will not leave, a prophet dating the apocalypse to next Tuesday, a shipment of 4000 unwanted hats. **But every mechanical consequence is played dead straight.** Hunger is hunger. The coup is a coup. If a crisis ever resolves itself as a gag, or a punishment isn't real, the drift theme dies and the game becomes a toy. **Funny mouths, honest math.**

## 9. NEW FILES

```
res://scripts/net/NetManager.gd            # WebSocketMultiplayerPeer, lobby codes, reconnect
res://scripts/net/RoleView.gd              # builds the per-role dictionary; the security boundary
res://scripts/net/CoopTurnController.gd    # minister phase, vali phase, timers
res://scripts/sim/ObjectiveManager.gd      # secret objectives, scoring
res://scripts/content/Objectives.gd        # 14+ objectives as data
res://scripts/content/TelegramPhrases.gd   # 30+ stock bureaucratic phrases
res://scenes/net/LobbyScreen.tscn          # host/join, role picking, lobby code
res://scenes/net/MinisterScreen.tscn       # GERÇEK vs RAPOR composer, free-text note
res://scenes/net/TelegramPanel.tscn        # public telegrams + private channels
res://scenes/ui/AccountabilityScreen.tscn  # deviation table, quotes, reveals
res://web/_headers                         # COOP/COEP
res://web/wrangler.toml                    # Cloudflare Worker config
res://web/worker.ts                        # Durable Object relay
res://DEPLOY.md                            # both hosting paths, step by step
```

- Extend `Reporting` so a submitted human report overrides the bias formula for that domain and turn, with the formula as the fallback for AI-held ministries. **One code path, two sources.**
- `RoleView.gd` is a security boundary, not a UI convenience. Test it by asserting a minister peer's received dictionary contains no key outside its domain.

## 10. QUALITY BAR

Typed GDScript, signals not node paths, no warnings. The web build must load from a cold Cloudflare Pages deploy and reach the title screen in under 10 seconds on a normal connection. A 6-player lobby must complete a full 60-turn game without a desync; if any figure can disagree between host and client, it is a bug — the host is the only source of truth. Deliver every file in full, including `DEPLOY.md` and the Worker.

---
