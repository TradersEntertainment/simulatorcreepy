// The seam co-op is built on: where a ministry's numbers come from.
//
// In the single-player game every reported figure is the truth bent by a formula —
// Distortion.Bias, driven by who holds the desk and how the city is governed. In co-op the
// same figures arrive from a human minister's submitted report, and later from a bot with a
// personality. COOP.md §1: the ONLY thing that changes between those worlds is which
// IReportSource is plugged into Reporting. Nothing outside Reporting knows the field exists,
// and no implementation ever reads what it should not: a source is handed the true value of
// one line of one domain, and answers with what that ministry claims.

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
}
