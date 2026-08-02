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
        VisualElement _appointmentCard;
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
            rail.Add(BuildFactionCard());
            rail.Add(BuildTelegramCard());
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
            dock.pickingMode = PickingMode.Ignore;
            _root.Add(dock);

            // ---- build hotbar
            var tools = UiKit.Glass().Pad(9, 10).Margin(right: 10);
            tools.style.flexDirection = FlexDirection.Row;
            tools.style.flexShrink = 0;

            foreach (string id in Buildings.Hotbar)
            {
                var def = Buildings.Get(id);
                if (def == null) continue;

                var tile = new Button { name = "btn_build_" + id };
                tile.text = string.Empty;
                tile.style.width = 62;
                tile.style.paddingTop = 8; tile.style.paddingBottom = 7;
                tile.style.paddingLeft = 0; tile.style.paddingRight = 0;
                tile.style.marginRight = 5; tile.style.marginLeft = 0;
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
                var caption = UiKit.Text(def.DockLabel.ToUpperInvariant(), 8.5f, UiKit.Hex("#8E9CB0"), FontStyle.Bold);
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

            // ---- law slots (4 of 7 unlocked; the book itself lands in a later slice)
            var laws = UiKit.Glass().Pad(9, 12).Margin(right: 10);
            laws.style.flexDirection = FlexDirection.Row;
            laws.style.alignItems = Align.Center;
            laws.style.flexShrink = 0;
            laws.Add(UiKit.Caption("Yasa").Margin(right: 8));
            for (int i = 0; i < 7; i++)
            {
                var slot = new VisualElement();
                slot.style.width = 34; slot.style.height = 34;
                slot.style.marginRight = 6;
                slot.style.backgroundColor = new Color(1, 1, 1, 0.05f);
                slot.Radius(9).Border(1, UiKit.Hairline);
                if (i >= 4) slot.style.opacity = 0.35f;
                laws.Add(slot);
            }
            dock.Add(laws);

            // ---- end turn
            _endTurn = new Button { name = "btn_end_turn" };
            _endTurn.text = string.Empty;
            _endTurn.style.paddingTop = 13; _endTurn.style.paddingBottom = 13;
            _endTurn.style.paddingLeft = 30; _endTurn.style.paddingRight = 30;
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
            PaintTelegrams();
            ((Label)_telegramHead[1]).text = $"{_state.Turn}. TUR";

            PaintAxis(_axisRow0, "Otorite", "Özgürlük", Reporting.AxisOrder, v => v.x);
            PaintAxis(_axisRow1, "Sermaye", "Eşitlik", Reporting.AxisEconomy, v => v.y);
            PaintFactions();
            PaintSelection();

            int toElection = _state.TurnsToElection;
            _electionNote.text = toElection == 0 ? "SEÇİM BU TUR" : $"SEÇİM {toElection} TUR SONRA";
        }

        static void RepaintPill(Label pill, string text, Color colour)
        {
            pill.text = text;
            pill.style.color = colour;
            pill.style.backgroundColor = UiKit.Alpha(colour, 0.17f);
        }
    }
}



