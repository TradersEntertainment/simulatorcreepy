// The HUD: a modern dark heads-up display floating over the 3D city, never a frame around it.
//
// Layout follows reference/src-sehir-3d.html exactly — identity and ledger along the top, the
// ~370 px rail on the right, the build dock and TURU BİTİR along the bottom, and the selection
// card bottom-left. Built entirely in code: no .uxml, no .uss, no prefabs, per CLAUDE.md.
//
// Every number on this screen comes from Reporting. If you find yourself reaching for
// GameState here, stop — that is the one rule the whole information system rests on.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Mesruiyet.Core;
using Mesruiyet.Sim;
using Mesruiyet.World;

namespace Mesruiyet.UI
{
    public sealed class Hud : MonoBehaviour
    {
        public static Hud Instance;
        /// <summary>True when the mouse is over a panel, so world clicks do not fall through it.</summary>
        public static bool PointerOverUi { get; private set; }

        GameState _state;
        UIDocument _doc;
        VisualElement _root;

        // Live pieces the tick refreshes.
        Label _turn, _season, _bufferValue, _bufferPill, _refusal;
        readonly List<VisualElement> _bufferSegments = new List<VisualElement>();
        readonly List<Label> _resValue = new List<Label>();
        readonly List<Label> _resFlow = new List<Label>();
        readonly List<VisualElement> _resWarn = new List<VisualElement>();
        VisualElement _axisRow0, _axisRow1;
        VisualElement _factionList;
        VisualElement _chainList;
        VisualElement _ministerRow, _telegramList, _telegramHead;
        VisualElement _councilHead, _councilBody;
        VisualElement _appointmentCard, _modal, _scrim;
        VisualElement _lawSlotRow;
        Button _decreeButton;
        readonly List<VisualElement> _resCell = new List<VisualElement>();
        VisualElement _selectionCard;
        VisualElement _labelLayer;
        readonly List<VisualElement> _districtLabels = new List<VisualElement>();
        readonly List<Button> _hotbar = new List<Button>();
        Button _endTurn;
        Label _electionNote;

        public void Init(GameState state, UIDocument doc)
        {
            Instance = this;
            _state = state;
            _doc = doc;
            _root = doc.rootVisualElement;

            _root.style.flexGrow = 1;
            _root.pickingMode = PickingMode.Ignore;

            BuildLabelLayer();
            BuildTop();
            BuildRail();
            BuildSelection();
            BuildDock();

            Placement.Instance.Changed += Refresh;
            TurnResolver.TurnCompleted += Refresh;
            // Appointments can arrive from the agent bridge as well as from the card below, so
            // subscribe to the cabinet itself rather than repainting at the click site.
            MinisterManager.Changed += Refresh;
            GovernanceManager.Changed += Refresh;
            Refresh();
        }

        // ================================================================ top strip

        void BuildTop()
        {
            var top = new VisualElement();
            top.style.position = Position.Absolute;
            top.style.top = 20; top.style.left = 24; top.style.right = 24;
            top.style.flexDirection = FlexDirection.Row;
            top.pickingMode = PickingMode.Ignore;
            _root.Add(top);

            // ---- identity
            var ident = UiKit.Glass().Pad(13, 20);
            ident.style.flexDirection = FlexDirection.Row;
            ident.style.alignItems = Align.Center;
            ident.style.marginRight = 14;

            var names = UiKit.Column();
            names.Add(UiKit.Text("MEŞRUİYET", 19, UiKit.Ink, FontStyle.Bold));
            names.Add(UiKit.Text("Delta Şehri · Vali", 11, UiKit.Muted).Margin(top: 2));
            ident.Add(names);

            var rule = new VisualElement();
            rule.style.width = 1;
            rule.style.height = 34;
            rule.style.marginLeft = 16; rule.style.marginRight = 16;
            rule.style.backgroundColor = new Color(1, 1, 1, 0.1f);
            ident.Add(rule);

            var when = UiKit.Column();
            _turn = UiKit.Text("1", 26, UiKit.Ink, FontStyle.Bold);
            _season = UiKit.Text("İLKBAHAR · 1. YIL", 10.5f, UiKit.Muted).Margin(top: 3);
            _season.style.letterSpacing = 1.1f;
            when.Add(_turn); when.Add(_season);
            ident.Add(when);

            top.Add(ident);

            // ---- ledger
            var bar = UiKit.Glass().Pad(9, 8);
            bar.style.flexGrow = 1;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;

            for (int i = 0; i < 6; i++) bar.Add(ResourceChip((Res)i, i < 5));
            top.Add(bar);
        }

        VisualElement ResourceChip(Res r, bool divider)
        {
            Color accent;
            switch (r)
            {
                case Res.Para: accent = UiKit.Amber; break;
                case Res.Yiyecek: accent = UiKit.Green; break;
                case Res.Su: accent = UiKit.Blue; break;
                case Res.Enerji: accent = UiKit.Hex("#C48CFF"); break;
                case Res.Malzeme: accent = UiKit.Hex("#B7C4D4"); break;
                default: accent = UiKit.Red; break;
            }

            var cell = UiKit.Row();
            cell.style.flexGrow = 1;
            cell.style.paddingLeft = 14; cell.style.paddingRight = 14;
            if (divider)
            {
                cell.style.borderRightWidth = 1;
                cell.style.borderRightColor = new Color(1, 1, 1, 0.07f);
            }

            var icon = UiKit.Text(Naming.ResourceGlyphs[(int)r], 14, accent, FontStyle.Bold);
            icon.style.width = 30; icon.style.height = 30;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            icon.style.backgroundColor = UiKit.Alpha(accent, 0.16f);
            icon.style.marginRight = 11;
            icon.Radius(9);
            cell.Add(icon);

            var col = UiKit.Column();
            var caption = UiKit.Caption(Naming.ResourceNames[(int)r]);
            caption.style.fontSize = 9.5f;
            col.Add(caption);

            var valueRow = UiKit.Row();
            var value = UiKit.Text("0", 17, UiKit.Ink, FontStyle.Bold);
            var warn = UiKit.WarnBadge();
            warn.style.display = DisplayStyle.None;
            valueRow.Add(value); valueRow.Add(warn);
            col.Add(valueRow);

            var flow = UiKit.Text("—", 10.5f, UiKit.Muted);
            col.Add(flow);
            cell.Add(col);

            _resValue.Add(value);
            _resFlow.Add(flow);
            _resWarn.Add(warn);
            _resCell.Add(cell);
            return cell;
        }

        // ================================================================ right rail

        void BuildRail()
        {
            // Six cards is taller than 1080 leaves room for, so the rail scrolls. The scroller
            // appears only when it is needed: the safety margin and the cabinet must always be
            // above the fold, because they are the two things the player governs by.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.width = 386;              // 372 of card plus room for the scroller
            scroll.style.position = Position.Absolute;
            scroll.style.top = 110; scroll.style.right = 24; scroll.style.bottom = 130;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _root.Add(scroll);

            var rail = scroll.contentContainer;
            rail.style.width = Length.Percent(100);

            rail.Add(BuildBufferCard());
            rail.Add(BuildMinisterCard());
            rail.Add(BuildChainCard());
            rail.Add(BuildAxisCard());
            rail.Add(BuildCouncilCard());
            rail.Add(BuildFactionCard());
            rail.Add(BuildTelegramCard());
        }

        // ---- the council
        //
        // The counterweight to the cabinet, and the reason the fog is not a blindfold. These
        // lines are read from the truth, not from Reporting — a representative from LİMAN does
        // not clear their complaint with the Tarım ministry first. When the chamber is adjourned
        // the card says so and goes quiet, which is the most expensive silence in the game.

        VisualElement BuildCouncilCard()
        {
            var card = UiKit.Glass().Pad(12, 15).Margin(bottom: 10);
            _councilHead = UiKit.Heading("Meclis", "21 sandalye");
            card.Add(_councilHead);
            _councilBody = UiKit.Column();
            card.Add(_councilBody);
            return card;
        }

