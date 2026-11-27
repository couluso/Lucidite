using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Lucidite
{
    public enum GamePhase { Title, RoundIntro, PlayerTurn, EnemyTurn, RoundEnd, Shop, Ended }

    public class RoundSummary
    {
        public int round;
        public bool playerWon;
        public int bloodTotal;
        public int playerCandles, enemyCandles;
        public readonly List<string> lines = new List<string>();
    }

    public class MatchStats
    {
        public int roundsPlayed, roundsWon, roundsLost;
        public int damageDealt, damageTaken, cardsPlayed;
        public int bloodEarned, cardsBought;
    }

    /// <summary>
    /// Chef d'orchestre. Une partie = une suite de manches (chaque joueuse a des bougies).
    /// Une manche = un combat au tour par tour : énergie, cartes à coût, dégâts et bouclier,
    /// intentions de la Dépression. La manche perdue éteint une bougie ; plus de bougie, partie perdue.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        static bool s_autoStartNextLoad;

        [Header("Données")]
        public GameConfig config;
        public CardDatabase database;

        [Header("Présentation")]
        public BoardView board;

        public GamePhase Phase { get; private set; } = GamePhase.Title;
        public bool IsPaused { get; private set; }
        public bool IsBusy => _busy > 0;
        public Fighter Player { get; private set; }
        public Fighter Enemy { get; private set; }
        /// <summary>Cartes que la Dépression a posées pour ce tour (dans l'ordre où elle les jouera).</summary>
        public IReadOnlyList<EnemyIntent> Intents => _intents;
        /// <summary>Son intention principale (la plus menaçante).</summary>
        public EnemyIntent Intent => EnemyIntent.Main(_intents);
        /// <summary>Ce qu'elle a acquis entre les manches.</summary>
        public EnemyProgress EnemyPerks => _brain != null ? _brain.progress : null;
        readonly List<EnemyIntent> _intents = new List<EnemyIntent>();
        public ShopSystem Shop { get; private set; }
        public MatchStats Stats { get; private set; } = new MatchStats();
        public int Blood { get; private set; }
        public int Round { get; private set; }
        public int Turn { get; private set; }
        public bool PlayerWon { get; private set; }

        // Événements
        public event Action<GamePhase> OnPhaseChanged;
        public event Action OnMatchStarted;
        public event Action<int> OnRoundStarted;
        public event Action<int> OnTurnStarted;
        public event Action<EnemyIntent> OnIntentChanged;
        public event Action OnStatsChanged;                 // PV, bouclier, énergie, statuts
        public event Action<Side, int, int> OnDamaged;      // côté, Lucidité perdue, dégâts bloqués
        public event Action<Side, int> OnHealed;
        public event Action<Side, int> OnBlockGained;
        public event Action<Side, int> OnCandleLost;        // côté, bougies restantes
        public event Action<CardInstance> OnCardPlayed;
        public event Action<RoundSummary> OnRoundEnded;
        public event Action<int, int> OnBloodChanged;
        public event Action OnShopOpened;
        public event Action OnShopClosed;
        public event Action<bool> OnMatchEnded;
        public event Action OnItemsChanged;
        public event Action OnDeckChanged;
        public event Action<string> OnMessage;
        public event Action<CardData, int> OnCrisisInjected;
        public event Action<bool> OnPausedChanged;
        public event Action OnSelectionChanged;
        public event Action<CardSuit> OnResonance;           // 3 cartes d'une même classe jouées dans le tour
        public event Action<Side, ItemData> OnItemUsed;      // pilule avalée (par la joueuse ou par elle)
        public event Action<Side, ItemData> OnItemBought;    // achat d'une relique ou d'une pilule (elle : entre les manches)

        EnemyBrain _brain;
        readonly List<CardInstance> _selected = new List<CardInstance>();
        int _nextDraw, _nextEnergy;
        // Saignement versé pendant la manche (Lucidité perdue × multiplicateur de saignement) : donne les gouttes bonus
        float _spilled, _enemySpilled;
        // Multiplicateurs de saignement du tour en cours (pilules, cartes), indexés par Side
        readonly float[] _turnBleed = { 1f, 1f };
        int _busy;
        bool _endTurnRequested;
        bool _shopDone;
        bool _roundAcknowledged;
        bool _lastRoundWon;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (s_autoStartNextLoad) { s_autoStartNextLoad = false; StartMatch(); }
            else SetPhase(GamePhase.Title);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame && Phase != GamePhase.Title && Phase != GamePhase.Ended)
                SetPaused(!IsPaused);
            if ((kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) && CanAct) EndTurn();
            if (kb.tKey.wasPressedThisFrame && CanAct) SortHand();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (kb.pKey.wasPressedThisFrame && Phase == GamePhase.PlayerTurn && !IsPaused) DebugKillOpponent();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>[Debug, touche P] Termine la manche : la Dépression perd sa dernière bougie et meurt.</summary>
        void DebugKillOpponent()
        {
            if (Enemy == null || Enemy.IsDead) return;
            Enemy.candles = 1;
            Enemy.block = 0;
            int lost = Enemy.LoseHp(Enemy.hp);
            OnDamaged?.Invoke(Side.Opponent, lost, 0);
            NotifyStats();
            Message("[Debug] La Dépression s'éteint.");
        }
