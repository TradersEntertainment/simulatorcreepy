// The serving cabinet, and the arithmetic of what it tells you.
//
// One minister per domain. Everything the UI reads about that domain passes through their
// bias, and nothing else does — so the player's blindness is always specific and always
// traceable to an appointment they made. The running total of that distortion is kept here
// because the accountability session at turn 60 reads it back, minister by minister.

using UnityEngine;

namespace Mesruiyet.Core
{
    public sealed class Minister
    {
        public MinisterDef Def;
        public int AppointedOnTurn;

        /// <summary>Sum of |bias| applied, and how many turns it was applied over.</summary>
        public float BiasSum;
        public int TurnsServed;

        /// <summary>This turn's message to the governor, in their own voice and their own numbers.</summary>
        public string Telegram = "";

        public string Name => Def.Name;
        public Domain Domain => Def.Domain;
        public bool Loyalist => Def.Loyalist;

        /// <summary>Average distortion across their term, as a percentage. For the final ledger.</summary>
        public float AverageBiasPercent => TurnsServed == 0 ? 0f : BiasSum / TurnsServed * 100f;
    }

    public sealed class Cabinet
    {
        public readonly Minister[] Ministers = new Minister[5];

        /// <summary>Ministers who have been dismissed, kept so the final session can call them back.</summary>
        public readonly System.Collections.Generic.List<Minister> Dismissed =
            new System.Collections.Generic.List<Minister>();

        public Minister Of(Domain d) => Ministers[(int)d];

        public static Cabinet Founding()
        {
            var c = new Cabinet();
            for (int i = 0; i < 5; i++)
            {
                var def = Core.Ministers.Candidate((Domain)i, Core.Ministers.FoundingLoyalists[i]);
                c.Ministers[i] = new Minister { Def = def, AppointedOnTurn = 0 };
            }
            return c;
        }

        /// <summary>Sack whoever holds the domain and appoint the chosen candidate in their place.</summary>
        public Minister Appoint(Domain d, bool loyalist, int turn)
        {
            var outgoing = Ministers[(int)d];
            if (outgoing != null) Dismissed.Add(outgoing);

            var incoming = new Minister
            {
                Def = Core.Ministers.Candidate(d, loyalist),
                AppointedOnTurn = turn,
            };
            Ministers[(int)d] = incoming;
            return incoming;
        }
    }

    public static class Distortion
    {
        /// <summary>
        /// The one formula, from PROMPT-CITY §5:
        ///     bias = style_bias × (1 − transparency) × authority_pressure
        ///
        /// Transparency is what a free press and an open council buy you. Authority pressure is
        /// what an obedient administration costs you: the further you drift towards OTORİTE, the
        /// harder your own people round their numbers in your favour. Nothing in the UI ever
        /// says so, which is the point — the fog thickens as a side effect of governing.
        /// </summary>
        public static float Bias(Minister m, GameState g)
        {
            if (m == null || Mathf.Abs(m.Def.StyleBias) < 0.0001f) return 0f;

            float authorityPressure = 1f + Mathf.Max(0, g.AxisOrder) / 100f;
            return m.Def.StyleBias * (1f - g.Transparency) * authorityPressure;
        }

        /// <summary>
        /// How honest the city's numbers are, 0..1. A press and an open council push it up;
        /// authority pushes it down. Everything about the information system is downstream of
        /// this single number, so it is computed in one place and nowhere else.
        /// </summary>
        public static float Transparency(GameState g)
        {
            int presses = 0;
            foreach (var b in g.Buildings)
                if (b.Def.Id == "matbaa" && b.Staffed) presses++;

            float t = 0.30f + 0.16f * presses;
            t += g.Modifiers.Transparency;                          // a free press law, or a censorship one
            t += g.EffectMagnitude(DecreeEffect.OpenSession);       // an open session, while it lasts
            t -= g.EffectMagnitude(DecreeEffect.PressDirective);
            t -= Mathf.Max(0, g.AxisOrder) / 200f;                  // checkpoints, and a quieter council

            // An adjourned chamber has nobody left to ask an awkward question.
            if (g.Council != null && g.Council.Suspended) t -= 0.15f;

            return Mathf.Clamp01(t);
        }

        /// <summary>Above this, a figure is too noisy to state and is shown as a range.</summary>
        public const float NoiseThreshold = 0.18f;
    }
}

