using UnityEngine;

namespace Lucidite
{
    /// <summary>Reliques (passives) et consommables achetables en boutique.</summary>
    [CreateAssetMenu(menuName = "Lucidité/Objet", fileName = "Item_")]
    public class ItemData : ScriptableObject
    {
        public string itemName = "Objet";
        public ItemKind kind = ItemKind.Consommable;
        public ItemEffect effect = ItemEffect.GagnerBouclier;
        public int value = 1;
        public int price = 60;
        [TextArea(2, 4)] public string flavorText = "";
        public Color tint = new Color(0.6f, 0.1f, 0.1f);

        public bool IsPassive => kind == ItemKind.Relique;
        public bool UsableInShop => effect == ItemEffect.Soigner || effect == ItemEffect.PurgerDeck;

        public static string KindLabel(ItemKind k) => k == ItemKind.Relique ? "Relique" : "Consommable";

        public string Describe()
        {
            int v = value;
            switch (effect)
            {
                case ItemEffect.LuciditeMax: return $"+{v} Lucidité maximale.";
                case ItemEffect.BouclierDebutManche: return $"Commence chaque manche avec {v} Bouclier.";
                case ItemEffect.PiocheDebutManche: return $"Pioche {v} carte(s) de plus au premier tour de chaque manche.";
                case ItemEffect.ForceDebutManche: return $"Commence chaque manche avec {v} Force.";
                case ItemEffect.EnergiePremierTour: return $"+{v} énergie au premier tour de chaque manche.";
                case ItemEffect.GouttesBonus: return $"+{v} gouttes de sang à chaque fin de manche.";
                case ItemEffect.RemiseBoutique: return $"Les prix de la boutique baissent de {v} %.";
                case ItemEffect.GagnerBouclier: return $"Gagne {v} Bouclier.";
                case ItemEffect.GagnerEnergie: return $"Gagne {v} énergie.";
                case ItemEffect.Soigner: return $"Récupère {v} Lucidité.";
                case ItemEffect.Piocher: return $"Pioche {v} carte(s).";
                case ItemEffect.AppliquerVulnerable: return $"Applique {v} Vulnérable à la Dépression.";
                case ItemEffect.AppliquerFaiblesse: return $"Applique {v} Faiblesse à la Dépression.";
                case ItemEffect.GagnerForce: return $"Gagne {v} Force pour la manche.";
                case ItemEffect.PurgerDeck: return "Retire définitivement toutes les Crises de ton deck.";
                default: return "";
            }
        }
    }
}
