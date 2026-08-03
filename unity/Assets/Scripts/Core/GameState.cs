// GameState holds the TRUE values. Nothing in the UI is allowed to read it.
//
// That single rule is the spine of the whole game: the player sees Reporting's version, the
// agent bridge sees this one, and the gap between them is the subject. Slice one has no
// ministers distorting anything yet, but the separation exists from the first line of code
// so it can never be retrofitted badly later.

using System.Collections.Generic;
using UnityEngine;

namespace Mesruiyet.Core
{
    public sealed class PlacedBuilding
    {
        public BuildingDef Def;
        public Vector2Int Tile;
        public DistrictId District;
        /// <summary>False when it has no workers or no power — it stands there costing upkeep.</summary>
        public bool Staffed = true;
        /// <summary>
        /// Shut down regardless of labour: a strike, a fire, a sabotaged mill. Events set this
        /// in a later slice; today it is how the agent loop reproduces a chain blockage.
        /// </summary>
        public bool Disabled;
        public int BuiltOnTurn;
    }

    public sealed class DistrictState
    {
        public DistrictDef Def;
        public float Grievance;
        public int Population;
        public int Housing;
        /// <summary>Turns spent above 70 grievance. Three in a row and the district acts.</summary>
        public int AngryStreak;

        /// <summary>Passed to a local strongman. Its production and its tiles are no longer yours.</summary>
        public bool Lost;

        /// <summary>Road tiles inside the district — its traffic capacity.</summary>
        public int RoadTiles;
        /// <summary>Traffic demand over road capacity. Above 1 the streets are jammed.</summary>
        public float Congestion;

        public string Name => Def.Name;
        public DistrictId Id => Def.Id;
    }

    /// <summary>A single turn, as it was and as it was described.</summary>
    public struct TurnRecord
    {
        public int Turn;
        public float TrueFood, ShownFood;
        public float TrueBuffer, ShownBuffer;
        public float TrueGrievance, ShownGrievance;
        public int AxisOrder, AxisEconomy;
        public int Legitimacy;
    }

    public sealed class GameState
    {
        public static GameState Current;

        // ---------------------------------------------------------------- time
        public int Turn = 1;
        public const int FinalTurn = 60;

        public int Season => (Turn - 1) % 4;
        public int Year => (Turn - 1) / 4 + 1;
        public string SeasonName => Naming.SeasonNames[Season];

        /// <summary>Elections fall on turns 12, 24, 36, 48 and 60 — the one channel that cannot lie.</summary>
        public static readonly int[] ElectionTurns = { 12, 24, 36, 48, 60 };

        public int TurnsToElection
        {
            get
            {
                for (int i = 0; i < ElectionTurns.Length; i++)
                    if (ElectionTurns[i] >= Turn) return ElectionTurns[i] - Turn;
                return 0;
            }
        }

        // ---------------------------------------------------------------- the ledger
        public readonly float[] Stock = new float[6];
        /// <summary>Net change applied on the last tick. Recomputed every turn by TurnResolver.</summary>
        public readonly float[] Flow = new float[6];
        /// <summary>Total İşgücü the city has, employed or not.</summary>
        public int LabourPool;
        public int LabourUsed;

        // ---------------------------------------------------------------- indices, 0..100
        public float Saglik = 50, Egitim = 50, Guvenlik = 50, Kultur = 50, Kirlilik = 6;

        // ---------------------------------------------------------------- politics
        public int Legitimacy = 60;
        /// <summary>OTORİTE(+) ↔ ÖZGÜRLÜK(−), −100..100.</summary>
        public int AxisOrder;
        /// <summary>SERMAYE(+) ↔ EŞİTLİK(−), −100..100.</summary>
        public int AxisEconomy;
        /// <summary>The last 15 turns of both axes. This is where the game's subject becomes visible.</summary>
        public readonly List<Vector2Int> AxisTrail = new List<Vector2Int>();

        public readonly float[] FactionLoyalty = { 50, 50, 50, 50, 50 };

