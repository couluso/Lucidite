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

        public int RelicValue(ItemEffect effect)
        {
            int total = 0;
            foreach (var r in relics) if (r.effect == effect) total += r.value;
            return total;
        }

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
            if (attacker != null && attacker.weak > 0) d *= 0.75f;
            if (target != null && target.vulnerable > 0) d *= 1.5f;
            return Mathf.Max(0, Mathf.FloorToInt(d));
        }
    }
}