        void PaintCouncil()
        {
            _councilBody.Clear();
            var council = _state.Council;

            // Seats as one stacked bar: who is in the room, at a glance.
            var bar = UiKit.Row();
            bar.style.height = 8;
            bar.style.marginBottom = 9;
            bar.Radius(4);
            bar.style.overflow = Overflow.Hidden;
            for (int f = 0; f < 5; f++)
            {
                if (council.SeatsFor[f] == 0) continue;
                var seg = new VisualElement();
                seg.style.flexGrow = council.SeatsFor[f];
                seg.style.height = Length.Percent(100);
                seg.style.backgroundColor = MoodColour(Reporting.FactionMood((Faction)f));
                seg.style.marginRight = 1;
                seg.tooltip = $"{Naming.FactionNames[f]}: {council.SeatsFor[f]} sandalye";
                bar.Add(seg);
            }
            _councilBody.Add(bar);

            foreach (var line in council.Murmurs)
            {
                var row = UiKit.Row();
                row.style.alignItems = Align.FlexStart;
                row.style.marginBottom = 6;
                row.Add(UiKit.Text("“", 13, UiKit.Dim).Margin(right: 5));
                var label = UiKit.Text(line, 11.5f,
                    council.Suspended ? UiKit.Dim : UiKit.Hex("#C9D4E2"));
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.flexGrow = 1;
                label.style.flexShrink = 1;
                row.Add(label);
                _councilBody.Add(row);
            }

            // The button that ends the debate. It states the trade and it is still pressed.
            if (GovernanceManager.Instance.CanSuspend || council.Suspended)
            {
                var suspend = new Button
                {
                    name = "btn_council_suspend",
                    text = council.Suspended ? "MECLİSİ TOPLA" : "MECLİSİ TATİL ET",
                };
                suspend.style.marginTop = 8;
                suspend.style.marginLeft = 0; suspend.style.marginRight = 0; suspend.style.marginBottom = 0;
                suspend.style.paddingTop = 8; suspend.style.paddingBottom = 8;
                suspend.style.fontSize = 10;
                suspend.style.unityFontStyleAndWeight = FontStyle.Bold;
                suspend.style.color = council.Suspended ? UiKit.Bg : UiKit.Red;
                suspend.style.backgroundColor = council.Suspended
                    ? UiKit.Green : UiKit.Alpha(UiKit.Red, 0.16f);
                suspend.Radius(9).Border(1, UiKit.Alpha(UiKit.Red, 0.4f));
                suspend.tooltip = council.Suspended
                    ? "Meclis yeniden toplanır. Kanunlar tekrar oylanır."
                    : "Kanunlar bedelsiz ve anında geçer. Karşılığında mahallelerden gelen " +
                      "şikâyetleri bir daha duymazsınız.";
                suspend.clicked += () =>
                {
                    GovernanceManager.Instance.SetSuspended(!_state.Council.Suspended);
                    Refresh();
                };
                _councilBody.Add(suspend);
            }
        }

        // ---- ministers
        //
        // Five faces and one number apiece: how reliable their reports are. Clicking one opens
        // the appointment card, which is the choice this whole game is about — and the loyalist
        // is the correct short-term answer nearly every time, which is what makes it a trap
        // rather than a puzzle.

        VisualElement BuildMinisterCard()
        {
            var card = UiKit.Glass().Pad(12, 15).Margin(bottom: 10);
            card.Add(UiKit.Heading("Bakanlar", "rapor güvenilirliği"));
            _ministerRow = UiKit.Row();
            card.Add(_ministerRow);
            return card;
        }

        static Color AccentFor(Domain d)
        {
            switch (d)
            {
                case Domain.Maliye: return UiKit.Amber;
                case Domain.Tarim: return UiKit.Green;
                case Domain.Guvenlik: return UiKit.Blue;
                case Domain.Imar: return UiKit.Hex("#C48CFF");
                default: return UiKit.Red;
            }
        }

        void PaintMinisters()
        {
            _ministerRow.Clear();

            for (int i = 0; i < 5; i++)
            {
                var domain = (Domain)i;
                var m = _state.Cabinet.Of(domain);
                if (m == null) continue;

                float bias = Mathf.Abs(Reporting.BiasFor(domain));

                var cell = UiKit.Column();
                cell.style.flexGrow = 1;
                cell.style.flexBasis = 0;
                cell.style.marginRight = i < 4 ? 8 : 0;
                cell.style.alignItems = Align.Center;

                var button = new Button { name = "btn_minister_" + domain.ToString().ToLowerInvariant() };
                button.text = string.Empty;
                button.style.backgroundColor = Color.clear;
                button.style.paddingTop = 0; button.style.paddingBottom = 0;
                button.style.paddingLeft = 0; button.style.paddingRight = 0;
                button.style.marginTop = 0; button.style.marginBottom = 0;
                button.style.marginLeft = 0; button.style.marginRight = 0;
                button.style.width = Length.Percent(100);
                button.style.alignItems = Align.Center;
                button.Border(0, Color.clear);

                var portrait = new Portrait(m.Def.Seed, AccentFor(domain), m.Loyalist, 54);
                button.Add(portrait);

                // The badge is the whole read: green means this ministry's numbers are clean,
                // red means they are not. It never says by how much or in which direction.
                var badge = UiKit.Text(m.Loyalist ? "!" : "✓", 9, UiKit.Bg, FontStyle.Bold);
                badge.style.position = Position.Absolute;
                badge.style.top = -4; badge.style.right = 6;
                badge.style.width = 16; badge.style.height = 16;
                badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                badge.style.backgroundColor = bias > 0.02f ? UiKit.Red : UiKit.Green;
                badge.Radius(8);
                button.Add(badge);

                var captured = domain;
                button.clicked += () => OpenAppointment(captured);
                cell.Add(button);

                var label = UiKit.Text(Ministers.DomainNames[i], 8.5f, UiKit.Muted, FontStyle.Bold);
                label.style.letterSpacing = 0.6f;
                label.style.marginTop = 5;
                cell.Add(label);

                button.tooltip = $"{m.Name} — {Ministers.Covers(domain)}\n" +
                                 (bias > 0.02f
                                     ? $"Raporları %{bias * 100:0} sapmalı."
                                     : "Raporları temiz.") +
                                 $"\n{m.Def.Trait}";
                _ministerRow.Add(cell);
            }
        }

        // ---- telegrams

        VisualElement BuildTelegramCard()
        {
            var card = UiKit.Glass().Pad(12, 15);
            _telegramHead = UiKit.Heading("Telgraflar", "1. tur");
            card.Add(_telegramHead);
            _telegramList = UiKit.Column();
            card.Add(_telegramList);
            return card;
        }

        void PaintTelegrams()
        {
            _telegramList.Clear();

            for (int i = 0; i < _state.Telegrams.Count; i++)
            {
                var parts = _state.Telegrams[i].Split('|');
                if (parts.Length < 3) continue;

                var row = UiKit.Row();
                row.style.alignItems = Align.FlexStart;
                row.style.paddingTop = 8; row.style.paddingBottom = 8;
                if (i > 0)
                {
                    row.style.borderTopWidth = 1;
                    row.style.borderTopColor = new Color(1, 1, 1, 0.07f);
                }

                var who = UiKit.Text(parts[0], 9.5f, UiKit.Muted, FontStyle.Bold);
                who.style.letterSpacing = 0.8f;
                who.style.width = 66;
                who.style.marginTop = 2;
                who.style.flexShrink = 0;
                row.Add(who);

                var msg = UiKit.Text("“" + parts[2] + "”", 11.5f, UiKit.Hex("#C9D4E2"));
                msg.style.whiteSpace = WhiteSpace.Normal;
                msg.style.flexGrow = 1;
                msg.style.flexShrink = 1;
                row.Add(msg);

                _telegramList.Add(row);
            }
        }

        // ---- supply chains
        //
        // The most important panel on the screen this slice. The ledger's Yiyecek line adds up
        // every stage and therefore cannot show a blockage; this is where the player sees that
        // the grain is fine and the bread is not.

        VisualElement BuildChainCard()
        {
            var card = UiKit.Glass().Pad(12, 15).Margin(bottom: 10);
            card.Add(UiKit.Heading("Tedarik Zincirleri", "aşama aşama"));
            _chainList = UiKit.Column();
            card.Add(_chainList);
            return card;
        }

