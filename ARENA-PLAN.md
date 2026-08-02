# MEŞRUİYET: ARENA — design plan

A 6-player, 15-minute, comedic city-war mode sharing the MEŞRUİYET codebase (Unity 6, C#,
Compatibility renderer, isometric orthographic camera, dark HUD). This is a **separate mode**,
not the campaign: the campaign is about slow invisible ideological drift, arena is about fast
visible violence. They share the city layer and nothing else.

Produced from 12 parallel design studies, each attacked by an adversarial reviewer. Where the
reviewers were right their fixes are folded in; where they were wrong it is said so.

---

## 0. THE CENTRAL FIX — read this before anything else

Eleven of twelve reviewers independently found the same fatal flaw, under seven different names.
It is not a balance issue, it is a sign error:

> The win condition is population. Attacking **costs** population — mobilised soldiers die, and
> the bombing makes your own citizens flee. Winning a battle **grants** no population; the
> victim's refugees route to whoever looks most attractive, which is never the city that just
> burned 10% of itself. Therefore the expected value of every attack is negative, for every
> player, in every round. The dominant strategy is: never open the war menu, build housing,
> absorb everyone else's refugees, win on arithmetic.

And because it is dominant *for all six players simultaneously*, the equilibrium is a match with
zero battles — in a mode whose entire premise is a 25-second battle everyone watches. The comedy
engine, which fires when armies are raised, would never run once.

**The fix has three parts and all three are required.**

