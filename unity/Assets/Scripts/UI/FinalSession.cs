// HESAP VERME OTURUMU — the public accountability session, and the payoff for the whole
// information system.
//
// The design is emphatic that reaching the end must not cut straight to a card. Four things
// happen first, in order, and only then the ending:
//
//   1. Gerçek rakamlar — every true value beside what you were told, turn by turn, with the
//      largest lifetime divergence per domain called out by name
//   2. Bakanların ifadesi — the five speak; loyalists blame the circumstances, experts state
//      plainly what they told you and when
//   3. Fraksiyonların ifadesi — one verdict each, shaped by their final mood
//   4. The axis trail redrawn full-screen from turn one
//
// The first time the player sees their own city truthfully is the moment they can no longer do
// anything about it. Written to land without a word of moralising.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Mesruiyet.Core;

namespace Mesruiyet.UI
{
    public static class FinalSession
    {
        public static VisualElement Build(GameState g, System.Action onClose)
        {
            var root = new VisualElement();
            root.name = "panel_final";
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
            root.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
            root.style.paddingTop = 40; root.style.paddingBottom = 30;
            root.style.paddingLeft = 60; root.style.paddingRight = 60;

            var head = UiKit.Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.marginBottom = 6;
            head.Add(UiKit.Text("HESAP VERME OTURUMU", 24, UiKit.Ink, FontStyle.Bold));
            head.Add(UiKit.Text($"{g.Turn}. TUR · {g.Year}. YIL", 13, UiKit.Muted, FontStyle.Bold));
            root.Add(head);

            var sub = UiKit.Text(
                "Her satırda solda gerçekte olan, sağda size bildirilen yazılıdır. " +
                "İkisi tuttuğunda sağ tarafta çizgi vardır.",
                12.5f, UiKit.Muted).Margin(bottom: 18);
            root.Add(sub);

            var columns = UiKit.Row();
            columns.style.flexGrow = 1;
            columns.style.alignItems = Align.Stretch;
            columns.Add(LedgerColumn(g));
            columns.Add(TestimonyColumn(g));
            columns.Add(EndingColumn(g, onClose));
            root.Add(columns);

            return root;
        }

        static VisualElement Card(string title, string note)
        {
            var card = UiKit.Glass().Pad(16, 18);
            card.style.flexGrow = 1;
            card.style.flexBasis = 0;
            card.style.marginRight = 14;
            card.Add(UiKit.Heading(title, note));
            return card;
        }

        // ---------------------------------------------------------------- 1. the true figures

        static VisualElement LedgerColumn(GameState g)
        {
            var card = Card("Gerçek Rakamlar", "size ne söylendi");

            // The single sentence that indicts the whole term: the average divergence per
            // domain, in the ministry's own name.
            foreach (var line in Divergences(g))
                card.Add(line);

            var list = new ScrollView(ScrollViewMode.Vertical);
            list.style.flexGrow = 1;
            list.verticalScrollerVisibility = ScrollerVisibility.Auto;
            list.style.marginTop = 12;

            var header = UiKit.Row();
            header.style.marginBottom = 4;
            AddCell(header, "TUR", 42, UiKit.Dim);
            AddCell(header, "FIRINDA EKMEK", 92, UiKit.Dim);
            AddCell(header, "TAMPON", 92, UiKit.Dim);
            AddCell(header, "HOŞNUTSUZLUK", 120, UiKit.Dim);
            list.Add(header);

            // What the second number in each pair actually is, so the table cannot be
            // misread as a ministry claiming to have counted loaves.
            var legend = UiKit.Text(
                "gerçek · bakanlığın bildirdiği (yiyecek toplamı, tampon, en yüksek hoşnutsuzluk)",
                9, UiKit.Dim).Margin(bottom: 6);
            legend.style.whiteSpace = WhiteSpace.Normal;
            list.Add(legend);

            foreach (var r in g.History)
            {
                var row = UiKit.Row();
                row.style.paddingTop = 2; row.style.paddingBottom = 2;

                AddCell(row, r.Turn.ToString(), 42, UiKit.Muted);
                AddPair(row, r.TrueFood, r.ShownFood, 92, higherIsWorse: false);
                AddPair(row, r.TrueBuffer, r.ShownBuffer, 92, higherIsWorse: false);
                AddPair(row, r.TrueGrievance, r.ShownGrievance, 120, higherIsWorse: true);
                list.Add(row);
            }

            card.Add(list);
            return card;
        }

