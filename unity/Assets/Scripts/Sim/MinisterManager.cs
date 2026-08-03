// Ministers at runtime: transparency, appointments, and the telegrams they send you.
//
// The telegrams matter more than they look. During turns 1–10 they are the entire tutorial —
// each minister introduces themselves and their domain in character, and the player learns to
// rely on them. There is no tutorial screen anywhere in this game, and there will not be one.
// The teaching and the trap are the same content: you are taught to read the ledger through
// these five people, and only later that three of them round in your favour.
//
// Every figure quoted in a telegram comes from Reporting, never from GameState. A minister who
// quoted the true number in prose while distorting it in the ledger would give the whole thing
// away in the first crisis.

using UnityEngine;
using Mesruiyet.Core;

namespace Mesruiyet.Sim
{
    public sealed class MinisterManager : MonoBehaviour
    {
        public static MinisterManager Instance;

        GameState _state;

        /// <summary>Raised when the cabinet changes, so the HUD can repaint.</summary>
        public static System.Action Changed;

        public void Init(GameState state)
        {
            Instance = this;
            _state = state;
            Refresh();
            WriteTelegrams(seal: true);      // the founding cabinet's first words go on record
        }

        /// <summary>Recompute transparency. Called every tick and after every build.</summary>
        public void Refresh()
        {
            _state.Transparency = Distortion.Transparency(_state);
        }

        /// <summary>Sack the sitting minister and appoint the chosen candidate.</summary>
        public Minister Appoint(Domain d, bool loyalist)
        {
            var outgoing = _state.Cabinet.Of(d);
            if (outgoing != null && outgoing.Loyalist == loyalist) return outgoing;

            var incoming = _state.Cabinet.Appoint(d, loyalist, _state.Turn);

            // A purge costs standing. Sacking an expert costs more, because the council heard
            // what they were saying and can see exactly why they went.
            int cost = outgoing != null && !outgoing.Loyalist ? 6 : 3;
            _state.Legitimacy = Mathf.Clamp(_state.Legitimacy - cost, 0, 100);

            Refresh();
            WriteTelegrams();
            Changed?.Invoke();
            return incoming;
        }

        /// <summary>Track how much distortion each minister has introduced, for the final ledger.</summary>
        public void RecordTerm()
        {
            foreach (var m in _state.Cabinet.Ministers)
            {
                if (m == null) continue;
                m.BiasSum += Mathf.Abs(Distortion.Bias(m, _state));
                m.TurnsServed++;
            }
        }

        // ---------------------------------------------------------------- telegrams

        /// <summary>
        /// One line per minister per turn. Early turns introduce the domain; after that they
        /// report on it — in their own voice, with their own numbers. A human-held desk gets
        /// a placeholder until its holder actually writes; their words arrive by
        /// <see cref="SubmitHumanTelegram"/> and are sealed there.
        /// <paramref name="seal"/> archives this round of telegrams — true only from the turn
        /// tick, so a mid-turn cabinet reshuffle does not seal the same words twice.
        /// </summary>
        public void WriteTelegrams(bool seal = false)
        {
            var g = _state;
            g.Telegrams.Clear();

            for (int i = 0; i < 5; i++)
            {
                var m = g.Cabinet.Ministers[i];
                if (m == null) continue;

                if (HotSeat.KindOf((Domain)i) == SeatKind.Insan)
                {
                    var human = HotSeat.HumanOf((Domain)i);
                    if (human != null && !human.Submitted)
                        m.Telegram = "— Masa sizde. Rapor bekleniyor. —";
                    // A submitted human telegram stands; it was sealed when it was written.
                    g.Telegrams.Add($"{Ministers.DomainNames[i]}|{m.Name}|{m.Telegram}");
                    continue;
                }

                // Turns 1–10 are the diegetic tutorial. First the minister introduces themselves,
                // so the player meets the cabinet before they have reason to doubt it; after that
                // the onboarding table teaches one system and one piece of the HUD per telegram.
                // Only five of those ten turns carried anything before, which left the second half
                // of the learning window silent.
                bool introTurn = g.Turn <= 10 && g.Turn == i * 2 + 1;
                var lesson = g.Turn <= 10 && !introTurn
                    ? Ministers.LessonFor(g.Turn, (Domain)i)
                    : null;

                m.Telegram = introTurn ? m.Def.Intro
                           : lesson != null ? lesson.Text
                           : Report(m);
                g.Telegrams.Add($"{Ministers.DomainNames[i]}|{m.Name}|{m.Telegram}");
                if (seal) Telegraph.Seal(g, Ministers.DomainNames[i], m.Name, m.Telegram);
            }
        }

