using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>
    /// Examen d'une carte (survol) : grand texte lisible sur le côté droit, sans cadre,
    /// avec l'explication des mots-clés.
    /// </summary>
    public class CardInspectorPanel : UIPanel
    {
        public CardArtLibrary artLibrary;
        public RawImage art;
        public TMP_Text titleText;
        public TMP_Text subtitleText;
        public TMP_Text rulesText;
        public TMP_Text keywordsText;
        public TMP_Text flavorText;
        public TMP_Text costText;
        public GameObject costRoot;

        CardView _hovered;
        bool _textMode;

        /// <summary>Appelé par l'UIManager.</summary>
        public void HandleHover(CardView v)
        {
            _hovered = v;
            // Survoler une carte 3D reprend toujours la main (évite un panneau resté bloqué en mode "texte"
            // quand l'élément d'interface survolé a été détruit, par ex. après un achat).
            if (v) _textMode = false;
            if (_textMode) return;
            if (v && v.Card != null) ShowCard(v.Card, true);
            else if (v && v.Intent != null) ShowIntent(v.Intent);
            else Hide();
        }

        public void ShowCard(CardInstance c, bool inCombat = false)
        {
            var d = c.data;
            var gm = GameManager.Instance;
            titleText.text = c.Name;
            subtitleText.text = $"{CardData.TypeLabel(d.type)}  ·  {CardData.RarityLabel(d.rarity)}  ·  {CardData.ThemeLabel(d.theme)}";
            rulesText.text = inCombat && gm && gm.Player != null ? c.Describe(gm.Player, gm.Enemy) : c.Describe();
            keywordsText.text = Keywords(c);
            flavorText.text = d.flavorText;
            if (costRoot) costRoot.SetActive(!c.Unplayable);
            if (costText) costText.text = c.Cost.ToString();
            if (art) { art.gameObject.SetActive(true); if (artLibrary) art.texture = artLibrary.Get(d.theme); }
            Show();
        }

        void ShowIntent(EnemyIntent it)
        {
            var gm = GameManager.Instance;
            titleText.text = it.move.moveName;
            subtitleText.text = "INTENTION DE LA DÉPRESSION";
            rulesText.text = gm ? it.Describe(gm.Enemy, gm.Player) : "";
            keywordsText.text = it.weak > 0 || it.vulnerable > 0 || it.crisis > 0 ? Glossary(it.weak > 0, it.vulnerable > 0, false, false, false, it.crisis > 0) : "";
            flavorText.text = it.move.flavorText;
            if (costRoot) costRoot.SetActive(false);
            if (art) { art.gameObject.SetActive(true); if (artLibrary) art.texture = artLibrary.Get(it.move.theme); }
            Show();
        }

        public void ShowText(string title, string subtitle, string body)
        {
            _textMode = true;
            titleText.text = title;
            subtitleText.text = subtitle.ToUpperInvariant();
            rulesText.text = body;
            keywordsText.text = "";
            flavorText.text = "";
            if (costRoot) costRoot.SetActive(false);
            if (art) art.gameObject.SetActive(false);
            Show();
        }

        /// <summary>Remet le panneau à zéro (fermeture de la boutique, élément survolé détruit...).</summary>
        public void ResetState()
        {
            _textMode = false;
            _hovered = null;
            Hide();
        }

        public void HideText()
        {
            _textMode = false;
            HandleHover(_hovered);
        }

        static string Keywords(CardInstance c) =>
            Glossary(c.Weak > 0, c.Vulnerable > 0, c.Exhaust, c.Ethereal, c.Strength > 0, c.IsCrisis);

        static string Glossary(bool weak, bool vuln, bool exhaust, bool ethereal, bool strength, bool crisis)
        {
            var sb = new StringBuilder();
            if (vuln) sb.AppendLine("<b>Vulnérable</b> : subit 50 % de dégâts en plus.");
            if (weak) sb.AppendLine("<b>Faiblesse</b> : inflige 25 % de dégâts en moins.");
            if (strength) sb.AppendLine("<b>Force</b> : +1 dégât par point sur chaque attaque.");
            if (exhaust) sb.AppendLine("<b>Épuiser</b> : retirée jusqu'à la fin de la manche.");
            if (ethereal) sb.AppendLine("<b>Éphémère</b> : épuisée si elle reste en main en fin de tour.");
            if (crisis) sb.AppendLine("<b>Crise</b> : carte parasite. Se transforme en boutique.");
            return sb.ToString().TrimEnd();
        }
    }
}
