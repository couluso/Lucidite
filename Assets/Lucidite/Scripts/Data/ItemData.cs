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
        [Tooltip("Statuette posée sur la table (reliques).")]
        public GameObject model;
        [Tooltip("La Dépression peut aussi l'acheter (posé de son côté de la table).")]
        public bool enemyCanBuy;
        [Tooltip("Objet rare : il n'apparaît en boutique qu'une fois sur quatre environ.")]
        public bool rare;

        public bool IsPassive => kind == ItemKind.Relique;
        public bool UsableInShop => effect == ItemEffect.PurgerDeck || effect == ItemEffect.NiveauClasse;
        /// <summary>Pilule qui agit sur les cartes sélectionnées dans ta main.</summary>
        public bool NeedsSelection => effect == ItemEffect.ConvertirClasse || effect == ItemEffect.CopierCarte;
        /// <summary>Bonus de dégâts par coup de l'ex-voto (DegatsClasse).</summary>
        public const int ClassDamage = 3;
        public CardSuit Suit => (CardSuit)Mathf.Clamp(value, 0, 4);

        public static string KindLabel(ItemKind k) => k == ItemKind.Relique ? "Relique" : "Pilule";

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
                case ItemEffect.PurgerDeck: return "Retire définitivement toutes les Crises de ton deck. (Aussi en boutique.)";
                case ItemEffect.SaignementAdverse: return "Ton adversaire saigne deux fois plus : plus de gouttes gagnées, plus de sang sur les cartes.";
                case ItemEffect.ResonanceFacile: return $"Résonance {CardData.SuitLabel(Suit)} avec 2 cartes au lieu de 3.";
                case ItemEffect.RelanceGratuite: return v > 1 ? $"{v} relances gratuites par boutique." : "Une relance gratuite par boutique.";
                case ItemEffect.SangCartes: return $"Tes cartes se couvrent de sang {v} % plus vite.";
                case ItemEffect.DegatsSiLuciditeBasse: return $"Lucidité ≤ 50 % : +{v} dégâts par coup.";
                case ItemEffect.SaignementReduitTour: return "Tu saignes deux fois moins pendant ce tour.";
                case ItemEffect.SaignementAdverseTour: return "Ton adversaire saigne deux fois plus pendant ce tour.";
                case ItemEffect.EnergieMax: return $"+{v} énergie à chaque tour.";
                case ItemEffect.DegatsClasse: return $"Tes cartes {CardData.SuitLabel(Suit)} infligent +{ClassDamage} dégâts par coup.";
                case ItemEffect.VoileAdverse: return "Au début de chaque manche, une relique de ton adversaire, tirée au hasard, est voilée : sans effet jusqu'à la fin de la manche.";
                case ItemEffect.Reliquaire: return "Double l'effet d'une autre de tes reliques, choisie à l'achat.";
                case ItemEffect.PremierCoupReduit: return $"Le premier coup que tu reçois à chaque tour est réduit de {v}.";
                case ItemEffect.SoinDebutTour: return $"Récupère {v} Lucidité au début de chacun de tes tours.";
                case ItemEffect.Epines: return $"Chaque fois que tu perds de la Lucidité, ton adversaire en perd {v}.";
                case ItemEffect.EnergieAdverseTour: return $"Ton adversaire perd {v} énergie ce tour : elle renonce à ses dernières cartes.";
                case ItemEffect.ConvertirClasse: return $"Jusqu'à 2 de tes cartes sélectionnées deviennent des cartes {CardData.SuitLabel(Suit)} (pour toute la partie).";
                case ItemEffect.CopierCarte: return "Sélectionne deux cartes : celle de droite devient une copie de celle de gauche (pour toute la partie).";
                case ItemEffect.Voler: return "Vole une relique de ton adversaire, tirée au hasard (une pilule s'il n'a pas de relique).";
                case ItemEffect.Purifier: return "Retire ta Faiblesse et ta Vulnérabilité.";
                case ItemEffect.Placebo: return "L'effet d'une autre pilule, tirée au hasard. Qui sait ?";
                case ItemEffect.NiveauClasse: return $"+1 niveau à la classe {CardData.SuitLabel(Suit)} pour toute la partie (+2 dégâts, bouclier et soin). Aussi en boutique.";
                default: return "";
            }
        }
    }
}
