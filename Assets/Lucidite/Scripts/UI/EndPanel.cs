using TMPro;
using UnityEngine.UI;

namespace Lucidite
{
    public class EndPanel : UIPanel
    {
        public TMP_Text titleText;
        public TMP_Text messageText;
        public TMP_Text statsText;
        public TMP_Text helpText;
        public Button restartButton;
        public Button menuButton;

        public void ShowEnd(bool playerWon, MatchStats st)
        {
            string hope = Palette.HexOf(Palette.Hope), blood = Palette.HexOf(Palette.BloodLight);
            if (playerWon)
            {
                titleText.text = $"<color=#{hope}>La lumière tient bon</color>";
                messageText.text = "Ses bougies sont éteintes. Ta Dépression s'efface... pour cette nuit.\nCe n'est pas une fin : c'est un début.";
            }
            else
            {
                titleText.text = $"<color=#{blood}>La dernière bougie s'éteint</color>";
                messageText.text = "Cette nuit, elle a gagné. Une défaite n'est pas un verdict.\nOn peut toujours rallumer une bougie — et demander de l'aide pour le faire.";
            }
            statsText.text =
                $"manches  {st.roundsWon} gagnées · {st.roundsLost} perdues\n" +
                $"cartes jouées  {st.cardsPlayed}   ·   dégâts infligés  {st.damageDealt}   ·   Lucidité perdue  {st.damageTaken}\n" +
                $"gouttes de sang récoltées  {st.bloodEarned}";
            if (helpText) helpText.text = "Besoin de parler ? 3114 — prévention du suicide, gratuit, 24h/24 (France).";
            Show();
        }
    }
}