        void PaintChains()
        {
            _chainList.Clear();

            for (int c = 0; c < _state.Chains.Length; c++)
            {
                var chain = _state.Chains[c];
                string diagnosis = Reporting.Diagnosis(chain.Def.Id);
                bool flowing = diagnosis == "akıyor";

                var block = UiKit.Column().Margin(bottom: c == 0 ? 20 : 0);

                var head = UiKit.Row();
                head.style.justifyContent = Justify.SpaceBetween;
                head.style.marginBottom = 7;
                head.Add(UiKit.Text(chain.Def.Name, 11.5f, UiKit.Ink, FontStyle.Bold));
                head.Add(UiKit.Pill(diagnosis, flowing ? UiKit.Green : UiKit.Red));
                block.Add(head);

                var stages = UiKit.Row();
                stages.style.alignItems = Align.Stretch;

                // Under an unreliable minister the panel goes dark rather than half-honest.
                // Showing "Değirmen durdu" beside a report that declines to mention it would
                // hand the player the answer and cost the lie its teeth — and a panel that
                // openly says it was told nothing is a better signal than a half-true one.
                bool reliable = Mathf.Abs(Reporting.BiasFor(chain.Def.Domain)) < 0.0001f;
                var bottleneck = reliable ? chain.Bottleneck() : null;

                for (int i = 0; i < chain.Stages.Length; i++)
                {
                    if (i > 0)
                    {
                        var arrow = UiKit.Text("▸", 11, UiKit.Dim);
                        arrow.style.marginLeft = 4; arrow.style.marginRight = 4;
                        arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
                        stages.Add(arrow);
                    }

                    var stage = chain.Stages[i];
                    bool guilty = stage == bottleneck;

                    var box = UiKit.Column();
                    box.style.flexGrow = 1;
                    box.style.flexBasis = 0;
                    box.style.backgroundColor = guilty
                        ? UiKit.Alpha(UiKit.Red, 0.15f)
                        : new Color(1, 1, 1, 0.04f);
                    box.Radius(8).Border(1, guilty ? UiKit.Alpha(UiKit.Red, 0.45f) : UiKit.Hairline).Pad(7, 8);

                    var name = UiKit.Text(stage.Name.ToUpperInvariant(), 8.5f,
                                          guilty ? UiKit.Red : UiKit.Muted, FontStyle.Bold);
                    name.style.letterSpacing = 0.6f;
                    box.Add(name);

                    var reported = Reporting.StageStock(chain, i);
                    var amount = UiKit.Row();
                    amount.Add(UiKit.Text($"{reported.Value:0}", 15, UiKit.Ink, FontStyle.Bold).Margin(top: 2));
                    if (!reported.Reliable) amount.Add(UiKit.WarnBadge());
                    box.Add(amount);

                    // Throughput is the diagnosis: a stage moving nothing has nobody working it,
                    // a stage moving less than it could is either starved or has nowhere to put
                    // what it makes — and a full buffer is a surplus, not a fault.
                    string rate;
                    Color rateColour;
                    if (!reliable) { rate = "bildirilmedi"; rateColour = UiKit.Dim; }
                    else if (stage.Dead) { rate = "durdu"; rateColour = UiKit.Red; }
                    else if (stage.Full) { rate = "dolu"; rateColour = UiKit.Green; }
                    else { rate = $"{stage.Moved:0}/{stage.Throughput:0} tur"; rateColour = UiKit.Dim; }
                    box.Add(UiKit.Text(rate, 9, rateColour));

                    if (reliable && stage.Idle > 0)
                        box.Add(UiKit.Text($"{stage.Idle} çalışmıyor", 9, UiKit.Amber));

                    box.tooltip = reliable
                        ? $"{stage.Name}: {stage.Def.Holds} · " +
                          $"stok {stage.Stock:0}/{stage.Capacity:0} · " +
                          $"{stage.Working} çalışıyor, {stage.Idle} boş"
                        : $"{stage.Name}: {stage.Def.Holds} · bakanlık ayrıntı bildirmiyor";
                    stages.Add(box);
                }

                block.Add(stages);

                // The sentence that makes the trap legible without giving the answer away.
                var final = chain.Final;
                var reportedTotal = Reporting.Stock(chain.Def.Ledger);
                string note = chain.Def.Id == "yiyecek"
                    ? $"halk yalnızca {final.Def.Holds} yer · defterdeki toplam {reportedTotal.Value:0}"
                    : $"inşaat yalnızca depodan çeker · defterdeki toplam {reportedTotal.Value:0}";
                                var noteLabel = UiKit.Text(note, 9.5f, UiKit.Muted).Margin(top: 9);
                noteLabel.style.whiteSpace = WhiteSpace.Normal;
                block.Add(noteLabel);

                _chainList.Add(block);
            }
        }

        VisualElement BuildBufferCard()
        {
            var card = UiKit.Glass().Pad(12, 15).Margin(bottom: 10);

            var head = UiKit.Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.marginBottom = 12;
            head.Add(UiKit.Caption("Güvenlik Payı"));
            _bufferPill = UiKit.Pill("SAĞLAM", UiKit.Green);
            head.Add(_bufferPill);
            card.Add(head);

            var row = UiKit.Row();
            _bufferValue = UiKit.Text("6", 25, UiKit.Ink, FontStyle.Bold);
            row.Add(_bufferValue);
            var unit = UiKit.Text(" tur", 13, UiKit.Muted).Margin(left: 3, right: 13);
            row.Add(unit);

            var segs = UiKit.Row();
            segs.style.flexGrow = 1;
            for (int i = 0; i < 9; i++)
            {
                var s = new VisualElement();
                s.style.flexGrow = 1;
                s.style.height = 9;
                s.style.marginRight = i < 8 ? 3 : 0;
                s.style.backgroundColor = UiKit.Track;
                s.Radius(3);
                segs.Add(s);
                _bufferSegments.Add(s);
            }
            row.Add(segs);
            card.Add(row);

            var note = UiKit.Text("Şoku kaç tur karşılayabilirsiniz.", 10.5f, UiKit.Muted).Margin(top: 10);
            card.Add(note);
            return card;
        }

        VisualElement BuildAxisCard()
        {
            var card = UiKit.Glass().Pad(12, 15).Margin(bottom: 10);
            card.Add(UiKit.Heading("İdeolojik Konum", "son 15 tur"));
            _axisRow0 = UiKit.Column().Margin(bottom: 15);
            _axisRow1 = UiKit.Column();
            card.Add(_axisRow0);
            card.Add(_axisRow1);
            return card;
        }

        /// <summary>
        /// One axis meter: a track that runs grey in the centre to red at both extremes, the
        /// knob, and the fading 15-turn drift trail behind it. The trail is the only place the
        /// game's subject becomes visible, so it is not decoration.
        /// </summary>
        void PaintAxis(VisualElement host, string left, string right, int value, System.Func<Vector2Int, int> pick)
        {
            host.Clear();

            var head = UiKit.Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.marginBottom = 7;
            head.Add(UiKit.Caption(left, UiKit.Hex("#9AA8BC")));
            head.Add(UiKit.Caption(right, UiKit.Hex("#9AA8BC")));
            host.Add(head);

            var track = new VisualElement();
            track.style.height = 8;
            track.style.marginBottom = 2;
            track.Radius(4);
            track.style.backgroundColor = UiKit.Hex("#4C5666");
            host.Add(track);

            // Extremes tinted in, since UI Toolkit has no gradient backgrounds.
            AddTrackTint(track, 0f, 0.10f, UiKit.Red);
            AddTrackTint(track, 0.10f, 0.22f, UiKit.Hex("#E08A3C"));
            AddTrackTint(track, 0.78f, 0.90f, UiKit.Hex("#E08A3C"));
            AddTrackTint(track, 0.90f, 1f, UiKit.Red);

            var mid = new VisualElement();
            mid.style.position = Position.Absolute;
            mid.style.left = Length.Percent(50);
            mid.style.top = -3; mid.style.bottom = -3;
            mid.style.width = 1;
            mid.style.backgroundColor = new Color(1, 1, 1, 0.28f);
            track.Add(mid);

            var trail = _state.AxisTrail;
            for (int i = 0; i < trail.Count; i++)
            {
                var dot = new VisualElement();
                dot.style.position = Position.Absolute;
                dot.style.width = 4; dot.style.height = 4;
                dot.style.top = 2;
                dot.style.left = Length.Percent(Pct(pick(trail[i])));
                dot.style.marginLeft = -2;
                float fade = trail.Count > 1 ? 0.12f + 0.7f * (i / (float)(trail.Count - 1)) : 0.7f;
                dot.style.backgroundColor = UiKit.Alpha(UiKit.Hex("#C6D2E2"), fade);
                dot.Radius(2);
                track.Add(dot);
            }

            int magnitude = Mathf.Abs(value);
            Color knobColour = magnitude >= 70 ? UiKit.Red : magnitude >= 40 ? UiKit.Amber : UiKit.Hex("#8C97A8");

            var knob = new VisualElement();
            knob.style.position = Position.Absolute;
            knob.style.width = 18; knob.style.height = 18;
            knob.style.top = -5;
            knob.style.left = Length.Percent(Pct(value));
            knob.style.marginLeft = -9;
            knob.style.backgroundColor = Color.white;
            knob.Radius(9).Border(4, knobColour);
            track.Add(knob);

            var foot = UiKit.Row();
            foot.style.justifyContent = Justify.SpaceBetween;
            foot.style.marginTop = 9;
            foot.Add(UiKit.Text(value > 0 ? "+" + value : value.ToString(), 13, UiKit.Ink, FontStyle.Bold));

            Color pillColour = magnitude >= 70 ? UiKit.Red : magnitude >= 40 ? UiKit.Amber : UiKit.Green;
            foot.Add(UiKit.Pill(Naming.Band(value), pillColour));
            host.Add(foot);
        }

        static void AddTrackTint(VisualElement track, float from, float to, Color colour)
        {
            var seg = new VisualElement();
            seg.style.position = Position.Absolute;
            seg.style.left = Length.Percent(from * 100f);
            seg.style.width = Length.Percent((to - from) * 100f);
            seg.style.top = 0; seg.style.bottom = 0;
            seg.style.backgroundColor = colour;
            track.Add(seg);
        }

        /// <summary>−100..100 mapped onto the 0..100 % of the track.</summary>
        static float Pct(int axis) => Mathf.Clamp01((axis + 100f) / 200f) * 100f;