        /// <summary>
        /// How honest the city's numbers are, 0..1. A free press and an open council push it up;
        /// authority and censorship push it down. Recomputed every tick by Distortion.Transparency.
        /// </summary>
        public float Transparency = 0.30f;

        /// <summary>The serving ministers. Every reported figure passes through one of them.</summary>
        public Cabinet Cabinet;

        /// <summary>This turn's telegrams, newest last. The onboarding channel and the trap in one.</summary>
        public readonly List<string> Telegrams = new List<string>();

        // ---------------------------------------------------------------- governance
        public Sim.Council Council;

        /// <summary>Laws currently in force. Never longer than <see cref="LawSlots"/>.</summary>
        public readonly List<LawDef> LawBook = new List<LawDef>();
        public int LawSlots = Laws.BaseSlots;

        /// <summary>Recomputed from the law book every tick, so a repeal undoes itself exactly.</summary>
        public LawModifiers Modifiers = LawModifiers.Neutral;

        public int DecreesLeft = Decrees.PerTurn;
        public readonly List<ActiveEffect> Effects = new List<ActiveEffect>();

        public readonly List<ElectionRecord> Elections = new List<ElectionRecord>();
        /// <summary>Set when an election was rigged; the scandal surfaces on this turn.</summary>
        public int ScandalTurn = -1;

        /// <summary>An election is due and the governor has not answered it yet.</summary>
        public bool ElectionPending;

        // ---------------------------------------------------------------- the outside world
        /// <summary>Mersa, 0..100. Rises on its own schedule and on your weakness.</summary>
        public float Threat;

        /// <summary>Garrison strength, from barracks and conscripts.</summary>
        public float Garrison;
        /// <summary>People taken out of the shared İşgücü pool and put in uniform.</summary>
        public int Conscripts;

        /// <summary>Turns until the garrison acts, or 0. You may not be told about it.</summary>
        public int CoupCountdown;

        public readonly List<Loan> Loans = new List<Loan>();

        /// <summary>The card on the desk, waiting to be answered.</summary>
        public EventDef PendingEvent;
        public readonly HashSet<string> FiredEvents = new HashSet<string>();
        /// <summary>What the last answered card did, for the report line.</summary>
        public string LastEventOutcome = "";

        public float TotalOwed
        {
            get
            {
                float t = 0;
                foreach (var l in Loans) t += l.Owed;
                return t;
            }
        }

        /// <summary>Law slots a creditor is sitting on. Half the book and you govern someone else's city.</summary>
        public int SealedSlots => Loans.Count;

        public bool IsElectionTurn
        {
            get
            {
                for (int i = 0; i < ElectionTurns.Length; i++)
                    if (ElectionTurns[i] == Turn) return true;
                return false;
            }
        }

        // ---------------------------------------------------------------- collapse
        /// <summary>
        /// What the administration can still actually do, 1 down to 0.45. Purges and a city that
        /// never accumulates both erode it, permanently, and nothing announces it.
        /// </summary>
        public float Competence = 1f;

        /// <summary>Build cost multiplier from needing everyone's consent. 1 upward.</summary>
        public float Consent = 1f;

        public float SecessionPressure;
        public int BankruptTurns;

        /// <summary>Turns left before DÖNÜŞSÜZ becomes final, or 0.</summary>
        public int NoReturnCountdown;
        /// <summary>True once a term has gone past 90 on an axis and come back. The best ending.</summary>
        public bool PulledBack;

        public Ending Ending = Ending.None;
        public bool IsOver => Ending != Ending.None;

        /// <summary>
        /// One row per turn: what was true and what the governor was told. The accountability
        /// session reads this back line by line, and it is the only reason it can.
        /// </summary>
        public readonly List<TurnRecord> History = new List<TurnRecord>();

        public int LostDistricts
        {
            get
            {
                int n = 0;
                foreach (var d in Districts) if (d.Lost) n++;
                return n;
            }
        }

