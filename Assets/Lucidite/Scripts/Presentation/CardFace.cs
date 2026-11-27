using System.Collections.Generic;

namespace Lucidite
{
    /// <summary>
    /// Ce qu'affiche le design des cartes en plus du texte : la valeur (rond en bas à droite)
    /// et les petites icônes de bonus (en bas à gauche). Partagé par la carte 3D et la carte d'interface.
    /// </summary>
    public static class CardFace
    {
        public const int MaxStats = 6;

        /// <summary>Valeur principale : dégâts (« 6×3 » pour plusieurs coups), sinon bouclier. Vide si la carte n'en a pas.</summary>
        public static string Value(CardInstance c, Fighter self = null, Fighter target = null)
        {
            var d = c.data;
            int low = d.special == CardSpecial.BonusSiLuciditeBasse && self != null && self.IsLowLucidity ? c.SpecialValue : 0;
            if (d.special == CardSpecial.DegatsSelonBouclier)
                return self != null ? CardInstance.Num(CombatRules.AttackDamage(self.block, self, target), self.block) : "=";
            if (c.Damage > 0)
            {
                int shown = self != null ? CombatRules.AttackDamage(c.Damage + low, self, target) : c.Damage;
                string n = CardInstance.Num(shown, c.Damage);
                return c.Hits > 1 ? $"{n}×{c.Hits}" : n;
            }
            if (c.Block > 0) return CardInstance.Num(c.Block + low, c.Block);
            if (d.special == CardSpecial.DoublerBouclier) return "×2";
            return "";
        }

        public static string Value(EnemyIntent it, Fighter enemy, Fighter player)
        {
            if (it.damagePerHit > 0)
            {
                string n = enemy != null ? CardInstance.Num(it.ShownDamage(enemy, player), it.damagePerHit) : it.damagePerHit.ToString();
                return it.hits > 1 ? $"{n}×{it.hits}" : n;
            }
            return it.block > 0 ? it.block.ToString() : "";
        }

        /// <summary>Effets secondaires de la carte (ce qui n'est pas déjà la valeur principale).</summary>
        public static void Stats(CardInstance c, List<CardStat> into)
        {
            into.Clear();
            var d = c.data;
            bool damageMain = d.special == CardSpecial.DegatsSelonBouclier || c.Damage > 0;
            if (d.special == CardSpecial.DegatsSelonBouclier || d.special == CardSpecial.DoublerBouclier || (damageMain && c.Block > 0))
                into.Add(CardStat.Bouclier);
            if (c.Strength > 0) into.Add(CardStat.Force);
            if (c.Weak > 0) into.Add(CardStat.Faiblesse);
            if (c.Vulnerable > 0) into.Add(CardStat.Vulnerable);
            if (c.Draw > 0) into.Add(CardStat.Pioche);
            if (c.EnergyGain > 0) into.Add(CardStat.Energie);
            if (c.Heal > 0) into.Add(CardStat.Soin);
            if (c.Blood > 0) into.Add(CardStat.Sang);
            if (d.special == CardSpecial.BonusSiLuciditeBasse) into.Add(CardStat.LuciditeBasse);
            if (d.special == CardSpecial.PurgerCrisesEnMain) into.Add(CardStat.Purge);
            if (d.loseLucidityIfHeld > 0) into.Add(CardStat.Retenue);
            if (d.energyLossOnDraw > 0) into.Add(CardStat.Ponction);
            if (c.Ethereal) into.Add(CardStat.Ephemere);
            if (c.Exhaust) into.Add(CardStat.Epuiser);
            if (into.Count > MaxStats) into.RemoveRange(MaxStats, into.Count - MaxStats);
        }

        public static void Stats(EnemyIntent it, List<CardStat> into)
        {
            into.Clear();
            if (it.damagePerHit > 0 && it.block > 0) into.Add(CardStat.Bouclier);
            if (it.strength > 0) into.Add(CardStat.Force);
            if (it.weak > 0) into.Add(CardStat.Faiblesse);
            if (it.vulnerable > 0) into.Add(CardStat.Vulnerable);
            if (it.crisis > 0) into.Add(CardStat.Crise);
        }
    }
}