        VisualElement BuildFactionCard()
        {
            var card = UiKit.Glass().Pad(12, 15);
            card.Add(UiKit.Heading("Fraksiyonlar", "yüz yüze · dürüst"));
            _factionList = UiKit.Column();
            card.Add(_factionList);
            return card;
        }

        void PaintFactions()
        {
            _factionList.Clear();

            for (int i = 0; i < 5; i++)
            {
                var f = (Faction)i;
                var mood = Reporting.FactionMood(f);
                bool reliable = Reporting.FactionReliable(f);

                var row = UiKit.Row();
                row.style.paddingTop = 6; row.style.paddingBottom = 6;

                var nameRow = UiKit.Row();
                nameRow.style.flexGrow = 1;
                nameRow.Add(UiKit.Text(Naming.FactionNames[i], 13, UiKit.Ink));
                if (!reliable) nameRow.Add(UiKit.WarnBadge());
                row.Add(nameRow);

                Color moodColour = MoodColour(mood);
                row.Add(UiKit.Bar(Reporting.FactionBar(f), moodColour, 88).Margin(right: 10));

                var label = UiKit.Text(reliable ? Naming.MoodNames[(int)mood] : "BİLDİRİLEN",
                                       10, reliable ? moodColour : UiKit.Muted, FontStyle.Bold);
                label.style.width = 74;
                label.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Add(label);

                _factionList.Add(row);
            }
        }

        static Color MoodColour(Mood m)
        {
            switch (m)
            {
                case Mood.Hosnut: return UiKit.Green;
                case Mood.Temkinli: return UiKit.Amber;
                case Mood.Kaygili: return UiKit.Hex("#E08A3C");
                default: return UiKit.Red;
            }
        }

        // ================================================================ modals
        //
        // One modal at a time, built fresh each time it opens. These panels are rare and small;
        // caching them would buy nothing and cost a stale-state bug the first time a law changed
        // while a panel was closed.

        VisualElement OpenModal(string name, string title, string subtitle, float width)
        {
            CloseModal();

            // A scrim, so the floating district labels do not read through the panel and so a
            // stray click on the map cannot land behind an open decision.
            _scrim = new VisualElement();
            _scrim.style.position = Position.Absolute;
            _scrim.style.left = 0; _scrim.style.top = 0;
            _scrim.style.right = 0; _scrim.style.bottom = 0;
            _scrim.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.72f);
            _root.Add(_scrim);

            var card = UiKit.Glass().Pad(20, 22);
            card.name = name;
            card.style.position = Position.Absolute;
            card.style.left = Length.Percent(50);
            card.style.top = 90;
            card.style.bottom = 130;
            card.style.width = width;
            card.style.marginLeft = -width * 0.5f;

            var head = UiKit.Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.marginBottom = 3;
            head.Add(UiKit.Text(title, 17, UiKit.Ink, FontStyle.Bold));

            var close = new Button { name = name + "_close", text = "✕" };
            close.style.backgroundColor = Color.clear;
            close.style.color = UiKit.Muted;
            close.style.width = 26; close.style.height = 26;
            close.style.marginTop = 0; close.style.marginBottom = 0;
            close.style.marginLeft = 0; close.style.marginRight = 0;
            close.Border(0, Color.clear);
            close.clicked += CloseModal;
            head.Add(close);
            card.Add(head);

            var sub = UiKit.Text(subtitle, 11.5f, UiKit.Muted).Margin(bottom: 14);
            sub.style.whiteSpace = WhiteSpace.Normal;
            card.Add(sub);

            _modal = card;
            _root.Add(card);
            return card;
        }

        void CloseModal()
        {
            _modal?.RemoveFromHierarchy();
            _modal = null;
            _scrim?.RemoveFromHierarchy();
            _scrim = null;
        }

        static ScrollView ModalList()
        {
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.style.flexGrow = 1;
            list.verticalScrollerVisibility = ScrollerVisibility.Auto;
            list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            return list;
        }

        // ---- decrees

        void OpenDecrees()
        {
            var card = OpenModal("panel_decrees", "KARARNAMELER",
                $"Bu tur {_state.DecreesLeft}/{_state.DecreeAllowance} hakkınız kaldı. " +
                "Kararname anında yürürlüğe girer ve meclise uğramaz.", 720);

            var list = ModalList();
            foreach (var d in Decrees.All) list.Add(DecreeRow(d));
            card.Add(list);
        }

        VisualElement DecreeRow(DecreeDef d)
        {
            bool allowed = GovernanceManager.Instance.CanIssue(d, out string refusal);

            var row = UiKit.Row();
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 8;
            row.style.backgroundColor = new Color(1, 1, 1, 0.04f);
            row.Radius(10).Border(1, UiKit.Hairline).Pad(11, 13);
            row.style.opacity = allowed ? 1f : 0.5f;

            var text = UiKit.Column();
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            text.Add(UiKit.Text(d.Name, 13.5f, UiKit.Ink, FontStyle.Bold));

            var blurb = UiKit.Text(d.Blurb, 11.5f, UiKit.Hex("#9AA8BC")).Margin(top: 3);
            blurb.style.whiteSpace = WhiteSpace.Normal;
            text.Add(blurb);

            // Costs and consequences as numbers, never as a verdict. The design forbids
            // labelling any option extreme; it may only be described by what it does.
            var terms = UiKit.Row();
            terms.style.flexWrap = Wrap.Wrap;
            terms.style.marginTop = 7;
            void Term(string s, Color c)
            {
                var chip = UiKit.Text(s, 9.5f, c, FontStyle.Bold);
                chip.style.backgroundColor = UiKit.Alpha(c, 0.16f);
                chip.Radius(5).Pad(3, 7).Margin(right: 5, top: 3);
                terms.Add(chip);
            }
            if (d.CostMoney > 0) Term($"{d.CostMoney} ₺", UiKit.Amber);
            if (d.CostLegitimacy > 0) Term($"−{d.CostLegitimacy} meşruiyet", UiKit.Amber);
            if (d.Order != 0) Term($"otorite {d.Order:+0;−0}", d.Order > 0 ? UiKit.Red : UiKit.Blue);
            if (d.Economy != 0) Term($"ekonomi {d.Economy:+0;−0}", UiKit.Hex("#C48CFF"));
            if (d.Grievance != 0)
                Term($"hoşnutsuzluk {d.Grievance:+0;−0}", d.Grievance > 0 ? UiKit.Red : UiKit.Green);
            AddFactionChips(Term, d.Tuccar, d.Isci, d.Ordu, d.Gelenek, d.Aydin);
            text.Add(terms);
            row.Add(text);

            var issue = new Button { name = "btn_decree_" + d.Id, text = "ÇIKAR" };
            issue.style.marginLeft = 12;
            issue.style.marginTop = 0; issue.style.marginBottom = 0; issue.style.marginRight = 0;
            issue.style.paddingTop = 9; issue.style.paddingBottom = 9;
            issue.style.paddingLeft = 16; issue.style.paddingRight = 16;
            issue.style.fontSize = 11;
            issue.style.unityFontStyleAndWeight = FontStyle.Bold;
            issue.style.flexShrink = 0;
            issue.style.color = UiKit.Bg;
            issue.style.backgroundColor = UiKit.Amber;
            issue.Radius(9).Border(0, Color.clear);
            issue.SetEnabled(allowed);
            issue.tooltip = allowed ? "" : refusal;
            issue.clicked += () =>
            {
                GovernanceManager.Instance.Issue(d, out _);
                OpenDecrees();
                Refresh();
            };
            row.Add(issue);
            return row;
        }

        static void AddFactionChips(System.Action<string, Color> term,
                                    int tuccar, int isci, int ordu, int gelenek, int aydin)
        {
            void One(string name, int v)
            {
                if (v == 0) return;
                term($"{name} {v:+0;−0}", v > 0 ? UiKit.Green : UiKit.Red);
            }
            One("tüccar", tuccar); One("işçi", isci); One("ordu", ordu);
            One("gelenek", gelenek); One("aydın", aydin);
        }

        // ---- the law book

        void OpenLaws()
        {
            var council = _state.Council;
            string subtitle = council.Suspended
                ? $"{_state.LawBook.Count}/{_state.LawSlots} yuva dolu. Meclis tatilde: " +
                  "kanunlar bedelsiz ve anında geçiyor."
                : $"{_state.LawBook.Count}/{_state.LawSlots} yuva dolu. Yeni bir kanun için " +
                  "önce bir kanunu kaldırmanız gerekir.";

            var card = OpenModal("panel_laws", "YASA KİTABI", subtitle, 760);

            var list = ModalList();

            if (_state.LawBook.Count > 0)
            {
                list.Add(UiKit.Caption("Yürürlükte").Margin(bottom: 8));
                foreach (var law in _state.LawBook.ToArray()) list.Add(LawRow(law, true));
                list.Add(UiKit.Caption("Gündemde").Margin(top: 10, bottom: 8));
            }

            foreach (var law in Laws.All)
            {
                if (_state.LawBook.Contains(law)) continue;
                list.Add(LawRow(law, false));
            }
            card.Add(list);
        }