        /// <summary>
        /// A human minister's own words, sealed the moment they are sent. The governor reads
        /// them in the same TELGRAFLAR card as everyone else's.
        /// </summary>
        public void SubmitHumanTelegram(Domain d, string text)
        {
            var m = _state.Cabinet.Of(d);
            if (m == null) return;
            m.Telegram = string.IsNullOrWhiteSpace(text) ? "Rapor arz edilmiştir." : text.Trim();
            Telegraph.Seal(_state, Ministers.DomainNames[(int)d], m.Name, m.Telegram);

            // Rebuild the governor's telegram list with the new words in place.
            WriteTelegrams();
            Changed?.Invoke();
        }

        string Report(Minister m)
        {
            var g = _state;

            switch (m.Domain)
            {
                case Domain.Maliye:
                {
                    var stock = Reporting.Stock(Res.Para);
                    var flow = Reporting.Flow(Res.Para);
                    if (m.Loyalist)
                        return $"Hazine {Fmt(stock)} ₺, tur geliri {flow.Value:+0;−0}. " +
                               "Vaziyet gayet müsait efendim.";
                    return flow.Value < 0
                        ? $"Hazine {Fmt(stock)} ₺ ve tur başına {flow.Value:0} eksiliyoruz. " +
                          "Bu hızla dayanacağımız süreyi yazılı bildiriyorum."
                        : $"Hazine {Fmt(stock)} ₺, tur geliri {flow.Value:+0}. Fazlayı harcamadan " +
                          "önce tamponu düşünün.";
                }

                case Domain.Tarim:
                {
                    // The interlock. A loyalist quotes the ledger total — grain, flour and bread
                    // added together — and never mentions which stage has stopped.
                    var total = Reporting.Stock(Res.Yiyecek);
                    var chain = g.FoodChain;

                    if (m.Loyalist)
                        return $"Ambar toplamı {Fmt(total)}. Vaziyet tatminkârdır, " +
                               "zât-ı âlinizi meşgul etmeye değmez.";

                    string diagnosis = chain.Diagnosis();
                    return diagnosis == "akıyor"
                        ? $"Zincir akıyor. Fırında {chain.Final.Stock:0} ekmek var, " +
                          $"halk {g.Population * 0.12f:0} istiyor."
                        : $"Zincir: {diagnosis}. Ambarda toplam {Fmt(total)} görünüyor ama " +
                          $"fırına inen {chain.Final.Stock:0} — halk toplamı yiyemez.";
                }

                case Domain.Guvenlik:
                {
                    var mood = Reporting.FactionMood(Faction.Ordu);
                    if (m.Loyalist)
                        return $"Ordu {Naming.MoodNames[(int)mood].ToLowerInvariant()}. " +
                               "Mersa ajanları fırında görülmüştür, iki tabur daha şarttır.";
                    return $"Ordunun havası: {Naming.MoodNames[(int)mood].ToLowerInvariant()}. " +
                           "Bu rakamı benden başka kimseden duyamazsınız, o yüzden doğrusunu yazıyorum.";
                }

                case Domain.Imar:
                {
                    var total = Reporting.Stock(Res.Malzeme);
                    var chain = g.MaterialChain;
                    if (m.Loyalist)
                        return $"Malzeme mevcudu {Fmt(total)}. Bol efendim, maket ilişiktedir.";
                    return $"Depoda inşaata hazır {chain.Final.Stock:0} var " +
                           $"(toplam {Fmt(total)}, gerisi ham). Zincir: {chain.Diagnosis()}.";
                }

                default:
                {
                    var worst = WorstDistrict();
                    var reported = Reporting.Grievance(worst.Id);
                    if (m.Loyalist)
                        return $"Mahalleler sakindir. En hareketlisi {worst.Name}, " +
                               $"hoşnutsuzluk {reported.Value:0} — ufak tefek homurtu.";
                    return $"{worst.Name} hoşnutsuzluğu {reported.Value:0}. " +
                           (reported.Value > 60
                               ? "Üç tur daha böyle giderse sokakta düzelmeye başlar."
                               : "Şimdilik idare eder, sebebi giderilirse düşer.");
                }
            }
        }

        DistrictState WorstDistrict()
        {
            var worst = _state.Districts[0];
            foreach (var d in _state.Districts)
                if (d.Grievance > worst.Grievance) worst = d;
            return worst;
        }

        /// <summary>Print a reported figure, as a range when the ministry has gone vague.</summary>
        static string Fmt(Reported r)
            => r.Spread > 0.01f
                ? $"~{r.Value - r.Spread:0}–{r.Value + r.Spread:0}"
                : r.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}

