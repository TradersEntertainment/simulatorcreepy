// The minister's desk — the co-op mode's whole idea on one screen.
//
// Two columns, from reference/ref-02-bakan-ekrani-coop.png: GERÇEK in red, which only this
// player sees, and RAPOR in green, which is all the governor will ever see. Between what
// those two columns say sits the entire game. Slice 3 builds the core of that reference —
// header, the two columns, the governor's-eye preview and the submit — as local hot-seat;
// the telegraph chips, secret objective and history chart arrive with their own slices.

using UnityEngine;
using UnityEngine.UIElements;
using Mesruiyet.Core;

namespace Mesruiyet.UI
{
    public static class MinisterScreen
    {
        static readonly Color TrueRed = UiKit.Hex("#F2564B");
        static readonly Color ReportGreen = UiKit.Hex("#3FCF77");

        /// <summary>Build the desk for one human-held domain. Closing never submits.</summary>
        public static VisualElement Build(Domain domain, GameState state, System.Action onClose)
        {
            var human = HotSeat.HumanOf(domain);
            var minister = state.Cabinet.Of(domain);
            var lines = ReportLines.For(domain);

            var root = new VisualElement { name = "panel_bakan" };
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
            root.style.backgroundColor = new Color(0f, 0f, 0f, 0.78f);
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var card = UiKit.Glass().Pad(24, 30);
            card.style.width = 760;
            root.Add(card);

            // ---- header
            var head = UiKit.Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.marginBottom = 4;
            var title = UiKit.Text(Ministers.DomainNames[(int)domain] + " BAKANI", 22, UiKit.Ink, FontStyle.Bold);
            title.style.letterSpacing = 2f;
            head.Add(title);
            var phase = UiKit.Text("BAKAN FAZI", 11, UiKit.Amber, FontStyle.Bold);
            phase.style.letterSpacing = 1.4f;
            head.Add(phase);
            card.Add(head);
            card.Add(UiKit.Text($"{minister.Name} · {state.Turn}. tur", 11.5f, UiKit.Muted).Margin(bottom: 16));

            // ---- the two columns
            var columns = UiKit.Row();
            columns.style.alignItems = Align.FlexStart;
            card.Add(columns);

            var truthCol = Column(columns, "GERÇEK · " + Ministers.DomainNames[(int)domain],
                                  "SADECE SEN GÖRÜYORSUN", TrueRed);
            var reportCol = Column(columns, "RAPOR · " + Ministers.DomainNames[(int)domain],
                                   "VALİYE GİDECEK", ReportGreen);
            reportCol.parent.style.marginLeft = 14;

            var fields = new TextField[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                float trueValue = line.True(state);

                var row = UiKit.Row();
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.marginBottom = 12;
                var label = UiKit.Column();
                label.Add(UiKit.Text(line.Label, 12, UiKit.Ink, FontStyle.Bold));
                label.Add(UiKit.Text(line.Hint, 9.5f, UiKit.Muted).Margin(top: 1));
                row.Add(label);
                var value = UiKit.Text(line.Fmt(trueValue), 16, TrueRed, FontStyle.Bold);
                row.Add(value);
                truthCol.Add(row);

                var repRow = UiKit.Row();
                repRow.style.justifyContent = Justify.SpaceBetween;
                repRow.style.marginBottom = 8;
                var repLabel = UiKit.Column();
                repLabel.Add(UiKit.Text(line.Label, 12, UiKit.Ink, FontStyle.Bold));
                repLabel.Add(UiKit.Text("bildireceğin değer", 9.5f, UiKit.Muted).Margin(top: 1));
                repRow.Add(repLabel);

                var field = new TextField { name = "fld_rapor_" + line.Key };
                field.value = trueValue.ToString("0.#");
                field.style.width = 96;
                field.style.unityTextAlign = TextAnchor.MiddleRight;
                repRow.Add(field);
                fields[i] = field;
                reportCol.Add(repRow);
            }

            // ---- the secret objective, in the conspirators' purple. No sabotage anywhere:
            // if the city falls, this card falls with it, and the strip says so.
            var secret = new VisualElement();
            var purple = UiKit.Hex("#C48CFF");
            secret.style.backgroundColor = UiKit.Alpha(purple, 0.08f);
            secret.Radius(12).Border(1, UiKit.Alpha(purple, 0.4f)).Pad(12, 16).Margin(top: 14);
            var secretHead = UiKit.Text("SANA VERİLEN GİZLİ HEDEF", 9, purple, FontStyle.Bold);
            secretHead.style.letterSpacing = 1.4f;
            secret.Add(secretHead);
            var objective = Objectives.Get(state.ObjectiveOf[(int)domain]);
            var secretText = UiKit.Text("“" + (objective?.Text ?? "—") + "”", 14, UiKit.Ink, FontStyle.Bold);
            secretText.Margin(top: 4);
            secret.Add(secretText);
            secret.Add(UiKit.Text("Şehir çökerse bu hedef de kaybeder. Kimse sabotajla görevli değil.",
                                  10, UiKit.Dim).Margin(top: 3));
            card.Add(secret);

            // ---- the official telegram: stock phrases plus your own words, sealed forever.
            var wire = UiKit.Glass().Pad(10, 14).Margin(top: 14);
            wire.Add(UiKit.Heading("Resmî telgraf → Vali", "mühürlenir · arşivlenir"));

            var chipRow = new VisualElement();
            chipRow.style.flexDirection = FlexDirection.Row;
            chipRow.style.flexWrap = Wrap.Wrap;
            chipRow.style.marginBottom = 8;
            var wireField = new TextField { name = "fld_telgraf", multiline = true };
            wireField.style.height = 54;
            wireField.style.whiteSpace = WhiteSpace.Normal;

            // Eight phrases per desk out of the thirty-two, rotated by domain so every desk
            // clicks a different register. The rest are typed — the field is free text.
            for (int p = 0; p < 8; p++)
            {
                string phrase = Telegraph.Phrases[((int)domain * 8 + p) % Telegraph.Phrases.Length];
                var chip = new Button { text = phrase };
                chip.style.fontSize = 9;
                chip.style.paddingTop = 3; chip.style.paddingBottom = 3;
                chip.style.paddingLeft = 7; chip.style.paddingRight = 7;
                chip.style.marginRight = 4; chip.style.marginBottom = 4;
                chip.style.marginLeft = 0; chip.style.marginTop = 0;
                chip.style.backgroundColor = new Color(1, 1, 1, 0.05f);
                chip.style.color = UiKit.Hex("#C9D4E2");
                chip.Radius(7).Border(1, UiKit.Hairline);
                chip.clicked += () =>
                    wireField.value = (wireField.value + " " + phrase).Trim();
                chipRow.Add(chip);
            }
            wire.Add(chipRow);
            wire.Add(wireField);
            card.Add(wire);

            // ---- the back rooms this desk is party to. The governor sees only that they exist.
            if (state.Channels.Count > 0)
            {
                var rooms = UiKit.Glass().Pad(10, 14).Margin(top: 14);
                rooms.Add(UiKit.Heading("Özel kanallar", "vali içeriği göremez"));
                foreach (var ch in state.Channels)
                {
                    bool mine = ch.A == domain || ch.B == domain;
                    var other = ch.A == domain ? ch.B : ch.A;
                    var row = UiKit.Text(
                        mine
                            ? $"{Ministers.DomainNames[(int)other]} — {(ch.Lines.Count > 0 ? ch.Lines[ch.Lines.Count - 1] : "kanal açık")}"
                            : $"{Ministers.DomainNames[(int)ch.A]} ↔ {Ministers.DomainNames[(int)ch.B]} — kanal var, içerik kapalı",
                        10.5f, mine ? UiKit.Ink : UiKit.Dim);
                    row.style.whiteSpace = WhiteSpace.Normal;
                    row.Margin(bottom: 4);
                    rooms.Add(row);
                }
                card.Add(rooms);
            }

            // ---- what the governor currently sees, from this desk
            var seen = UiKit.Glass().Pad(10, 14).Margin(top: 16);
            seen.Add(UiKit.Heading("Valinin şu an gördüğü", "senin masandan"));
            var seenBody = UiKit.Row();
            seenBody.style.justifyContent = Justify.SpaceBetween;
            foreach (var line in lines)
            {
                var cell = UiKit.Column();
                cell.Add(UiKit.Caption(line.Label));
                float reported = Reporting.Source.Report(domain, line.Line, line.True(state));
                cell.Add(UiKit.Text(line.Fmt(reported), 13, UiKit.Ink, FontStyle.Bold).Margin(top: 2));
                seenBody.Add(cell);
            }
            seen.Add(seenBody);
            card.Add(seen);

            // ---- submit / close
            var buttons = UiKit.Row();
            buttons.style.marginTop = 18;
            buttons.style.justifyContent = Justify.SpaceBetween;

            var close = new Button { name = "btn_rapor_kapat", text = "KAPAT" };
            Style(close, new Color(1, 1, 1, 0.06f), UiKit.Ink);
            close.clicked += onClose;
            buttons.Add(close);

            var submit = new Button { name = "btn_rapor_gonder", text = "RAPORU GÖNDER" };
            Style(submit, UiKit.Amber, UiKit.Hex("#1A1206"));
            submit.clicked += () =>
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    // An unparseable field falls back to the truth — an accidental lie is
                    // not a thing this screen should be able to produce.
                    float trueNow = lines[i].True(state);
                    float claimed = float.TryParse(fields[i].value.Replace(',', '.'),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : trueNow;
                    human.SetClaim(lines[i].Line, claimed, trueNow);
                }
                human.Submit();
                Sim.MinisterManager.Instance?.SubmitHumanTelegram(domain, wireField.value);
                onClose();
            };
            buttons.Add(submit);
            card.Add(buttons);

