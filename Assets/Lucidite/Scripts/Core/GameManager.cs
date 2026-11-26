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
        /// <summary>Achats de la Dépression entre deux manches (texte de chaque renforcement).</summary>
        public event Action<List<string>> OnEnemyShopped;
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

        EnemyBrain _brain;
        readonly List<CardInstance> _selected = new List<CardInstance>();
        int _nextDraw, _nextEnergy, _spilled;
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
        }

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
            foreach (var c in database.playerStarterDeck) if (c) Player.deck.Add(new CardInstance(c));
            _brain = new EnemyBrain(database.enemyMoves);

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

                // Elle aussi passe par la "boutique" : elle se renforce pour la manche suivante
                int perks = config.enemyPerksPerRound + (_lastRoundWon ? 0 : 1);
                var bought = _brain.Shop(perks, Round);
                if (bought.Count > 0) OnEnemyShopped?.Invoke(bought);

                SetPhase(GamePhase.Shop);
                Shop.GenerateOffers();
                _shopDone = false;
                OnShopOpened?.Invoke();
                yield return new WaitUntil(() => _shopDone);
                OnShopClosed?.Invoke();
            }
        }

        IEnumerator RoundRoutine()
        {
            SetPhase(GamePhase.RoundIntro);
            Turn = 0;
            Player.ResetForRound(Player.maxHp);
            var perks = _brain.progress;
            Enemy.ResetForRound(config.enemyBaseHp + (Round - 1) * config.enemyHpPerRound + perks.bonusMaxHp);
            Enemy.maxEnergy = Mathf.Min(config.enemyMaxEnergy,
                config.enemyEnergyBase + Mathf.FloorToInt((Round - 1) * config.enemyEnergyPerRound) + perks.bonusEnergy);
            Enemy.strength = perks.startStrength;
            Enemy.block = perks.startBlock;
            Player.block = Player.RelicValue(ItemEffect.BouclierDebutManche);
            Player.strength = Player.RelicValue(ItemEffect.ForceDebutManche);
            Player.hand.Clear();
            Player.deck.ResetForRound();
            _brain.ResetForRound();
            _selected.Clear(); _nextDraw = 0; _nextEnergy = 0; _spilled = 0;
            OnRoundStarted?.Invoke(Round);
            NotifyStats();
            if (board) yield return board.RoundStartRoutine(Round);

            while (!Player.IsDead && !Enemy.IsDead)
            {
                yield return PlayerTurnRoutine();
                if (Player.IsDead || Enemy.IsDead) break;
                yield return EnemyTurnRoutine();
            }

            // ----- Fin de manche
            SetPhase(GamePhase.RoundEnd);
            bool won = Enemy.IsDead && !Player.IsDead;
            _lastRoundWon = won;
            Stats.roundsPlayed++;
            var loser = won ? Enemy : Player;
            loser.candles = Mathf.Max(0, loser.candles - 1);
            if (won) Stats.roundsWon++; else Stats.roundsLost++;
            OnCandleLost?.Invoke(loser.side, loser.candles);

            var summary = new RoundSummary { round = Round, playerWon = won, playerCandles = Player.candles, enemyCandles = Enemy.candles };
            int blood;
            if (won)
            {
                blood = config.rewardRoundWin;
                summary.lines.Add($"Manche gagnée : +{config.rewardRoundWin}");
                int bonus = config.lucidityPerBonusBlood > 0 ? Player.hp / config.lucidityPerBonusBlood : 0;
                if (bonus > 0) { blood += bonus; summary.lines.Add($"Lucidité préservée ({Player.hp}) : +{bonus}"); }
            }
            else
            {
                blood = config.rewardRoundLoss;
                summary.lines.Add($"Manche perdue : +{config.rewardRoundLoss}");
            }
            int spilledBlood = Mathf.FloorToInt(_spilled * config.bloodPerDamage);
            if (spilledBlood > 0) { blood += spilledBlood; summary.lines.Add($"Sang versé ({_spilled}) : +{spilledBlood}"); }
            int relicBlood = Player.RelicValue(ItemEffect.GouttesBonus);
            if (relicBlood > 0) { blood += relicBlood; summary.lines.Add($"Reliques : +{relicBlood}"); }
            summary.bloodTotal = blood;

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
            OnTurnStarted?.Invoke(Turn);

            int draw = config.drawPerTurn + (Turn == 1 ? Player.RelicValue(ItemEffect.PiocheDebutManche) : 0) + _nextDraw;
            _nextDraw = 0;
            _busy++;
            yield return DrawCards(draw);
            _busy--;
            NotifyStats();

            while (!_endTurnRequested && !Player.IsDead && !Enemy.IsDead) yield return null;
            while (_busy > 0) yield return null;
            if (Enemy.IsDead || Player.IsDead) yield break;

            // Les cartes sélectionnées sont posées face cachée devant toi, puis retournées et résolues une à une
            var toPlay = new List<CardInstance>(_selected);
            _selected.Clear();
            OnSelectionChanged?.Invoke();
            if (toPlay.Count > 0)
            {
                foreach (var c in toPlay) Player.hand.Remove(c);
                if (board) yield return board.LayDownRoutine(toPlay);
                var resolved = new List<(CardInstance, bool)>();
                foreach (var c in toPlay)
                {
                    if (Enemy.IsDead) break;
                    yield return ResolveCardRoutine(c);
                    resolved.Add((c, c.Exhaust || c.IsCrisis));
                }
                // Cartes non résolues (adversaire déjà vaincu) : défaussées
                foreach (var c in toPlay)
                    if (!resolved.Exists(r => r.Item1 == c)) { Player.deck.Discard(c); resolved.Add((c, false)); }
                if (board) yield return board.ClearPlayedRoutine(resolved);
                if (Enemy.IsDead) yield break;
            }

            // Fin du tour : effets des cartes gardées, Éphémère, défausse
            var discarded = new List<CardInstance>();
            var exhausted = new List<CardInstance>();
            int heldPain = 0;
            foreach (var c in Player.hand)
            {
                heldPain += c.data.loseLucidityIfHeld;
                if (c.Ethereal) { Player.deck.ExhaustCard(c); exhausted.Add(c); }
                else { Player.deck.Discard(c); discarded.Add(c); }
            }
            Player.hand.Clear();
            if (board) yield return board.DiscardHandRoutine(discarded, exhausted);
            if (heldPain > 0)
            {
                int lost = Player.LoseHp(heldPain);
                Stats.damageTaken += lost;
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
                    int dmg = CombatRules.AttackDamage(it.damagePerHit, Enemy, Player);
                    int lost = Player.TakeDamage(dmg, out int blocked);
                    Stats.damageTaken += lost;
                    OnDamaged?.Invoke(Side.Player, lost, blocked);
                    NotifyStats();
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

        IEnumerator DrawCards(int count)
        {
            var drawn = new List<CardInstance>();
            for (int i = 0; i < count; i++)
            {
                if (Player.hand.Count >= config.maxHandSize) break;
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
        IEnumerator ResolveCardRoutine(CardInstance c)
        {
            Player.energy = Mathf.Max(0, Player.energy - c.Cost);
            Stats.cardsPlayed++;
            NotifyStats();
            OnCardPlayed?.Invoke(c);
            if (board) yield return board.RevealPlayedRoutine(c);

            bool low = Player.IsLowLucidity;
            int bonus = c.data.special == CardSpecial.BonusSiLuciditeBasse && low ? c.SpecialValue : 0;

            // Dégâts
            int baseDmg = c.data.special == CardSpecial.DegatsSelonBouclier ? Player.block : (c.Damage > 0 ? c.Damage + bonus : 0);
            if (baseDmg > 0 || c.data.special == CardSpecial.DegatsSelonBouclier)
            {
                for (int h = 0; h < c.Hits && !Enemy.IsDead; h++)
                {
                    int dmg = CombatRules.AttackDamage(baseDmg, Player, Enemy);
                    int lost = Enemy.TakeDamage(dmg, out int blocked);
                    Stats.damageDealt += lost;
                    _spilled += lost;
                    OnDamaged?.Invoke(Side.Opponent, lost, blocked);
                    NotifyStats();
                    if (board) yield return board.PlayerHitRoutine(lost, blocked);
                }
            }
            // Bouclier
            if (c.Block > 0)
            {
                int b = c.Block + bonus;
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
            if (Phase == GamePhase.Shop) return item.data.UsableInShop && (item.data.effect != ItemEffect.Soigner);
            return CanAct;
        }

        public void UseItem(OwnedItem item)
        {
            if (!CanUseItem(item)) { Message("Impossible d'utiliser cet objet maintenant."); AudioManager.Play(Sfx.Error); return; }
            StartCoroutine(UseItemRoutine(item));
        }

        IEnumerator UseItemRoutine(OwnedItem item)
        {
            _busy++;
            int v = item.data.value;
            Player.items.Remove(item);
            NotifyItemsChanged();
            Message($"{item.data.itemName}.");
            AudioManager.Play(Sfx.Heal, 0.6f);
            switch (item.data.effect)
            {
                case ItemEffect.GagnerBouclier: Player.block += v; OnBlockGained?.Invoke(Side.Player, v); break;
                case ItemEffect.GagnerEnergie: Player.energy += v; break;
                case ItemEffect.Soigner: { int h = Player.Heal(v); if (h > 0) OnHealed?.Invoke(Side.Player, h); break; }
                case ItemEffect.Piocher: yield return DrawCards(v); break;
                case ItemEffect.AppliquerVulnerable: Enemy.vulnerable += v; break;
                case ItemEffect.AppliquerFaiblesse: Enemy.weak += v; break;
                case ItemEffect.GagnerForce: Player.strength += v; break;
                case ItemEffect.PurgerDeck:
                {
                    var toRemove = new List<CardInstance>();
                    foreach (var c in Player.deck.All) if (c.IsCrisis && !Player.hand.Contains(c)) toRemove.Add(c);
                    foreach (var c in toRemove) Player.deck.Remove(c);
                    NotifyDeckChanged();
                    Message($"{toRemove.Count} Crise(s) retirée(s) de ton deck.");
                    break;
                }
            }
            NotifyStats();
            yield return null;
            _busy--;
        }
    }
}
