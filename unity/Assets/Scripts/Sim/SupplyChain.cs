// A supply chain at runtime: buffers, throughput, and where it is stuck.
//
// Material advances exactly one stage per turn. That lag is the point — the design asks that
// a cut ore supply freeze construction three turns later, not the same afternoon, so the
// player has to read the chain rather than the total to see trouble coming.
//
// Stages are ticked back to front. Doing it front to back would let a grain of wheat travel
// the whole chain in a single turn and the lag would vanish.

using System.Text;
using UnityEngine;

namespace Mesruiyet.Core
{
    public sealed class ChainStage
    {
        public ChainStageDef Def;

        /// <summary>What is sitting in this stage's buffer right now.</summary>
        public float Stock;
        public float Capacity;
        /// <summary>How much this stage could move this turn, from staffed buildings.</summary>
        public float Throughput;
        /// <summary>How much it actually moved. Below throughput means it was starved.</summary>
        public float Moved;
        /// <summary>Working buildings driving this stage.</summary>
        public int Working;
        /// <summary>Buildings that exist but have no workers.</summary>
        public int Idle;

        public string Name => Def.Name;

        /// <summary>Nothing to run this stage at all — the hard blockage.</summary>
        public bool Dead => Throughput <= 0.01f;

        /// <summary>Running below capacity because the stage upstream is not feeding it.</summary>
        public bool Starved => !Dead && Moved < Throughput - 0.5f;

        public bool Full => Stock >= Capacity - 0.5f;
    }

    public sealed class Chain
    {
        public ChainDef Def;
        public ChainStage[] Stages;

        /// <summary>The product the city actually consumes — bread, dressed material.</summary>
        public ChainStage Final => Stages[Stages.Length - 1];

        /// <summary>The headline figure: every stage added together. This is the number that lies.</summary>
        public float Total
        {
            get
            {
                float t = 0;
                foreach (var s in Stages) t += s.Stock;
                return t;
            }
        }

        public static Chain FromDef(ChainDef def)
        {
            var c = new Chain { Def = def, Stages = new ChainStage[def.Stages.Length] };
            for (int i = 0; i < def.Stages.Length; i++)
                c.Stages[i] = new ChainStage
                {
                    Def = def.Stages[i],
                    Stock = def.Stages[i].StartStock,
                    Capacity = def.Stages[i].BaseCapacity,
                };
            return c;
        }

        /// <summary>Recount throughput and storage from what is standing and staffed.</summary>
        public void Survey(GameState state)
        {
            foreach (var stage in Stages)
            {
                stage.Throughput = 0;
                stage.Capacity = stage.Def.BaseCapacity;
                stage.Working = 0;
                stage.Idle = 0;
            }

            foreach (var b in state.Buildings)
            {
                foreach (var stage in Stages)
                {
                    if (b.Def.Id == stage.Def.BuildingId)
                    {
                        if (b.Staffed) { stage.Throughput += stage.Def.PerBuilding; stage.Working++; }
                        else stage.Idle++;
                    }

                    if (!string.IsNullOrEmpty(stage.Def.StoreBuildingId) &&
                        b.Def.Id == stage.Def.StoreBuildingId && b.Staffed)
                        stage.Capacity += stage.Def.StorePer;
                }
            }
        }

        /// <summary>
        /// Advance one turn. Back to front, so nothing skips a stage, and each stage moves at
        /// most what it can carry, what is waiting for it, and what it has room to hold.
        /// </summary>
        public void Tick()
        {
            for (int i = Stages.Length - 1; i >= 1; i--)
            {
                var to = Stages[i];
                var from = Stages[i - 1];

                float room = Mathf.Max(0, to.Capacity - to.Stock);
                float moved = Mathf.Min(to.Throughput, Mathf.Min(from.Stock, room));

                from.Stock -= moved;
                to.Stock += moved;
                to.Moved = moved;
            }

            // The first stage extracts rather than receives: farms and quarries make something
            // out of land, and stop when their own buffer is full.
            var head = Stages[0];
            float headRoom = Mathf.Max(0, head.Capacity - head.Stock);
            head.Moved = Mathf.Min(head.Throughput, headRoom);
            head.Stock += head.Moved;
        }

