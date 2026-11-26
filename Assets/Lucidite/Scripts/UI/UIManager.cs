using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>Relie les panneaux d'interface aux événements du GameManager.</summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        public TitlePanel title;
        public RulesPanel rules;
        public OptionsPanel options;
        public HUDPanel hud;
        public CardInspectorPanel inspector;
        public RoundSummaryPanel roundSummary;
        public ShopPanel shop;
        public DeckPickerPanel deckPicker;
        public DeckPanel deck;
        public PausePanel pause;
        public EndPanel end;
        public ToastFeed toast;
        public ToastFeed banner;
        public Image fader;

        GameManager _gm;
        float _fade;

        void Awake()
        {
            Instance = this;
            foreach (var p in GetComponentsInChildren<UIPanel>(true)) p.HideInstant();
            if (fader) { fader.color = Palette.Void; fader.raycastTarget = false; _fade = 1f; }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CardPicker.OnHoverChanged -= HandleHover;
        }

        void Start()
        {
            _gm = GameManager.Instance;
            CameraRig.ShakeMultiplier = OptionsPanel.ShakeEnabled ? 1f : 0.15f;
            WireButtons();
            CardPicker.OnHoverChanged += HandleHover;
            if (!_gm) return;

            _gm.OnPhaseChanged += HandlePhase;
            _gm.OnRoundEnded += s => roundSummary.ShowSummary(s, _gm.Player.maxCandles, _gm.Enemy.maxCandles);
            _gm.OnEnemyShopped += bought =>
            {
                shop.EnemyNews = string.Join("   ·   ", bought);
                if (toast) foreach (var b in bought) toast.Push("La Dépression — " + b);
            };
            _gm.OnShopOpened += () => { roundSummary.Hide(); shop.Show(); };
            _gm.OnShopClosed += () => { shop.Hide(); deckPicker.Hide(); inspector.ResetState(); };
            _gm.OnMatchEnded += won => StartCoroutine(EndRoutine(won));
            _gm.OnMessage += msg => { if (toast) toast.Push(msg); };
            _gm.OnPausedChanged += paused => { if (paused) pause.Show(); else { pause.Hide(); rules.Hide(); options.Hide(); } };
            _gm.OnRoundStarted += round => { if (banner) banner.Push($"MANCHE {round}"); };
            _gm.OnTurnStarted += turn => { if (banner && turn > 1) banner.Push("TON TOUR"); };
            _gm.OnItemsChanged += () => { if (shop.IsVisible) shop.Refresh(); };

            HandlePhase(_gm.Phase);
        }

        void HandleHover(CardView v) { if (inspector) inspector.HandleHover(v); }

        void WireButtons()
        {
            title.playButton.onClick.AddListener(() => { title.Hide(); if (_gm) _gm.StartMatch(); });
            title.rulesButton.onClick.AddListener(() => rules.Show());
            title.optionsButton.onClick.AddListener(() => options.Show());
            title.quitButton.onClick.AddListener(() => _gm?.QuitGame());

            pause.resumeButton.onClick.AddListener(() => _gm?.SetPaused(false));
            pause.rulesButton.onClick.AddListener(() => rules.Show());
            pause.optionsButton.onClick.AddListener(() => options.Show());
            pause.restartButton.onClick.AddListener(() => _gm?.RestartMatch());
            pause.menuButton.onClick.AddListener(() => _gm?.ReturnToTitle());

            end.restartButton.onClick.AddListener(() => _gm?.RestartMatch());
            end.menuButton.onClick.AddListener(() => _gm?.ReturnToTitle());

            if (hud.endTurnButton) hud.endTurnButton.onClick.AddListener(() => { AudioManager.Play(Sfx.EndTurn); _gm?.EndTurn(); });
            if (hud.drawPileButton) hud.drawPileButton.onClick.AddListener(() => deck.ShowPile(DeckPanel.Pile.Draw));
            if (hud.discardPileButton) hud.discardPileButton.onClick.AddListener(() => deck.ShowPile(DeckPanel.Pile.Discard));
            if (shop.deckPicker == null) shop.deckPicker = deckPicker;

            foreach (var b in GetComponentsInChildren<Button>(true))
                b.onClick.AddListener(() => AudioManager.Play(Sfx.UIClick));
        }

        void HandlePhase(GamePhase p)
        {
            switch (p)
            {
                case GamePhase.Title:
                    title.Show();
                    hud.Hide();
                    break;
                case GamePhase.RoundIntro:
                case GamePhase.PlayerTurn:
                case GamePhase.EnemyTurn:
                    title.Hide();
                    hud.Show();
                    break;
                case GamePhase.RoundEnd:
                case GamePhase.Shop:
                    hud.Hide();
                    inspector.Hide();
                    break;
            }
        }

        IEnumerator EndRoutine(bool won)
        {
            shop.Hide();
            roundSummary.Hide();
            hud.Hide();
            yield return new WaitForSeconds(2.5f);
            end.ShowEnd(won, _gm.Stats);
        }

        void Update()
        {
            if (!fader) return;
            _fade = Mathf.MoveTowards(_fade, 0f, Time.unscaledDeltaTime / 1.2f);
            fader.color = Palette.Void.WithAlpha(_fade);
            fader.enabled = _fade > 0.001f;
        }
    }
}
