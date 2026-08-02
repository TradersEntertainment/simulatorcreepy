// Creditors, and the one rule that makes foreign debt interesting: the collateral is a law slot.
//
// A loan does not buy you out of politics. It hands one of your four slots to somebody else and
// you cannot take it back while the debt stands, so every credit line permanently shrinks your
// capacity to govern and drags the economy axis towards SERMAYE whether you wanted that or not.
// Three loans and half the law book belongs to a bank in another city.
//
// This is why the design insists escaping a crisis is impossible — only choosing which axis you
// escape along. Money is always available. What it costs is the ability to make law.

using UnityEngine;

namespace Mesruiyet.Core
{
    public sealed class CreditorDef
    {
        public string Id;
        public string Name;
        public string Blurb;

        /// <summary>Cash paid on signature.</summary>
        public int Principal;
        /// <summary>Interest charged every turn while the debt stands, as a fraction of principal.</summary>
        public float InterestRate;

        /// <summary>The law they insist on. It occupies a slot and cannot be repealed unpaid.</summary>
        public string DemandedLaw;

        /// <summary>What settling the debt costs, once you can afford to think about it.</summary>
        public int Settlement => Mathf.RoundToInt(Principal * 1.35f);
    }

    public static class Creditors
    {
        public static readonly CreditorDef[] All =
        {
            new CreditorDef
            {
                Id = "mersa_bankasi", Name = "Mersa Ticaret Bankası",
                Blurb = "Komşunun bankası. Parası ucuz, şartı ağır: limanın işletmesi " +
                        "borç kapanana kadar onların kanununa bağlanır.",
                Principal = 900, InterestRate = 0.035f,
                DemandedLaw = "liman_imtiyazi",
            },
            new CreditorDef
            {
                Id = "tuccar_loncasi", Name = "Tüccar Loncası Sandığı",
                Blurb = "Şehrin kendi tüccarları. Yakın, hızlı, ve karşılığında mülkiyetin " +
                        "dokunulmazlığını kanunla güvence altına istiyorlar.",
                Principal = 650, InterestRate = 0.028f,
                DemandedLaw = "mulkiyet",
            },
            new CreditorDef
            {
                Id = "yabanci_konsorsiyum", Name = "Yabancı Konsorsiyum",
                Blurb = "Adını duymadığınız bir konsorsiyum. En büyük parayı verirler; " +
                        "karşılığında iş bırakma eyleminin kanunla kaldırılmasını isterler.",
                Principal = 1400, InterestRate = 0.045f,
                DemandedLaw = "grev_yasagi",
            },
        };

        public static CreditorDef Get(string id)
        {
            foreach (var c in All)
                if (c.Id == id) return c;
            return null;
        }
    }
}
