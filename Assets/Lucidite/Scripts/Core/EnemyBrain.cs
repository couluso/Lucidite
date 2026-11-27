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
        /// <summary>Ses gouttes de sang : gagnées comme la joueuse, dépensées en secret dans sa "boutique".</summary>
        public int blood;
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

        // ================================================================== Ses pilules

        public const int MaxRelics = 4, MaxPills = 3;

        /// <summary>
        /// Au début du tour de la joueuse, elle avale au plus une pilule : celle qui lui sert le plus maintenant
        /// (envie de 0 à 1 selon la situation), si cette envie dépasse un seuil. Le seuil baisse quand elle en a plusieurs
        /// en réserve et à mesure que la manche avance : elle ne les garde pas indéfiniment.
        /// </summary>
        public OwnedItem ChoosePill(Fighter self, Fighter foe, List<EnemyIntent> plan, int turn)
        {
            if (self == null || self.items.Count == 0) return null;
            int dmg = 0, firstDmg = 0, lastDmg = 0, hits = 0;
            if (plan != null)
                foreach (var it in plan) { dmg += it.TotalDamage(self, foe); if (it.damagePerHit > 0) hits += it.hits; }
            if (plan != null && plan.Count >= 2) { firstDmg = plan[0].TotalDamage(self, foe); lastDmg = plan[plan.Count - 1].TotalDamage(self, foe); }
            float missing = self.maxHp > 0 ? 1f - self.hp / (float)self.maxHp : 0f;

            float threshold = 0.55f - 0.1f * (self.items.Count - 1) - 0.05f * Mathf.Max(0, turn - 1);
            OwnedItem best = null;
            float bestScore = 0f;
            foreach (var o in self.items)
            {
                float s;
                switch (o.data.effect)
                {
                    case ItemEffect.Soigner: s = missing * 1.6f; break;
                    case ItemEffect.SaignementReduitTour: s = missing * 0.8f; break;
                    case ItemEffect.SaignementAdverseTour: s = dmg >= 10 ? 0.55f : 0.2f; break;
                    case ItemEffect.GagnerForce: s = hits > 0 ? 0.3f + hits * 0.12f : 0f; break;
                    case ItemEffect.GagnerBouclier: s = 0.35f + (turn >= 2 ? 0.2f : 0f) + missing * 0.3f; break;
                    case ItemEffect.AppliquerVulnerable: s = dmg > 0 ? 0.3f + Mathf.Min(dmg, 20) / 40f : 0.1f; break;
                    case ItemEffect.AppliquerFaiblesse: s = foe.hand.Count >= 4 ? 0.55f : 0.35f; break;
                    case ItemEffect.GagnerEnergie: s = 0.6f; break;
                    case ItemEffect.EnergieAdverseTour: s = foe.hand.Count >= 3 ? 0.6f : 0.2f; break;
                    case ItemEffect.CopierCarte: s = plan != null && plan.Count >= 2 && firstDmg > lastDmg ? 0.5f + (firstDmg - lastDmg) / 20f : 0f; break;
                    case ItemEffect.Voler: s = foe.relics.Count > 0 && self.relics.Count < MaxRelics ? 0.9f : (foe.items.Count > 0 ? 0.5f : 0f); break;
                    case ItemEffect.Purifier: s = (self.weak + self.vulnerable) * 0.3f; break;
                    case ItemEffect.Placebo: s = 0.4f; break;
                    default: s = 0f; break;
                }
                if (s <= 0f) continue;
                s += Random.Range(-0.1f, 0.1f);
                if (s > bestScore) { bestScore = s; best = o; }
            }
            return best != null && bestScore >= threshold ? best : null;
        }

        // ================================================================== Sa "boutique"

        // ------------------------------------------------------------------ Estimation de son jeu

        /// <summary>Tours qu'une manche dure en moyenne (sert à chiffrer les bonus « par tour »).</summary>
        const float TurnsPerRound = 5f;

        /// <summary>Dégâts moyens d'une de ses cartes d'attaque (pondérés par leur fréquence), à cette manche.</summary>
        float AvgAttack(int round, float damageScalePerRound, out float avgHits)
        {
            float wSum = 0f, dmg = 0f, hits = 0f;
            float scale = 1f + damageScalePerRound * (round - 1);
            foreach (var m in _moves)
            {
                if (!Available(m, round) || m.damage <= 0) continue;
                float w = Mathf.Max(0.01f, m.weight);
                progress.moveBonus.TryGetValue(m, out var b);
                wSum += w;
                dmg += w * m.damage * Mathf.Max(1, m.hits) * scale * (1f + b);
                hits += w * Mathf.Max(1, m.hits);
            }
            avgHits = wSum > 0f ? hits / wSum : 1f;
            return wSum > 0f ? dmg / wSum : 6f;
        }

        /// <summary>Part des cartes qu'elle joue qui sont des attaques.</summary>
        float AttackShare(int round)
        {
            float all = 0f, atk = 0f;
            foreach (var m in _moves)
            {
                if (!Available(m, round)) continue;
                float w = Mathf.Max(0.01f, m.weight);
                all += w;
                if (m.damage > 0) atk += w;
            }
            return all > 0f ? atk / all : 0.6f;
        }

        /// <summary>
        /// Valeur (en « points de Lucidité », environ) d'une relique ou d'une pilule pour elle, dans la situation actuelle :
        /// par manche pour une relique (l'appelant multiplie par les manches restantes), pour son unique usage pour une
        /// pilule. 0 = inutile pour elle.
        /// </summary>
        float ItemValue(ItemData i, Fighter self, Fighter foe, int round, float atkPerTurn, float hitsPerTurn, float roundsLeft)
        {
            int v = i.value;
            switch (i.effect)
            {
                // Reliques (effet à chaque manche)
                case ItemEffect.LuciditeMax: return v;
                case ItemEffect.BouclierDebutManche: return v * 0.9f;
                case ItemEffect.ForceDebutManche: return v * hitsPerTurn * TurnsPerRound;
                case ItemEffect.EnergieMax: return atkPerTurn * TurnsPerRound * 0.9f;
                case ItemEffect.EnergiePremierTour: return atkPerTurn * 0.9f;
                case ItemEffect.DegatsSiLuciditeBasse: return v * hitsPerTurn * TurnsPerRound * 0.45f;
                case ItemEffect.PremierCoupReduit: return v * TurnsPerRound * 0.8f;
                case ItemEffect.SoinDebutTour: return v * TurnsPerRound * 0.85f;
                case ItemEffect.Epines: return v * TurnsPerRound * 1.4f;      // la joueuse frappe souvent plusieurs fois
                case ItemEffect.GouttesBonus: return v * 0.3f;   // de l'économie
                case ItemEffect.SaignementAdverse: return 5f;
                case ItemEffect.VoileAdverse:
                    return foe != null && foe.relics.Count > 0 ? 6f + 4f * foe.relics.Count : 2f;
                case ItemEffect.Reliquaire:
                {
                    // Vaut la meilleure de ses reliques (elle sera doublée)
                    float best = 0f;
                    foreach (var r in self.relics)
                        if (r && r.effect != ItemEffect.Reliquaire)
                            best = Mathf.Max(best, ItemValue(r, self, foe, round, atkPerTurn, hitsPerTurn, roundsLeft));
                    return best * 0.9f;
                }
                // Pilules (un seul usage)
                case ItemEffect.Soigner: return v * 0.8f;
                case ItemEffect.GagnerBouclier: return v * 0.75f;
                case ItemEffect.GagnerForce: return v * hitsPerTurn * 2.5f;   // dure le reste de la manche
                case ItemEffect.GagnerEnergie: return atkPerTurn * v * 0.6f;
                case ItemEffect.AppliquerVulnerable: return atkPerTurn * 0.5f * Mathf.Min(v, 3);
                case ItemEffect.AppliquerFaiblesse: return 2f * Mathf.Min(v, 3);
                case ItemEffect.EnergieAdverseTour: return 6f;
                case ItemEffect.CopierCarte: return atkPerTurn * 0.5f;
                case ItemEffect.Voler: return foe != null && foe.relics.Count > 0 ? 18f : (foe != null && foe.items.Count > 0 ? 6f : 0f);
                case ItemEffect.Purifier: return 4f;
                case ItemEffect.Placebo: return 5f;
                case ItemEffect.SaignementReduitTour: return 2f;
                case ItemEffect.SaignementAdverseTour: return 3f;
                default: return 0f;
            }
        }

        struct Option
        {
            public string name;
            public int price;
            public float value;
            public bool repeatable; // renforcement qu'on peut prendre plusieurs fois (rendement décroissant)
            public System.Func<string> buy;
        }

        /// <summary>
        /// Panier de valeur totale maximale dans le budget (au plus <paramref name="depth"/> achats). Un renforcement
        /// répétable peut y figurer plusieurs fois (×0.85 à chaque répétition) ; le reste une seule fois.
        /// Le premier élément du panier est l'achat le plus précieux.
        /// </summary>
        static List<Option> BestBasket(List<Option> options, int budget, int depth, out float total)
        {
            var best = new List<Option>();
            float bestTotal = 0f;
            var cur = new List<Option>();
            var taken = new int[options.Count];
            void Search(int left, float sum, int start)
            {
                if (sum > bestTotal) { bestTotal = sum; best = new List<Option>(cur); }
                if (cur.Count >= depth) return;
                for (int i = start; i < options.Count; i++)
                {
                    var o = options[i];
                    if (o.price <= 0 || o.value <= 0f || o.price > left) continue;
                    if (taken[i] > 0 && !o.repeatable) continue;
                    float v = o.value * Mathf.Pow(0.85f, taken[i]);
                    taken[i]++; cur.Add(o);
                    Search(left - o.price, sum + v, i); // i : autorise la répétition, sans permutations en double
                    taken[i]--; cur.RemoveAt(cur.Count - 1);
                }
            }
            Search(budget, 0f, 0);
            best.Sort((a, b) => b.value.CompareTo(a.value));
            total = bestTotal;
            return best;
        }

        // ================================================================== Sa "boutique"

        /// <summary>
        /// Entre deux manches, la Dépression dépense ses gouttes de sang comme si elle passait elle aussi par la boutique.
        /// Elle voit, comme toi, un petit nombre d'offres tirées au hasard (reliques, pilules) en plus de ses renforcements,
        /// estime la valeur de chacune dans la situation (combien elle frappe, ce que tu possèdes, les manches restantes),
        /// et achète au meilleur rapport valeur / prix — avec un peu d'aléa pour rester imprévisible. Elle économise
        /// quand un achat nettement meilleur sera à sa portée à la manche suivante.
        /// </summary>
        public List<string> Shop(GameConfig cfg, int round, Fighter self, Fighter foe, List<ItemData> items, bool sheWonLastRound, System.Action<ItemData> onBought)
        {
            var bought = new List<string>();
            if (self == null) return bought;
            int next = round + 1;
            float atk = AvgAttack(next, cfg.enemyDamageScalePerRound, out float avgHits);
            float share = AttackShare(next);
            // Énergie à la prochaine manche ≈ cartes jouées par tour (la plupart coûtent 1)
            int nextEnergy = Mathf.Min(cfg.enemyMaxEnergy, cfg.enemyEnergyBase + Mathf.FloorToInt((next - 1) * cfg.enemyEnergyPerRound))
                             + progress.bonusEnergy + self.RelicValue(ItemEffect.EnergieMax);
            int cardsPerTurn = Mathf.Max(1, Mathf.Min(cfg.enemyMaxCardsPerTurn, nextEnergy));
            float atkPerTurn = atk * share;                        // dégâts d'une carte jouée, en moyenne
            float hitsPerTurn = avgHits * share * cardsPerTurn;   // coups portés par tour
            // Manches encore à jouer (au plus bougies + bougies - 1), en espérance : un renforcement durable s'y répète
            float roundsLeft = Mathf.Max(1f, (self.candles + (foe != null ? foe.candles : 1) - 1) * 0.6f);
            // Revenu attendu à la prochaine manche (pour décider d'économiser)
            float income = (cfg.rewardRoundWin + cfg.rewardRoundLoss) * 0.5f * cfg.enemyBloodScale;

            // Ses offres de la manche : quelques reliques et pilules tirées au hasard (comme ta boutique)
            var relicPool = new List<ItemData>();
            var pillPool = new List<ItemData>();
            if (items != null)
                foreach (var i in items)
                {
                    if (i == null || !i.enemyCanBuy) continue;
                    if (i.rare && Random.value > 0.25f) continue;
                    if (i.kind == ItemKind.Relique) { if (!self.relics.Contains(i)) relicPool.Add(i); }
                    else pillPool.Add(i);
                }
            var relicOffers = Draw(relicPool, cfg.relicOffers + 1);
            var pillOffers = Draw(pillPool, cfg.pillOffers);

            // Combien de fois elle a déjà pris un même renforcement (rendements décroissants)
            int hpBought = 0, strBought = 0, blockBought = 0;
            foreach (var l in progress.log)
            {
                if (l.StartsWith("Insomnie")) hpBought++;
                else if (l.StartsWith("Rancune")) strBought++;
                else if (l.StartsWith("Carapace")) blockBought++;
            }

            for (int guard = 0; guard < 12; guard++)
            {
                var options = new List<Option>();
                // Elle a perdu la manche : la survie compte plus ; elle l'a gagnée : l'attaque compte plus.
                // Tout ce qui est durable (renforcements, reliques) se répète à chaque manche restante.
                float defense = (sheWonLastRound ? 0.9f : 1.15f) * roundsLeft, offense = (sheWonLastRound ? 1.15f : 1f) * roundsLeft;

                options.Add(new Option
                {
                    name = "Emprise", price = cfg.enemyPriceHp, value = cfg.enemyPerkHp * defense * Mathf.Pow(0.85f, hpBought),
                    buy = () => { progress.bonusMaxHp += cfg.enemyPerkHp; hpBought++; return $"Insomnie chronique : +{cfg.enemyPerkHp} Emprise maximale."; }
                });
                options.Add(new Option
                {
                    name = "Force", price = cfg.enemyPriceStrength, value = hitsPerTurn * TurnsPerRound * offense * Mathf.Pow(0.85f, strBought),
                    buy = () => { progress.startStrength += 1; strBought++; return "Rancune : commence chaque manche avec +1 Force."; }
                });
                options.Add(new Option
                {
                    name = "Bouclier", price = cfg.enemyPriceBlock, value = cfg.enemyPerkBlock * 0.9f * defense * Mathf.Pow(0.8f, blockBought),
                    buy = () => { progress.startBlock += cfg.enemyPerkBlock; blockBought++; return $"Carapace d'apathie : +{cfg.enemyPerkBlock} Bouclier en début de manche."; }
                });
                if (progress.bonusEnergy < cfg.enemyMaxBonusEnergy && next >= 2)
                    options.Add(new Option
                    {
                        name = "Énergie", price = cfg.enemyPriceEnergy, value = atkPerTurn * TurnsPerRound * 0.9f * offense,
                        buy = () => { progress.bonusEnergy += 1; return "Ruminations : +1 énergie par tour (elle joue plus de cartes)."; }
                    });

                // Affûter : sa carte qui rapporte le plus (dégâts × fréquence), au plus deux fois la même
                EnemyMoveData bestMove = null;
                float bestMoveValue = 0f, wAll = 0f;
                foreach (var m in _moves) if (Available(m, next)) wAll += Mathf.Max(0.01f, m.weight);
                foreach (var m in _moves)
                {
                    if (!Available(m, next) || m.damage <= 0) continue;
                    if (progress.moveBonus.TryGetValue(m, out var b) && b >= cfg.enemyPerkSharpen * 1.5f) continue;
                    float freq = Mathf.Max(0.01f, m.weight) / Mathf.Max(0.01f, wAll) * cardsPerTurn * TurnsPerRound;
                    float val = m.damage * Mathf.Max(1, m.hits) * cfg.enemyPerkSharpen * freq;
                    if (val > bestMoveValue) { bestMoveValue = val; bestMove = m; }
                }
                if (bestMove)
                {
                    var m = bestMove;
                    options.Add(new Option
                    {
                        name = "Affûter", price = cfg.enemyPriceSharpen, value = bestMoveValue * offense,
                        buy = () =>
                        {
                            progress.moveBonus.TryGetValue(m, out var b);
                            progress.moveBonus[m] = b + cfg.enemyPerkSharpen;
                            return $"Affûte « {m.moveName} » : +{Mathf.RoundToInt(cfg.enemyPerkSharpen * 100f)} % de dégâts.";
                        }
                    });
                }

                // Nouvelle obsession : la plus forte des cartes exclusives encore verrouillées
                EnemyMoveData bestLocked = null;
                foreach (var m in _moves)
                    if (m && m.shopOnly && !progress.unlocked.Contains(m) && m.minRound <= next &&
                        (!bestLocked || m.damage * m.hits + m.block > bestLocked.damage * bestLocked.hits + bestLocked.block))
                        bestLocked = m;
                if (bestLocked)
                {
                    var m = bestLocked;
                    options.Add(new Option
                    {
                        name = "Obsession", price = cfg.enemyPriceNewMove,
                        value = (m.damage * Mathf.Max(1, m.hits) * 0.6f + m.block * 0.5f + (m.applyWeak + m.applyVulnerable + m.crisisCount) * 2.5f) * 0.9f * roundsLeft,
                        buy = () => { progress.unlocked.Add(m); return $"Nouvelle obsession : « {m.moveName} » rejoint ses cartes."; }
                    });
                }

                // Reliques et pilules de ses offres
                foreach (var i in relicOffers)
                {
                    if (self.relics.Contains(i) || self.relics.Count >= MaxRelics) continue;
                    if (i.effect == ItemEffect.Reliquaire && GameManager.RandomOtherRelic(self, i) == null) continue;
                    var item = i;
                    // Effet à chaque manche restante ; une relique se voit et se garde : léger bonus par rapport à un
                    // renforcement invisible équivalent
                    float val = ItemValue(i, self, foe, next, atkPerTurn, hitsPerTurn, roundsLeft) * roundsLeft * 1.5f;
                    if (val <= 0f) continue;
                    options.Add(new Option
                    {
                        name = i.itemName, price = i.price, value = val,
                        buy = () =>
                        {
                            var txt = BuyItem(item, self, foe, onBought);
                            // Reliquaire : elle magnifie sa relique la plus utile, pas une au hasard
                            if (item.effect == ItemEffect.Reliquaire)
                            {
                                ItemData target = null; float tv = 0f;
                                foreach (var r in self.relics)
                                {
                                    if (!r || r == item || r.effect == ItemEffect.Reliquaire) continue;
                                    float rv = ItemValue(r, self, foe, next, atkPerTurn, hitsPerTurn, roundsLeft);
                                    if (rv > tv) { tv = rv; target = r; }
                                }
                                if (target) self.buffTarget = target;
                            }
                            return txt;
                        }
                    });
                }
                if (self.items.Count < MaxPills)
                    foreach (var i in pillOffers)
                    {
                        var item = i;
                        // Pilule à usage unique : elle tient à en avoir une ou deux sous la main (moins elle en a, plus elle en veut)
                        float want = self.items.Count == 0 ? 2.2f : self.items.Count == 1 ? 1.3f : 0.85f;
                        float val = ItemValue(i, self, foe, next, atkPerTurn, hitsPerTurn, roundsLeft) * want;
                        // Pas deux fois la même pilule en réserve
                        foreach (var o in self.items) if (o.data == i) val *= 0.4f;
                        if (val <= 0f) continue;
                        options.Add(new Option { name = i.itemName, price = i.price, value = val, buy = () => BuyItem(item, self, foe, onBought) });
                    }

                // Aléa ±15 % sur la valeur de chaque option (elle reste imprévisible)
                for (int o = 0; o < options.Count; o++)
                {
                    var op = options[o];
                    op.value *= Random.Range(0.85f, 1.15f);
                    op.repeatable = op.name == "Emprise" || op.name == "Force" || op.name == "Bouclier";
                    options[o] = op;
                }
                // Meilleur PANIER pour tout son budget (jusqu'à 3 achats), pas le meilleur rapport valeur / prix :
                // sinon les petits renforcements à 50 gagnent toujours contre une relique à 95.
                var now = BestBasket(options, progress.blood, 4, out float nowValue);
                if (now.Count == 0) break;
                // Économiser : avec ses gouttes de la manche suivante, un panier nettement meilleur serait à sa portée
                var later = BestBasket(options, progress.blood + Mathf.RoundToInt(income), 4, out float laterValue);
                if (laterValue > nowValue * 1.4f && later.Exists(o => o.price > progress.blood))
                {
                    var goal = later.Find(o => o.price > progress.blood);
                    progress.log.Add($"(économise pour {goal.name})");
                    break;
                }
                var best = now[0];
                // Pas de gaspillage sur un achat vraiment médiocre
                if (best.value / best.price < 0.03f) break;

                progress.blood -= best.price;
                var text = best.buy();
                bought.Add(text);
                progress.log.Add(text);
                relicOffers.Remove(relicOffers.Find(x => x && x.itemName == best.name));
                if (pillOffers.Exists(x => x && x.itemName == best.name)) pillOffers.Remove(pillOffers.Find(x => x.itemName == best.name));
            }

            // Jamais sans pilule si elle peut s'en offrir une avec ce qui lui reste : la plus utile parmi ses offres
            if (self.items.Count == 0)
            {
                ItemData pill = null; float pv = 0f;
                foreach (var i in pillOffers)
                {
                    if (!i || i.price > progress.blood) continue;
                    float v = ItemValue(i, self, foe, next, atkPerTurn, hitsPerTurn, roundsLeft);
                    if (v > pv) { pv = v; pill = i; }
                }
                if (pill)
                {
                    progress.blood -= pill.price;
                    var text = BuyItem(pill, self, foe, onBought);
                    bought.Add(text);
                    progress.log.Add(text);
                }
            }
            return bought;
        }

        static List<ItemData> Draw(List<ItemData> pool, int n)
        {
            var copy = new List<ItemData>(pool);
            var res = new List<ItemData>();
            while (res.Count < n && copy.Count > 0)
            {
                int k = Random.Range(0, copy.Count);
                res.Add(copy[k]);
                copy.RemoveAt(k);
            }
            return res;
        }

        static string BuyItem(ItemData i, Fighter self, Fighter foe, System.Action<ItemData> onBought)
        {
            if (i.kind == ItemKind.Relique)
            {
                self.relics.Add(i);
                if (i.effect == ItemEffect.Reliquaire) self.buffTarget = GameManager.RandomOtherRelic(self, i); // affiné par l'appelant
            }
            else self.items.Add(new OwnedItem(i));
            onBought?.Invoke(i);
            return i.kind == ItemKind.Relique ? $"Relique : {i.itemName}." : $"Pilule : {i.itemName}.";
        }
    }
}
