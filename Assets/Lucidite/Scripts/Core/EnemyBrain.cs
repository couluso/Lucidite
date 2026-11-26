using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>Intention résolue pour une carte de la Dépression (valeurs déjà mises à l'échelle).</summary>
    public class EnemyIntent
    {
        public EnemyMoveData move;
        public int damagePerHit;  // avant Force/Faiblesse/Vulnérable
        public int hits;
        public int block;
        public int strength;
        public int weak;
        public int vulnerable;
        public int crisis;

        public int ShownDamage(Fighter enemy, Fighter player) => CombatRules.AttackDamage(damagePerHit, enemy, player);

        public string Describe(Fighter enemy, Fighter player)
        {
            var lines = new List<string>();
            if (move && move.cost > 0) lines.Add($"<color=#8E8A86>Coût : {move.cost} énergie</color>");
            if (damagePerHit > 0)
            {
                int d = ShownDamage(enemy, player);
                lines.Add(hits > 1 ? $"Inflige {d} dégâts {hits} fois." : $"Inflige {d} dégâts.");
            }
            if (block > 0) lines.Add($"Gagne {block} Bouclier.");
            if (strength > 0) lines.Add($"Gagne {strength} Force.");
            if (weak > 0) lines.Add($"T'applique {weak} Faiblesse.");
            if (vulnerable > 0) lines.Add($"T'applique {vulnerable} Vulnérable.");
            if (crisis > 0) lines.Add($"Glisse {crisis} Crise(s) dans ton deck.");
            return string.Join("\n", lines);
        }

        public int TotalDamage(Fighter enemy, Fighter player) => damagePerHit > 0 ? ShownDamage(enemy, player) * hits : 0;

        /// <summary>Résumé très court (au-dessus de sa tête).</summary>
        public string Short(Fighter enemy, Fighter player)
        {
            if (damagePerHit > 0)
            {
                int d = ShownDamage(enemy, player);
                return hits > 1 ? $"{d}×{hits}" : d.ToString();
            }
            if (block > 0) return $"+{block}";
            return "";
        }

        /// <summary>Résumé de tout son tour : dégâts cumulés, bouclier, nombre de malédictions.</summary>
        public static string Summary(IReadOnlyList<EnemyIntent> plan, Fighter enemy, Fighter player)
        {
            if (plan == null || plan.Count == 0) return "";
            if (plan.Count == 1) return plan[0].Short(enemy, player);
            int dmg = 0, block = 0, curses = 0;
            foreach (var it in plan)
            {
                dmg += it.TotalDamage(enemy, player);
                block += it.block;
                if (it.weak > 0 || it.vulnerable > 0 || it.crisis > 0) curses++;
            }
            var parts = new List<string>();
            if (dmg > 0) parts.Add(dmg.ToString());
            if (block > 0) parts.Add($"+{block}");
            if (curses > 0) parts.Add("+malus");
            return $"{string.Join(" ", parts)}  <size=70%>({plan.Count} cartes)</size>";
        }

        /// <summary>L'intention la plus menaçante (pour l'icône).</summary>
        public static EnemyIntent Main(IReadOnlyList<EnemyIntent> plan)
        {
            if (plan == null || plan.Count == 0) return null;
            foreach (var it in plan) if (it.damagePerHit > 0) return it;
            foreach (var it in plan) if (it.weak > 0 || it.vulnerable > 0 || it.crisis > 0) return it;
            return plan[0];
        }
    }

    /// <summary>
    /// Ce que la Dépression a "acheté" entre les manches. Ses renforcements durent toute la partie.
    /// </summary>
    public class EnemyProgress
    {
        public int bonusMaxHp;
        public int startStrength;
        public int bonusEnergy;
        public int startBlock;
        public readonly Dictionary<EnemyMoveData, float> moveBonus = new Dictionary<EnemyMoveData, float>();
        public readonly HashSet<EnemyMoveData> unlocked = new HashSet<EnemyMoveData>();
        public readonly List<string> log = new List<string>();
    }

    /// <summary>
    /// Choisit les actions de la Dépression : à chaque tour elle dépense son énergie en cartes
    /// (tirage pondéré, jamais trois fois la même d'affilée).
    /// </summary>
    public class EnemyBrain
    {
        readonly List<EnemyMoveData> _moves;
        readonly List<EnemyMoveData> _history = new List<EnemyMoveData>();
        public readonly EnemyProgress progress = new EnemyProgress();

        public EnemyBrain(List<EnemyMoveData> moves) { _moves = moves; }

        public void ResetForRound() => _history.Clear();

        bool Available(EnemyMoveData m, int round) =>
            m != null && (m.shopOnly ? progress.unlocked.Contains(m) : m.minRound <= round);

        /// <summary>Planifie un tour : autant de cartes que son énergie le permet (max <paramref name="maxCards"/>).</summary>
        public List<EnemyIntent> PlanTurn(int round, float damageScalePerRound, int energy, int maxCards)
        {
            var plan = new List<EnemyIntent>();
            int remaining = Mathf.Max(1, energy);
            bool attacked = false;
            for (int n = 0; n < maxCards; n++)
            {
                var pool = new List<EnemyMoveData>();
                foreach (var m in _moves)
                {
                    if (!Available(m, round) || Mathf.Max(0, m.cost) > remaining) continue;
                    int h = _history.Count;
                    bool repeated = h >= 2 && _history[h - 1] == m && _history[h - 2] == m;
                    bool sameAsLast = h >= 1 && _history[h - 1] == m;
                    if (repeated || (sameAsLast && m.damage == 0)) continue;
                    pool.Add(m);
                }
                if (pool.Count == 0) break;

                float total = 0f;
                foreach (var m in pool) total += Weight(m, attacked, n);
                float r = Random.value * total;
                var pick = pool[pool.Count - 1];
                foreach (var m in pool)
                {
                    r -= Weight(m, attacked, n);
                    if (r <= 0f) { pick = m; break; }
                }
                _history.Add(pick);
                plan.Add(Resolve(pick, round, damageScalePerRound));
                attacked |= pick.damage > 0;
                remaining -= Mathf.Max(0, pick.cost);
                if (remaining <= 0) break;
            }
            if (plan.Count == 0 && _moves.Count > 0)
            {
                // Toujours au moins une carte : la moins chère disponible
                EnemyMoveData cheapest = null;
                foreach (var m in _moves) if (Available(m, round) && (!cheapest || m.cost < cheapest.cost)) cheapest = m;
                if (cheapest) plan.Add(Resolve(cheapest, round, damageScalePerRound));
            }
            return plan;
        }

        // Le premier choix est libre ; ensuite elle préfère varier (une attaque + autre chose)
        static float Weight(EnemyMoveData m, bool attacked, int index)
        {
            float w = Mathf.Max(0.01f, m.weight);
            if (index > 0 && attacked && m.damage > 0) w *= 0.6f;
            if (index > 0 && !attacked && m.damage > 0) w *= 1.5f;
            return w;
        }

        EnemyIntent Resolve(EnemyMoveData pick, int round, float damageScalePerRound)
        {
            float scale = 1f + damageScalePerRound * (round - 1);
            if (progress.moveBonus.TryGetValue(pick, out var bonus)) scale *= 1f + bonus;
            return new EnemyIntent
            {
                move = pick,
                damagePerHit = Mathf.RoundToInt(pick.damage * scale),
                hits = Mathf.Max(1, pick.hits),
                block = Mathf.RoundToInt(pick.block * scale),
                strength = pick.strengthGain,
                weak = pick.applyWeak,
                vulnerable = pick.applyVulnerable,
                crisis = pick.crisisCount
            };
        }

        // ================================================================== Sa "boutique"

        /// <summary>
        /// Entre deux manches, la Dépression se renforce comme si elle passait elle aussi par la boutique.
        /// Renvoie la liste de ses achats (pour l'annoncer à la joueuse).
        /// </summary>
        public List<string> Shop(int purchases, int round)
        {
            var bought = new List<string>();
            for (int i = 0; i < purchases; i++)
            {
                var options = new List<System.Func<string>>
                {
                    () => { progress.bonusMaxHp += 10; return "Insomnie chronique : +10 Emprise maximale."; },
                    () => { progress.startStrength += 1; return "Rancune : commence chaque manche avec +1 Force."; },
                    () => { progress.startBlock += 8; return "Carapace d'apathie : commence chaque manche avec 8 Bouclier."; }
                };
                if (progress.bonusEnergy < 2 && round >= 2)
                    options.Add(() => { progress.bonusEnergy += 1; return "Ruminations : +1 énergie par tour (elle joue plus de cartes)."; });

                // Affûter une de ses cartes
                var sharpen = new List<EnemyMoveData>();
                foreach (var m in _moves) if (Available(m, round + 1) && m.damage > 0) sharpen.Add(m);
                if (sharpen.Count > 0)
                    options.Add(() =>
                    {
                        var m = sharpen[Random.Range(0, sharpen.Count)];
                        progress.moveBonus.TryGetValue(m, out var b);
                        progress.moveBonus[m] = b + 0.3f;
                        return $"Affûte « {m.moveName} » : +30 % de dégâts.";
                    });

                // Nouvelle obsession : une carte exclusive
                var locked = new List<EnemyMoveData>();
                foreach (var m in _moves) if (m && m.shopOnly && !progress.unlocked.Contains(m) && m.minRound <= round + 1) locked.Add(m);
                if (locked.Count > 0)
                {
                    options.Add(() =>
                    {
                        var m = locked[Random.Range(0, locked.Count)];
                        progress.unlocked.Add(m);
                        return $"Nouvelle obsession : « {m.moveName} » rejoint ses cartes.";
                    });
                    options.Add(options[options.Count - 1]); // plus de chances d'obtenir une nouvelle carte
                }

                var text = options[Random.Range(0, options.Count)]();
                bought.Add(text);
                progress.log.Add(text);
            }
            return bought;
        }
    }
}