        VisualElement LawRow(LawDef law, bool inForce)
        {
            var council = _state.Council;
            int support = council.SupportFor(law, _state);
            int force = council.ForceCost(law, _state);
            bool allowed = inForce || GovernanceManager.Instance.CanAdopt(law, out _);

            var row = UiKit.Row();
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 8;
            row.style.backgroundColor = inForce
                ? UiKit.Alpha(UiKit.Amber, 0.10f)
                : new Color(1, 1, 1, 0.04f);
            row.Radius(10).Border(1, inForce ? UiKit.Alpha(UiKit.Amber, 0.35f) : UiKit.Hairline).Pad(11, 13);
            row.style.opacity = allowed ? 1f : 0.5f;

            var glyph = UiKit.Text(law.Glyph, 16, inForce ? UiKit.Amber : UiKit.Muted);
            glyph.style.width = 26;
            glyph.style.marginRight = 8;
            glyph.style.flexShrink = 0;
            row.Add(glyph);

            var text = UiKit.Column();
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            text.Add(UiKit.Text(law.Name, 13.5f, UiKit.Ink, FontStyle.Bold));

            var blurb = UiKit.Text(law.Blurb, 11.5f, UiKit.Hex("#9AA8BC")).Margin(top: 3);
            blurb.style.whiteSpace = WhiteSpace.Normal;
            text.Add(blurb);

            var terms = UiKit.Row();
            terms.style.flexWrap = Wrap.Wrap;
            terms.style.marginTop = 7;
            void Term(string s, Color c)
            {
                var chip = UiKit.Text(s, 9.5f, c, FontStyle.Bold);
                chip.style.backgroundColor = UiKit.Alpha(c, 0.16f);
                chip.Radius(5).Pad(3, 7).Margin(right: 5, top: 3);
                terms.Add(chip);
            }
            if (law.Order != 0) Term($"otorite {law.Order:+0;−0}", law.Order > 0 ? UiKit.Red : UiKit.Blue);
            if (law.Economy != 0) Term($"ekonomi {law.Economy:+0;−0}", UiKit.Hex("#C48CFF"));
            if (System.Math.Abs(law.TaxMultiplier - 1f) > 0.001f)
                Term($"vergi ×{law.TaxMultiplier:0.00}", UiKit.Amber);
            if (System.Math.Abs(law.ChainThroughputMultiplier - 1f) > 0.001f)
                Term($"üretim ×{law.ChainThroughputMultiplier:0.00}", UiKit.Green);
            if (System.Math.Abs(law.FarmThroughputMultiplier - 1f) > 0.001f)
                Term($"tarla ×{law.FarmThroughputMultiplier:0.00}", UiKit.Green);
            if (System.Math.Abs(law.FoodDemandMultiplier - 1f) > 0.001f)
                Term($"ekmek talebi ×{law.FoodDemandMultiplier:0.00}", UiKit.Green);
            if (System.Math.Abs(law.BuildCostMultiplier - 1f) > 0.001f)
                Term($"inşaat ×{law.BuildCostMultiplier:0.00}", UiKit.Blue);
            if (System.Math.Abs(law.TransparencyDelta) > 0.001f)
                Term($"şeffaflık {law.TransparencyDelta:+0.00;−0.00}",
                     law.TransparencyDelta > 0 ? UiKit.Green : UiKit.Red);
            if (law.ExtraDecrees != 0) Term($"+{law.ExtraDecrees} kararname", UiKit.Amber);
            AddFactionChips(Term, law.TuccarPerTurn, law.IsciPerTurn, law.OrduPerTurn,
                            law.GelenekPerTurn, law.AydinPerTurn);
            text.Add(terms);

            if (!inForce)
            {
                string vote = council.Suspended
                    ? "Meclis tatilde — oylama yok."
                    : force > 0
                        ? $"Mecliste {support}/{Council.Seats} oy · zorlamak {force} meşruiyet"
                        : $"Mecliste {support}/{Council.Seats} oy · çoğunluk var";
                text.Add(UiKit.Text(vote, 10.5f, force > 0 ? UiKit.Amber : UiKit.Green).Margin(top: 6));
            }
            row.Add(text);

            var act = new Button
            {
                name = (inForce ? "btn_law_repeal_" : "btn_law_adopt_") + law.Id,
                text = inForce ? "KALDIR" : "KABUL ET",
            };
            act.style.marginLeft = 12;
            act.style.marginTop = 0; act.style.marginBottom = 0; act.style.marginRight = 0;
            act.style.paddingTop = 9; act.style.paddingBottom = 9;
            act.style.paddingLeft = 14; act.style.paddingRight = 14;
            act.style.fontSize = 11;
            act.style.unityFontStyleAndWeight = FontStyle.Bold;
            act.style.flexShrink = 0;
            act.style.color = UiKit.Bg;
            act.style.backgroundColor = inForce ? UiKit.Red : UiKit.Green;
            act.Radius(9).Border(0, Color.clear);
            act.SetEnabled(allowed);
            act.clicked += () =>
            {
                if (inForce) GovernanceManager.Instance.Repeal(law, out _);
                else GovernanceManager.Instance.Adopt(law, out _);
                OpenLaws();
                Refresh();
            };
            row.Add(act);
            return row;
        }

        // ---- the election
        //
        // The one channel that cannot lie. The number below comes from true district grievance
        // and no minister touches it, which is why the authoritarian player is quietly pushed to
        // cancel the exact instrument that would have cured their blindness.

        void OpenElection()
        {
            float support = GovernanceManager.Instance.TrueSupport();

            var card = OpenModal("panel_election", $"{_state.Turn}. TUR · SEÇİM",
                "Sandık, bakanlarınızdan geçmeyen tek kanaldır. Buradaki rakam şehrin " +
                "kendisinden gelir.", 700);
            card.style.bottom = StyleKeyword.Auto;

            var gauge = UiKit.Column().Margin(bottom: 16);
            gauge.Add(UiKit.Caption("Sandıktan çıkacak destek"));
            var big = UiKit.Text($"%{support:0}", 40, support >= 50 ? UiKit.Green : UiKit.Red, FontStyle.Bold);
            gauge.Add(big);
            gauge.Add(UiKit.Bar(support / 100f, support >= 50 ? UiKit.Green : UiKit.Red, 640, 8).Margin(top: 6));
            card.Add(gauge);

            var options = UiKit.Row();
            options.style.alignItems = Align.Stretch;
            options.Add(ElectionOption("yap", "YAP", UiKit.Green,
                "Seçim yapılır. Şehir size ne düşündüğünü açıkça söyler — iyi ya da kötü.",
                new[] { "Kazanırsanız +10 meşruiyet ve fraksiyonlarda iyi niyet",
                        "Kaybederseniz −12 meşruiyet ve meclisin şart koştuğu bir tâviz" }));
            options.Add(ElectionOption("ertele", "ERTELE", UiKit.Amber,
                "Seçim ileri bir tarihe bırakılır.",
                new[] { "otorite +12", "−15 meşruiyet", "her fraksiyonun havası bir kademe düşer" }));
            options.Add(ElectionOption("hile", "HİLE YAP", UiKit.Red,
                "Sonuç ilan edilir.",
                new[] { "Bugün bedeli yok", "otorite +9",
                        "Gerçek sonuç kayda geçer; bakan biri varsa birkaç tur içinde çıkar" }));
            card.Add(options);
        }

        VisualElement ElectionOption(string id, string label, Color colour, string blurb, string[] terms)
        {
            var box = UiKit.Column();
            box.style.flexGrow = 1;
            box.style.flexBasis = 0;
            box.style.marginRight = id == "hile" ? 0 : 12;
            box.style.backgroundColor = new Color(1, 1, 1, 0.04f);
            box.Radius(12).Border(1, UiKit.Hairline).Pad(14, 15);

            box.Add(UiKit.Pill(label, colour));
            var text = UiKit.Text(blurb, 11.5f, UiKit.Hex("#C9D4E2")).Margin(top: 10, bottom: 8);
            text.style.whiteSpace = WhiteSpace.Normal;
            box.Add(text);

            foreach (var t in terms)
            {
                var row = UiKit.Row();
                row.style.alignItems = Align.FlexStart;
                row.style.marginBottom = 4;
                row.Add(UiKit.Text("·", 11.5f, colour).Margin(right: 7));
                var line = UiKit.Text(t, 11f, UiKit.Hex("#9AA8BC"));
                line.style.whiteSpace = WhiteSpace.Normal;
                line.style.flexGrow = 1;
                row.Add(line);
                box.Add(row);
            }

            box.Add(UiKit.Spacer());

            var pick = new Button { name = "btn_election_" + id, text = label };
            pick.style.marginTop = 10;
            pick.style.marginLeft = 0; pick.style.marginRight = 0; pick.style.marginBottom = 0;
            pick.style.paddingTop = 10; pick.style.paddingBottom = 10;
            pick.style.fontSize = 11;
            pick.style.unityFontStyleAndWeight = FontStyle.Bold;
            pick.style.color = UiKit.Bg;
            pick.style.backgroundColor = colour;
            pick.Radius(9).Border(0, Color.clear);
            pick.clicked += () =>
            {
                string outcome = GovernanceManager.Instance.ResolveElection(id);
                CloseModal();
                Refresh();
                Debug.Log("[Seçim] " + outcome);
            };
            box.Add(pick);
            return box;
        }

