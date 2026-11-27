using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    public class OwnedItem
    {
        public ItemData data;
        public OwnedItem(ItemData d) { data = d; }
    }

    /// <summary>Un combattant : l'étudiante ou sa Dépression.</summary>
    public class Fighter
    {
        public readonly Side side;
        public readonly string displayName;

        // Bougies = manches restantes
        public int candles;
        public int maxCandles;

        // Combat (remis à zéro à chaque manche)
        public int hp;
        public int maxHp;
        public int block;
        public int strength;
        public int weak;        // tours restants
        public int vulnerable;  // tours restants

        // Joueuse uniquement
        public int energy;
        public int maxEnergy;
        public readonly Deck deck;
        public readonly List<CardInstance> hand = new List<CardInstance>();
        public readonly List<ItemData> relics = new List<ItemData>();
        public readonly List<OwnedItem> items = new List<OwnedItem>();
        /// <summary>Relique voilée par le Voile noir de l'adversaire : sans effet pendant la manche.</summary>
        public ItemData veiled;
        /// <summary>Relique magnifiée par le Reliquaire (effet doublé).</summary>
        public ItemData buffTarget;
        /// <summary>A déjà reçu un coup ce tour (relique « premier coup réduit »).</summary>
        public bool hitThisTurn;

        public Fighter(Side side, string name, int candles, int seed)
        {
            this.side = side;
            displayName = name;
            this.candles = maxCandles = candles;
            deck = new Deck(seed);
        }

        public bool IsDead => hp <= 0;
        public bool IsOut => candles <= 0;
        public bool IsLowLucidity => hp * 2 <= maxHp;

        public void ResetForRound(int newMaxHp)
        {
            maxHp = newMaxHp;
            hp = maxHp;
            block = 0;
            strength = 0;
            weak = 0;
            vulnerable = 0;
        }

        /// <summary>Puissance d'une relique : 0 si voilée, 2 si magnifiée par un Reliquaire actif, 1 sinon.</summary>
        public int Power(ItemData r)
        {
            if (!r || r == veiled || !relics.Contains(r)) return 0;
            if (r == buffTarget && r.effect != ItemEffect.Reliquaire)
                foreach (var x in relics) if (x.effect == ItemEffect.Reliquaire && x != veiled) return 2;
            return 1;
        }

        public bool IsBuffed(ItemData r) => Power(r) >= 2;

        public int RelicValue(ItemEffect effect)
        {
            int total = 0;
            foreach (var r in relics) if (r.effect == effect) total += r.value * Power(r);
            return total;
        }

        /// <summary>Somme des puissances des reliques de cet effet (et de cette valeur si précisée).</summary>
        public int RelicPower(ItemEffect effect, int value = int.MinValue)
        {
            int total = 0;
            foreach (var r in relics) if (r.effect == effect && (value == int.MinValue || r.value == value)) total += Power(r);
            return total;
        }

        public bool HasRelic(ItemEffect effect, int value) => RelicPower(effect, value) > 0;
        public bool HasRelic(ItemEffect effect) => RelicPower(effect) > 0;

        /// <summary>Encaisse des dégâts : le bouclier absorbe d'abord. Renvoie la Lucidité réellement perdue.</summary>
        public int TakeDamage(int amount, out int blocked)
        {
            blocked = Mathf.Min(block, amount);
            block -= blocked;
            int lost = Mathf.Min(hp, amount - blocked);
            hp -= lost;
            return lost;
        }

        public int LoseHp(int amount)
        {
            int lost = Mathf.Min(hp, Mathf.Max(0, amount));
            hp -= lost;
            return lost;
        }

        public int Heal(int amount)
        {
            int before = hp;
            hp = Mathf.Min(maxHp, hp + Mathf.Max(0, amount));
            return hp - before;
        }

        public void TickStatuses()
        {
            if (weak > 0) weak--;
            if (vulnerable > 0) vulnerable--;
        }

        public string StatusText()
        {
            var parts = new List<string>();
            if (strength != 0) parts.Add($"Force {strength}");
            if (weak > 0) parts.Add($"Faiblesse {weak}");
            if (vulnerable > 0) parts.Add($"Vulnérable {vulnerable}");
            return string.Join("  ·  ", parts);
        }
    }

    /// <summary>Calculs de combat (dégâts, Force, Faiblesse, Vulnérable).</summary>
    public static class CombatRules
    {
        public static int AttackDamage(int baseDamage, Fighter attacker, Fighter target)
        {
            float d = baseDamage + (attacker != null ? attacker.strength : 0);
            // Relique « Crâne » : plus de dégâts quand on est au bord du gouffre
            if (attacker != null && baseDamage > 0 && attacker.IsLowLucidity) d += attacker.RelicValue(ItemEffect.DegatsSiLuciditeBasse);
            if (attacker != null && attacker.weak > 0) d *= 0.75f;
            if (target != null && target.vulnerable > 0) d *= 1.5f;
            return Mathf.Max(0, Mathf.FloorToInt(d));
        }
    }
}
