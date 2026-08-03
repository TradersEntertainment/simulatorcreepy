// What each desk actually reports on: the lines, their labels, and where the truth lives.
//
// The minister screen's two columns, the agent bridge's report command and the human
// source all speak in these rows. One table, so a desk cannot be asked for a line it does
// not own, and a human minister sees exactly the truth their formula counterpart would bend.

using UnityEngine;

namespace Mesruiyet.Core
{
    public sealed class ReportLineDef
    {
        public ReportLine Line;
        /// <summary>Bridge / scenario name, ascii and lowercase.</summary>
        public string Key;
        /// <summary>What the minister screen prints.</summary>
        public string Label;
        /// <summary>What the small print under it says the number is.</summary>
        public string Hint;
        public System.Func<GameState, float> True;
        /// <summary>Formatted for the screen — "480", "−5/tur", "9 tur".</summary>
        public System.Func<float, string> Fmt;
    }

    public static class ReportLines
    {
        static string Plain(float v) => v.ToString("0");
        static string Signed(float v) => v.ToString("+0;−0") + "/tur";
        static string Turns(float v) => v.ToString("0") + " tur";

        static readonly ReportLineDef[][] ByDomain =
        {
            // MALİYE — the treasury and its slope.
            new[]
            {
                new ReportLineDef { Line = ReportLine.Stok, Key = "para", Label = "Hazine",
                    Hint = "kasadaki para", True = g => g.Stock[(int)Res.Para], Fmt = Plain },
                new ReportLineDef { Line = ReportLine.Akis, Key = "gelir", Label = "Tur geliri",
                    Hint = "geçen turun net akışı", True = g => g.Flow[(int)Res.Para], Fmt = Signed },
            },

            // TARIM — the interlock: the total, the bread, and the buffer inferred from them.
            new[]
            {
                new ReportLineDef { Line = ReportLine.Stok, Key = "tahil", Label = "Ambar toplamı",
                    Hint = "tahıl + un + ekmek", True = g => g.Stock[(int)Res.Yiyecek], Fmt = Plain },
                new ReportLineDef { Line = ReportLine.Urun, Key = "ekmek", Label = "Fırındaki ekmek",
                    Hint = "halkın yiyebildiği", True = g => g.FoodChain.Final.Stock, Fmt = Plain },
                new ReportLineDef { Line = ReportLine.Tampon, Key = "tampon", Label = "Güvenlik payı",
                    Hint = "şok soğurma süresi", True = g => g.BufferTurns, Fmt = Turns },
            },

            // GÜVENLİK — the army speaks only through this desk.
            new[]
            {
                new ReportLineDef { Line = ReportLine.OrduSadakati, Key = "ordu", Label = "Ordu sadakati",
                    Hint = "garnizonun gerçek havası", True = g => g.FactionLoyalty[(int)Faction.Ordu], Fmt = Plain },
            },

            // İMAR — the yard total and what construction can actually draw.
            new[]
            {
                new ReportLineDef { Line = ReportLine.Stok, Key = "malzeme", Label = "Malzeme toplamı",
                    Hint = "ham + işlenmiş", True = g => g.Stock[(int)Res.Malzeme], Fmt = Plain },
                new ReportLineDef { Line = ReportLine.Urun, Key = "depo", Label = "Depodaki işlenmiş",
                    Hint = "inşaata hazır olan", True = g => g.DepotStock, Fmt = Plain },
            },

            // HALK — the street, rounded at your peril.
            new[]
            {
                new ReportLineDef { Line = ReportLine.Hosnutsuzluk, Key = "hosnutsuzluk", Label = "En yüksek hoşnutsuzluk",
                    Hint = "en hareketli mahalle", True = WorstGrievance, Fmt = Plain },
                new ReportLineDef { Line = ReportLine.Nufus, Key = "nufus", Label = "Nüfus",
                    Hint = "kayıtlı hane sayımı", True = g => g.Population, Fmt = Plain },
            },
        };

        static float WorstGrievance(GameState g)
        {
            float worst = 0;
            foreach (var d in g.Districts) worst = Mathf.Max(worst, d.Grievance);
            return worst;
        }

        public static ReportLineDef[] For(Domain d) => ByDomain[(int)d];

        /// <summary>Resolve a bridge key like "tahil" against a desk's own lines.</summary>
        public static ReportLineDef Find(Domain d, string key)
        {
            foreach (var line in ByDomain[(int)d])
                if (line.Key == key) return line;
            return null;
        }
    }

    // ------------------------------------------------------------------ the seats

    public enum SeatKind { Formul, Bot, Insan }

    /// <summary>
    /// Local hot-seat: who plays each desk on this machine. COOP.md slice 3 — the minister
    /// screen and the human source, with the network nowhere in sight. When every seat is
    /// on the formula this class steps aside entirely and Reporting behaves as single player.
    /// </summary>
    public static class HotSeat
    {
        static readonly SeatKind[] _kinds = { SeatKind.Formul, SeatKind.Formul, SeatKind.Formul, SeatKind.Formul, SeatKind.Formul };
        static readonly HumanSource[] _humans = { new HumanSource(), new HumanSource(), new HumanSource(), new HumanSource(), new HumanSource() };

        public static SeatKind KindOf(Domain d) => _kinds[(int)d];
        public static HumanSource HumanOf(Domain d) => _kinds[(int)d] == SeatKind.Insan ? _humans[(int)d] : null;

        public static bool AnyHuman
        {
            get { foreach (var k in _kinds) if (k == SeatKind.Insan) return true; return false; }
        }

        /// <summary>Human desks whose report has not gone out yet — the end-turn gate.</summary>
        public static int PendingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < 5; i++)
                    if (_kinds[i] == SeatKind.Insan && !_humans[i].Submitted) n++;
                return n;
            }
        }

        public static void SetSeat(Domain d, SeatKind kind)
        {
            _kinds[(int)d] = kind;
            if (kind == SeatKind.Insan) _humans[(int)d].NewTurn();
            Apply();
        }

        /// <summary>Rebuild Reporting.Source to match the seats. All-formula collapses to single player.</summary>
        static void Apply()
        {
            bool plain = true;
            foreach (var k in _kinds) if (k != SeatKind.Formul) plain = false;

            if (plain) { Reporting.Source = new FormulaSource(); return; }

            var seats = new SeatSource();
            for (int i = 0; i < 5; i++)
                seats.Set((Domain)i,
                    _kinds[i] == SeatKind.Insan ? (IReportSource)_humans[i]
                  : _kinds[i] == SeatKind.Bot ? new BotSource()
                  : new FormulaSource());
            Reporting.Source = seats;
        }

        /// <summary>Called by the resolver after every tick: new truth, new reports.</summary>
        public static void NewTurn()
        {
            foreach (var h in _humans) h.NewTurn();
        }

        /// <summary>Back to plain single player — the restart path calls this.</summary>
        public static void Reset()
        {
            for (int i = 0; i < 5; i++) _kinds[i] = SeatKind.Formul;
            NewTurn();
            Apply();
        }
    }
}