**1 · SÜRGÜN — conquest transfers people, it does not delete them.** On a won battle the attacker
seizes `35%` of the defender's displaced population as forced resettlement, marched home as a
visible column. Capped by the attacker's free housing: `alınan = min(0.35 × yerindenEdilen,
boşKonut)`. Population taken this way arrives as `SÜRGÜN` and converts to settled population only
if morale ≥ 45 at round end — otherwise it leaks away next round. War now pays in the scoring
currency, and it pays *only* if you built somewhere to put them. Fighting and building stop being
alternatives and become a combination.

**2 · Safety means strength, not niceness.** Refugee routing weights `Güvenlik` heavily, and
`Güvenlik` is computed from standing army, intact walls and **battles won**, not from parks. A
plot that attacked and won reads *safer* than a plot that sat still. A plot that was attacked and
lost reads dangerous. Being militarily strong is therefore an attractiveness investment, which
removes the pacifist's monopoly on refugees.

**3 · The turtle bleeds.** `HUZUR GÖÇÜ`: every city sheds `4%` of population per round into a
continental drift pool that is redistributed by attractiveness regardless of any battle. Doing
nothing is not neutral, it is slow decay. Combined with a hard housing cap, hoarding has a
ceiling and passivity has a cost.

**Two further guards, from the reviewers:**
- The crown-bearer (current #1 by population) gets **half** of every peace/attractiveness bonus
  and pays `+10%` per-category building cost (`BÜROKRASİ`). The calm small city is rewarded; the
  calm giant is not.
- Every building type is **unique per city** (`Benzersiz` flag). Half the discovered exploits
  were "build thirteen of the best building".

---

## 1. THE MATCH, MINUTE BY MINUTE

Five rounds. Each round is four phases on one clock:

| Phase | R1 | R2–R5 | What happens |
|---|---|---|---|
| **İNŞA** | 90 s | 100 s | Build, zone, repair. Money accrues only during this phase. |
| **KARAR** | 45 s | 45 s | Choose posture and target. First 30 s secret, last 15 s cards face-up. |
| **SAVAŞ** | 25 s | 25 s | All battles resolve simultaneously; the director cuts between them. |
| **RAPOR** | 12 s | 12 s | Population board with the flow decomposed. Overlaid on the next build phase. |

`172 + 4 × 182 = 900 s = 15:00` of match, plus ~60 s lobby and ~40 s pre-match flyover. Call it
**17 minutes door to door** and do not pretend otherwise.

**Escalation rails, announced as a full-screen banner each round.** R1 `TANIŞMA`: probes only,
buildings cannot be destroyed, no SÜRGÜN — you learn the map and everyone's opening. R2–R3
`SINIRLAR`: full attacks, buildings destructible, SÜRGÜN active. R4 `KIRILMA`: siege unlocked,
two attackers may hit the same plot. R5 `BÜYÜK GÖÇ`: all displacement pools ×1.8, so one final
battle can move a third of a city, and the crown changes hands in the last ninety seconds.

**Asymmetric starts.** Six starting kits dealt from a fixed rotation — fishing town (LİMAN,
Kirlilik 60), university town (ÜNİVERSİTE, Eğitim 70, Ekonomi 15), factory town (FABRİKA,
Teknoloji 55, Moral 35), garrison town (KIŞLA, Ordu high, Nüfus low), farm town (huge Nüfus, no
tech), merchant town (₺ high, nothing built). This kills the solved opening, and more importantly
it makes the comedy engine produce a different set of jokes in match two than in match one.

---

## 2. THE COMEDY ENGINE — the centrepiece

The one dimension the reviewer passed with only fixes. Comedy is **derived from state, never
written**. A deterministic generator reads the city profile and emits an army.

**Profile.** Seven shorts — `Eğitim, Ekonomi, Teknoloji, Moral, Kirlilik, Din` (0–100) and `Nüfus`
— plus a 16-bit building mask.

**Tech tier is a hard gate, not a curve.** `T0 İLKEL · T1 ZANAAT · T2 SANAYİ · T3 MAKİNE ·
T4 İLERİ`, each requiring both a building chain and an education-seats-per-capita threshold.
`TeknolojiKademesi = clamp(min(BinaKademesi, EğitimKademesi) − YıkımŞoku, 0, 4)`.

**Four slots, always four cards.** `ÖNCÜ / HAT / DESTEK / ÖZEL`. Each archetype declares hard
gates (all must pass) and soft scoring clauses; the highest scorer per role takes the slot. At
least 30 archetypes. Examples with their gates:

| Unit | Gate | Feel |
|---|---|---|
| `TAŞ ATAN GENÇLER` | Eğitim<35 & Nüfus≥800 | the founding joke |
| `MANCINIK MÜFREZESİ` | ÜNİVERSİTE yok & T≤1 | İsmail's catapult |
| `KREDİ KARTLI SÜVARİ` | Ekonomi 45–75 & Borç>0 | cavalry on instalments |
| `FANATİK TARAFTAR GRUBU` | Moral≥75 | football fans, high morale, no gear |
| `ADAK KOÇU SÜRÜSÜ` | Din≥65 & Ekonomi<45 | livestock |
| `İHA FİLOSU (PİLOTSUZ)` | T4 & Nüfus<600 | drones, nobody to fly them |
| `PARALI ASKERLER` | Ekonomi≥80 & Ordu<30 | may change sides mid-battle |
| `EMEKLİ TAARRUZ TUGAYI` | Nüfus≥2500 & Eğitim<55 | pensioners |
| `HAYVANAT BAHÇESİ FİRARİLERİ` | HAYVANAT BAHÇESİ destroyed | escaped animals |
| `TEK KİŞİLİK ORDU` | Nüfus<400 | one guy |

**The flavour line is assembled, never stored.** A shared cause table indexed by `(stat, band)`
yields clause fragments; the dominant cause across the four slots builds the line:
`{Oyuncu}'in {sebep} için savaşa {sonuç} ile katılacak.` Turkish suffixing is code, not a lookup —
implement `TurkishGrammar.Genitive()` and `Clitic()` with real vowel harmony. Getting "İsmail'in"
right and "Ayşe'nin" right is the difference between charming and broken.

**Per-card cause arrow — the ten-second rule.** Every unit card is three rows: a coloured cause
chip, the unit name and count, the honest power number.
`[ÜNİVERSİTE YOK] → MANCINIK MÜFREZESİ ×2 · Güç 48`. A viewer with no sound understands the loss.

**Numbers are honest.** `Güç = Adet × (Atk+Def)/2 × MoralÇarpanı × TeknolojiÇarpanı ×
DuruşÇarpanı`. The names are jokes; the arithmetic never is.

**Roster diff.** Any slot that changed since last round renders the old unit ghosted at 35% with
a red arrow and the cause chip that flipped. This is how "he lost his university" becomes visible
in one frame.

---

