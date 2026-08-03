// The seam co-op is built on: where a ministry's numbers come from.
//
// In the single-player game every reported figure is the truth bent by a formula —
// Distortion.Bias, driven by who holds the desk and how the city is governed. In co-op the
// same figures arrive from a human minister's submitted report, and later from a bot with a
// personality. COOP.md §1: the ONLY thing that changes between those worlds is which
// IReportSource is plugged into Reporting. Nothing outside Reporting knows the field exists,
// and no implementation ever reads what it should not: a source is handed the true value of
// one line of one domain, and answers with what that ministry claims.

using UnityEngine;

namespace Mesruiyet.Core
{
    /// <summary>
    /// One line of a ministry's report. Ministers.DomainOf(Res) says which desk owns a
    /// resource; this says which figure of that desk is being asked for — including the
    /// chain stages, the buffer and grievance, which have no Res of their own. A human
    /// minister's report is a dictionary over these lines, nothing more.
    /// </summary>
    public enum ReportLine
    {
        /// <summary>The desk's overall lean, used to probe how hard a source distorts.</summary>
        Genel = 0,

        Stok,             // a ledger stock figure
        Akis,             // a ledger flow figure
        Isgucu,           // labour in use
        IsgucuHavuzu,     // labour available
        Urun,             // a chain's final product — bread, dressed material
        ZincirAsama,      // one stage of a chain
        Nufus,
        Hosnutsuzluk,     // the one line where SMALLER is the comfortable claim
        Tampon,
        OrduSadakati,     // the army's mood, which reaches you only through Güvenlik
    }

    /// <summary>
    /// Where this ministry's numbers come from this turn. Takes the true value, returns the
    /// reported one. Implementations: FormulaSource (single player and empty seats),
    /// HumanSource (a report from the network), BotSource (formula plus a personality).
    /// </summary>
    public interface IReportSource
    {
        /// <summary>What this desk reports for this line, given the true value.</summary>
        float Report(Domain domain, ReportLine line, float trueValue);

        /// <summary>Whether this desk's report is in. A formula is always in; a human may not be.</summary>
        bool HasSubmitted(Domain domain);
    }

    /// <summary>
    /// Today's behaviour, verbatim: the reported value is the true value bent by
    /// Distortion.Bias, in whichever direction flatters the governor. Single player runs on
    /// this forever; co-op keeps it for every seat nobody sits in.
    /// </summary>
    public sealed class FormulaSource : IReportSource
    {
        public float Report(Domain domain, ReportLine line, float trueValue)
        {
            var g = GameState.Current;
            if (g == null) return trueValue;

            float bias = Distortion.Bias(g.Cabinet?.Of(domain), g);

            // Grievance is the one line where rounding DOWN is the favour; everywhere else
            // the comfortable number is the bigger one. The direction belongs to the line,
            // not to the caller — a human source has no sign to apply, they just claim.
            if (line == ReportLine.Hosnutsuzluk) bias = -bias;

            return trueValue * (1f + bias);
        }

        /// <summary>The formula never keeps the governor waiting.</summary>
        public bool HasSubmitted(Domain domain) => true;
    }

    /// <summary>
    /// The formula with a person on top. A bot seat reports like the character holding it:
    /// the profile comes from MinisterDef, so sacking Rıza Efendi really does change what
    /// the granary numbers do. All shaping is deterministic — the same turn asked twice
    /// reports the same figure, which is what makes it testable and replayable.
    /// </summary>
    public sealed class BotSource : IReportSource
    {
        public float Report(Domain domain, ReportLine line, float trueValue)
        {
            var g = GameState.Current;
            var m = g?.Cabinet?.Of(domain);
            if (m == null) return trueValue;

            float bias = Distortion.Bias(m, g);
            if (line == ReportLine.Hosnutsuzluk) bias = -bias;

            switch (m.Def.Profile)
            {
                case BotProfile.Sisirici:
                    // Half again as comfortable, in whichever direction comfort lies.
                    return trueValue * (1f + bias * 1.5f);

                case BotProfile.Saklayici:
                {
                    // The signature move: the product and stage lines are quoted as if the
                    // chain were flowing — a share of the (genuinely healthy) total — so the
                    // blockage never reaches the governor's desk. Totals get the plain shine.
                    if (line == ReportLine.Urun || line == ReportLine.ZincirAsama)
                    {
                        var chain = domain == Domain.Tarim ? g.FoodChain : g.MaterialChain;
                        if (chain != null)
                        {
                            float flowing = chain.Total / Mathf.Max(1, chain.Stages.Length);
                            return Mathf.Max(trueValue * (1f + bias),
                                             flowing * (1f + bias * 0.5f));
                        }
                    }
                    return trueValue * (1f + bias);
                }

                case BotProfile.Alarmci:
                {
                    // Panic runs the other way: comfort shrinks, danger grows, and the two
                    // extra battalions are always short. Even an honest alarmist alarms.
                    float alarm = Mathf.Max(0.12f, Mathf.Abs(bias) * 0.8f);
                    if (line == ReportLine.Hosnutsuzluk) return trueValue * (1f + alarm);
                    if (line == ReportLine.OrduSadakati) return trueValue * (1f - alarm);
                    return trueValue * (1f - alarm * 0.5f);
                }

                case BotProfile.Beceriksiz:
                {
                    // No agenda, wrong anyway: a stable ±7% wobble seeded by turn, desk and
                    // line. Below the noise threshold, so NO range warns the governor — the
                    // clean-looking wrong number is the whole personality.
                    int h = (g.Turn * 73856093) ^ ((int)domain * 19349663) ^ ((int)line * 83492791);
                    float wobble = (((h & 1023) / 1023f) - 0.5f) * 0.14f;
                    return trueValue * (1f + bias + wobble);
                }

                case BotProfile.Yalaka:
                {
                    // Magnificent, everything, always. The floor is the point: transparency
                    // laws that tame the formula do not tame flattery.
                    float shine = Mathf.Max(Mathf.Abs(bias), 0.25f) * 1.3f;
                    return line == ReportLine.Hosnutsuzluk
                        ? trueValue * (1f - shine)
                        : trueValue * (1f + shine);
                }

                default:
                    return trueValue * (1f + bias);
            }
        }

        /// <summary>Bots file on time, with a cosmetic delay handled by the phase clock later.</summary>
        public bool HasSubmitted(Domain domain) => true;
    }
}