#endif

        // ================================================================== API

        public void StartMatch()
        {
            StopAllCoroutines();
            StartCoroutine(MatchRoutine());
        }

        public void RestartMatch()
        {
            s_autoStartNextLoad = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToTitle()
        {
            s_autoStartNextLoad = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            OnPausedChanged?.Invoke(paused);
        }

        /// <summary>La joueuse peut jouer une carte / finir son tour.</summary>
        public bool CanAct => Phase == GamePhase.PlayerTurn && !IsPaused && _busy == 0 && !_endTurnRequested;

        // ------------------------------------------------------------------ Sélection des cartes

        public IReadOnlyList<CardInstance> Selected => _selected;
        public bool IsSelected(CardInstance c) => c != null && _selected.Contains(c);
        public int SelectedCost { get { int s = 0; foreach (var c in _selected) s += c.Cost; return s; } }
        /// <summary>Énergie encore disponible une fois la sélection payée.</summary>
        public int AvailableEnergy => Player != null ? Player.energy - SelectedCost : 0;

        /// <summary>La carte peut être ajoutée à la sélection (ou y est déjà).</summary>
        public bool IsPlayable(CardInstance c, out string reason)
        {
            reason = null;
            if (c == null || Player == null || !Player.hand.Contains(c)) { reason = ""; return false; }
            if (c.Unplayable) { reason = "Cette carte ne peut pas être jouée."; return false; }
            if (IsSelected(c)) return true;
            if (c.Cost > AvailableEnergy) { reason = "Pas assez d'énergie."; return false; }
            return true;
        }

        /// <summary>Clic sur une carte : la sélectionne (elle se soulève et saigne) ou la désélectionne.</summary>
        public void ToggleSelect(CardInstance c)
        {
            if (!CanAct || c == null) return;
            if (_selected.Remove(c))
            {
                AudioManager.Play(Sfx.CardSlide, 0.6f);
                OnSelectionChanged?.Invoke();
                NotifyStats();
                return;
            }
            if (!IsPlayable(c, out var reason))
            {
                if (!string.IsNullOrEmpty(reason)) { Message(reason); AudioManager.Play(Sfx.Error); }
                return;
            }
            _selected.Add(c);
            AudioManager.Play(Sfx.CardFlip, 0.8f);
            OnSelectionChanged?.Invoke();
            NotifyStats();
        }

        // ------------------------------------------------------------------ Ordre de la main (les cartes sont jouées de gauche à droite)

        /// <summary>Déplace une carte de la main (glisser-déposer) : elle prend la place <paramref name="index"/>.</summary>
        public void MoveInHand(CardInstance c, int index)
        {
            if (Player == null || !CanAct) return;
            int from = Player.hand.IndexOf(c);
            if (from < 0) return;
            index = Mathf.Clamp(index, 0, Player.hand.Count - 1);
            if (index == from) return;
            Player.hand.RemoveAt(from);
            Player.hand.Insert(index, c);
            AudioManager.Play(Sfx.CardSlide, 0.35f);
            OnSelectionChanged?.Invoke();
        }

        int _sortMode;
        /// <summary>Trie la main : par classe puis coût, ou (deuxième appui) par coût puis classe.</summary>
        public void SortHand()
        {
            if (Player == null || !CanAct || Player.hand.Count < 2) return;
            var order = new List<CardInstance>(Player.hand);
            int Key(CardInstance c) => c.IsCrisis ? 1000 : 0;
            if (_sortMode == 0)
                order.Sort((a, b) => Key(a) != Key(b) ? Key(a) - Key(b) : a.suit != b.suit ? a.suit - b.suit : a.Cost != b.Cost ? a.Cost - b.Cost : string.CompareOrdinal(a.data.cardName, b.data.cardName));
            else
                order.Sort((a, b) => Key(a) != Key(b) ? Key(a) - Key(b) : a.Cost != b.Cost ? a.Cost - b.Cost : a.suit != b.suit ? a.suit - b.suit : string.CompareOrdinal(a.data.cardName, b.data.cardName));
            _sortMode = 1 - _sortMode;
            Player.hand.Clear();
            Player.hand.AddRange(order);
            AudioManager.Play(Sfx.CardSlide, 0.7f);
            Message(_sortMode == 1 ? "Main triée par classe." : "Main triée par coût.");
            OnSelectionChanged?.Invoke();
        }

        /// <summary>Cartes sélectionnées dans l'ordre de la main (de gauche à droite) : l'ordre où elles seront jouées.</summary>
        public List<CardInstance> SelectedInHandOrder()
        {
            var l = new List<CardInstance>();
            if (Player == null) return l;
            foreach (var c in Player.hand) if (_selected.Contains(c)) l.Add(c);
            return l;
        }

        /// <summary>Retire de la sélection les cartes (les plus à droite d'abord) que l'énergie ne permet plus de payer.</summary>
        void FitSelectionToEnergy()
        {
            if (Player == null) return;
            var order = SelectedInHandOrder();
            for (int i = order.Count - 1; i >= 0 && SelectedCost > Player.energy; i--) _selected.Remove(order[i]);
            OnSelectionChanged?.Invoke();
        }

        /// <summary>« Jouer le tour » : les cartes sélectionnées sont posées et résolues, puis la Dépression joue.</summary>
        public void EndTurn()
        {
            if (!CanAct) return;
            _endTurnRequested = true;
        }

        public void CloseShop() { if (Phase == GamePhase.Shop) _shopDone = true; }
        public void AcknowledgeRound() { _roundAcknowledged = true; }
        public void SpendBlood(int amount) => AddBlood(-amount);

        public void AddBlood(int delta)
        {
            if (delta == 0) return;
            Blood = Mathf.Max(0, Blood + delta);
            if (delta > 0) Stats.bloodEarned += delta;
            OnBloodChanged?.Invoke(Blood, delta);
        }

        /// <summary>
        /// Saignement d'un combattant : multiplie ses projections de sang (et donc le sang sur les cartes)
        /// et les gouttes que l'adversaire gagne en le faisant saigner.
        /// </summary>
        public float BleedMultiplier(Side victim)
        {
            var attacker = victim == Side.Player ? Enemy : Player;
            float m = _turnBleed[(int)victim];
            if (attacker != null) m *= 1f + attacker.RelicPower(ItemEffect.SaignementAdverse); // ×2, ×3 si magnifiée
            return m;
        }

        /// <summary>Saignement d'une perte de Lucidité (le compte de la manche donne les gouttes bonus).</summary>
        void Spill(Side victim, int lost)
        {
            if (lost <= 0) return;
            if (victim == Side.Opponent) _spilled += lost * BleedMultiplier(victim);
            else _enemySpilled += lost * BleedMultiplier(victim);
            var attacker = victim == Side.Player ? Enemy : Player;
            if (attacker != null && attacker.HasRelic(ItemEffect.SaignementAdverse))
                RelicFx(attacker, ItemEffect.SaignementAdverse, $"saigne ×{1 + attacker.RelicPower(ItemEffect.SaignementAdverse)}");
        }

        /// <summary>Une relique vient de faire effet (statuette qui sursaute, s'illumine et affiche son effet).</summary>
        public event Action<Side, ItemData, string> OnRelicTriggered;

        /// <summary>
        /// Signale que les reliques de cet effet (et de cette valeur si précisée) viennent d'agir pour ce combattant.
        /// Les reliques voilées ne font rien ; le Reliquaire qui magnifie l'une d'elles réagit avec elle.
        /// </summary>
        public void RelicFx(Fighter f, ItemEffect effect, string label, int value = int.MinValue)
        {
            if (f == null) return;
            foreach (var r in f.relics)
            {
                if (!r || r.effect != effect || (value != int.MinValue && r.value != value) || f.Power(r) <= 0) continue;
                OnRelicTriggered?.Invoke(f.side, r, label);
                if (f.IsBuffed(r))
                    foreach (var q in f.relics)
                        if (q && q.effect == ItemEffect.Reliquaire && f.Power(q) > 0) OnRelicTriggered?.Invoke(f.side, q, "×2");
            }
        }

        public void NotifyDeckChanged() => OnDeckChanged?.Invoke();
        public void NotifyItemsChanged() => OnItemsChanged?.Invoke();
        public void NotifyStats() => OnStatsChanged?.Invoke();
        public void Message(string msg) => OnMessage?.Invoke(msg);

        void SetPhase(GamePhase p)
        {
            Phase = p;
            OnPhaseChanged?.Invoke(p);
        }

        // ================================================================== Partie

        IEnumerator MatchRoutine()
        {
            SetPhase(GamePhase.RoundIntro);
            int seed = Environment.TickCount;
            Player = new Fighter(Side.Player, "Toi", config.playerCandles, seed);
            Enemy = new Fighter(Side.Opponent, "Ta Dépression", config.enemyCandles, seed * 7 + 13);
            Player.maxHp = config.playerMaxLucidity;
            Player.maxEnergy = config.energyPerTurn;
            _sortMode = 0;
            // Deck de départ : un « Se lever » et un « Respirer » de chaque classe, deux « Crier » d'une même classe au hasard
            CardInstance.ResetLevels();
            CardInstance.LevelBonus = config.suitLevelBonus;
            PillLooks.NewRun(seed, database.items);
            if (database.starterPerSuit.Count + database.starterRandomSuit.Count > 0)
            {
                foreach (var c in database.starterPerSuit)
                    if (c) for (int s = 1; s <= 4; s++) Player.deck.Add(new CardInstance(c, (CardSuit)s));
                var shared = RandomSuit();
                foreach (var c in database.starterRandomSuit) if (c) Player.deck.Add(new CardInstance(c, shared));
            }
            else foreach (var c in database.playerStarterDeck) if (c) Player.deck.Add(new CardInstance(c, RandomSuit()));
            _brain = new EnemyBrain(database.enemyMoves);
            _brain.progress.blood = config.enemyStartingBlood;
            // Elle commence avec une pilule dans son pilulier (on la verra l'avaler dès la première manche)
            var herPills = database.items.FindAll(i => i && i.enemyCanBuy && !i.rare && i.kind != ItemKind.Relique && !i.IsPassive);
            if (herPills.Count > 0) Enemy.items.Add(new OwnedItem(herPills[UnityEngine.Random.Range(0, herPills.Count)]));

            Stats = new MatchStats();
            Blood = 0;
            AddBlood(config.startingBlood);
            Shop = new ShopSystem(config, database, Player, () => Blood, SpendBlood);

            OnMatchStarted?.Invoke();
            if (board) yield return board.IntroRoutine();

            Round = 0;
            while (true)
            {
                Round++;
                yield return RoundRoutine();
                if (CheckMatchEnd()) yield break;

                // Elle aussi passe par la "boutique" avec ses gouttes de sang ; ses reliques et pilules se posent de son côté
                var bought = _brain.Shop(config, Round, Enemy, Player, database.items, !_lastRoundWon, (item) => OnItemBought?.Invoke(Side.Opponent, item));
#if UNITY_EDITOR
                {
                    var log = _brain.progress.log;
                    string saving = log.Count > 0 && log[log.Count - 1].StartsWith("(") ? "  " + log[log.Count - 1] : "";
                    Debug.Log($"[Lucidité] Boutique de la Dépression (manche {Round}, reste {_brain.progress.blood} gouttes) : " +
                              (bought.Count > 0 ? string.Join(" | ", bought) : "rien") + saving);
                }
#endif

                SetPhase(GamePhase.Shop);
                Shop.GenerateOffers();
                _shopDone = false;
                OnShopOpened?.Invoke();
                int sv;
                if ((sv = Player.RelicValue(ItemEffect.RemiseBoutique)) > 0) RelicFx(Player, ItemEffect.RemiseBoutique, $"-{sv} %");
                if ((sv = Player.RelicValue(ItemEffect.RelanceGratuite)) > 0) RelicFx(Player, ItemEffect.RelanceGratuite, sv > 1 ? $"{sv} relances offertes" : "relance offerte");
                yield return new WaitUntil(() => _shopDone);
                OnShopClosed?.Invoke();
            }
        }

        IEnumerator RoundRoutine()
        {
            SetPhase(GamePhase.RoundIntro);
            Turn = 0;
            // Voile noir : une relique adverse au hasard est sans effet pendant la manche (choix simultanés)
            Player.veiled = Enemy.veiled = null;
            var veilOnEnemy = Player.HasRelic(ItemEffect.VoileAdverse) && Enemy.relics.Count > 0 ? Enemy.relics[UnityEngine.Random.Range(0, Enemy.relics.Count)] : null;
            var veilOnPlayer = Enemy.HasRelic(ItemEffect.VoileAdverse) && Player.relics.Count > 0 ? Player.relics[UnityEngine.Random.Range(0, Player.relics.Count)] : null;
            Enemy.veiled = veilOnEnemy; Player.veiled = veilOnPlayer;
            Player.ResetForRound(config.playerMaxLucidity + Player.RelicValue(ItemEffect.LuciditeMax));
            Player.maxEnergy = config.energyPerTurn + Player.RelicValue(ItemEffect.EnergieMax);
            var perks = _brain.progress;
            Enemy.ResetForRound(config.enemyBaseHp + (Round - 1) * config.enemyHpPerRound + perks.bonusMaxHp + Enemy.RelicValue(ItemEffect.LuciditeMax));
            Enemy.maxEnergy = Mathf.Min(config.enemyMaxEnergy,
                config.enemyEnergyBase + Mathf.FloorToInt((Round - 1) * config.enemyEnergyPerRound)) + perks.bonusEnergy + Enemy.RelicValue(ItemEffect.EnergieMax);
            Enemy.strength = perks.startStrength + Enemy.RelicValue(ItemEffect.ForceDebutManche);
            Enemy.block = perks.startBlock + Enemy.RelicValue(ItemEffect.BouclierDebutManche);
            Player.block = Player.RelicValue(ItemEffect.BouclierDebutManche);
            Player.strength = Player.RelicValue(ItemEffect.ForceDebutManche);
            Player.hand.Clear();
            Player.deck.ResetForRound();
            _brain.ResetForRound();
            _selected.Clear(); _nextDraw = 0; _nextEnergy = 0; _spilled = 0f; _enemySpilled = 0f;
            _turnBleed[0] = _turnBleed[1] = 1f;
            OnRoundStarted?.Invoke(Round);
            NotifyStats();
            if (board) yield return board.RoundStartRoutine(Round);
            if (veilOnEnemy) { Message($"Ton voile noir recouvre « {veilOnEnemy.itemName} » : sans effet cette manche."); RelicFx(Player, ItemEffect.VoileAdverse, $"voile « {veilOnEnemy.itemName} »"); }
            if (veilOnPlayer) { Message($"Son voile noir recouvre ta relique « {veilOnPlayer.itemName} » : sans effet cette manche."); RelicFx(Enemy, ItemEffect.VoileAdverse, $"voile « {veilOnPlayer.itemName} »"); }
            // Reliques de début de manche : chacune sursaute en appliquant son bonus
            foreach (var f in new[] { Player, Enemy })
            {
                string life = f == Player ? "Lucidité" : "Emprise";
                int v;
                if ((v = f.RelicValue(ItemEffect.LuciditeMax)) > 0) RelicFx(f, ItemEffect.LuciditeMax, $"+{v} {life} max");
                if ((v = f.RelicValue(ItemEffect.ForceDebutManche)) > 0) RelicFx(f, ItemEffect.ForceDebutManche, $"+{v} Force");
                if ((v = f.RelicValue(ItemEffect.BouclierDebutManche)) > 0) RelicFx(f, ItemEffect.BouclierDebutManche, $"+{v} Bouclier");
            }

            while (!Player.IsDead && !Enemy.IsDead)
            {
                yield return PlayerTurnRoutine();
                if (Player.IsDead || Enemy.IsDead) break;
                yield return EnemyTurnRoutine();
            }

            // ----- Fin de manche
            SetPhase(GamePhase.RoundEnd);
            Player.veiled = Enemy.veiled = null;
            bool won = Enemy.IsDead && !Player.IsDead;
            _lastRoundWon = won;
            Stats.roundsPlayed++;
            var loser = won ? Enemy : Player;
            loser.candles = Mathf.Max(0, loser.candles - 1);
            if (won) Stats.roundsWon++; else Stats.roundsLost++;
            OnCandleLost?.Invoke(loser.side, loser.candles);

            var summary = new RoundSummary { round = Round, playerWon = won, playerCandles = Player.candles, enemyCandles = Enemy.candles };
            int blood;
            // Une base (victoire ou défaite), puis un bonus selon à quel point tu l'as fait saigner
            blood = won ? config.rewardRoundWin : config.rewardRoundLoss;
            summary.lines.Add(won ? $"Base (manche gagnée) : +{blood}" : $"Base (manche perdue) : +{blood}");
            if (config.lucidityPerBonusBlood > 0 && won)
            {
                int bonus = Player.hp / config.lucidityPerBonusBlood;
                if (bonus > 0) { blood += bonus; summary.lines.Add($"Lucidité préservée ({Player.hp}) : +{bonus}"); }
            }
            int spilledBlood = Mathf.FloorToInt(_spilled * config.bloodPerDamage);
            if (spilledBlood > 0) { blood += spilledBlood; summary.lines.Add($"Saignement de la Dépression ({Mathf.RoundToInt(_spilled)}) : +{spilledBlood}"); }
            int relicBlood = Player.RelicValue(ItemEffect.GouttesBonus);
            if (relicBlood > 0) { blood += relicBlood; summary.lines.Add($"Reliques : +{relicBlood}"); RelicFx(Player, ItemEffect.GouttesBonus, $"+{relicBlood} gouttes"); }
            int herRelicBlood = Enemy.RelicValue(ItemEffect.GouttesBonus);
            if (herRelicBlood > 0) RelicFx(Enemy, ItemEffect.GouttesBonus, $"+{herRelicBlood} gouttes");
            summary.bloodTotal = blood;

            // Ses gouttes à elle : même barème que la joueuse (manche gagnée / perdue + sang qu'elle a fait couler)
            int enemyBlood = (won ? config.rewardRoundLoss : config.rewardRoundWin) + Mathf.FloorToInt(_enemySpilled * config.bloodPerDamage)
                             + Enemy.RelicValue(ItemEffect.GouttesBonus);
            _brain.progress.blood += Mathf.RoundToInt(enemyBlood * config.enemyBloodScale);

            Player.hand.Clear();
            if (board) yield return board.ClearRoundRoutine(won);
            AddBlood(blood);

            if (Player.IsOut || Enemy.IsOut) yield break;
            _roundAcknowledged = false;
            OnRoundEnded?.Invoke(summary);
            while (!_roundAcknowledged) yield return null;
        }

        IEnumerator PlayerTurnRoutine()
        {
            Turn++;
            SetPhase(GamePhase.PlayerTurn);
            _endTurnRequested = false;
            if (Turn > 1) Player.block = 0;
            Player.energy = Player.maxEnergy + (Turn == 1 ? Player.RelicValue(ItemEffect.EnergiePremierTour) : 0) + _nextEnergy;
            _nextEnergy = 0;
            _selected.Clear();
            OnSelectionChanged?.Invoke();

            // Elle dépense son énergie : une ou plusieurs cartes posées face visible devant elle
            Enemy.energy = Enemy.maxEnergy;
            _intents.Clear();
            _intents.AddRange(_brain.PlanTurn(Round, config.enemyDamageScalePerRound, Enemy.maxEnergy, config.enemyMaxCardsPerTurn));
            int spent = 0;
            foreach (var it in _intents) spent += it.move ? it.move.cost : 0;
            Enemy.energy = Mathf.Max(0, Enemy.maxEnergy - spent);
            OnIntentChanged?.Invoke(Intent);
            if (board) board.ShowIntents(_intents);
            _turnBleed[0] = _turnBleed[1] = 1f;
            Player.hitThisTurn = Enemy.hitThisTurn = false;
            OnTurnStarted?.Invoke(Turn);
            int regen = Player.RelicValue(ItemEffect.SoinDebutTour);
            if (regen > 0) { int h = Player.Heal(regen); if (h > 0) { OnHealed?.Invoke(Side.Player, h); RelicFx(Player, ItemEffect.SoinDebutTour, $"+{h} Lucidité"); } }
            // Reliques d'énergie et de pioche (les tiennes) et d'énergie (les siennes : elle a pu poser plus de cartes)
            int rv;
            if ((rv = Player.RelicValue(ItemEffect.EnergieMax)) > 0) RelicFx(Player, ItemEffect.EnergieMax, $"+{rv} énergie");
            if (Turn == 1 && (rv = Player.RelicValue(ItemEffect.EnergiePremierTour)) > 0) RelicFx(Player, ItemEffect.EnergiePremierTour, $"+{rv} énergie");
            if (Turn == 1 && (rv = Player.RelicValue(ItemEffect.PiocheDebutManche)) > 0) RelicFx(Player, ItemEffect.PiocheDebutManche, $"+{rv} cartes");
            if ((rv = Enemy.RelicValue(ItemEffect.EnergieMax)) > 0) RelicFx(Enemy, ItemEffect.EnergieMax, $"+{rv} énergie");
            // Elle avale parfois une de ses pilules (posées de son côté de la table)
            var pill = _brain.ChoosePill(Enemy, Player, _intents, Turn);
            if (pill != null)
            {
                _busy++; // pas d'action pendant qu'elle avale
                yield return new WaitForSeconds(0.4f);
                var swallow = TableItems.Instance ? TableItems.Instance.BeginSwallow(Side.Opponent, pill) : null;
                Enemy.items.Remove(pill);
                Message($"La Dépression avale une pilule : {pill.data.itemName}.");
#if UNITY_EDITOR
                Debug.Log($"[Lucidité] La Dépression avale : {pill.data.itemName} (manche {Round}, tour {Turn}, il lui en reste {Enemy.items.Count}).");
#endif
                if (swallow != null) yield return swallow;
                yield return ApplyItemRoutine(Side.Opponent, pill.data);
                _busy--;
            }

            // Pioche normale jusqu'à la limite de main, puis pioches bonus (cartes « Pioche », relique) au-delà
            int bonusDraw = (Turn == 1 ? Player.RelicValue(ItemEffect.PiocheDebutManche) : 0) + _nextDraw;
            _nextDraw = 0;
            _busy++;
            yield return DrawCards(config.drawPerTurn);
            if (bonusDraw > 0) yield return DrawCards(bonusDraw, bonus: true);
            _busy--;
            NotifyStats();

            while (!_endTurnRequested && !Player.IsDead && !Enemy.IsDead) yield return null;
            while (_busy > 0) yield return null;
            if (Enemy.IsDead || Player.IsDead) yield break;

            // Les cartes sélectionnées sont posées face cachée devant toi, puis retournées et résolues une à une,
            // de gauche à droite (l'ordre de ta main)
            var toPlay = SelectedInHandOrder();
            _selected.Clear();
            OnSelectionChanged?.Invoke();
            if (toPlay.Count > 0)
            {
                foreach (var c in toPlay) Player.hand.Remove(c);
                if (board) yield return board.LayDownRoutine(toPlay);
                var resolved = new List<(CardInstance, bool)>();
                var suitCount = new int[5];
                var resonated = new bool[5];
                foreach (var c in toPlay)
                {
                    if (Enemy.IsDead) break;
                    yield return ResolveCardRoutine(c, c.suit != CardSuit.Aucune ? suitCount[(int)c.suit] : 0);
                    resolved.Add((c, c.Exhaust || c.IsCrisis));
                    // Résonance : 3 cartes d'une même classe (2 avec son ex-voto) ; le Joker compte pour toutes
                    if (c.data.special == CardSpecial.Joker) for (int s = 1; s <= 4; s++) suitCount[s]++;
                    else if (c.suit != CardSuit.Aucune) suitCount[(int)c.suit]++;
                    for (int s = 1; s <= 4 && !Enemy.IsDead; s++)
                        if (!resonated[s] && suitCount[s] >= ResonanceNeed((CardSuit)s))
                        {
                            resonated[s] = true;
                            yield return ResonanceRoutine((CardSuit)s);
                        }
                }
                // Cartes non résolues (adversaire déjà vaincu) : défaussées
                foreach (var c in toPlay)
                    if (!resolved.Exists(r => r.Item1 == c)) { Player.deck.Discard(c); resolved.Add((c, false)); }
                if (board) yield return board.ClearPlayedRoutine(resolved);
                if (Enemy.IsDead) yield break;
            }

            // Fin du tour : les cartes non jouées RESTENT en main pour le tour suivant.
            // Seules partent les Éphémères (épuisées) et les Crises injouables (défaussées, après avoir fait mal).
            var discarded = new List<CardInstance>();
            var exhausted = new List<CardInstance>();
            int heldPain = 0;
            foreach (var c in Player.hand)
            {
                heldPain += c.data.loseLucidityIfHeld;
                if (c.Ethereal) { Player.deck.ExhaustCard(c); exhausted.Add(c); }
                else if (c.Unplayable) { Player.deck.Discard(c); discarded.Add(c); }
            }
            Player.hand.RemoveAll(c => exhausted.Contains(c) || discarded.Contains(c));
            if (board && discarded.Count + exhausted.Count > 0) yield return board.DiscardHandRoutine(discarded, exhausted);
            if (heldPain > 0)
            {
                int lost = Player.LoseHp(heldPain);
                Stats.damageTaken += lost;
                Spill(Side.Player, lost);
                OnDamaged?.Invoke(Side.Player, lost, 0);
                Message($"Les Crises gardées en main te rongent : -{lost} Lucidité.");
            }
            Player.TickStatuses();
            NotifyStats();
        }

        IEnumerator EnemyTurnRoutine()
        {
            SetPhase(GamePhase.EnemyTurn);
            Enemy.block = 0;
            int regen = Enemy.RelicValue(ItemEffect.SoinDebutTour);
            if (regen > 0) { int h = Enemy.Heal(regen); if (h > 0) { OnHealed?.Invoke(Side.Opponent, h); RelicFx(Enemy, ItemEffect.SoinDebutTour, $"+{h} Emprise"); } }
            NotifyStats();
            yield return new WaitForSeconds(config.enemyActDelay);

            // Elle joue ses cartes une à une, dans l'ordre où elle les a posées
            for (int index = 0; index < _intents.Count && !Player.IsDead; index++)
            {
                var it = _intents[index];
                if (board) yield return board.EnemyActRoutine(it, index);

                if (it.block > 0) { Enemy.block += it.block; OnBlockGained?.Invoke(Side.Opponent, it.block); }
                if (it.strength > 0) { Enemy.strength += it.strength; Message("La Dépression se renforce."); }

                for (int h = 0; h < it.hits && it.damagePerHit > 0 && !Player.IsDead; h++)
                {
                    int lost = Hit(Enemy, Player, CombatRules.AttackDamage(it.damagePerHit, Enemy, Player), out int blocked);
                    if (board) yield return board.EnemyHitRoutine(lost, blocked, index);
                }
                if (it.weak > 0) Player.weak += it.weak;
                if (it.vulnerable > 0) Player.vulnerable += it.vulnerable;

                if (it.crisis > 0)
                {
                    var added = new List<CardInstance>();
                    for (int i = 0; i < it.crisis; i++)
                    {
                        var data = it.move.specificCrisis ? it.move.specificCrisis : database.RandomCrisis();
                        if (!data) break;
                        var ci = new CardInstance(data);
                        Player.deck.Add(ci, toDiscard: !it.move.crisisToDrawPile, shuffleIntoDraw: it.move.crisisToDrawPile);
                        added.Add(ci);
                        OnCrisisInjected?.Invoke(data, 1);
                    }
                    if (board && added.Count > 0) yield return board.CrisisRoutine(added, it.move.crisisToDrawPile);
                    NotifyDeckChanged();
                }
                NotifyStats();
                if (board) yield return board.EnemyCardDoneRoutine(index);
            }
            _intents.Clear();

            Enemy.TickStatuses();
            NotifyStats();
            if (board) yield return board.EnemyEndRoutine();
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>
        /// Un coup porté : bouclier, relique « premier coup réduit » de la victime, saignement, puis ses épines
        /// (l'attaquant perd de la Lucidité à son tour). Renvoie la Lucidité perdue par la victime.
        /// </summary>
        int Hit(Fighter attacker, Fighter victim, int dmg, out int blocked)
        {
            if (!victim.hitThisTurn)
            {
                victim.hitThisTurn = true;
                int red = victim.RelicValue(ItemEffect.PremierCoupReduit);
                if (red > 0 && dmg > 0)
                {
                    int before = dmg;
                    dmg = Mathf.Max(0, dmg - red);
                    RelicFx(victim, ItemEffect.PremierCoupReduit, $"-{before - dmg} dégâts");
                }
            }
            // Vanité : bonus de dégâts déjà compté dans dmg (CombatRules) quand l'attaquant vacille
            if (dmg > 0 && attacker.IsLowLucidity && attacker.RelicValue(ItemEffect.DegatsSiLuciditeBasse) > 0)
                RelicFx(attacker, ItemEffect.DegatsSiLuciditeBasse, $"+{attacker.RelicValue(ItemEffect.DegatsSiLuciditeBasse)} dégâts");
            int lost = victim.TakeDamage(dmg, out blocked);
            if (victim == Player) Stats.damageTaken += lost; else Stats.damageDealt += lost;
            Spill(victim.side, lost);
            OnDamaged?.Invoke(victim.side, lost, blocked);
            int thorns = victim.RelicValue(ItemEffect.Epines);
            if (lost > 0 && thorns > 0 && !attacker.IsDead)
            {
                int back = attacker.LoseHp(thorns);
                if (attacker == Player) Stats.damageTaken += back; else Stats.damageDealt += back;
                Spill(attacker.side, back);
                if (back > 0) { OnDamaged?.Invoke(attacker.side, back, 0); RelicFx(victim, ItemEffect.Epines, $"épines -{back}"); }
            }
            NotifyStats();
            return lost;
        }

        /// <summary>bonus = pioche venant d'un effet (carte, objet, relique) : ignore la limite de main, pas le plafond absolu.</summary>
        IEnumerator DrawCards(int count, bool bonus = false)
        {
            var drawn = new List<CardInstance>();
            int limit = bonus ? Mathf.Max(config.maxHandSize, config.absoluteMaxHandSize) : config.maxHandSize;
            for (int i = 0; i < count; i++)
            {
                if (Player.hand.Count >= limit) break;
                var c = Player.deck.Draw();
                if (c == null) break;
                Player.hand.Add(c);
                drawn.Add(c);
                if (c.data.energyLossOnDraw > 0)
                {
                    Player.energy = Mathf.Max(0, Player.energy - c.data.energyLossOnDraw);
                    Message($"« {c.data.cardName} » te vide de ton énergie.");
                }
            }
            if (board && drawn.Count > 0) yield return board.DrawRoutine(drawn);
            NotifyStats();
        }

        /// <summary>Résout une carte déjà posée sur la table (retournée face visible, effets appliqués).</summary>
        int ResonanceNeed(CardSuit s) => Mathf.Max(1, 3 - Player.RelicPower(ItemEffect.ResonanceFacile, (int)s));

        IEnumerator ResonanceRoutine(CardSuit s)
        {
            int lv = CardInstance.SuitLevels[(int)s] * CardInstance.LevelBonus;
            Message($"Résonance — {CardData.SuitLabel(s)} : {CardData.ResonanceLabel(s)}.");
            AudioManager.Play(Sfx.Heartbeat, 0.9f);
            OnResonance?.Invoke(s);
            if (Player.HasRelic(ItemEffect.ResonanceFacile, (int)s)) RelicFx(Player, ItemEffect.ResonanceFacile, $"Résonance à {ResonanceNeed(s)}", (int)s);
            switch (s)
            {
                case CardSuit.Coeur: { int h = Player.Heal(4 + lv); if (h > 0) OnHealed?.Invoke(Side.Player, h); break; }
                case CardSuit.Souffle: _nextDraw += 2; break;
                case CardSuit.Regard: Enemy.vulnerable += 2; Enemy.weak += 1; break;
                case CardSuit.Main: Player.block += 8 + lv; OnBlockGained?.Invoke(Side.Player, 8 + lv); break;
            }
            NotifyStats();
            yield return new WaitForSeconds(0.45f);
        }

        IEnumerator ResolveCardRoutine(CardInstance c, int sameSuitBefore)
        {
            Player.energy = Mathf.Max(0, Player.energy - c.Cost);
            Stats.cardsPlayed++;
            NotifyStats();
            OnCardPlayed?.Invoke(c);
            if (board) yield return board.RevealPlayedRoutine(c);

            bool low = Player.IsLowLucidity;
            var sp = c.data.special;
            int bonus = sp == CardSpecial.BonusSiLuciditeBasse && low ? c.SpecialValue : 0;
            int mult = sp == CardSpecial.DoubleSiEcarlate && c.Scarlet ? 2 : 1;
            // Saignement du tour : appliqué avant les coups pour que cette carte en profite
            if (sp == CardSpecial.SaignementAdverseTour) { _turnBleed[(int)Side.Opponent] *= 2f; Message("Elle saigne deux fois plus ce tour."); }
            if (sp == CardSpecial.SaignementReduitTour) { _turnBleed[(int)Side.Player] *= 0.5f; Message("Tu saignes deux fois moins ce tour."); }

            // Dégâts
            int classDmg = c.suit != CardSuit.Aucune ? ItemData.ClassDamage * Player.RelicPower(ItemEffect.DegatsClasse, (int)c.suit) : 0;
            int baseDmg = sp == CardSpecial.DegatsSelonBouclier ? Player.block : (c.Damage > 0 ? (c.Damage + bonus + classDmg) * mult : 0);
            int hits = c.Hits + (sp == CardSpecial.CoupsParClasse ? sameSuitBefore : 0);
            if (classDmg > 0 && baseDmg > 0 && sp != CardSpecial.DegatsSelonBouclier)
                RelicFx(Player, ItemEffect.DegatsClasse, $"+{classDmg} dégâts", (int)c.suit);
            if (baseDmg > 0 || sp == CardSpecial.DegatsSelonBouclier)
            {
                for (int h = 0; h < hits && !Enemy.IsDead; h++)
                {
                    int lost = Hit(Player, Enemy, CombatRules.AttackDamage(baseDmg, Player, Enemy), out int blocked);
                    if (board) yield return board.PlayerHitRoutine(lost, blocked);
                }
            }
            // Bouclier
            int extraBlock = 0;
            if (sp == CardSpecial.BouclierParClasseEnMain && c.suit != CardSuit.Aucune)
                foreach (var h in Player.hand) if (h.suit == c.suit) extraBlock += c.SpecialValue;
            if (c.Block > 0 || extraBlock > 0)
            {
                int b = (c.Block > 0 ? (c.Block + bonus) * mult : 0) + extraBlock;
                Player.block += b;
                OnBlockGained?.Invoke(Side.Player, b);
            }
            if (c.data.special == CardSpecial.DoublerBouclier && Player.block > 0)
            {
                OnBlockGained?.Invoke(Side.Player, Player.block);
                Player.block *= 2;
            }
            if (c.Vulnerable > 0) Enemy.vulnerable += c.Vulnerable;
            if (c.Weak > 0) Enemy.weak += c.Weak;
            if (c.Strength > 0) Player.strength += c.Strength;
            if (c.EnergyGain > 0) _nextEnergy += c.EnergyGain;
            if (c.Heal > 0)
            {
                int healed = Player.Heal(c.Heal);
                if (healed > 0) OnHealed?.Invoke(Side.Player, healed);
            }
            if (sp == CardSpecial.SoinSelonSang)
            {
                int healed = Player.Heal(c.HealFromBlood);
                if (healed > 0) OnHealed?.Invoke(Side.Player, healed);
            }
            if (sp == CardSpecial.PerdreLucidite && c.SpecialValue > 0)
            {
                int lost = Player.LoseHp(c.SpecialValue);
                Stats.damageTaken += lost;
                Spill(Side.Player, lost);
                OnDamaged?.Invoke(Side.Player, lost, 0);
            }
            if (c.Blood > 0) AddBlood(c.Blood);
            NotifyStats();

            // Carte -> défausse ou épuisée (la carte reste visible sur la table jusqu'à la fin de la résolution)
            if (c.Exhaust || c.IsCrisis) Player.deck.ExhaustCard(c); else Player.deck.Discard(c);

            if (c.data.special == CardSpecial.PurgerCrisesEnMain)
            {
                var crises = Player.hand.FindAll(x => x.IsCrisis);
                foreach (var cr in crises)
                {
                    Player.hand.Remove(cr);
                    Player.deck.ExhaustCard(cr);
                    if (board) board.RemoveFromHand(cr, true);
                }
                _nextDraw += crises.Count;
            }
            // Les cartes sont jouées en fin de tour : la pioche bonus arrive au tour suivant
            if (c.Draw > 0) _nextDraw += c.Draw;

            yield return new WaitForSeconds(config.cardResolveDelay);
        }

        bool CheckMatchEnd()
        {
            if (Phase == GamePhase.Ended) return true;
            if (!Player.IsOut && !Enemy.IsOut) return false;
            PlayerWon = Enemy.IsOut && !Player.IsOut;
            SetPhase(GamePhase.Ended);
            OnMatchEnded?.Invoke(PlayerWon);
            return true;
        }

        // ================================================================== Objets

        public bool CanUseItem(OwnedItem item)
        {
            if (item == null || item.data.IsPassive || IsPaused) return false;
            if (Phase == GamePhase.Shop) return item.data.UsableInShop;
            return CanAct;
        }

        /// <summary>Ce qui empêche d'avaler la pilule maintenant (null si rien).</summary>
        public string ItemBlockReason(OwnedItem item)
        {
            if (!CanUseItem(item)) return Phase == GamePhase.Shop ? "Cette pilule s'avale pendant ton tour." : "Impossible d'avaler une pilule maintenant.";
            var d = item.data;
            if (d.effect == ItemEffect.ConvertirClasse && _selected.Count == 0) return "Sélectionne d'abord une ou deux cartes de ta main.";
            if (d.effect == ItemEffect.CopierCarte && _selected.Count < 2) return "Sélectionne d'abord deux cartes : celle de droite copiera celle de gauche.";
            if (d.effect == ItemEffect.Voler && Enemy != null && Enemy.relics.Count == 0 && Enemy.items.Count == 0) return "Elle n'a rien à voler.";
            return null;
        }

        public void UseItem(OwnedItem item)
        {
            string why = item != null ? ItemBlockReason(item) : "";
            if (why != null) { Message(why); AudioManager.Play(Sfx.Error); return; }
            StartCoroutine(UseItemRoutine(item));
        }

        IEnumerator UseItemRoutine(OwnedItem item)
        {
            _busy++;
            // L'animation prend la pilule en charge avant qu'elle quitte le pilulier
            var swallow = TableItems.Instance && Phase != GamePhase.Shop ? TableItems.Instance.BeginSwallow(Side.Player, item) : null;
            Player.items.Remove(item);
            NotifyItemsChanged();
            Message($"{item.data.itemName}.");
            if (swallow != null) yield return swallow;
            yield return ApplyItemRoutine(Side.Player, item.data);
            _busy--;
        }

        public void NotifyItemBought(Side side, ItemData item) => OnItemBought?.Invoke(side, item);

        /// <summary>Effet d'une pilule, pour la joueuse ou pour la Dépression (la pilule est déjà retirée de son pilulier).</summary>
        IEnumerator ApplyItemRoutine(Side side, ItemData d)
        {
            var self = side == Side.Player ? Player : Enemy;
            var foe = side == Side.Player ? Enemy : Player;
            var foeSide = side == Side.Player ? Side.Opponent : Side.Player;
            int v = d.value;
            AudioManager.Play(Sfx.Heal, 0.6f);
            OnItemUsed?.Invoke(side, d);
            switch (d.effect)
            {
                case ItemEffect.GagnerBouclier: self.block += v; OnBlockGained?.Invoke(side, v); break;
                case ItemEffect.GagnerEnergie:
                    if (side == Side.Player) Player.energy += v;
                    else
                    {
                        // Elle a de quoi poser d'autres cartes ce tour
                        Enemy.energy += v;
                        var extra = _brain.PlanTurn(Round, config.enemyDamageScalePerRound, v, Mathf.Max(1, v));
                        if (extra.Count > 0) { _intents.AddRange(extra); if (board) board.UpdateIntents(_intents); OnIntentChanged?.Invoke(Intent); }
                    }
                    break;
                case ItemEffect.EnergieAdverseTour:
                    if (side == Side.Player)
                    {
                        // Elle renonce à ses dernières cartes jusqu'à rentrer dans son énergie réduite
                        int budget = Enemy.maxEnergy - v, spent = 0;
                        foreach (var it in _intents) spent += it.move ? it.move.cost : 0;
                        var dropped = new List<string>();
                        while (_intents.Count > 0 && spent > Mathf.Max(0, budget))
                        {
                            var last = _intents[_intents.Count - 1];
                            spent -= last.move ? last.move.cost : 0;
                            dropped.Add(last.move ? last.move.moveName : "?");
                            _intents.RemoveAt(_intents.Count - 1);
                        }
                        Enemy.energy = Mathf.Max(0, Enemy.energy - v);
                        if (dropped.Count > 0) { Message($"Elle n'a plus la force : elle renonce à « {string.Join(" », « ", dropped)} »."); if (board) board.UpdateIntents(_intents); OnIntentChanged?.Invoke(Intent); }
                        else Message("Elle s'engourdit, sans renoncer à rien.");
                    }
                    else
                    {
                        Player.energy = Mathf.Max(0, Player.energy - v);
                        FitSelectionToEnergy();
                        Message($"Tu te sens engourdie : -{v} énergie ce tour.");
                    }
                    break;
                case ItemEffect.ConvertirClasse:
                    if (side == Side.Player && d.Suit != CardSuit.Aucune)
                    {
                        var sel = SelectedInHandOrder();
                        int n = 0;
                        foreach (var c in sel)
                        {
                            if (n >= 2 || c.IsCrisis) continue;
                            c.suit = d.Suit; n++;
                            var view = board ? board.ViewOf(c) : null;
                            if (view) { view.Refresh(); view.Punch(0.3f); }
                        }
                        Message(n > 1 ? $"{n} cartes deviennent {CardData.SuitLabel(d.Suit)}." : $"La carte devient {CardData.SuitLabel(d.Suit)}.");
                        NotifyDeckChanged();
                    }
                    break;
                case ItemEffect.CopierCarte:
                    if (side == Side.Player)
                    {
                        var sel = SelectedInHandOrder();
                        if (sel.Count >= 2)
                        {
                            CardInstance src = sel[0], dst = sel[1];
                            dst.data = src.data; dst.upgraded = src.upgraded; dst.suit = src.suit;
                            var view = board ? board.ViewOf(dst) : null;
                            if (view) { view.Refresh(); view.Punch(0.35f); }
                            Message($"« {src.Name} » se dédouble.");
                            FitSelectionToEnergy();
                            NotifyDeckChanged();
                        }
                    }
                    else if (_intents.Count >= 2)
                    {
                        // Elle : sa dernière carte posée devient une copie de la première
                        var a = _intents[0];
                        _intents[_intents.Count - 1] = new EnemyIntent { move = a.move, damagePerHit = a.damagePerHit, hits = a.hits, block = a.block, strength = a.strength, weak = a.weak, vulnerable = a.vulnerable, crisis = a.crisis };
                        Message($"Déjà-vu : « {(a.move ? a.move.moveName : "?")} » revient une seconde fois.");
                        if (board) board.UpdateIntents(_intents);
                        OnIntentChanged?.Invoke(Intent);
                    }
                    break;
                case ItemEffect.Voler: Steal(self, foe); break;
                case ItemEffect.Purifier: self.weak = 0; self.vulnerable = 0; break;
                case ItemEffect.Placebo:
                {
                    var pool = new List<ItemData>();
                    foreach (var i in database.items)
                        if (i && i.kind == ItemKind.Consommable && i.effect != ItemEffect.Placebo && !i.NeedsSelection && i.effect != ItemEffect.PurgerDeck
                            && (side == Side.Player || i.enemyCanBuy))
                            pool.Add(i);
                    if (pool.Count > 0)
                    {
                        var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                        Message($"Placebo... ça agit comme : {pick.itemName}.");
                        yield return new WaitForSeconds(0.25f);
                        yield return ApplyItemRoutine(side, pick);
                    }
                    break;
                }
                case ItemEffect.Soigner: { int h = self.Heal(v); if (h > 0) OnHealed?.Invoke(side, h); break; }
                case ItemEffect.Piocher: if (side == Side.Player) yield return DrawCards(v, bonus: true); break;
                case ItemEffect.AppliquerVulnerable: foe.vulnerable += v; break;
                case ItemEffect.AppliquerFaiblesse: foe.weak += v; break;
                case ItemEffect.GagnerForce: self.strength += v; break;
                case ItemEffect.SaignementReduitTour: _turnBleed[(int)side] *= 0.5f; break;
                case ItemEffect.SaignementAdverseTour: _turnBleed[(int)foeSide] *= 2f; break;
                case ItemEffect.NiveauClasse:
                    if (side == Side.Player && d.Suit != CardSuit.Aucune)
                    {
                        CardInstance.SuitLevels[(int)d.Suit]++;
                        Message($"{CardData.SuitLabel(d.Suit)} : niveau {CardInstance.SuitLevels[(int)d.Suit]}.");
                        NotifyDeckChanged();
                    }
                    break;
                case ItemEffect.PurgerDeck:
                {
                    if (side != Side.Player) break;
                    var toRemove = new List<CardInstance>();
                    foreach (var c in Player.deck.All) if (c.IsCrisis && !Player.hand.Contains(c)) toRemove.Add(c);
                    foreach (var c in toRemove) Player.deck.Remove(c);
                    NotifyDeckChanged();
                    Message($"{toRemove.Count} Crise(s) retirée(s) de ton deck.");
                    break;
                }
            }
            NotifyItemsChanged();
            NotifyStats();
            yield return null;
        }

        static CardSuit RandomSuit() => (CardSuit)UnityEngine.Random.Range(1, 5);

        int RelicSlots(Fighter f) => f == Player ? config.relicSlots : EnemyBrain.MaxRelics;
        int PillSlots(Fighter f) => f == Player ? config.itemSlots : EnemyBrain.MaxPills;

        /// <summary>Vol : une relique de l'adversaire au hasard (si la place le permet), sinon une de ses pilules.</summary>
        void Steal(Fighter thief, Fighter victim)
        {
            string who = thief == Player ? "Tu voles" : "Elle te vole";
            if (victim.relics.Count > 0 && thief.relics.Count < RelicSlots(thief))
            {
                var r = victim.relics[UnityEngine.Random.Range(0, victim.relics.Count)];
                victim.relics.Remove(r);
                if (victim.buffTarget == r) victim.buffTarget = null;
                if (victim.veiled == r) victim.veiled = null;
                thief.relics.Add(r);
                if (r.effect == ItemEffect.Reliquaire && thief.buffTarget == null) thief.buffTarget = RandomOtherRelic(thief, r);
                Message($"{who} « {r.itemName} ».");
            }
            else if (victim.items.Count > 0 && thief.items.Count < PillSlots(thief))
            {
                var o = victim.items[UnityEngine.Random.Range(0, victim.items.Count)];
                victim.items.Remove(o);
                thief.items.Add(o);
                Message($"{who} une pilule : {o.data.itemName}.");
            }
            else Message("Rien à prendre : il n'y a plus de place.");
            NotifyItemsChanged();
        }

        public static ItemData RandomOtherRelic(Fighter f, ItemData except)
        {
            var l = f.relics.FindAll(x => x != except && x.effect != ItemEffect.Reliquaire);
            return l.Count > 0 ? l[UnityEngine.Random.Range(0, l.Count)] : null;
        }
    }
}
