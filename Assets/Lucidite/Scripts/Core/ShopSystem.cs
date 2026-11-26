using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Boutique entre les manches, volontairement courte : 3 cartes, 1 relique, 1 consommable,
    /// et trois services sur le deck (améliorer, retirer, transformer une Crise).
    /// </summary>
    public class ShopSystem
    {
        readonly GameConfig _cfg;
        readonly CardDatabase _db;
        readonly Fighter _player;
        readonly System.Func<int> _getBlood;
        readonly System.Action<int> _spendBlood;
        int _removals;

        public readonly List<CardData> cardOffers = new List<CardData>();
        public readonly List<ItemData> itemOffers = new List<ItemData>();
        public bool upgradeUsed, removeUsed, transformUsed;

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
        public int UpgradeCost => Discounted(_cfg.upgradeCost);
        public int RemoveCost => Discounted(_cfg.removeCostBase + _removals * _cfg.removeCostIncrease);
        public int TransformCost => Discounted(_cfg.transformCost);

        public void GenerateOffers()
        {
            cardOffers.Clear();
            itemOffers.Clear();
            upgradeUsed = removeUsed = transformUsed = false;

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
                cardOffers.Add(c);
                pool.RemoveAll(x => x == c);
            }

            var relics = new List<ItemData>();
            var consumables = new List<ItemData>();
            foreach (var i in _db.items)
            {
                if (i == null) continue;
                if (i.kind == ItemKind.Relique) { if (!_player.relics.Contains(i)) relics.Add(i); }
                else consumables.Add(i);
            }
            if (relics.Count > 0) itemOffers.Add(relics[Random.Range(0, relics.Count)]);
            if (consumables.Count > 0) itemOffers.Add(consumables[Random.Range(0, consumables.Count)]);
        }

        bool Pay(int cost)
        {
            if (_getBlood() < cost) return false;
            _spendBlood(cost);
            return true;
        }

        public string BuyCard(CardData data)
        {
            if (!cardOffers.Contains(data)) return "Offre invalide.";
            if (!Pay(CardPrice(data))) return "Pas assez de gouttes de sang.";
            _player.deck.Add(new CardInstance(data));
            cardOffers.Remove(data);
            return null;
        }

        public bool CanHoldItem(ItemData item) =>
            item.kind == ItemKind.Relique ? _player.relics.Count < _cfg.relicSlots : _player.items.Count < _cfg.itemSlots;

        public string BuyItem(ItemData item)
        {
            if (!itemOffers.Contains(item)) return "Offre invalide.";
            if (!CanHoldItem(item)) return "Plus de place pour cet objet.";
            if (!Pay(ItemPrice(item))) return "Pas assez de gouttes de sang.";
            if (item.kind == ItemKind.Relique)
            {
                _player.relics.Add(item);
                if (item.effect == ItemEffect.LuciditeMax) _player.maxHp += item.value;
            }
            else _player.items.Add(new OwnedItem(item));
            itemOffers.Remove(item);
            return null;
        }

        public string Upgrade(CardInstance card)
        {
            if (upgradeUsed) return "Déjà fait pour cette boutique.";
            if (!card.CanUpgrade) return "Cette carte ne peut pas évoluer.";
            if (!Pay(UpgradeCost)) return "Pas assez de gouttes de sang.";
            card.Upgrade();
            upgradeUsed = true;
            return null;
        }

        public string Remove(CardInstance card)
        {
            if (removeUsed) return "Déjà fait pour cette boutique.";
            if (_player.deck.All.Count <= 5) return "Ton deck doit garder au moins 5 cartes.";
            if (!Pay(RemoveCost)) return "Pas assez de gouttes de sang.";
            _player.deck.Remove(card);
            _removals++;
            removeUsed = true;
            return null;
        }

        public string Transform(CardInstance card)
        {
            if (transformUsed) return "Déjà fait pour cette boutique.";
            if (!card.CanTransform) return "Cette carte ne peut pas être transformée.";
            if (!Pay(TransformCost)) return "Pas assez de gouttes de sang.";
            card.Transform();
            transformUsed = true;
            return null;
        }
    }
}
