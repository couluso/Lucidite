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
        PileView _hoverPile;
        TableItemView _hoverItem;
        PillBoxView _hoverBox;
        // Glisser-déposer dans la main : appui sur une carte, puis déplacement au-delà d'un seuil
        CardView _pressCard;
        Vector2 _pressPos;
        bool _dragging;
        const float DragThreshold = 14f;

        void Update()
        {
            var gm = GameManager.Instance;
            var mouse = Mouse.current;
            CardView hit = null;
            PileView pile = null;
            TableItemView item = null;
            PillBoxView box = null;
            Ray ray = default;
            bool hasRay = false;

            if (mouse != null && PSXScreen.Instance != null && gm != null && gm.Phase != GamePhase.Title && !gm.IsPaused && !PointerOverUI())
            {
                ray = PSXScreen.Instance.ScreenPointToRay(mouse.position.ReadValue());
                hasRay = true;
                if (Physics.Raycast(ray, out var info, maxDistance, cardMask, QueryTriggerInteraction.Collide))
                {
                    var v = info.collider.GetComponentInParent<CardView>();
                    if (v && v.FaceUp) hit = v;
                    else if (!v)
                    {
                        pile = info.collider.GetComponent<PileView>();
                        item = info.collider.GetComponent<TableItemView>();
                        box = info.collider.GetComponentInParent<PillBoxView>();
                    }
                }
            }

            // Glisser une carte de la main : elle change de place (l'ordre de la main est l'ordre de jeu)
            if (mouse != null && gm != null && playerHand)
            {
                if (mouse.leftButton.wasPressedThisFrame && playerHand.Contains(hit) && gm.CanAct)
                {
                    _pressCard = hit; _pressPos = mouse.position.ReadValue(); _dragging = false;
                }
                if (_pressCard && mouse.leftButton.isPressed)
                {
                    if (!_dragging && (mouse.position.ReadValue() - _pressPos).magnitude > DragThreshold && gm.CanAct)
                    {
                        _dragging = true;
                        playerHand.Dragged = _pressCard;
                        AudioManager.Play(Sfx.CardSlide, 0.5f);
                    }
                    if (_dragging && hasRay && _pressCard.Card != null)
                    {
                        var t = playerHand.transform;
                        var plane = new Plane(t.forward, t.position);
                        if (plane.Raycast(ray, out float enter))
                            gm.MoveInHand(_pressCard.Card, playerHand.IndexAtLocalX(t.InverseTransformPoint(ray.GetPoint(enter)).x));
                    }
                    if (_dragging) hit = _pressCard;
                }
                if (_pressCard && mouse.leftButton.wasReleasedThisFrame)
                {
                    // Simple clic (sans glisser) : sélectionne / désélectionne la carte
                    if (!_dragging && hit == _pressCard && gm.CanAct) gm.ToggleSelect(_pressCard.Card);
                    _pressCard = null; _dragging = false; playerHand.Dragged = null;
                }
                if (_pressCard && (!playerHand.Contains(_pressCard) || !gm.CanAct)) { _pressCard = null; _dragging = false; playerHand.Dragged = null; }
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

            // Pioche / défausse sur la table : contour orange au survol, clic = liste des cartes
            if (pile != _hoverPile)
            {
                if (_hoverPile) _hoverPile.SetHovered(false);
                _hoverPile = pile;
                if (pile) { pile.SetHovered(true); AudioManager.Play(Sfx.Hover, 0.5f); }
            }
            if (pile && mouse != null && mouse.leftButton.wasPressedThisFrame && UIManager.Instance && UIManager.Instance.deck)
            {
                AudioManager.Play(Sfx.Select, 0.6f);
                UIManager.Instance.deck.ShowPile(pile.pile);
            }

            // Piluliers : le tien s'ouvre / se ferme au clic, le sien montre ce qu'il contient
            if (box != _hoverBox)
            {
                var ui = UIManager.Instance;
                if (_hoverBox) { _hoverBox.SetHovered(false); if (ui && ui.inspector) ui.inspector.HideText(); }
                _hoverBox = box;
                if (box)
                {
                    box.SetHovered(true);
                    AudioManager.Play(Sfx.Hover, 0.4f);
                    if (ui && ui.inspector) ui.inspector.ShowText(box.Title, box.Subtitle, box.Body);
                }
            }
            if (box && box.side == Side.Player && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                box.Toggle();
                var ui = UIManager.Instance;
                if (ui && ui.inspector) ui.inspector.ShowText(box.Title, box.Subtitle, box.Body);
            }

            // Reliques et pilules posées sur la table : survol = description, clic sur une de tes pilules = l'avaler
            if (item != _hoverItem)
            {
                if (_hoverItem) _hoverItem.SetHovered(false);
                var ui = UIManager.Instance;
                if (_hoverItem && ui && ui.inspector) ui.inspector.HideText();
                _hoverItem = item;
                if (item)
                {
                    item.SetHovered(true);
                    AudioManager.Play(Sfx.Hover, 0.4f);
                    if (ui && ui.inspector)
                    {
                        ui.inspector.ShowText(item.Data.itemName, item.Subtitle, item.Body);
                    }
                }
            }
            if (item && item.Clickable && mouse != null && mouse.leftButton.wasPressedThisFrame && gm != null) gm.UseItem(item.Owned);
            if (item && _hoverItem == item && UIManager.Instance && UIManager.Instance.inspector && Time.frameCount % 10 == 0)
                UIManager.Instance.inspector.ShowText(item.Data.itemName, item.Subtitle, item.Body); // voile / reliquaire peuvent changer

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
                    // Sélectionnée : écarlate sombre brillant, pulsation lente (plus de gouttes de sang).
                    // Survolée : ambre. Jouable : rouge discret.
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
                    var scarlet = Color.Lerp(Palette.Hex("5A0408"), Palette.Hex("8E0C14"), pulse);
                    if (selected) c.SetHighlight(true, scarlet, 2.6f + pulse * 1.8f);
                    else if (hovered) c.SetHighlight(true, playable && gm.CanAct ? Palette.Hope : Palette.AshDark);
                    else c.SetHighlight(false, Palette.Blood);
                }
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
