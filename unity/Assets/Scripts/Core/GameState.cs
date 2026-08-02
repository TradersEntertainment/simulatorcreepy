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

        public string Name => Def.Name;
        public DistrictId Id => Def.Id;
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

        public bool IsElectionTurn
        {
            get
            {
                for (int i = 0; i < ElectionTurns.Length; i++)
                    if (ElectionTurns[i] == Turn) return true;
                return false;
            }
        }

        public int DecreeAllowance => Decrees.PerTurn + Modifiers.ExtraDecrees;

        public float EffectMagnitude(DecreeEffect kind)
        {
            float total = 0;
            foreach (var e in Effects)
                if (e.Kind == kind) total += e.Magnitude;
            return total;
        }

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
            AxisOrder = Mathf.Clamp(AxisOrder + order, -100, 100);
            AxisEconomy = Mathf.Clamp(AxisEconomy + economy, -100, 100);
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

