using System.Collections.Generic;

namespace Lucidite
{
    /// <summary>Deck : cartes possédées + piles de la manche (pioche, défausse, épuisées).</summary>
    public class Deck
    {
        readonly List<CardInstance> _all = new List<CardInstance>();
        readonly List<CardInstance> _draw = new List<CardInstance>();
        readonly List<CardInstance> _discard = new List<CardInstance>();
        readonly List<CardInstance> _exhaust = new List<CardInstance>();
        readonly System.Random _rng;

        public IReadOnlyList<CardInstance> All => _all;
        public IReadOnlyList<CardInstance> DrawPile => _draw;
        public IReadOnlyList<CardInstance> DiscardPile => _discard;
        public IReadOnlyList<CardInstance> ExhaustPile => _exhaust;
        public int DrawCount => _draw.Count;
        public int DiscardCount => _discard.Count;

        public Deck(int seed) { _rng = new System.Random(seed); }

        public bool Contains(CardInstance c) => _all.Contains(c);

        /// <summary>Ajoute une carte au deck. Pendant une manche, elle peut aller dans la pioche ou la défausse.</summary>
        public void Add(CardInstance c, bool toDiscard = false, bool shuffleIntoDraw = false)
        {
            _all.Add(c);
            if (toDiscard) _discard.Add(c);
            else if (shuffleIntoDraw) _draw.Insert(_rng.Next(_draw.Count + 1), c);
        }

        public void Remove(CardInstance c)
        {
            _all.Remove(c);
            _draw.Remove(c);
            _discard.Remove(c);
            _exhaust.Remove(c);
        }

        /// <summary>Début de manche : toutes les cartes du deck dans la pioche, mélangées.</summary>
        public void ResetForRound()
        {
            _draw.Clear(); _discard.Clear(); _exhaust.Clear();
            _draw.AddRange(_all);
            Shuffle(_draw);
        }

        public CardInstance Draw()
        {
            if (_draw.Count == 0)
            {
                _draw.AddRange(_discard);
                _discard.Clear();
                Shuffle(_draw);
            }
            if (_draw.Count == 0) return null;
            var c = _draw[_draw.Count - 1];
            _draw.RemoveAt(_draw.Count - 1);
            return c;
        }

        public void Discard(CardInstance c)
        {
            if (c != null && _all.Contains(c) && !_discard.Contains(c)) _discard.Add(c);
        }

        public void ExhaustCard(CardInstance c)
        {
            if (c != null && !_exhaust.Contains(c)) _exhaust.Add(c);
        }

        void Shuffle(List<CardInstance> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