        // ---------------------------------------------------------------- the budget
        /// <summary>Tax rate, 0 to 0.60. The only lever on income the governor actually holds.</summary>
        public float TaxRate = 0.22f;

        /// <summary>Funding 0..1 for Sağlık · Eğitim · Güvenlik · Kültür · Altyapı.</summary>
        public readonly float[] Funding = { 0.2f, 0.2f, 0.2f, 0.15f, 0.25f };

        public static readonly string[] FundingNames =
            { "Sağlık", "Eğitim", "Güvenlik", "Kültür", "Altyapı" };

        /// <summary>What the funding sliders cost per turn, at the current population.</summary>
        public float FundingCost
        {
            get
            {
                float total = 0;
                foreach (var f in Funding) total += f;
                return total * Population * 0.05f;
            }
        }

        // ---------------------------------------------------------------- the charter
        /// <summary>Three clauses, chosen on turn five and never revisited.</summary>
        public readonly List<ClauseDef> Charter = new List<ClauseDef>();
        public bool CharterPending;

        /// <summary>Walls the charter puts on the axes. Nothing may cross them, ever.</summary>
        public int OrderFloor = -100, OrderCeiling = 100;
        public int EconomyFloor = -100, EconomyCeiling = 100;

        // Laws can hand decrees back as well as grant them, so this floors at zero: none is a
        // legitimate position to be in, below none is a counter running backwards.
        public int DecreeAllowance => Mathf.Max(0, Decrees.PerTurn + Modifiers.ExtraDecrees);

        public float EffectMagnitude(DecreeEffect kind)
        {
            float total = 0;
            foreach (var e in Effects)
                if (e.Kind == kind) total += e.Magnitude;
            return total;
        }

        // ---------------------------------------------------------------- co-op: objectives & telegraph
        /// <summary>Each desk's hidden objective id, dealt once per term. Even bots have one.</summary>
        public readonly string[] ObjectiveOf = new string[5];

        /// <summary>Every telegram ever sent, sealed as "turn|desk|name|text". The
        /// accountability session reads these back word for word.</summary>
        public readonly List<string> TelegraphArchive = new List<string>();

        /// <summary>A back room between two desks. The governor sees that it exists, never inside.</summary>
        public sealed class PrivateChannel
        {
            public Domain A, B;
            public int OpenedTurn;
            public readonly List<string> Lines = new List<string>();
        }
        public readonly List<PrivateChannel> Channels = new List<PrivateChannel>();

        /// <summary>The turn the governor last issued a ferman — one open announcement per turn.</summary>
        public int FermanTurn = -1;

        // ---------------------------------------------------------------- delegation
        /// <summary>
        /// Which minister runs each district, indexed by DistrictId; −1 means the governor
        /// does. A delegated district builds itself, one building a turn, with the minister's
        /// judgement — the whole point is that the player does not have to place everything.
        /// </summary>
        public readonly int[] Delegation = { -1, -1, -1, -1, -1, -1 };

        /// <summary>What the delegated ministers did this turn, ready-formatted as telegrams.</summary>
        public readonly List<string> DelegationNotes = new List<string>();

        // ---------------------------------------------------------------- the city
        public DistrictState[] Districts;
        public readonly List<PlacedBuilding> Buildings = new List<PlacedBuilding>();

        /// <summary>The two supply chains. Their totals feed the Yiyecek and Malzeme ledger lines.</summary>
        public Chain[] Chains;

        /// <summary>
        /// Why each ledger line moved, in the player's words. §14 of the design: a player who
        /// cannot see why a number moved cannot learn the game.
        /// </summary>
        public readonly List<string>[] Ledger =
        {
            new List<string>(), new List<string>(), new List<string>(),
            new List<string>(), new List<string>(), new List<string>(),
        };

        public Chain GetChain(string id)
        {
            foreach (var c in Chains)
                if (c.Def.Id == id) return c;
            return null;
        }

        public Chain FoodChain => GetChain("yiyecek");
        public Chain MaterialChain => GetChain("malzeme");

