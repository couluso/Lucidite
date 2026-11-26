using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Boutique entre les manches : 3 cartes (chacune avec sa classe), des pilules, des reliques (statuettes),
    /// et des services sur le deck (faire évoluer, transformer une Crise, brûler), utilisables plusieurs fois :
    /// leur prix monte à chaque utilisation et revient à la base à la boutique suivante. « Relancer » renouvelle
    /// toutes les offres (prix croissant lui aussi).
    /// </summary>
    public class ShopSystem
    {
        readonly GameConfig _cfg;
        readonly CardDatabase _db;
        readonly Fighter _player;
        readonly System.Func<int> _getBlood;
        readonly System.Action<int> _spendBlood;
        int _removals;
        int _upgrades, _transforms, _rerolls, _freeRerolls;

        public readonly List<CardInstance> cardOffers = new List<CardInstance>();
        public readonly List<ItemData> pillOffers = new List<ItemData>();
        public readonly List<ItemData> relicOffers = new List<ItemData>();

        public ShopSystem(GameConfig cfg, CardDatabase db, Fighter player, System.Func<int> getBlood, System.Action<int> spendBlood)
        {
            _cfg = cfg; _db = db; _player = player; _getBlood = getBlood; _spendBlood = spendBlood;
        }

        int Discounted(int p) => Mathf.Max(1, Mathf.RoundToInt(p * (1f - _player.RelicValue(ItemEffect.RemiseBoutique) / 100f)));

        public int CardPrice(CardData c)
        {
            if (c.priceOverride > 0) return Discounted(c.priceOverride);
            switch (c.rarity)
            {
                case CardRarity.Rare: return Discounted(_cfg.priceRare);
                case CardRarity.PeuCommune: return Discounted(_cfg.priceUncommon);
                default: return Discounted(_cfg.priceCommon);
            }
        }
        public int ItemPrice(ItemData i) => Discounted(i.price);
        public int UpgradeCost => Discounted(_cfg.upgradeCost + _upgrades * _cfg.serviceCostIncrease);
        public int TransformCost => Discounted(_cfg.transformCost + _transforms * _cfg.serviceCostIncrease);
        public int RemoveCost => Discounted(_cfg.removeCostBase + _removals * _cfg.removeCostIncrease);
        public bool FreeReroll => _freeRerolls > 0;
        public int RerollCost => FreeReroll ? 0 : Discounted(_cfg.rerollCost + _rerolls * _cfg.rerollCostIncrease);

        /// <summary>Nouvelle boutique : prix des services remis à la base, nouvelles offres.</summary>
        public void GenerateOffers()
        {
            _upgrades = _transforms = _rerolls = 0;
            _freeRerolls = _player.RelicValue(ItemEffect.RelanceGratuite);
            Roll();
        }

        void Roll()
        {
            cardOffers.Clear();
            pillOffers.Clear();
            relicOffers.Clear();

            var pool = new List<CardData>();
            foreach (var c in _db.shopPool)
            {
                if (c == null || !c.inShop) continue;
                int w = c.rarity == CardRarity.Rare ? 1 : c.rarity == CardRarity.PeuCommune ? 2 : 3;
                for (int i = 0; i < w; i++) pool.Add(c);
            }
            while (cardOffers.Count < _cfg.cardOffers && pool.Count > 0)
            {
                var c = pool[Random.Range(0, pool.Count)];
                cardOffers.Add(new CardInstance(c, (CardSuit)Random.Range(1, 5)));
                pool.RemoveAll(x => x == c);
            }

            var relics = new List<ItemData>();
            var pills = new List<ItemData>();
            foreach (var i in _db.items)
            {
                if (i == null) continue;
                if (i.rare && Random.value > 0.25f) continue;
                if (i.kind == ItemKind.Relique) { if (!_player.relics.Contains(i)) relics.Add(i); }
                else pills.Add(i);
            }
            Pick(pills, pillOffers, _cfg.pillOffers);
            Pick(relics, relicOffers, _cfg.relicOffers);
        }

        static void Pick(List<ItemData> from, List<ItemData> into, int count)
        {
            var bag = new List<ItemData>(from);
            while (into.Count < count && bag.Count > 0)
            {
                int k = Random.Range(0, bag.Count);
                into.Add(bag[k]);
                bag.RemoveAt(k);
            }
        }

        bool Pay(int cost)
        {
            if (_getBlood() < cost) return false;
            if (cost > 0) _spendBlood(cost);
            return true;
        }

        public string Reroll()
        {
            if (!Pay(RerollCost)) return "Pas assez de gouttes de sang.";
            if (_freeRerolls > 0) _freeRerolls--; else _rerolls++;
            Roll();
            return null;
        }

        public string BuyCard(CardInstance offer)
        {
            if (!cardOffers.Contains(offer)) return "Offre invalide.";
            if (!Pay(CardPrice(offer.data))) return "Pas assez de gouttes de sang.";
            _player.deck.Add(offer);
            cardOffers.Remove(offer);
            return null;
        }

        public bool CanHoldItem(ItemData item) =>
            item.kind == ItemKind.Relique ? _player.relics.Count < _cfg.relicSlots : _player.items.Count < _cfg.itemSlots;

        public string BuyItem(ItemData item)
        {
            var list = item.kind == ItemKind.Relique ? relicOffers : pillOffers;
            if (!list.Contains(item)) return "Offre invalide.";
            if (!CanHoldItem(item)) return item.kind == ItemKind.Relique ? "Plus de place sur la table." : "Ton pilulier est plein.";
            if (item.kind != ItemKind.Relique && !Pay(ItemPrice(item))) return "Pas assez de gouttes de sang.";
            if (item.kind == ItemKind.Relique)
            {
                if (item.effect == ItemEffect.Reliquaire && GameManager.RandomOtherRelic(_player, item) == null) return "Il te faut une autre relique à magnifier.";
                if (!Pay(ItemPrice(item))) return "Pas assez de gouttes de sang.";
                _player.relics.Add(item);
            }
            else _player.items.Add(new OwnedItem(item));
            list.Remove(item);
            return null;
        }

        public string Upgrade(CardInstance card)
        {
            if (!card.CanUpgrade) return "Cette carte ne peut pas évoluer.";
            if (!Pay(UpgradeCost)) return "Pas assez de gouttes de sang.";
            card.Upgrade();
            _upgrades++;
            return null;
        }

        public string Remove(CardInstance card)
        {
            if (_player.deck.All.Count <= 5) return "Ton deck doit garder au moins 5 cartes.";
            if (!Pay(RemoveCost)) return "Pas assez de gouttes de sang.";
            _player.deck.Remove(card);
            _removals++;
            return null;
        }

        public string Transform(CardInstance card)
        {
            if (!card.CanTransform) return "Cette carte ne peut pas être transformée.";
            if (!Pay(TransformCost)) return "Pas assez de gouttes de sang.";
            card.Transform();
            _transforms++;
            return null;
        }
    }
}