## 3. COMBAT

**Simulate, then play back.** The server runs the battle to completion and emits a `BattleScript`
of keyframes; the client is a replay player. This makes slow-motion, replays, spectator sync and
determinism free, and makes cheating impossible.

**Three lanes, fixed geometry**, drawn from the real border between the two plots. Attacker walks;
**defender holds and owns the range** — walls sit in *front* of the garrison so the attacker must
cross fire to reach them. Defender advantage is four things: position, `+10` morale, the wall's
breach timer, and the terrain multiplier of that specific edge.

**Counter triangle:** `KALABALIK` swarms `MENZİL` (×1.75), `AĞIR` cleaves `KALABALIK` (×1.75),
`MENZİL` shreds `AĞIR` (×1.75). Tech tier multiplies unit power `{1.0, 1.5, 2.4, 3.8, 6.0}` and
buys free shooting seconds through range advantage.

**Sublinear army.** `OrduGücü = 12 × pow(Asker, 0.70) × TechMult × MoralMult × DuruşMult`. The
exponent is the master anti-snowball dial (range 0.55–0.85). A city with 3× the population does
not get 3× the army.

**Postures.** Attacker picks `YÜKLEN` (+20% damage, +15% speed, −15% defence at home), `TUT`
(baseline), `KUŞAT` (25% of the army, targets a building, minimal casualties). Defender picks
`SAVUNMA` (+35% defence, names one protected building) or `AÇIK KAPI` (no defence, +attractiveness
— the real pacifist option, now costed properly).

**SAPMA — missing is funnier than hitting.** Scatter chance by attacker tier `{0.40, 0.25, 0.12,
0.05, 0.02}`. A T0 army aiming at the university hits the zoo four times out of ten, and the zoo
animals become a unit archetype next round.

**HÂDİSE — conditional, never a die roll.** A table of 25+ incidents with preconditions read from
city state and fixed trigger ticks. "Mancınık geri tepti" fires when a T1 ranged unit has
Eğitim<30. Same state, same joke, every time — which means players learn to *cause* them.

**One attack declaration per player per round.** This keeps the 45-second decision phase readable
and stops dogpile spam.

---

## 4. BUILDINGS, TARGETING, DESTRUCTION

Six `ANIT SLOTU` per plot around the central plaza, filled from nine candidates. Four damage
states from one bar: `SAĞLAM → HASARLI` (half effect) `→ YIKIK` (zero) `→ KÜL` (needs rubble
clearing). Tech tier recomputes at the *start of the next battle*, so a destroyed university
means next round's army is visibly worse — the cause-and-effect the stream needs.

**Repair is cheaper than rebuild:** HASARLI→SAĞLAM 20% of cost; YIKIK→SAĞLAM 60%. Rubble keeps the
slot, so destruction is tempo, not elimination.

**Sivil hedef has a reputation price.** Buildings are `MİLİTER` or `SİVİL`. Hitting civilian
targets is efficient and costs `Meşruiyet`, which gates diplomacy. Efficient cruelty is available
and it costs something — the campaign's theme, compressed into one decision.

**Military buildings house people.** The reviewer's sharpest note: nobody built the university
because six slots with five obviously-better options is not a decision. Give `KIŞLA` and `SURLAR`
population capacity so the military build is also a housing build, and the slot choice becomes
genuinely hard.

---

## 5. POPULATION AND MIGRATION

Three pools per plot: `YERLEŞİK` (settled, produces, grows), `YENİ GELEN` (arrived this round,
produces nothing, may leave), `SÜRGÜN` (forcibly resettled, converts only at morale ≥ 45).

**Push.** `displacementRatio = clamp(0.030 + 0.55×hasarPayı + 0.30×(1 − Moral/100), 0, 0.45)`.

**Pull.** `Çekim = 0.28×Güvenlik + 0.20×KonutBoşluk + 0.16×Refah + 0.14×Eğitim + 0.10×Sağlık +
0.08×Kültür − 0.30×Kirlilik`, where **Güvenlik counts army, walls and battles won.**