        // ================================================================ appointment
        //
        // The central repeated choice, and it must be a fair trap: SADIK is genuinely the better
        // answer this turn — cheaper decrees, no friction, no complaints — and the cost lands
        // three crises later, when the numbers you governed by turn out to have been someone's
        // idea of good news.

        void OpenAppointment(Domain domain)
        {
            _appointmentCard?.RemoveFromHierarchy();

            var sitting = _state.Cabinet.Of(domain);

            var card = UiKit.Glass().Pad(20, 22);
            card.name = "panel_appointment";
            card.style.position = Position.Absolute;
            card.style.left = Length.Percent(50);
            card.style.top = Length.Percent(50);
            card.style.width = 620;
            card.style.marginLeft = -310;
            card.style.marginTop = -190;

            var head = UiKit.Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.marginBottom = 4;
            head.Add(UiKit.Text(Ministers.DomainNames[(int)domain] + " BAKANLIĞI", 17, UiKit.Ink, FontStyle.Bold));

            var close = new Button { name = "btn_appoint_close", text = "✕" };
            close.style.backgroundColor = Color.clear;
            close.style.color = UiKit.Muted;
            close.style.width = 26; close.style.height = 26;
            close.style.marginTop = 0; close.style.marginBottom = 0;
            close.style.marginLeft = 0; close.style.marginRight = 0;
            close.Border(0, Color.clear);
            close.clicked += CloseAppointment;
            head.Add(close);
            card.Add(head);

            card.Add(UiKit.Text(Ministers.Covers(domain), 11.5f, UiKit.Muted).Margin(bottom: 4));
            card.Add(UiKit.Text($"Görevde: {sitting.Name} · {sitting.Def.Trait}", 11.5f, UiKit.Dim)
                     .Margin(bottom: 16));

            var options = UiKit.Row();
            options.style.alignItems = Align.Stretch;
            options.Add(Candidate(domain, true));
            options.Add(Candidate(domain, false));
            card.Add(options);

            _appointmentCard = card;
            _root.Add(card);
        }

        VisualElement Candidate(Domain domain, bool loyalist)
        {
            var def = Ministers.Candidate(domain, loyalist);
            var sitting = _state.Cabinet.Of(domain);
            bool serving = sitting.Loyalist == loyalist;

            var box = UiKit.Column();
            box.style.flexGrow = 1;
            box.style.flexBasis = 0;
            box.style.marginRight = loyalist ? 14 : 0;
            box.style.backgroundColor = new Color(1, 1, 1, 0.04f);
            box.Radius(12).Border(1, UiKit.Hairline).Pad(14, 15);

            var top = UiKit.Row();
            top.Add(new Portrait(def.Seed, AccentFor(domain), loyalist, 46));

            var names = UiKit.Column().Margin(left: 12);
            names.style.flexGrow = 1;
            names.Add(UiKit.Pill(loyalist ? "SADIK" : "UZMAN", loyalist ? UiKit.Amber : UiKit.Green));
            names.Add(UiKit.Text(def.Name, 14, UiKit.Ink, FontStyle.Bold).Margin(top: 6));
            top.Add(names);
            box.Add(top);

            var trait = UiKit.Text(def.Trait, 11.5f, UiKit.Hex("#9AA8BC")).Margin(top: 10, bottom: 10);
            trait.style.whiteSpace = WhiteSpace.Normal;
            box.Add(trait);

            // State the trade plainly. The design is emphatic that nothing may be labelled
            // extreme or evil — only by what it does. So: what it does.
            string[] terms = loyalist
                ? new[] { "Kararname maliyeti −%25", "Meclise şikâyet etmez",
                          "Kendi alanındaki rakamları lehinize yuvarlar" }
                : new[] { "Kendi alanında verim +%20", "Rakamları olduğu gibi bildirir",
                          "Tavsiyesine rağmen hareket ederseniz meclise gider" };

            foreach (var t in terms)
            {
                var row = UiKit.Row();
                row.style.alignItems = Align.FlexStart;
                row.style.marginBottom = 5;
                row.Add(UiKit.Text("·", 11.5f, loyalist ? UiKit.Amber : UiKit.Green).Margin(right: 7));
                var label = UiKit.Text(t, 11.5f, UiKit.Hex("#C9D4E2"));
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.flexGrow = 1;
                row.Add(label);
                box.Add(row);
            }

            var pick = new Button
            {
                name = $"btn_appoint_{domain.ToString().ToLowerInvariant()}_{(loyalist ? "sadik" : "uzman")}",
                text = serving ? "GÖREVDE" : "ATA",
            };
            pick.style.marginTop = 10;
            pick.style.marginLeft = 0; pick.style.marginRight = 0; pick.style.marginBottom = 0;
            pick.style.paddingTop = 9; pick.style.paddingBottom = 9;
            pick.style.fontSize = 11;
            pick.style.unityFontStyleAndWeight = FontStyle.Bold;
            pick.style.color = serving ? UiKit.Muted : UiKit.Bg;
            pick.style.backgroundColor = serving
                ? new Color(1, 1, 1, 0.06f)
                : (loyalist ? UiKit.Amber : UiKit.Green);
            pick.Radius(9).Border(0, Color.clear);
            pick.SetEnabled(!serving);

            var capturedDomain = domain;
            pick.clicked += () =>
            {
                MinisterManager.Instance.Appoint(capturedDomain, loyalist);
                CloseAppointment();
                Refresh();
            };
            box.Add(pick);
            return box;
        }

        void CloseAppointment()
        {
            _appointmentCard?.RemoveFromHierarchy();
            _appointmentCard = null;
        }

        // ================================================================ selection card

        void BuildSelection()
        {
            _selectionCard = UiKit.Glass().Pad(15, 17);
            _selectionCard.style.position = Position.Absolute;
            _selectionCard.style.left = 24;
            _selectionCard.style.bottom = 128;
            _selectionCard.style.width = 340;
            _root.Add(_selectionCard);
        }

        void PaintSelection()
        {
            _selectionCard.Clear();

            var armed = Placement.Instance.Selected;
            var inspected = Placement.Instance.Inspected;

            BuildingDef def = armed ?? inspected?.Def;
            if (def == null)
            {
                _selectionCard.style.display = DisplayStyle.None;
                return;
            }
            _selectionCard.style.display = DisplayStyle.Flex;

            // Which district's politics apply: where it stands, or where the cursor is hovering.
            DistrictId? district = inspected != null && armed == null
                ? inspected.District
                : Districts.At(Placement.Instance.Hover.x, Placement.Instance.Hover.y)?.Id;

            _selectionCard.Add(UiKit.Text(def.Name, 17, UiKit.Ink, FontStyle.Bold));

            if (district.HasValue)
            {
                var effect = def.PoliticsIn(district.Value);
                string tag = string.IsNullOrEmpty(effect.Tag)
                    ? "SİYASETEN NÖTR"
                    : Districts.Get(district.Value).Name + " · " + effect.Tag;
                Color tagColour = string.IsNullOrEmpty(effect.Tag) ? UiKit.Muted
                    : effect.Grievance > 0 || effect.Order > 0 ? UiKit.Red : UiKit.Green;

                var pill = UiKit.Text(tag, 9.5f, tagColour, FontStyle.Bold);
                pill.style.letterSpacing = 1f;
                pill.style.whiteSpace = WhiteSpace.Normal;
                pill.style.backgroundColor = UiKit.Alpha(tagColour, 0.18f);
                pill.Radius(5).Pad(3, 8).Margin(top: 6, bottom: 8);
                _selectionCard.Add(pill);
            }

            var blurb = UiKit.Text(def.Blurb, 11.5f, UiKit.Hex("#9AA8BC")).Margin(bottom: 8);
            blurb.style.whiteSpace = WhiteSpace.Normal;
            _selectionCard.Add(blurb);

            AddRow("Maliyet", $"{def.CostMoney} ₺ · {def.CostMaterial} malzeme", UiKit.Ink);
            if (def.Upkeep > 0) AddRow("Bakım", $"−{def.Upkeep} ₺ / tur", UiKit.Red);
            if (def.Workers > 0) AddRow("İşgücü", $"{def.Workers} kişi", UiKit.Ink);
            if (def.Housing > 0) AddRow("Barınma", $"+{def.Housing} kişi", UiKit.Green);

            for (int r = 0; r < 6; r++)
            {
                float v = def.Output[r];
                if (Mathf.Abs(v) < 0.01f) continue;
                AddRow(Naming.ResourceNames[r], (v > 0 ? "+" : "") + v.ToString("0.#") + " / tur", UiKit.FlowColour(v));
            }

            if (def.Pollution > 0) AddRow("Kirlilik", "+" + def.Pollution, UiKit.Red);

            if (district.HasValue)
            {
                var e = def.PoliticsIn(district.Value);
                AddFactionRow("Tüccarlar", e.Tuccar);
                AddFactionRow("İşçiler", e.Isci);
                AddFactionRow("Ordu", e.Ordu);
                AddFactionRow("Gelenek", e.Gelenek);
                AddFactionRow("Aydınlar", e.Aydin);
                if (e.Order != 0) AddRow("Otorite ekseni", (e.Order > 0 ? "+" : "") + e.Order, e.Order > 0 ? UiKit.Red : UiKit.Blue);
                if (e.Economy != 0) AddRow("Ekonomi ekseni", (e.Economy > 0 ? "+" : "") + e.Economy, UiKit.Amber);
            }

            if (!string.IsNullOrEmpty(Placement.Instance.LastRefusal))
            {
                _refusal = UiKit.Text(Placement.Instance.LastRefusal, 11.5f, UiKit.Red).Margin(top: 8);
                _refusal.style.whiteSpace = WhiteSpace.Normal;
                _selectionCard.Add(_refusal);
            }

            void AddRow(string k, string v, Color colour)
            {
                var row = UiKit.Row();
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.paddingTop = 5; row.style.paddingBottom = 5;
                row.style.borderTopWidth = 1;
                row.style.borderTopColor = new Color(1, 1, 1, 0.07f);
                row.Add(UiKit.Text(k, 12.5f, UiKit.Hex("#8E9CB0")));
                row.Add(UiKit.Text(v, 12.5f, colour, FontStyle.Bold));
                _selectionCard.Add(row);
            }

            void AddFactionRow(string name, int delta)
            {
                if (delta == 0) return;
                AddRow(name, (delta > 0 ? "+" : "") + delta, delta > 0 ? UiKit.Green : UiKit.Red);
            }
        }