        static void AddCell(VisualElement row, string text, float width, Color colour)
        {
            var l = UiKit.Text(text, 10, colour, FontStyle.Bold);
            l.style.width = width;
            row.Add(l);
        }

        /// <summary>
        /// True, then what you were told. The gap is coloured only when it mattered — a
        /// ministry that flattered you about a comfortable number is not the story.
        /// </summary>
        static void AddPair(VisualElement row, float trueValue, float shown, float width, bool higherIsWorse)
        {
            bool lied = Mathf.Abs(trueValue - shown) > Mathf.Max(1f, Mathf.Abs(trueValue) * 0.05f);
            bool flattering = higherIsWorse ? shown < trueValue : shown > trueValue;

            var cell = UiKit.Row();
            cell.style.width = width;
            cell.Add(UiKit.Text($"{trueValue:0}", 10.5f, UiKit.Ink, FontStyle.Bold));
            cell.Add(UiKit.Text(lied ? $" · {shown:0}" : " · —", 10.5f,
                                lied && flattering ? UiKit.Red : UiKit.Dim));
            row.Add(cell);
        }

        static IEnumerable<VisualElement> Divergences(GameState g)
        {
            var served = new List<Minister>(g.Cabinet.Dismissed);
            foreach (var m in g.Cabinet.Ministers) if (m != null) served.Add(m);

            served.Sort((a, b) => b.AverageBiasPercent.CompareTo(a.AverageBiasPercent));

            int shown = 0;
            foreach (var m in served)
            {
                if (m.AverageBiasPercent < 1f || shown >= 3) continue;
                shown++;

                var l = UiKit.Text(
                    $"{Ministers.DomainNames[(int)m.Domain]}: {m.TurnsServed} tur boyunca " +
                    $"ortalama %{m.AverageBiasPercent:0} sapmalı rapor — {m.Name}",
                    11.5f, UiKit.Red, FontStyle.Bold).Margin(bottom: 4);
                l.style.whiteSpace = WhiteSpace.Normal;
                yield return l;
            }

            if (shown == 0)
                yield return UiKit.Text("Hiçbir bakanlık kayda değer bir sapma bildirmedi.",
                                        11.5f, UiKit.Green, FontStyle.Bold).Margin(bottom: 4);
        }

        // ---------------------------------------------------------------- 2 & 3. testimony

        static VisualElement TestimonyColumn(GameState g)
        {
            var card = Card("İfadeler", "bakanlar ve fraksiyonlar");

            var list = new ScrollView(ScrollViewMode.Vertical);
            list.style.flexGrow = 1;
            list.verticalScrollerVisibility = ScrollerVisibility.Auto;

            foreach (var m in g.Cabinet.Ministers)
            {
                if (m == null) continue;
                list.Add(Statement(Ministers.DomainNames[(int)m.Domain] + " · " + m.Name,
                                   MinisterStatement(m), m.Loyalist ? UiKit.Red : UiKit.Green));
            }

            list.Add(UiKit.Caption("Fraksiyonlar").Margin(top: 12, bottom: 8));
            for (int f = 0; f < 5; f++)
                list.Add(Statement(Naming.FactionNames[f], FactionVerdict(g, (Faction)f),
                                   MoodColour(g.FactionLoyalty[f])));

            card.Add(list);
            return card;
        }

        static VisualElement Statement(string who, string what, Color accent)
        {
            var block = UiKit.Column().Margin(bottom: 11);
            block.Add(UiKit.Text(who, 10.5f, accent, FontStyle.Bold));
            var body = UiKit.Text("“" + what + "”", 11.5f, UiKit.Hex("#C9D4E2")).Margin(top: 3);
            body.style.whiteSpace = WhiteSpace.Normal;
            block.Add(body);
            return block;
        }