**Routing.** Weighted softmax over the road graph: `w(d) = Çekim(d)^1.6 × mesafeAzalması ×
akrabalık`, with hop decay `0.55^(hops−1)`. Nobody flees toward the city that just bombed them
(`akrabalık = 0.35`). The **outside world** is always a candidate with fixed low attractiveness —
so total war is lose-lose for everyone and the continent's population actually shrinks. The
scoreboard showing 60.000 people becoming 41.000 is the mode's moral, delivered as a number.

**Absorption is easy, keeping is hard.** Arrivals land as `YENİ GELEN` and settle only if the city
is under its housing cap AND morale ≥ 50 at round end. Otherwise they move on next round, and the
crowd standing outside your border is rendered as actual low-poly figures.

---

## 6. MAP AND BORDERS

One continent, **KADRA HAVZASI**: six plots with hard visible borders, nine edges, plus a neutral
ruined centre worth fighting over. Fixed topology for competitive integrity; **randomised per
match**: which three edges are bridges, where the ruins' bonuses sit, and which two plots are
coastal.

`Açıklık(p)` = sum of the plot's edge widths — the one number that governs exposure. A corner plot
is safe and poor; a central plot is rich and exposed. The map deliberately inverts intuition: the
safe player must attack to win, the exposed player can win by surviving.

Chokepoints are literal: `assaultWidth = max(1, baseWidth − 2 × surSeviyesi)`, and surplus
soldiers who cannot fit through the gorge stand behind it doing nothing — visible, and funny.

Bridges change the **price** of an attack, never its possibility. Burning a bridge forces a
`KUŞATMA YÜRÜYÜŞÜ` at 60% effectiveness; it does not make you unreachable. The reviewers were
right that a cancellable attack creates an island exploit.

---

## 7. DIPLOMACY

Deliberately thin — this is a 15-minute match, not a negotiation game.

- **One pact per player per round**, offered only in the secret half of the decision phase,
  lasting two battles, worth **zero** attractiveness (immunity is already the reward).
- **Meşruiyet 0–100** is the only diplomatic currency, public as a column of light over the city
  hall. Betrayal costs −30 and grants a one-time +45% first-strike for eight seconds. Betrayal is
  legal, profitable once, and career-ending — exactly as it should be.
- **Haraç** — tribute is paid in citizens, and 30% visibly disappears on the road ("yol vergisi").
  This is how a frightened leader buys a round, and why it is not free.
- **Taç** — a physical crown over the current #1, recomputed live. Half peace bonuses, +10%
  building costs, and everyone can see who to shoot.

---

## 8. SMALL LOBBIES AND AI SEATS

**The map is always six cities.** With two humans the refugee triangle collapses if there are only
two destinations, so empty seats are filled by bots, never by shrinking the map. 1–6 humans, all
configurations valid, including zero (a watchable demo match).

**Bots are characters, not filler** — and this is nearly free, because the comedy engine already
derives everything from stats. Give a bot an extreme profile and it behaves absurdly by itself.

| Bot | Profile | Role in the match |
|---|---|---|
| `PROFESÖR KENT` | all education, no army | rich, defenceless, T4 drones with no pilots |
| `TÜCCAR KENT` | all economy | mercenaries who defect for a better offer |
| `ORDU KENT` | all military, starving | must raid every round or starve |
| `ÇİFTÇİ KENT` | huge population, no defence | the prize; whoever hits it first gets hit next |
| `KAOS KENT` | randomised each round | unpredictable, always funny |
| `BARIŞ KENT` | never attacks, maximally attractive | **if all the humans brawl, it wins** |

`BARIŞ KENT` is the teaching bot: losing your first match to a city that never fired a shot
explains the mode better than any tutorial.

Bots never cheat — same public information, same rules. Three difficulty tiers. A disconnected
human is taken over by a profile-matched bot so the match never stalls, and gets the seat back on
reconnect.

**Small-lobby options:** a 3-round (~9 minute) variant for 2–3 players, and 2v2 / 3v3 teams with
adjacent borders and pooled population.

---

## 9. NETCODE

`Mesruiyet.Arena.Core` is a `netstandard2.1` assembly with **zero UnityEngine references** and one
mutator: `ArenaSim.Step(ref MatchState, in OrderBatch)`. No `float`, no `System.Random`, no
`DateTime`, no `Dictionary` iteration — fixed-point and channelled RNG only, so the server, the
Unity client, the bot and the replay all agree bit for bit.

