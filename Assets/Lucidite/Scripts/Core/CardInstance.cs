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

        public CardInstance(CardData data)
        {
            uid = s_nextId++;
            this.data = data;
        }

        int Up(int v) => upgraded ? v : 0;

        public string Name => data.cardName + (upgraded ? "+" : "");
        public CardType Type => data.type;
        public bool IsCrisis => data.type == CardType.Crise;
        public bool Unplayable => data.unplayable;
        public int Cost => Mathf.Max(0, data.cost + Up(data.upCost));
        public int Damage => Mathf.Max(0, data.damage + Up(data.upDamage));
        public int Hits => Mathf.Max(1, data.hits);
        public int Block => Mathf.Max(0, data.block + Up(data.upBlock));
        public int Draw => Mathf.Max(0, data.draw + Up(data.upDraw));
        public int EnergyGain => data.energyGain;
        public int Heal => data.heal > 0 ? data.heal + Up(data.upMagic) : 0;
        public int Weak => data.applyWeak > 0 ? data.applyWeak + Up(data.upMagic) : 0;
        public int Vulnerable => data.applyVulnerable > 0 ? data.applyVulnerable + Up(data.upMagic) : 0;
        public int Strength => data.strengthGain > 0 ? data.strengthGain + Up(data.upMagic) : 0;
        public int Blood => data.bloodGain;
        public int SpecialValue => data.specialValue + Up(data.upMagic);
        public bool Exhaust => data.exhaust && !(upgraded && data.upRemovesExhaust);
        public bool Ethereal => data.ethereal;

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
            if (Unplayable) sb.AppendLine("<b>Injouable.</b>");
            if (Ethereal) sb.AppendLine("<b>Éphémère.</b>");

            int baseDmg = Damage;
            bool lowBonus = data.special == CardSpecial.BonusSiLuciditeBasse;
            if (data.special == CardSpecial.DegatsSelonBouclier)
            {
                sb.AppendLine(self != null
                    ? $"Inflige des dégâts égaux à ton Bouclier ({Num(CombatRules.AttackDamage(self.block, self, target), self.block)})."
                    : "Inflige des dégâts égaux à ton Bouclier.");
            }
            else if (baseDmg > 0)
            {
                int shown = self != null ? CombatRules.AttackDamage(baseDmg + (lowBonus && self.IsLowLucidity ? SpecialValue : 0), self, target) : baseDmg;
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

        static string Num(int shown, int baseValue)
        {
            if (shown > baseValue) return $"<color=#7FBF6A>{shown}</color>";
            if (shown < baseValue) return $"<color=#D9534F>{shown}</color>";
            return shown.ToString();
        }
    }
}