        /// <summary>
        /// Loyalists blame each other and the circumstances. Experts state what they said and
        /// when. Neither apologises, because neither thinks they did anything unusual.
        /// </summary>
        static string MinisterStatement(Minister m)
        {
            if (!m.Loyalist)
                return m.TurnsServed == 0
                    ? "Görevde yeterince kalmadım. Söyleyecek bir şeyim yok."
                    : $"{m.TurnsServed} tur boyunca rakamları olduğu gibi yazdım. " +
                      "Tutanaklar duruyor; hangi turda ne bildirdiğim orada yazılı.";

            switch (m.Domain)
            {
                case Domain.Maliye:
                    return "Hazine rakamları daima müsaitti. Harcamalar benim uhdemde değildi.";
                case Domain.Tarim:
                    return "Ambar toplamlarını eksiksiz bildirdim. Değirmenin işleyişi " +
                           "İmar'ın işiydi, benim değil.";
                case Domain.Guvenlik:
                    return "Ordunun memnuniyetini her tur arz ettim. Sonradan olanlar " +
                           "benim arzımın konusu değildir.";
                case Domain.Imar:
                    return "Malzeme mevcudu daima yeterliydi. Depo ile ocak arasındaki fark " +
                           "teknik bir ayrıntıdır.";
                default:
                    return "Mahalleler sakindi. Sokakta olanlar Güvenlik'in alanına girer.";
            }
        }

        static string FactionVerdict(GameState g, Faction f)
        {
            float loyalty = g.FactionLoyalty[(int)f];
            bool warm = loyalty >= 60, cold = loyalty <= 25;

            switch (f)
            {
                case Faction.Tuccarlar:
                    return warm ? "Bu şehirde iş yapılabildi. Fazlasını istemiyoruz."
                         : cold ? "Sermayemizi başka limana taşıdık. Sebebini siz biliyorsunuz."
                         : "Ne kazandık ne kaybettik. Bir tüccar için bu da bir cevaptır.";
                case Faction.Isciler:
                    return warm ? "Ekmeğimiz vardı ve işimiz vardı. Bunu unutmayacağız."
                         : cold ? "Rıhtımda üç kış aç kaldık. Kimin karnının tok olduğunu da gördük."
                         : "Çalıştık. Karşılığını tam alamadık ama aç da kalmadık.";
                case Faction.Ordu:
                    return warm ? "Emir aldık, yerine getirdik. Garnizon size sadık kaldı."
                         : cold ? "Bize maaş yerine vatan anlatıldı. Askerin karnı sözle doymuyor."
                         : "Görevimizi yaptık. Fazlasını sormadık.";
                case Faction.Gelenek:
                    return warm ? "Şehrin âdetleri yerinde kaldı. Bu az bir şey değildir."
                         : cold ? "Bildiğimiz ne varsa değiştirildi ve hiçbiri sorulmadı."
                         : "Bazı şeyler değişti, bazıları kaldı. Zaman böyle geçer.";
                default:
                    return warm ? "Konuşabildik ve yazabildik. Bir şehir için bundan mühimi yok."
                         : cold ? "Matbaayı siz kapatmadınız — sadece kimse basmaya cesaret edemedi."
                         : "Sesimizi kısmadınız, yükseltmemize de yardım etmediniz.";
            }
        }

        static Color MoodColour(float loyalty)
            => loyalty >= 60 ? UiKit.Green : loyalty <= 25 ? UiKit.Red : UiKit.Amber;

        // ---------------------------------------------------------------- 4. the trail, then the card

