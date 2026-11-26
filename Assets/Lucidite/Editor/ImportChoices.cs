using UnityEngine;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Choix des assets tiers utilisés par "Construire le jeu".
    /// Asset : Assets/Lucidite/Data/ImportChoices.asset (créé au premier setup).
    /// </summary>
    [CreateAssetMenu(menuName = "Lucidité/Choix des assets importés", fileName = "ImportChoices")]
    public class ImportChoices : ScriptableObject
    {
        [Header("Quaternius (CC0)")]
        [Tooltip("Table et chaises Quaternius, ré-texturées en bois PSX.")]
        public bool useQuaterniusFurniture = true;
        [Tooltip("Armoire, bibliothèque, tabouret, lampadaire, plante, vase dans la pièce.")]
        public bool useQuaterniusDecor = true;

        [Header("Apfelgarten (CC0)")]
        [Tooltip("Textures bois PSX 128 px pour le mobilier (généré et importé).")]
        public bool useApfelgartenWood = true;

        [Header("PSX FPS Hands (CC0, NiKo Gillsky)")]
        [Tooltip("Bras de la joueuse = modèle PSX FPS Hands posé par cinématique inverse sur les cartes.")]
        public bool useFpsHands = true;

        [Header("psx maiden (CC-BY 4.0, petya-petyavich)")]
        [Tooltip("La Dépression = la servante PSX, grisée, couverte de coups et de sang, posée assise. Nécessite le package glTFast.")]
        public bool useMaiden = true;
        public float maidenHeight = 1.55f;
        [Range(0f, 1f)] public float maidenBruises = 0.7f;
        [Range(0f, 1f)] public float maidenBlood = 0.7f;

        [Header("PSX High School Character")]
        [Tooltip("Remplace la Dépression générée par ce personnage, posé assis et grisé. " +
                 "Attention : c'est un garçon en veste rouge et jean, pas une lycéenne. Licence à vérifier.")]
        public bool useHighSchoolCharacter = false;
        public Color characterTint = new Color(0.62f, 0.6f, 0.62f);
        [Tooltip("Taille du personnage debout (m).")]
        public float characterHeight = 1.58f;
    }
}