        /// <summary>How many turns the city could absorb a shock. Pillar 3, made a number.</summary>
        public float BufferTurns = 6f;

        public int Population
        {
            get
            {
                int n = 0;
                foreach (var d in Districts) n += d.Population;
                return n;
            }
        }

        public float AverageGrievance
        {
            get
            {
                float sum = 0;
                foreach (var d in Districts) sum += d.Grievance;
                return sum / Districts.Length;
            }
        }

        public static GameState NewGame()
        {
            var g = new GameState();

            // Fully qualified: the Districts field below shadows the Districts content table.
            // Stored by enum value, never by declaration order — District(id) indexes this.
            var defs = Mesruiyet.Core.Districts.All;
            g.Districts = new DistrictState[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                g.Districts[(int)def.Id] = new DistrictState
                {
                    Def = def,
                    Population = def.StartPopulation,
                    Grievance = 10 + (100 - def.Wealth) * 0.06f,
                    Housing = def.StartPopulation + 20,
                };
            }

            g.Cabinet = Cabinet.Founding();
            g.Council = new Sim.Council();
            Objectives.Deal(g);

            g.Chains = new Chain[Mesruiyet.Core.Chains.All.Length];
            for (int i = 0; i < g.Chains.Length; i++)
                g.Chains[i] = Mesruiyet.Core.Chain.FromDef(Mesruiyet.Core.Chains.All[i]);

            // 400 settlers, a small treasury, a blank grid. Yiyecek and Malzeme are not stored
            // here — they are the sum of their chain's stages, written back on every tick.
            g.Stock[(int)Res.Para] = 900;
            g.Stock[(int)Res.Su] = 260;
            g.Stock[(int)Res.Enerji] = 120;

            g.AxisTrail.Add(Vector2Int.zero);
            return g;
        }

        public DistrictState District(DistrictId id) => Districts[(int)id];

        /// <summary>Dressed material sitting in the depot — what construction can actually draw.</summary>
        public float DepotStock => MaterialChain.Final.Stock;

        public bool CanAfford(BuildingDef def)
            => Stock[(int)Res.Para] >= def.CostMoney && DepotStock >= def.CostMaterial;

        /// <summary>Clamp an axis and remember that it moved. Everything that shifts politics comes here.</summary>
        public void ShiftAxes(int order, int economy)
        {
            // Every axis change in the game passes through here, which is what lets the charter
            // be a wall rather than a suggestion. A city that wrote "Söz Serbesttir" cannot
            // legislate its way past 55 on order however badly it needs to on turn forty.
            AxisOrder = Mathf.Clamp(AxisOrder + order, Mathf.Max(-100, OrderFloor), Mathf.Min(100, OrderCeiling));
            AxisEconomy = Mathf.Clamp(AxisEconomy + economy, Mathf.Max(-100, EconomyFloor), Mathf.Min(100, EconomyCeiling));
        }

        /// <summary>Adopt a founding clause. Applied once, on turn five, and never undone.</summary>
        public void AddClause(ClauseDef clause)
        {
            Charter.Add(clause);
            OrderFloor = Mathf.Max(OrderFloor, clause.OrderFloor);
            OrderCeiling = Mathf.Min(OrderCeiling, clause.OrderCeiling);
            EconomyFloor = Mathf.Max(EconomyFloor, clause.EconomyFloor);
            EconomyCeiling = Mathf.Min(EconomyCeiling, clause.EconomyCeiling);
            LawSlots += clause.ExtraLawSlots;
            ShiftAxes(0, 0);                 // pull the current position inside the new walls
        }

        public void ShiftFaction(Faction f, int delta)
            => FactionLoyalty[(int)f] = Mathf.Clamp(FactionLoyalty[(int)f] + delta, 0, 100);

        public void RecordTrail()
        {
            AxisTrail.Add(new Vector2Int(AxisOrder, AxisEconomy));
            if (AxisTrail.Count > 15) AxisTrail.RemoveAt(0);
        }
    }
}