        static VisualElement EndingColumn(GameState g, System.Action onClose)
        {
            var card = Card("Sürüklenme", "1. turdan bugüne");
            card.style.marginRight = 0;

            card.Add(new AxisTrailChart(g, 320, 190).Margin(bottom: 14));

            var def = Endings.Get(g.Ending);
            var title = UiKit.Text(def.Title, 26, def.Survival ? UiKit.Green : UiKit.Red, FontStyle.Bold);
            title.style.whiteSpace = WhiteSpace.Normal;
            card.Add(title);
            card.Add(UiKit.Text(def.Subtitle.ToUpperInvariant(), 10, UiKit.Muted, FontStyle.Bold)
                     .Margin(top: 3, bottom: 10));

            var epilogue = UiKit.Text(def.Epilogue, 12, UiKit.Hex("#C9D4E2"));
            epilogue.style.whiteSpace = WhiteSpace.Normal;
            card.Add(epilogue);

            var stats = UiKit.Column().Margin(top: 14);
            void Stat(string k, string v)
            {
                var row = UiKit.Row();
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.paddingTop = 4; row.style.paddingBottom = 4;
                row.style.borderTopWidth = 1;
                row.style.borderTopColor = new Color(1, 1, 1, 0.07f);
                row.Add(UiKit.Text(k, 11.5f, UiKit.Hex("#8E9CB0")));
                row.Add(UiKit.Text(v, 11.5f, UiKit.Ink, FontStyle.Bold));
                stats.Add(row);
            }
            Stat("Süre", $"{g.Turn} tur · {g.Year} yıl");
            Stat("Nüfus", g.Population.ToString("N0"));
            Stat("Kaybedilen mahalle", g.LostDistricts.ToString());
            Stat("Eksenler", $"otorite {g.AxisOrder:+0;−0} · ekonomi {g.AxisEconomy:+0;−0}");
            Stat("Mühürlü yasa yuvası", $"{g.SealedSlots}/{g.LawSlots}");
            Stat("Meşruiyet", g.Legitimacy.ToString());
            card.Add(stats);

            var close = new Button { name = "btn_final_close", text = "KAPAT" };
            close.style.marginTop = 14;
            close.style.marginLeft = 0; close.style.marginRight = 0; close.style.marginBottom = 0;
            close.style.paddingTop = 10; close.style.paddingBottom = 10;
            close.style.fontSize = 11;
            close.style.unityFontStyleAndWeight = FontStyle.Bold;
            close.style.color = UiKit.Bg;
            close.style.backgroundColor = UiKit.Amber;
            close.Radius(9).Border(0, Color.clear);
            close.clicked += () => onClose?.Invoke();
            card.Add(close);

            return card;
        }
    }

    /// <summary>
    /// The whole term as one line. This is the only place the drift the player never noticed
    /// becomes a shape rather than a number, so it is drawn full size and not as a sparkline.
    /// </summary>
    public sealed class AxisTrailChart : VisualElement
    {
        readonly GameState _g;

        public AxisTrailChart(GameState g, float width, float height)
        {
            _g = g;
            style.width = width;
            style.height = height;
            style.backgroundColor = new Color(1, 1, 1, 0.04f);
            this.Radius(10).Border(1, UiKit.Hairline);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            if (w <= 1 || h <= 1 || _g.History.Count < 2) return;

            float pad = 14f;
            float x0 = pad, x1 = w - pad, y0 = pad, y1 = h - pad;

            // The centre line: pragmatism, and the place nobody stays.
            p.strokeColor = new Color(1, 1, 1, 0.16f);
            p.lineWidth = 1f;
            p.BeginPath();
            p.MoveTo(new Vector2(x0, (y0 + y1) * 0.5f));
            p.LineTo(new Vector2(x1, (y0 + y1) * 0.5f));
            p.Stroke();

            void Line(System.Func<TurnRecord, int> pick, Color colour)
            {
                p.strokeColor = colour;
                p.lineWidth = 2f;
                p.BeginPath();
                for (int i = 0; i < _g.History.Count; i++)
                {
                    float t = i / (float)Mathf.Max(1, _g.History.Count - 1);
                    float v = Mathf.Clamp(pick(_g.History[i]), -100, 100);
                    var at = new Vector2(Mathf.Lerp(x0, x1, t),
                                         Mathf.Lerp(y1, y0, (v + 100f) / 200f));
                    if (i == 0) p.MoveTo(at); else p.LineTo(at);
                }
                p.Stroke();
            }

            Line(r => r.AxisOrder, UiKit.Red);
            Line(r => r.AxisEconomy, UiKit.Hex("#C48CFF"));
        }
    }
}

