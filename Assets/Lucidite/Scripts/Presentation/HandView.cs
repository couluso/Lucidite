using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Dispose les cartes d'une main en éventail (jusqu'à 10 cartes). Côté joueuse, la carte survolée
    /// se redresse, grandit et s'approche de la caméra. Côté Dépression, les cartes sont tenues dos à toi.
    /// </summary>
    public class HandView : MonoBehaviour
    {
        public Side side = Side.Player;
        public float radius = 0.55f;
        public float maxAngleStep = 8f;
        [Tooltip("Ouverture totale maximale de l'éventail (degrés).")]
        public float maxSpread = 46f;
        public float depthStep = 0.003f;
        public float cardScale = 0.066f;

        [Header("Survol (joueuse)")]
        public float hoverLift = 0.03f;
        public float hoverForward = 0.08f;
        public float hoverScale = 1.45f;
        public float neighbourSpread = 0.016f;
        [Tooltip("Hauteur dont une carte sélectionnée dépasse les autres.")]
        public float selectedLift = 0.045f;

        readonly HashSet<CardView> _wasSelected = new HashSet<CardView>();

        readonly List<CardView> _cards = new List<CardView>();
        public IReadOnlyList<CardView> Cards => _cards;
        public CardView Hovered { get; set; }
        /// <summary>Carte que la joueuse fait glisser pour réorganiser sa main.</summary>
        public CardView Dragged { get; set; }
        public bool Contains(CardView v) => v && _cards.Contains(v);

        public void Add(CardView v)
        {
            if (_cards.Contains(v)) return;
            v.transform.SetParent(transform, true);
            _cards.Add(v);
        }

        public void Remove(CardView v)
        {
            _cards.Remove(v);
            _wasSelected.Remove(v);
            if (Hovered == v) Hovered = null;
        }

        public void Clear()
        {
            _cards.Clear();
            Hovered = null;
        }

        /// <summary>
        /// Coins inférieurs de l'éventail "au repos" (sans survol ni sélection) : bas-gauche de la carte la plus à gauche,
        /// bas-droite de la plus à droite. Sert à placer les mains.
        /// </summary>
        public bool GetRestFanCorners(out Vector3 leftWorld, out Vector3 rightWorld)
        {
            leftWorld = rightWorld = Vector3.zero;
            int n = 0;
            foreach (var c in _cards) if (c) n++;
            if (n == 0) return false;
            float step = n > 1 ? Mathf.Min(maxAngleStep, maxSpread / (n - 1)) : 0f;
            Vector3 Corner(int i, float sx)
            {
                float t = i - (n - 1) * 0.5f;
                float theta = t * step;
                float rad = theta * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius - radius, -t * depthStep);
                var rot = Quaternion.Euler(0f, 0f, -theta);
                var corner = rot * new Vector3(sx * 0.5f, -0.75f, 0f) * cardScale;
                return transform.TransformPoint(pos + corner);
            }
            leftWorld = Corner(0, -1f);
            rightWorld = Corner(n - 1, 1f);
            return true;
        }

        /// <summary>Axe "haut" (monde) des cartes extrêmes au repos : les mains s'alignent sur leur inclinaison.</summary>
        public bool GetRestFanUps(out Vector3 leftUp, out Vector3 rightUp)
        {
            leftUp = rightUp = transform.up;
            int n = 0;
            foreach (var c in _cards) if (c) n++;
            if (n == 0) return false;
            float step = n > 1 ? Mathf.Min(maxAngleStep, maxSpread / (n - 1)) : 0f;
            float half = (n - 1) * 0.5f * step;
            leftUp = transform.TransformDirection(Quaternion.Euler(0f, 0f, half) * Vector3.up);
            rightUp = transform.TransformDirection(Quaternion.Euler(0f, 0f, -half) * Vector3.up);
            return true;
        }

        /// <summary>Emplacement de l'éventail le plus proche d'un point (coordonnées locales de la main).</summary>
        public int IndexAtLocalX(float localX)
        {
            int n = _cards.Count;
            if (n <= 1) return 0;
            float step = Mathf.Min(maxAngleStep, maxSpread / (n - 1));
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Sin((i - (n - 1) * 0.5f) * step * Mathf.Deg2Rad) * radius;
                float d = Mathf.Abs(x - localX);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>Côté joueuse, l'éventail suit l'ordre de sa main (les cartes sont jouées de gauche à droite).</summary>
        void SyncOrder()
        {
            var gm = GameManager.Instance;
            if (side != Side.Player || !gm || gm.Player == null) return;
            var hand = gm.Player.hand;
            int Key(CardView v) { int k = v && v.Card != null ? hand.IndexOf(v.Card) : -1; return k < 0 ? 10000 : k; }
            bool sorted = true;
            for (int i = 1; i < _cards.Count && sorted; i++) if (Key(_cards[i - 1]) > Key(_cards[i])) sorted = false;
            if (sorted) return;
            // Tri stable par insertion (peu de cartes)
            for (int i = 1; i < _cards.Count; i++)
            {
                var v = _cards[i]; int k = Key(v), j = i - 1;
                while (j >= 0 && Key(_cards[j]) > k) { _cards[j + 1] = _cards[j]; j--; }
                _cards[j + 1] = v;
            }
        }

        void LateUpdate()
        {
            _cards.RemoveAll(c => !c);
            SyncOrder();
            int n = _cards.Count;
            if (n == 0) return;
            float step = n > 1 ? Mathf.Min(maxAngleStep, maxSpread / (n - 1)) : 0f;
            int hoveredIndex = Hovered ? _cards.IndexOf(Hovered) : -1;
            for (int i = 0; i < n; i++)
            {
                var v = _cards[i];
                float t = i - (n - 1) * 0.5f;
                float theta = t * step;
                float rad = theta * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius - radius, -t * depthStep);
                var rot = Quaternion.Euler(0f, 0f, -theta);
                float s = cardScale;

                if (hoveredIndex >= 0 && i != hoveredIndex)
                    pos.x += Mathf.Sign(i - hoveredIndex) * neighbourSpread / Mathf.Max(1, Mathf.Abs(i - hoveredIndex));

                // Carte sélectionnée : surélevée au-dessus des autres, légèrement en avant (lueur écarlate : CardPicker)
                bool selected = side == Side.Player && v.Card != null && GameManager.Instance && GameManager.Instance.IsSelected(v.Card);
                if (selected)
                {
                    pos.y += selectedLift;
                    pos.z -= 0.012f;
                    s *= 1.06f;
                }
                if (side == Side.Player)
                {
                    if (selected != _wasSelected.Contains(v))
                    {
                        if (selected) { _wasSelected.Add(v); v.Punch(0.18f); } else _wasSelected.Remove(v);
                    }
                }

                if (v == Dragged && side == Side.Player)
                {
                    // Glissée : soulevée, droite, un peu en avant
                    pos = new Vector3(pos.x, hoverLift * 1.4f, -hoverForward * 0.6f);
                    rot = Quaternion.identity;
                    s *= 1.2f;
                }
                else if (i == hoveredIndex && side == Side.Player)
                {
                    pos = new Vector3(pos.x, hoverLift + (selected ? selectedLift * 0.5f : 0f), -hoverForward);
                    rot = Quaternion.identity;
                    s *= hoverScale;
                }
                v.SetTarget(pos, rot, Vector3.one * s);
            }
        }
    }
}
