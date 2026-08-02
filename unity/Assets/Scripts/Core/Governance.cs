// What the law book and the decree pad add up to.
//
// LawModifiers is recomputed from the slots every tick rather than applied when a law is
// adopted. That costs a few multiplications and buys the thing that matters: repealing a law
// undoes it exactly, with no accumulated drift and no bookkeeping to get wrong. A law is only
// ever "in the book" or "not in the book".

using System.Collections.Generic;
using UnityEngine;

namespace Mesruiyet.Core
{
    /// <summary>A decree whose effect outlives the turn it was issued in.</summary>
    public sealed class ActiveEffect
    {
        public DecreeEffect Kind;
        public float Magnitude;
        public int TurnsLeft;
        public string Name;
    }

    /// <summary>The sum of every law currently in a slot.</summary>
    public struct LawModifiers
    {
        public float Tax;
        public float ChainThroughput;
        public float FarmThroughput;
        public float BuildCost;
        public float FoodDemand;
        public float LabourParticipation;
        public float Transparency;
        public float GrainCapacity;
        public float GrievanceSettle;
        public int Pollution;
        public int ExtraDecrees;

        public static LawModifiers Neutral => new LawModifiers
        {
            Tax = 1f,
            ChainThroughput = 1f,
            FarmThroughput = 1f,
            BuildCost = 1f,
            FoodDemand = 1f,
            GrainCapacity = 1f,
        };

        public static LawModifiers From(IReadOnlyList<LawDef> book)
        {
            var m = Neutral;
            for (int i = 0; i < book.Count; i++)
            {
                var l = book[i];
                m.Tax *= l.TaxMultiplier;
                m.ChainThroughput *= l.ChainThroughputMultiplier;
                m.FarmThroughput *= l.FarmThroughputMultiplier;
                m.BuildCost *= l.BuildCostMultiplier;
                m.FoodDemand *= l.FoodDemandMultiplier;
                m.GrainCapacity *= l.GrainCapacityMultiplier;
                m.LabourParticipation += l.LabourParticipationDelta;
                m.Transparency += l.TransparencyDelta;
                m.GrievanceSettle += l.GrievanceSettleBonus;
                m.Pollution += l.PollutionDelta;
                m.ExtraDecrees += l.ExtraDecrees;
            }
            return m;
        }
    }

    /// <summary>How an election went, kept so the accountability session can read it back.</summary>
    public sealed class ElectionRecord
    {
        public int Turn;
        /// <summary>What the city actually thought, whatever the governor did about it.</summary>
        public float TrueSupport;
        /// <summary>YAP · ERTELE · HİLE.</summary>
        public string Choice;
        public bool Won;
        public string Note = "";
    }
}