        /// <summary>
        /// How many turns the chain can meet <paramref name="demandPerTurn"/> before it comes up
        /// short, by running the pipeline forward on a copy of the stocks.
        ///
        /// Worth the arithmetic rather than dividing stock by rate: a chain can be perfectly
        /// balanced and still starve you, because material takes one turn per stage and an empty
        /// bakery is an empty bakery however much grain is behind it. TAMPON is the number the
        /// player plans around, so it has to know that.
        /// </summary>
        public int TurnsUntilShortfall(float demandPerTurn, int horizon = 9)
        {
            if (demandPerTurn <= 0.01f) return horizon;

            var stock = new float[Stages.Length];
            for (int i = 0; i < Stages.Length; i++) stock[i] = Stages[i].Stock;

            for (int turn = 0; turn < horizon; turn++)
            {
                for (int i = Stages.Length - 1; i >= 1; i--)
                {
                    float room = Mathf.Max(0, Stages[i].Capacity - stock[i]);
                    float moved = Mathf.Min(Stages[i].Throughput, Mathf.Min(stock[i - 1], room));
                    stock[i - 1] -= moved;
                    stock[i] += moved;
                }

                float headRoom = Mathf.Max(0, Stages[0].Capacity - stock[0]);
                stock[0] += Mathf.Min(Stages[0].Throughput, headRoom);

                int last = Stages.Length - 1;
                if (stock[last] < demandPerTurn - 0.5f) return turn;
                stock[last] -= demandPerTurn;
            }

            return horizon;
        }

        /// <summary>Take from the end of the chain. Returns what was actually available.</summary>
        public float Draw(float amount)
        {
            var final = Final;
            float taken = Mathf.Min(final.Stock, amount);
            final.Stock -= taken;
            return taken;
        }

        /// <summary>
        /// What the chain can actually sustain per turn: its narrowest stage. Two mills behind
        /// two farms still only deliver what the farms grow, and the safety margin has to be
        /// computed from this rather than from the last stage's nameplate capacity.
        /// </summary>
        public float EffectiveRate
        {
            get
            {
                float rate = float.MaxValue;
                foreach (var s in Stages) rate = Mathf.Min(rate, s.Throughput);
                return rate == float.MaxValue ? 0f : rate;
            }
        }

        /// <summary>
        /// The stage choking the chain, or null when nothing is. This is what the panel points
        /// at, and what a loyalist minister will decline to mention.
        /// </summary>
        public ChainStage Bottleneck()
        {
            // A dead stage is the worst kind: nothing is running it at all.
            foreach (var s in Stages)
                if (s.Dead) return s;

            // Otherwise the narrowest stage — including the first, because farms throttle a
            // chain just as surely as mills do, and including the last, because ovens can fail
            // to bake what the mills grind.
            ChainStage narrowest = Stages[0];
            float widest = Stages[0].Throughput;
            for (int i = 1; i < Stages.Length; i++)
            {
                if (Stages[i].Throughput < narrowest.Throughput) narrowest = Stages[i];
                if (Stages[i].Throughput > widest) widest = Stages[i].Throughput;
            }

            // Every stage moving at the same rate is a balanced chain, not a stuck one. A full
            // granary behind a balanced chain means you are growing more than you eat, which is
            // the thing you were hoping for, so it must not be reported as a fault.
            return widest - narrowest.Throughput > 0.5f ? narrowest : null;
        }

        /// <summary>One line of plain Turkish for the panel and, later, for a minister to distort.</summary>
        public string Diagnosis()
        {
            var b = Bottleneck();
            if (b == null) return "akıyor";
            if (b.Dead) return $"{b.Name} durdu";
            return $"{b.Name} dar boğaz";
        }

        public string ToJson()
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"id\":\"").Append(Def.Id).Append("\",");
            sb.Append("\"total\":").Append(F(Total)).Append(',');
            sb.Append("\"final\":").Append(F(Final.Stock)).Append(',');
            sb.Append("\"diagnosis\":\"").Append(Diagnosis()).Append("\",");
            sb.Append("\"stages\":[");
            for (int i = 0; i < Stages.Length; i++)
            {
                var s = Stages[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":\"").Append(s.Name).Append("\",");
                sb.Append("\"stock\":").Append(F(s.Stock)).Append(',');
                sb.Append("\"capacity\":").Append(F(s.Capacity)).Append(',');
                sb.Append("\"throughput\":").Append(F(s.Throughput)).Append(',');
                sb.Append("\"moved\":").Append(F(s.Moved)).Append(',');
                sb.Append("\"working\":").Append(s.Working).Append(',');
                sb.Append("\"idle\":").Append(s.Idle).Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
