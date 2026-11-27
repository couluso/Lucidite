using System.Text;
using UnityEngine;

namespace Lucidite
{
    /// <summary>Carte possédée en partie : données + amélioration éventuelle.</summary>
    public class CardInstance
    {
        static int s_nextId = 1;

        public readonly int uid;
        public CardData data;
        public bool upgraded;

        /// <summary>Classe de la carte (choisie à sa création : deck de départ, boutique).</summary>
        public CardSuit suit;

        public CardInstance(CardData data, CardSuit suit = CardSuit.Aucune)
        {
            uid = s_nextId++;
            this.data = data;
            this.suit = data && data.type == CardType.Crise ? CardSuit.Aucune : suit;
        }

        /// <summary>Niveaux des classes de la joueuse (pilules), pour toute la partie. Indexé par CardSuit.</summary>
        public static readonly int[] SuitLevels = new int[5];
        /// <summary>Bonus par niveau (GameConfig.suitLevelBonus).</summary>
        public static int LevelBonus = 2;
        public static void ResetLevels() { for (int i = 0; i < SuitLevels.Length; i++) SuitLevels[i] = 0; }
        public int Level => suit == CardSuit.Aucune ? 0 : SuitLevels[(int)suit];
        int Lv => Level * LevelBonus;

        /// <summary>Sang accumulé sur la carte (projections qui l'atteignent sur la table), de 0 à 1.</summary>
        public float blood;
        /// <summary>Totalement recouverte de sang : effets renforcés.</summary>
        public bool Scarlet => blood >= ScarletThreshold && !IsCrisis;
        /// <summary>Part de la carte couverte de sang pour qu'elle devienne Écarlate (elle continue de se tacher ensuite).</summary>
        public const float ScarletThreshold = 0.7f;
        public const float ScarletMultiplier = 1.35f;

        /// <summary>Ajoute du sang. Renvoie vrai si la carte vient de devenir Écarlate.</summary>
        public bool AddBlood(float amount)
        {
            if (amount <= 0f || blood >= 1f) return false;
            bool was = Scarlet;
            blood = Mathf.Min(1f, blood + amount);
            return !was && Scarlet;
        }

        int Up(int v) => upgraded ? v : 0;
        int Sc(int v) => Scarlet && v > 0 ? Mathf.CeilToInt(v * ScarletMultiplier) : v;   // Écarlate : dégâts / bouclier
        int Sc1(int v) => Scarlet && v > 0 ? v + 1 : v;                                   // Écarlate : statuts, soin

        public string Name => data.cardName + (upgraded ? "+" : "") + (Scarlet ? " écarlate" : "");
        public CardType Type => data.type;
        public bool IsCrisis => data.type == CardType.Crise;
        public bool Unplayable => data.unplayable;
        public int Cost => Mathf.Max(0, data.cost + Up(data.upCost));
        public int Damage => Sc(data.damage > 0 ? Mathf.Max(0, data.damage + Up(data.upDamage) + Lv) : 0);
        public int Hits => Mathf.Max(1, data.hits);
        public int Block => Sc(data.block > 0 ? Mathf.Max(0, data.block + Up(data.upBlock) + Lv) : 0);
        public int Draw => Mathf.Max(0, data.draw + Up(data.upDraw));
        public int EnergyGain => data.energyGain;
        public int Heal => Sc1(data.heal > 0 ? data.heal + Up(data.upMagic) + Lv : 0);
        public int Weak => Sc1(data.applyWeak > 0 ? data.applyWeak + Up(data.upMagic) : 0);
        public int Vulnerable => Sc1(data.applyVulnerable > 0 ? data.applyVulnerable + Up(data.upMagic) : 0);
        public int Strength => Sc1(data.strengthGain > 0 ? data.strengthGain + Up(data.upMagic) : 0);
        public int Blood => data.bloodGain;
        public int SpecialValue => data.specialValue + Up(data.upMagic);
        public bool Exhaust => data.exhaust && !(upgraded && data.upRemovesExhaust);
        public bool Ethereal => data.ethereal;

        /// <summary>Texture de face (design des cartes) : version + si elle diffère.</summary>
        public Texture2D Face => upgraded && data.faceUpgraded ? data.faceUpgraded : data.face;

        public bool CanUpgrade => !upgraded && !IsCrisis;
        public bool CanTransform => data.transformsInto != null;

        public void Upgrade()
        {
            if (!CanUpgrade) return;
            if (data.evolvesInto != null) { data = data.evolvesInto; upgraded = true; }
            else upgraded = true;
        }

        public void Transform()
        {
            if (data.transformsInto == null) return;
            data = data.transformsInto;
            upgraded = false;
        }

        // ------------------------------------------------------------------ Texte