            return root;
        }

        static VisualElement Column(VisualElement parent, string title, string pill, Color accent)
        {
            var col = new VisualElement();
            col.style.flexGrow = 1;
            col.style.flexBasis = 0;
            col.style.backgroundColor = UiKit.Alpha(accent, 0.06f);
            col.Radius(12).Border(1, UiKit.Alpha(accent, 0.4f)).Pad(14, 16);
            parent.Add(col);

            var head = UiKit.Text(title, 12, accent, FontStyle.Bold);
            head.style.letterSpacing = 1.4f;
            col.Add(head);

            var badge = UiKit.Text(pill, 8.5f, accent, FontStyle.Bold);
            badge.style.letterSpacing = 1f;
            badge.style.backgroundColor = UiKit.Alpha(accent, 0.14f);
            badge.style.alignSelf = Align.FlexStart;
            badge.Radius(5).Pad(2, 6).Margin(top: 5, bottom: 12);
            col.Add(badge);

            return col;
        }

        static void Style(Button b, Color bg, Color ink)
        {
            b.style.backgroundColor = bg;
            b.style.color = ink;
            b.style.fontSize = 11;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.letterSpacing = 1.2f;
            b.style.paddingTop = 10; b.style.paddingBottom = 10;
            b.style.paddingLeft = 18; b.style.paddingRight = 18;
            b.style.marginLeft = 0; b.style.marginRight = 0;
            b.style.marginTop = 0; b.style.marginBottom = 0;
            b.Radius(9).Border(1, bg == UiKit.Amber ? Color.clear : UiKit.Hairline);
        }
    }
}
