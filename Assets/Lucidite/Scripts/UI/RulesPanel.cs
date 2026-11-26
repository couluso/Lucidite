using TMPro;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>Menu des règles, présenté comme une page de carnet.</summary>
    public class RulesPanel : UIPanel
    {
        public TMP_Text headerText;
        public TMP_Text bodyText;
        public TMP_Text pageText;
        public Button prevButton;
        public Button nextButton;
        public Button closeButton;

        int _page;

        static readonly string[] Titles =
        {
            "Le principe",
            "Ton tour",
            "Dégâts & bouclier",
            "Les cartes",
            "Son tour",
            "Entre les manches",
            "Commandes",
            "Avant de jouer",
            "Crédits"
        };

        static readonly string[] Pages =
        {
            "Tu es assise face à ta Dépression : une version de toi plus grise, plus abîmée.\n\n" +
            "La partie se joue en <b>manches</b>. Chacune de vous a des <b>bougies</b> sur la table : " +
            "perdre une manche en éteint une.\n\n" +
            "<b>Éteins toutes ses bougies avant qu'elle n'éteigne les tiennes.</b>\n\n" +
            "Une manche se gagne en faisant tomber son <b>Emprise</b> à 0 avant que ta <b>Lucidité</b> ne tombe à 0.",

            "Au début de ton tour, tu reçois <b>3 énergies</b> et tu <b>pioches 5 cartes</b>.\n\n" +
            "Chaque carte a un <b>coût</b> (le chiffre dans le coin). <b>Clique sur une carte pour la sélectionner</b> : " +
            "elle se soulève et saigne. Re-clique pour la reposer (clic droit : tout reposer). Tu sélectionnes autant de cartes que ton énergie le permet.\n\n" +
            "<b>« Jouer le tour »</b> (ou E) : tes cartes sont posées face cachée devant toi, puis retournées une à une. " +
            "Les cartes non jouées vont à la défausse. Pioche et énergie gagnées par tes cartes arrivent au tour suivant.",

            "<b>Dégâts</b> : font baisser l'Emprise de la Dépression (ou ta Lucidité).\n\n" +
            "<b>Bouclier</b> : absorbe les dégâts avant ta Lucidité. Il disparaît au début de ton tour suivant.\n\n" +
            "<b>Force</b> : +1 dégât par point sur chaque attaque.\n" +
            "<b>Faiblesse</b> : inflige 25 % de dégâts en moins.\n" +
            "<b>Vulnérable</b> : subit 50 % de dégâts en plus.\n\n" +
            "Les effets de Faiblesse et Vulnérable durent quelques tours.",

            "<b>Attaque</b> — inflige des dégâts.\n" +
            "<b>Défense</b> — donne du bouclier.\n" +
            "<b>Tourment</b> — pioche, énergie, Force, affaiblissements...\n" +
            "<b>Crise</b> — cartes parasites que la Dépression glisse dans ton deck. Souvent injouables, parfois douloureuses si tu les gardes en main.\n\n" +
            "<b>Épuiser</b> : la carte est retirée jusqu'à la fin de la manche.\n" +
            "<b>Éphémère</b> : épuisée si elle reste en main à la fin du tour.",

            "Au début de ton tour, la Dépression <b>pose ses cartes face visible</b> sur la table : ce sont ses <b>intentions</b>. " +
            "Elle a sa propre <b>énergie</b> (1 à la première manche, puis +1 à chaque manche) et chacune de ses cartes en coûte : " +
            "plus la partie avance, plus elle en joue par tour. Survole-les pour les lire ; le résumé au-dessus d'elle additionne ses dégâts.\n\n" +
            "<b>Elle aussi se renforce entre les manches</b>, comme si elle passait par la boutique : Emprise, Force, bouclier, énergie, " +
            "cartes affûtées ou nouvelles obsessions. Elle en prend davantage quand elle gagne une manche.",

            "Après chaque manche, tu gagnes des <b>gouttes de sang</b> (plus si tu gagnes, et selon la Lucidité préservée).\n\n" +
            "Elles se dépensent dans la boutique :\n" +
            "• acheter une <b>carte</b> ou un <b>objet</b> (reliques passives, consommables) ;\n" +
            "• <b>faire évoluer</b> une carte (version améliorée) ;\n" +
            "• <b>brûler</b> une carte pour alléger ton deck ;\n" +
            "• <b>transformer</b> une Crise : la crise traversée devient une force.",

            "<b>Survoler une carte</b> : la lever et l'examiner (détails à droite).\n" +
            "<b>Clic gauche</b> : sélectionner / reposer une carte. <b>Clic droit</b> : tout reposer.\n" +
            "<b>E / Entrée</b> : jouer le tour.\n" +
            "<b>Clic sur un objet</b> (bas gauche) : l'utiliser.\n" +
            "<b>Clic sur « pioche » / « défausse »</b> : voir la pile.\n" +
            "<b>Échap</b> : pause.",

            "Ce jeu aborde la dépression, l'anxiété, l'automutilation et les idées suicidaires, avec des images de blessures et de sang.\n\n" +
            "Si tu traverses une période difficile, tu n'es pas seule. Parles-en à quelqu'un de confiance.\n\n" +
            "<b>France : 3114</b> — numéro national de prévention du suicide, gratuit, 24h/24, 7j/7.\n" +
            "En cas d'urgence : <b>15</b> ou <b>112</b>.",

            "<b>Personnage de la Dépression</b> — basé sur « psx maiden » (https://sketchfab.com/3d-models/psx-maiden-d46828e60eb141c5866545f62fe662ce) par petya-petyavich (https://sketchfab.com/petya-petyavich), sous licence CC-BY-4.0. Texture modifiée (grisée, blessures, sang) et pose assise.\n\n" +
            "<b>Bras de la joueuse</b> — PSX FPS Hands par NiKo Gillsky (CC0).\n" +
            "<b>Mobilier</b> — Furniture Pack par Quaternius (CC0).\n" +
            "<b>Textures bois</b> — Chairs & Tables PSX Asset Pack par Apfelgarten (CC0).\n\n" +
            "<b>Polices</b> — IM Fell English SC (OFL), Special Elite (Apache 2.0), VT323 (OFL)."
        };

        void Awake()
        {
            if (prevButton) prevButton.onClick.AddListener(() => SetPage(_page - 1));
            if (nextButton) nextButton.onClick.AddListener(() => SetPage(_page + 1));
            if (closeButton) closeButton.onClick.AddListener(Hide);
        }

        public override void Show()
        {
            base.Show();
            SetPage(0);
        }

        void SetPage(int p)
        {
            _page = (p + Pages.Length) % Pages.Length;
            if (headerText) headerText.text = Titles[_page];
            if (bodyText) bodyText.text = Pages[_page];
            if (pageText) pageText.text = $"— {_page + 1} / {Pages.Length} —";
        }
    }
}
