// The telegraph office — the one open channel, the sealed archive, and the back rooms.
//
// COOP.md §5: the official telegram is how a minister speaks to the governor, built from
// stock phrases plus free text. Every telegram is sealed and archived the moment it is
// written, because the accountability session at turn 60 quotes them word for word — the
// player is told this up front, and writes anyway. Private channels connect two ministers;
// the governor sees THAT one exists, never what is in it. Those are archived too.

using UnityEngine;

namespace Mesruiyet.Core
{
    public static class Telegraph
    {
        /// <summary>
        /// The stock phrases of a career bureaucrat, ready to click into a telegram. The
        /// phrasing does the work: each one sounds like information and commits to nothing.
        /// </summary>
        public static readonly string[] Phrases =
        {
            "Durum tatminkârdır.",
            "Tedbir alınmıştır.",
            "Zât-ı âlinizi meşgul etmeye değmez.",
            "Mesele tetkik edilmektedir.",
            "Tahkikat başlatılmıştır.",
            "Bütçe kifayetsizdir.",
            "Şahsen kefilim.",
            "Vaziyet kontrol altındadır.",
            "Endişeye mahal yoktur.",
            "Rakamlar yerindedir.",
            "Ambar doludur.",
            "Halk sükûnettedir.",
            "Ordu emrinizdedir.",
            "İki tabur daha şarttır.",
            "Malzeme boldur.",
            "Zincir akmaktadır.",
            "Hasat bereketlidir.",
            "Kış çetin geçecektir.",
            "İvedi tahsisat rica olunur.",
            "Mahalline memur gönderilmiştir.",
            "Evrak tanzim edilmektedir.",
            "Müsaadenizle arz ederim.",
            "Bilvesile hürmetlerimi sunarım.",
            "Teftiş neticesi bilahare arz edilecektir.",
            "Depo sayımı sürmektedir.",
            "Fırınlar gece de çalışmaktadır.",
            "Yol inşaatı plan dahilindedir.",
            "Vergi tahsilâtı hızlanmıştır.",
            "Şikâyetler münferittir.",
            "Basında çıkanlar asılsızdır.",
            "Sınırda hareketlilik vardır.",
            "Garnizon tetiktedir.",
        };

        /// <summary>Seal a message into the permanent archive: turn, desk, name, words.</summary>
        public static void Seal(GameState g, string desk, string name, string text)
            => g.TelegraphArchive.Add($"{g.Turn}|{desk}|{name}|{text}");

        /// <summary>
        /// The governor's one open announcement per turn. Everyone reads it; it goes on the
        /// record like everything else.
        /// </summary>
        public static bool Ferman(GameState g, string text, out string why)
        {
            why = "";
            if (string.IsNullOrWhiteSpace(text)) { why = "Boş ferman yayınlanmaz."; return false; }
            if (g.FermanTurn == g.Turn) { why = "Bu tur bir ferman yayınlandı."; return false; }

            g.FermanTurn = g.Turn;
            g.Telegrams.Add($"VALİ|Ferman|{text.Trim()}");
            Seal(g, "VALİ", "Ferman", text.Trim());
            return true;
        }

        /// <summary>The channel between two desks, if it has been opened.</summary>
        public static GameState.PrivateChannel ChannelBetween(GameState g, Domain a, Domain b)
        {
            foreach (var c in g.Channels)
                if ((c.A == a && c.B == b) || (c.A == b && c.B == a)) return c;
            return null;
        }

        public static GameState.PrivateChannel OpenChannel(GameState g, Domain a, Domain b)
        {
            var existing = ChannelBetween(g, a, b);
            if (existing != null) return existing;
            var c = new GameState.PrivateChannel { A = a, B = b, OpenedTurn = g.Turn };
            g.Channels.Add(c);
            return c;
        }

        /// <summary>
        /// Bots find each other. Two bot-held desks whose objectives share a tag open a
        /// channel and say so — in the guarded language of people who know the record may
        /// open someday. Runs once per pair; single player has no bot seats and never sees
        /// any of this.
        /// </summary>
        public static void BotChannels(GameState g)
        {
            for (int a = 0; a < 5; a++)
            for (int b = a + 1; b < 5; b++)
            {
                if (HotSeat.KindOf((Domain)a) != SeatKind.Bot) continue;
                if (HotSeat.KindOf((Domain)b) != SeatKind.Bot) continue;
                if (ChannelBetween(g, (Domain)a, (Domain)b) != null) continue;

                var oa = Objectives.Get(g.ObjectiveOf[a]);
                var ob = Objectives.Get(g.ObjectiveOf[b]);
                bool aligned = oa != null && ob != null && oa.Tag == ob.Tag;

                var ch = OpenChannel(g, (Domain)a, (Domain)b);
                string na = g.Cabinet.Of((Domain)a)?.Name ?? Ministers.DomainNames[a];
                string nb = g.Cabinet.Of((Domain)b)?.Name ?? Ministers.DomainNames[b];
                ch.Lines.Add($"{na}: " + (aligned
                    ? "Menfaatlerimiz aynı istikamettedir. Raporları uyumlu tutalım."
                    : "Bir tanışıklık fena olmaz. İcabında birbirimizi kollarız."));
                ch.Lines.Add($"{nb}: Mutabıkız. Bu yazışma aramızda kalsın.");
                Seal(g, $"ÖZEL·{Ministers.DomainNames[a]}·{Ministers.DomainNames[b]}", na, ch.Lines[0]);
                Seal(g, $"ÖZEL·{Ministers.DomainNames[a]}·{Ministers.DomainNames[b]}", nb, ch.Lines[1]);
            }
        }
    }
}