**Authority:** a Cloudflare Durable Object per lobby, running the same core in WASM. Orders are
validated server-side; only the resolved state is broadcast. All city state is public — the only
secret in the game is **intent**, withheld during the first 30 s of the decision phase and then
revealed. This matches the campaign's co-op plan, so the relay work is shared between modes.

Reconnect by HMAC token with battle seek. Client hash checks each round as a divergence detector.

---

## 10. THE BROADCAST LAYER

Four elements never leave the screen: the **population bar** (the win condition as one stacked
bar), the round/phase clock, the six seat cards, and the ticker.

**Director:** rescoring at 10 Hz, minimum shot 4 s, six hard-cut events that bypass scoring
(building destroyed, war declared, leader changed, rout, refugee column crossing a border, crown
moved). Because combat is a replay, **the director knows the future** and can pre-frame the good
moment instead of chasing it.

**Ticker is a card queue, not a marquee** — scrolling text is unreadable. One card at a time, hold
`2.2 s + 0.045 s × characters`.

**TUR TABLOSU**, the signature screen: an 8-second Sankey of where everyone's people went,
overlaid on the first 20 s of the next build phase rather than pausing the match.

**Twitch chat: out of v1.** The reviewer was right that a second monitor with perfect information
solves the game, and chat influence is a balance liability before the core is proven. Revisit
after the mode is fun without it.

---

## 11. BUILD ORDER — verifiable slices

Each slice ends green through the agent loop in `unity/CLAUDE.md`: build → run → play → screenshot
→ read logs. Nothing is "done" because it looks done.

1. **Match skeleton.** Six seats, five rounds, four phases, a clock, population as score, no war.
   Verify: `--autoplay` runs a full match to round 5 headless and prints a scoreboard.
2. **Economy and building.** Build queue, housing cap, unique buildings, income, `HUZUR GÖÇÜ`.
   Verify: a bot-only match produces six different skylines and nobody hits an infinite loop.
3. **Comedy engine, headless.** No graphics at all — a console command that takes a stat profile
   and prints the army and the line. **This is the riskiest system and it can be tested first.**
4. **Combat sim + SÜRGÜN + migration.** Numbers only, no visuals. Verify: over 200 autoplay
   matches, attacking is not dominated and turtling does not win more than ~1 in 6.
5. **Battle playback.** The 25-second visual, three lanes, the three bars.
6. **Map, borders, chokepoints, bridges.**
7. **Bots and small lobbies.**
8. **Netcode: Durable Object, six real seats.**
9. **Broadcast layer.**

---

## 12. THE RISKIEST ASSUMPTION, AND THE CHEAPEST TEST

The whole mode rests on one bet: **that a generated joke is still funny the fortieth time you see
it.** Every reviewer flagged repetition as the failure mode — the catapult line is hilarious in
match one and wallpaper by match four.

The test costs almost nothing and needs no game: implement the profile → army → line generator as
a **console program**, feed it 200 randomised city profiles, print the results, and read them.
If fewer than half raise a smile and the rest feel like the same sentence with different nouns,
the mode does not work yet and no amount of Unity will fix it. Do this before slice 4.

---

## 13. OPEN QUESTIONS — designer decisions, not implementation details

1. **Does SÜRGÜN population resist?** Plain population is simple. Prisoners who riot if morale
   drops give counterplay and comedy, and punish conquest without infrastructure. Recommend:
   they resist.
2. **Is betrayal allowed?** It is the best comedy and the worst feel-bad. Recommend: allowed,
   heavily telegraphed, career-ending.
3. **3-round short match for 2–3 players — ship in v1 or later?** Recommend: v1, it is a timer
   change and small lobbies are the common case.
4. **Does the campaign's lying-minister layer appear in arena at all?** Recommend: no. Arena's
   only secret is intent. Two information systems in a 15-minute match is one too many.
5. **Named-player comedy** — the engine writes "İsmail'in üniversitesi olmadığı için…" using real
   usernames. Funny with friends, a harassment vector with strangers. Recommend: on in private
   lobbies, off in public matchmaking.