        /// <summary>Texte de règles. Avec un contexte de combat, les valeurs affichent les modificateurs (Force, Faiblesse...).</summary>
        public string Describe(Fighter self = null, Fighter target = null)
        {
            var sb = new StringBuilder();
            if (Scarlet) sb.AppendLine("<color=#C0303A><b>Écarlate</b></color> : effets renforcés.");
            if (data.special == CardSpecial.Joker) sb.AppendLine("Compte comme une carte de chaque classe pour la Résonance.");
            if (Unplayable) sb.AppendLine("<b>Injouable.</b>");
            if (Ethereal) sb.AppendLine("<b>Éphémère.</b>");

            int baseDmg = Damage;
            bool lowBonus = data.special == CardSpecial.BonusSiLuciditeBasse;
            int classDmg = self != null && suit != CardSuit.Aucune ? ItemData.ClassDamage * self.RelicPower(ItemEffect.DegatsClasse, (int)suit) : 0;
            if (data.special == CardSpecial.DegatsSelonBouclier)
            {
                sb.AppendLine(self != null
                    ? $"Inflige des dégâts égaux à ton Bouclier ({Num(CombatRules.AttackDamage(self.block, self, target), self.block)})."
                    : "Inflige des dégâts égaux à ton Bouclier.");
            }
            else if (baseDmg > 0)
            {
                int shown = self != null ? CombatRules.AttackDamage(baseDmg + classDmg + (lowBonus && self.IsLowLucidity ? SpecialValue : 0), self, target) : baseDmg;
                string n = Num(shown, baseDmg);
                sb.AppendLine(Hits > 1 ? $"Inflige {n} dégâts {Hits} fois." : $"Inflige {n} dégâts.");
            }
            if (Block > 0)
            {
                int shown = Block + (lowBonus && self != null && self.IsLowLucidity ? SpecialValue : 0);
                sb.AppendLine($"Gagne {Num(shown, Block)} Bouclier.");
            }
            if (lowBonus) sb.AppendLine($"Si ta Lucidité ≤ 50 % : +{SpecialValue}.");
            if (data.special == CardSpecial.DoublerBouclier) sb.AppendLine("Double ton Bouclier.");
            if (data.special == CardSpecial.DoubleSiEcarlate) sb.AppendLine(Scarlet ? "<color=#C0303A>Écarlate : effet doublé.</color>" : "Doublé si la carte est Écarlate.");
            if (data.special == CardSpecial.CoupsParClasse) sb.AppendLine("+1 coup par carte de la même classe déjà jouée ce tour.");
            if (data.special == CardSpecial.BouclierParClasseEnMain) sb.AppendLine($"+{SpecialValue} Bouclier par carte de la même classe gardée en main.");
            if (data.special == CardSpecial.SaignementAdverseTour) sb.AppendLine("Ce tour, elle saigne deux fois plus.");
            if (data.special == CardSpecial.SaignementReduitTour) sb.AppendLine("Ce tour, tu saignes deux fois moins.");
            if (data.special == CardSpecial.SoinSelonSang) sb.AppendLine($"Récupère {SpecialValue} Lucidité, +1 par 10 % de sang sur cette carte ({HealFromBlood}).");
            if (data.special == CardSpecial.PerdreLucidite) sb.AppendLine($"Perds {SpecialValue} Lucidité.");
            if (data.special == CardSpecial.PurgerCrisesEnMain) sb.AppendLine("Épuise les Crises de ta main ; tour suivant, pioche autant de cartes.");
            if (Vulnerable > 0) sb.AppendLine($"Applique {Vulnerable} Vulnérable.");
            if (Weak > 0) sb.AppendLine($"Applique {Weak} Faiblesse.");
            if (Strength > 0) sb.AppendLine($"Gagne {Strength} Force.");
            if (Draw > 0) sb.AppendLine(Draw > 1 ? $"Tour suivant : pioche {Draw} cartes de plus." : "Tour suivant : pioche 1 carte de plus.");
            if (EnergyGain > 0) sb.AppendLine($"Tour suivant : +{EnergyGain} énergie.");
            if (Heal > 0) sb.AppendLine($"Récupère {Heal} Lucidité.");
            if (Blood > 0) sb.AppendLine($"Gagne {Blood} gouttes de sang.");
            if (data.loseLucidityIfHeld > 0) sb.AppendLine($"Fin du tour, si en main : perds {data.loseLucidityIfHeld} Lucidité.");
            if (data.energyLossOnDraw > 0) sb.AppendLine($"Quand elle est piochée : perds {data.energyLossOnDraw} énergie.");
            if (Exhaust) sb.AppendLine("<b>Épuiser.</b>");
            return sb.ToString().TrimEnd();
        }

        /// <summary>Soin de « Transfusion » : valeur de base + 1 par tranche de 10 % de sang sur la carte.</summary>
        public int HealFromBlood => SpecialValue + Mathf.FloorToInt(blood * 10f + 0.001f) + Lv;

        public static string Num(int shown, int baseValue)
        {
            if (shown > baseValue) return $"<color=#7FBF6A>{shown}</color>";
            if (shown < baseValue) return $"<color=#D9534F>{shown}</color>";
            return shown.ToString();
        }
    }
}
