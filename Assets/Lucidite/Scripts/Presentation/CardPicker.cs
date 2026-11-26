using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Lucidite
{
    /// <summary>
    /// Survol / clic des cartes 3D à travers l'écran PSX. Clic gauche sur une carte de la main : la jouer.
    /// </summary>
    public class CardPicker : MonoBehaviour
    {
        public HandView playerHand;
        public LayerMask cardMask = ~(1 << 2); // ignore la couche "Ignore Raycast" (débris, collisions de la pièce)
        public float maxDistance = 5f;

        public static event Action<CardView> OnHoverChanged;
        public CardView CurrentHover { get; private set; }

        void Update()
        {
            var gm = GameManager.Instance;
            var mouse = Mouse.current;
            CardView hit = null;

            if (mouse != null && PSXScreen.Instance != null && gm != null && gm.Phase != GamePhase.Title && !gm.IsPaused && !PointerOverUI())
            {
                var ray = PSXScreen.Instance.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out var info, maxDistance, cardMask, QueryTriggerInteraction.Collide))
                {
                    var v = info.collider.GetComponentInParent<CardView>();
                    if (v && v.FaceUp) hit = v;
                }
            }

            if (hit != CurrentHover)
            {
                if (CurrentHover) CurrentHover.IsHovered = false;
                CurrentHover = hit;
                if (hit) hit.IsHovered = true;
                bool isPlayerCard = playerHand && playerHand.Contains(hit);
                if (playerHand) playerHand.Hovered = isPlayerCard ? hit : null;
                if (isPlayerCard) AudioManager.Play(Sfx.Hover, 0.5f);
                OnHoverChanged?.Invoke(hit);
            }

            // Surbrillance : cartes jouables (énergie suffisante) et carte survolée
            if (playerHand && gm != null && gm.Player != null)
            {
                foreach (var c in playerHand.Cards)
                {
                    if (!c || c.Card == null) continue;
                    bool playable = gm.IsPlayable(c.Card, out _);
                    bool selected = gm.IsSelected(c.Card);
                    c.SetAffordable(playable);
                    bool hovered = c == playerHand.Hovered;
                    // Sélectionnée : écarlate pulsant. Survolée : ambre. Jouable : rouge discret.
                    // Écarlate sombre, pulsation lente
                    var scarlet = Color.Lerp(Palette.Hex("4A0306"), Palette.Hex("7E0A10"), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
                    if (selected) c.SetHighlight(true, scarlet);
                    else if (hovered) c.SetHighlight(true, playable && gm.CanAct ? Palette.Hope : Palette.AshDark);
                    else c.SetHighlight(false, Palette.Blood);
                }
            }

            // Clic : sélectionne / désélectionne la carte (elle sera jouée avec « Jouer le tour »)
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && gm != null && playerHand && playerHand.Contains(hit))
            {
                if (gm.CanAct) gm.ToggleSelect(hit.Card);
            }
            // Clic droit : tout désélectionner
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && gm != null && gm.CanAct)
                foreach (var c in new System.Collections.Generic.List<CardInstance>(gm.Selected)) gm.ToggleSelect(c);
        }

        static bool PointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