        // ================================================================ bottom dock

        void BuildDock()
        {
            var dock = new VisualElement();
            dock.style.position = Position.Absolute;
            dock.style.left = 0; dock.style.right = 0; dock.style.bottom = 26;
            dock.style.flexDirection = FlexDirection.Row;
            dock.style.alignItems = Align.Center;
            dock.style.justifyContent = Justify.Center;
            // Nineteen build tiles plus the instruments plus TURU BİTİR is wider than 1600 at
            // full size, and a clipped hotbar is worse than a two-row one. Wrapping is the
            // safety net; the sizes below are what keep it on one row at 1920.
            dock.style.flexWrap = Wrap.WrapReverse;
            dock.pickingMode = PickingMode.Ignore;
            _root.Add(dock);

            // ---- build hotbar
            var tools = UiKit.Glass().Pad(9, 8).Margin(right: 8);
            tools.style.flexDirection = FlexDirection.Row;
            tools.style.flexShrink = 0;

            foreach (string id in Buildings.Hotbar)
            {
                var def = Buildings.Get(id);
                if (def == null) continue;

                var tile = new Button { name = "btn_build_" + id };
                tile.text = string.Empty;
                tile.style.width = 51;
                tile.style.paddingTop = 8; tile.style.paddingBottom = 7;
                tile.style.paddingLeft = 0; tile.style.paddingRight = 0;
                tile.style.marginRight = 3; tile.style.marginLeft = 0;
                tile.style.marginTop = 0; tile.style.marginBottom = 0;
                tile.style.backgroundColor = Color.clear;
                // Captions are clipped rather than allowed to spill: "ENERJİ SANTRALİ" is wider
                // than any sane tile, and neighbouring labels running into each other is worse
                // than a truncated one.
                tile.style.overflow = Overflow.Hidden;
                tile.Radius(10).Border(1, Color.clear);
                tile.style.flexDirection = FlexDirection.Column;
                tile.style.alignItems = Align.Center;

                tile.Add(UiKit.Text(def.Glyph, 17, UiKit.Ink));
                var caption = UiKit.Text(def.DockLabel.ToUpperInvariant(), 7.5f, UiKit.Hex("#8E9CB0"), FontStyle.Bold);
                caption.style.letterSpacing = 0.6f;
                caption.style.marginTop = 4;
                caption.style.whiteSpace = WhiteSpace.NoWrap;
                caption.tooltip = def.Name;
                tile.Add(caption);

                var captured = def;
                tile.clicked += () =>
                {
                    if (Placement.Instance.Selected == captured) Placement.Instance.Disarm();
                    else Placement.Instance.Arm(captured);
                };

                tools.Add(tile);
                _hotbar.Add(tile);
            }
            dock.Add(tools);

            // ---- decrees and the law book
            var instruments = UiKit.Glass().Pad(9, 11).Margin(right: 8);
            instruments.style.flexDirection = FlexDirection.Row;
            instruments.style.alignItems = Align.Center;
            instruments.style.flexShrink = 0;

            _decreeButton = new Button { name = "btn_decrees", text = "KARARNAME" };
            _decreeButton.style.marginTop = 0; _decreeButton.style.marginBottom = 0;
            _decreeButton.style.marginLeft = 0; _decreeButton.style.marginRight = 14;
            _decreeButton.style.paddingTop = 10; _decreeButton.style.paddingBottom = 10;
            _decreeButton.style.paddingLeft = 11; _decreeButton.style.paddingRight = 11;
            _decreeButton.style.fontSize = 9.5f;
            _decreeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _decreeButton.style.color = UiKit.Ink;
            _decreeButton.style.backgroundColor = new Color(1, 1, 1, 0.06f);
            _decreeButton.Radius(9).Border(1, UiKit.Hairline);
            _decreeButton.clicked += OpenDecrees;
            instruments.Add(_decreeButton);

            var lawButton = new Button { name = "btn_lawbook" };
            lawButton.text = string.Empty;
            lawButton.style.flexDirection = FlexDirection.Row;
            lawButton.style.alignItems = Align.Center;
            lawButton.style.backgroundColor = Color.clear;
            lawButton.style.marginTop = 0; lawButton.style.marginBottom = 0;
            lawButton.style.marginLeft = 0; lawButton.style.marginRight = 0;
            lawButton.style.paddingTop = 0; lawButton.style.paddingBottom = 0;
            lawButton.style.paddingLeft = 0; lawButton.style.paddingRight = 0;
            lawButton.Border(0, Color.clear);
            lawButton.Add(UiKit.Caption("Yasa").Margin(right: 8));

            _lawSlotRow = UiKit.Row();
            lawButton.Add(_lawSlotRow);
            lawButton.clicked += OpenLaws;
            instruments.Add(lawButton);

            dock.Add(instruments);

            // ---- end turn
            _endTurn = new Button { name = "btn_end_turn" };
            _endTurn.text = string.Empty;
            _endTurn.style.paddingTop = 13; _endTurn.style.paddingBottom = 13;
            _endTurn.style.paddingLeft = 22; _endTurn.style.paddingRight = 22;
            _endTurn.style.marginLeft = 0; _endTurn.style.marginRight = 0;
            _endTurn.style.backgroundColor = UiKit.Amber;
            _endTurn.style.flexShrink = 0;
            _endTurn.Radius(14).Border(0, Color.clear);
            _endTurn.style.flexDirection = FlexDirection.Column;
            _endTurn.style.alignItems = Align.Center;

            var big = UiKit.Text("TURU BİTİR", 14, UiKit.Hex("#1A1206"), FontStyle.Bold);
            big.style.letterSpacing = 1.8f;
            _endTurn.Add(big);
            _electionNote = UiKit.Text("SEÇİM 11 TUR SONRA", 9, new Color(0.10f, 0.07f, 0.02f, 0.62f), FontStyle.Bold);
            _electionNote.style.letterSpacing = 1.2f;
            _electionNote.style.marginTop = 2;
            _endTurn.Add(_electionNote);

            _endTurn.clicked += () => TurnResolver.Instance.BeginTurn();
            dock.Add(_endTurn);
        }

        // ================================================================ world labels

        void BuildLabelLayer()
        {
            _labelLayer = new VisualElement();
            _labelLayer.style.position = Position.Absolute;
            _labelLayer.style.left = 0; _labelLayer.style.top = 0;
            _labelLayer.style.right = 0; _labelLayer.style.bottom = 0;
            _labelLayer.pickingMode = PickingMode.Ignore;
            _root.Add(_labelLayer);

            foreach (var d in _state.Districts)
            {
                var holder = new VisualElement();
                holder.style.position = Position.Absolute;
                holder.pickingMode = PickingMode.Ignore;
                holder.style.alignItems = Align.Center;

                var box = new VisualElement();
                box.style.backgroundColor = (Color)new Color32(11, 14, 19, 210);
                box.Radius(9).Border(1, new Color(1, 1, 1, 0.11f)).Pad(5, 11);
                box.style.alignItems = Align.Center;

                var name = UiKit.Text(d.Name, 11.5f, UiKit.Ink, FontStyle.Bold);
                name.style.letterSpacing = 1f;
                box.Add(name);

                var grievance = UiKit.Text("hoşnutsuzluk 0", 9.5f, UiKit.Muted).Margin(top: 1);
                box.Add(grievance);
                holder.Add(box);

                var stem = new VisualElement();
                stem.style.width = 1;
                stem.style.height = 16;
                stem.style.backgroundColor = new Color(1, 1, 1, 0.22f);
                holder.Add(stem);

                _labelLayer.Add(holder);
                _districtLabels.Add(holder);
            }
        }

