// The two supply chains, as data. Exactly two — the design is explicit that everything else
// stays a pool, because a builder with six chains is an accounting exercise and this one is
// supposed to be about politics.
//
// The reason the food chain exists at all is the gap it creates. The ledger's `Yiyecek` line
// is the sum of every stage, so a granary full of unmilled grain reads as a healthy city
// while the bakeries run dry and people starve. That gap is honest arithmetic today; once
// ministers land, a loyalist Tarım reports the total and never mentions the blockage, and the
// player spends their safety margin without knowing it is gone.

namespace Mesruiyet.Core
{
    public sealed class ChainStageDef
    {
        /// <summary>The stage's own name — what the panel calls it.</summary>
        public string Name;
        /// <summary>What sits in this stage's buffer, in Turkish, for the panel.</summary>
        public string Holds;
        /// <summary>The building that drives this stage. Unstaffed buildings move nothing.</summary>
        public string BuildingId;
        /// <summary>Units this stage can move per turn, per staffed building.</summary>
        public float PerBuilding;

        public float BaseCapacity;
        /// <summary>Optional storage building that raises this stage's buffer.</summary>
        public string StoreBuildingId;
        public float StorePer;

        /// <summary>Stock the city is founded with, so turn one is not a famine.</summary>
        public float StartStock;
    }

    public sealed class ChainDef
    {
        public string Id;
        public string Name;
        /// <summary>Which ledger line the sum of every stage rolls up into.</summary>
        public Res Ledger;
        /// <summary>The domain whose minister reports on it — the distortion hook.</summary>
        public string Domain;
        /// <summary>What the last stage produces, in the player's words: "ekmek", "malzeme".</summary>
        public string Product;
        public ChainStageDef[] Stages;
    }

    public static class Chains
    {
        public static readonly ChainDef[] All =
        {
            new ChainDef
            {
                Id = "yiyecek",
                Name = "EKMEK",
                Ledger = Res.Yiyecek,
                Domain = "TARIM",
                Product = "ekmek",
                Stages = new[]
                {
                    // Grain keeps for years and piles up; flour and bread do not. That asymmetry
                    // in the buffer sizes is not flavour — it is the trap. Stop the mills and the
                    // granary swells while the bakeries empty, so the ledger's food total *rises*
                    // through the exact turns the city starts starving.
                    new ChainStageDef
                    {
                        Name = "Tarla", Holds = "tahıl", BuildingId = "tarla",
                        PerBuilding = 17f, BaseCapacity = 260f,
                        StoreBuildingId = "ambar", StorePer = 220f,
                        StartStock = 130f,
                    },
                    new ChainStageDef
                    {
                        Name = "Değirmen", Holds = "un", BuildingId = "degirmen",
                        PerBuilding = 34f, BaseCapacity = 90f,
                        StartStock = 70f,
                    },
                    new ChainStageDef
                    {
                        Name = "Fırın", Holds = "ekmek", BuildingId = "firin",
                        PerBuilding = 34f, BaseCapacity = 110f,
                        StartStock = 95f,
                    },
                },
            },

            new ChainDef
            {
                Id = "malzeme",
                Name = "MALZEME",
                Ledger = Res.Malzeme,
                Domain = "İMAR",
                Product = "işlenmiş malzeme",
                Stages = new[]
                {
                    new ChainStageDef
                    {
                        Name = "Ocak", Holds = "ham taş", BuildingId = "ocak",
                        PerBuilding = 22f, BaseCapacity = 120f,
                        StartStock = 90f,
                    },
                    new ChainStageDef
                    {
                        Name = "İşlik", Holds = "kereste ve taş", BuildingId = "islik",
                        PerBuilding = 24f, BaseCapacity = 150f,
                        StoreBuildingId = "depo", StorePer = 160f,
                        StartStock = 150f,
                    },
                },
            },
        };

        public static ChainDef Get(string id)
        {
            foreach (var c in All)
                if (c.Id == id) return c;
            return null;
        }
    }
}
