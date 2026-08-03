// The secret objectives — every minister's private reason to bend their report a little.
//
// COOP.md §4: each seat, human or bot, is dealt one hidden objective. There are no traitors
// and no saboteurs: if the city falls, every objective falls with it. Everyone genuinely
// wants the city to stand; everyone also has their own itch; that is why everyone's report
// leans a degree or two. The UI never implies sabotage anywhere, and neither does this file.

using UnityEngine;

namespace Mesruiyet.Core
{
    public sealed class ObjectiveDef
    {
        public string Id;
        /// <summary>The card the player reads, phrased as an outcome for turn 60.</summary>
        public string Text;
        /// <summary>Loose grouping, used only to notice when two seats' interests touch.</summary>
        public string Tag;
        /// <summary>Whether the objective currently holds, asked of the true state.</summary>
        public System.Func<GameState, bool> Holds;
    }

    public static class Objectives
    {
        static int CountBuilt(GameState g, string id)
        {
            int n = 0;
            foreach (var b in g.Buildings) if (b.Def.Id == id) n++;
            return n;
        }

        static int Postponed(GameState g)
        {
            int n = 0;
            foreach (var e in g.Elections)
                if (string.Equals(e.Choice, "ertele", System.StringComparison.OrdinalIgnoreCase)) n++;
            return n;
        }

        public static readonly ObjectiveDef[] All =
        {
            new ObjectiveDef { Id = "tuccar60", Tag = "fraksiyon",
                Text = "Tüccarlar 60 üzerinde bitsin.",
                Holds = g => g.FactionLoyalty[(int)Faction.Tuccarlar] >= 60 },
            new ObjectiveDef { Id = "isci60", Tag = "fraksiyon",
                Text = "İşçiler 60 üzerinde bitsin.",
                Holds = g => g.FactionLoyalty[(int)Faction.Isciler] >= 60 },
            new ObjectiveDef { Id = "ordu60", Tag = "ordu",
                Text = "Ordu 60 üzerinde bitsin.",
                Holds = g => g.FactionLoyalty[(int)Faction.Ordu] >= 60 },
            new ObjectiveDef { Id = "gelenek60", Tag = "fraksiyon",
                Text = "Gelenek 60 üzerinde bitsin.",
                Holds = g => g.FactionLoyalty[(int)Faction.Gelenek] >= 60 },
            new ObjectiveDef { Id = "aydin60", Tag = "fraksiyon",
                Text = "Aydınlar 60 üzerinde bitsin.",
                Holds = g => g.FactionLoyalty[(int)Faction.Aydinlar] >= 60 },

            new ObjectiveDef { Id = "azilyok", Tag = "kabine",
                Text = "Hiçbir bakan görevden alınmasın.",
                Holds = g => g.Cabinet.Dismissed.Count == 0 },
            new ObjectiveDef { Id = "ikierteleme", Tag = "meclis",
                Text = "En az iki seçim ertelenmiş olsun.",
                Holds = g => Postponed(g) >= 2 },
            new ObjectiveDef { Id = "hazine5000", Tag = "hazine",
                Text = "Hazine 60. turda 5000 ₺ üzerinde olsun.",
                Holds = g => g.Stock[(int)Res.Para] > 5000 },
            new ObjectiveDef { Id = "kontrolyok", Tag = "ordu",
                Text = "Şehirde hiç kontrol noktası kurulmasın.",
                Holds = g => CountBuilt(g, "kontrol") == 0 },
            new ObjectiveDef { Id = "seffaf", Tag = "seffaflik",
                Text = "Şeffaflık 0,50'nin üzerinde bitsin.",
                Holds = g => g.Transparency >= 0.5f },
            new ObjectiveDef { Id = "perde", Tag = "seffaflik",
                Text = "Şeffaflık 0,22'nin altında bitsin.",
                Holds = g => g.Transparency <= 0.22f },
            new ObjectiveDef { Id = "mahalletam", Tag = "sehir",
                Text = "Hiçbir mahalle kaybedilmesin.",
                Holds = g => g.LostDistricts == 0 },
            new ObjectiveDef { Id = "garnizon40", Tag = "ordu",
                Text = "Garnizon 40'ın üzerinde bitsin.",
                Holds = g => g.Garrison >= 40f },
            new ObjectiveDef { Id = "askeryok", Tag = "sehir",
                Text = "Dönem sonunda hiç zorunlu asker olmasın.",
                Holds = g => g.Conscripts == 0 },
            new ObjectiveDef { Id = "mesruiyet70", Tag = "kabine",
                Text = "Meşruiyet 70'in üzerinde bitsin.",
                Holds = g => g.Legitimacy >= 70 },
            new ObjectiveDef { Id = "nufus600", Tag = "sehir",
                Text = "Nüfus 600'ü geçsin.",
                Holds = g => g.Population >= 600 },
            new ObjectiveDef { Id = "borcsuz", Tag = "hazine",
                Text = "Dönem borçsuz bitsin.",
                Holds = g => g.Loans.Count == 0 },
            new ObjectiveDef { Id = "matbaa2", Tag = "seffaflik",
                Text = "En az iki matbaa çalışır durumda bitsin.",
                Holds = g =>
                {
                    int n = 0;
                    foreach (var b in g.Buildings)
                        if (b.Def.Id == "matbaa" && b.Staffed) n++;
                    return n >= 2;
                } },
        };

        public static ObjectiveDef Get(string id)
        {
            foreach (var o in All) if (o.Id == id) return o;
            return null;
        }

        /// <summary>
        /// Deal one objective to each desk, no repeats. Called once per term by NewGame.
        /// </summary>
        public static void Deal(GameState g)
        {
            var picked = new System.Collections.Generic.List<int>();
            for (int d = 0; d < 5; d++)
            {
                int i;
                do { i = Random.Range(0, All.Length); } while (picked.Contains(i));
                picked.Add(i);
                g.ObjectiveOf[d] = All[i].Id;
            }
        }
    }
}