        void UpdateLabels()
        {
            var cam = IsoCamera.Instance != null ? IsoCamera.Instance.Cam : Camera.main;
            if (cam == null) return;

            for (int i = 0; i < _districtLabels.Count; i++)
            {
                var d = _state.Districts[i];
                var bounds = d.Def.Bounds;
                Vector3 world = CityGrid.World(
                    bounds.xMin + bounds.width / 2,
                    bounds.yMin + bounds.height / 2,
                    16f);

                Vector2 panel = RuntimePanelUtils.CameraTransformWorldToPanel(_labelLayer.panel, world, cam);
                var holder = _districtLabels[i];

                // Hide rather than let a label sit under the top ledger or the right rail:
                // the panels are translucent, so an overlapping label shows through as noise.
                float w = _root.resolvedStyle.width, h = _root.resolvedStyle.height;
                bool clear = panel.x > 20 && panel.x < w - 400
                          && panel.y > 108 && panel.y < h - 140;
                holder.style.display = clear ? DisplayStyle.Flex : DisplayStyle.None;
                if (!clear) continue;

                holder.style.left = panel.x - holder.resolvedStyle.width * 0.5f;
                holder.style.top = panel.y - holder.resolvedStyle.height;

                var box = holder[0];
                var grievance = (Label)box[1];
                var reported = Reporting.Grievance(d.Id);
                grievance.text = $"hoşnutsuzluk {reported.Value:0}" + (reported.Reliable ? "" : " ?");
                grievance.style.color = UiKit.GrievanceColour(reported.Value);
            }
        }

        // ================================================================ refresh

        void Update()
        {
            UpdateLabels();
            UpdatePointerOverUi();

            // The dock reflects what the hotbar has armed without waiting for a rebuild.
            for (int i = 0; i < _hotbar.Count; i++)
            {
                var def = Buildings.Get(Buildings.Hotbar[i]);
                bool on = Placement.Instance.Selected == def;
                bool affordable = _state.CanAfford(def);
                _hotbar[i].style.backgroundColor = on ? UiKit.Alpha(UiKit.Blue, 0.18f) : Color.clear;
                _hotbar[i].Border(1, on ? UiKit.Alpha(UiKit.Blue, 0.45f) : Color.clear);
                _hotbar[i].style.opacity = affordable ? 1f : 0.42f;
            }

            _endTurn.SetEnabled(TurnResolver.Idle);
        }

        void UpdatePointerOverUi()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || _root.panel == null) { PointerOverUi = false; return; }

            Vector2 screen = mouse.position.ReadValue();
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var hit = _root.panel.Pick(panelPos);
            PointerOverUi = hit != null && hit != _root && hit != _labelLayer;
        }

        /// <summary>Repaint everything that only changes on a tick or a build.</summary>
        public void Refresh()
        {
            _turn.text = _state.Turn.ToString();
            _season.text = $"{_state.SeasonName} · {_state.Year}. YIL";

            for (int r = 0; r < 6; r++)
            {
                if (r == (int)Res.Isgucu)
                {
                    var used = Reporting.Labour();
                    var pool = Reporting.LabourPool();
                    _resValue[r].text = $"{used.Value:0}/{pool.Value:0}";
                    float idle = pool.Value - used.Value;
                    _resFlow[r].text = idle > 0.5f ? $"boşta {idle:0}" : "tam istihdam";
                    _resFlow[r].style.color = idle > 0.5f ? UiKit.Amber : UiKit.Green;
                    _resWarn[r].style.display = used.Reliable ? DisplayStyle.None : DisplayStyle.Flex;
                    continue;
                }

                var stock = Reporting.Stock((Res)r);
                var flow = Reporting.Flow((Res)r);

                _resValue[r].text = stock.Reliable
                    ? stock.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                    : $"~{stock.Value - stock.Spread:0}–{stock.Value + stock.Spread:0}";
                _resFlow[r].text = UiKit.Signed(flow.Value);
                _resFlow[r].style.color = UiKit.FlowColour(flow.Value);
                _resWarn[r].style.display = stock.Reliable ? DisplayStyle.None : DisplayStyle.Flex;
            }

            var buffer = Reporting.Buffer();
            _bufferValue.text = buffer.Value.ToString("0");
            for (int i = 0; i < _bufferSegments.Count; i++)
            {
                bool on = i < Mathf.RoundToInt(buffer.Value);
                Color c = buffer.Value <= 2 ? UiKit.Red : buffer.Value <= 4 ? UiKit.Amber : UiKit.Green;
                _bufferSegments[i].style.backgroundColor = on ? c : UiKit.Track;
            }
            RepaintPill(_bufferPill,
                buffer.Value <= 2 ? "KRİTİK" : buffer.Value <= 4 ? "İNCE" : "SAĞLAM",
                buffer.Value <= 2 ? UiKit.Red : buffer.Value <= 4 ? UiKit.Amber : UiKit.Green);

            // Every ledger figure explains itself on hover. §14 of the design: a player who
            // cannot see why a number moved cannot learn the game.
            for (int r = 0; r < 6; r++)
            {
                var terms = Reporting.Explain((Res)r);
                _resCell[r].tooltip = terms.Count == 0
                    ? Naming.ResourceNames[r]
                    : Naming.ResourceNames[r] + "\n· " + string.Join("\n· ", terms);
            }

            PaintChains();
            PaintMinisters();
            PaintCouncil();
            PaintLawSlots();
            PaintTelegrams();

            ((Label)_councilHead[1]).text = _state.Council.Suspended
                ? "TATİLDE" : $"{Council.Seats} SANDALYE";

            _decreeButton.text = $"KARARNAME {_state.DecreesLeft}/{_state.DecreeAllowance}";
            _decreeButton.style.color = _state.DecreesLeft > 0 ? UiKit.Ink : UiKit.Dim;

            // An election is not a notification you can dismiss. It opens, and it waits — and
            // it closes itself the moment it has been answered, however it was answered.
            bool electionOpen = _modal != null && _modal.name == "panel_election";
            if (_state.ElectionPending && !electionOpen) OpenElection();
            else if (!_state.ElectionPending && electionOpen) CloseModal();
            ((Label)_telegramHead[1]).text = $"{_state.Turn}. TUR";

            PaintAxis(_axisRow0, "Otorite", "Özgürlük", Reporting.AxisOrder, v => v.x);
            PaintAxis(_axisRow1, "Sermaye", "Eşitlik", Reporting.AxisEconomy, v => v.y);
            PaintFactions();
            PaintSelection();

            int toElection = _state.TurnsToElection;
            _electionNote.text = toElection == 0 ? "SEÇİM BU TUR" : $"SEÇİM {toElection} TUR SONRA";
        }

        /// <summary>
        /// The law book as seven squares. Filled slots carry the law's glyph; the three beyond
        /// the current allowance are dimmed, so the shape of the constraint is visible without
        /// opening anything. A city that could hold every good law would not have to choose.
        /// </summary>
        void PaintLawSlots()
        {
            _lawSlotRow.Clear();

            for (int i = 0; i < Laws.MaxSlots; i++)
            {
                bool unlocked = i < _state.LawSlots;
                var law = i < _state.LawBook.Count ? _state.LawBook[i] : null;

                var slot = UiKit.Text(law != null ? law.Glyph : (unlocked ? "+" : "·"),
                                      law != null ? 15 : 12,
                                      law != null ? UiKit.Amber : UiKit.Dim);
                slot.style.width = 28; slot.style.height = 28;
                slot.style.marginRight = 4;
                slot.style.unityTextAlign = TextAnchor.MiddleCenter;
                slot.style.backgroundColor = law != null
                    ? UiKit.Alpha(UiKit.Amber, 0.14f)
                    : new Color(1, 1, 1, 0.05f);
                slot.Radius(9).Border(1, law != null ? UiKit.Alpha(UiKit.Amber, 0.42f) : UiKit.Hairline);
                if (!unlocked) slot.style.opacity = 0.3f;
                slot.tooltip = law != null ? $"{law.Name}\n{law.Blurb}" : "Boş yasa yuvası";
                _lawSlotRow.Add(slot);
            }
        }

        static void RepaintPill(Label pill, string text, Color colour)
        {
            pill.text = text;
            pill.style.color = colour;
            pill.style.backgroundColor = UiKit.Alpha(colour, 0.17f);
        }
    }
}






