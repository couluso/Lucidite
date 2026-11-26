using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>Fin de manche : quelle bougie s'est éteinte, bougies restantes, gouttes gagnées.</summary>
    public class RoundSummaryPanel : UIPanel
    {
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public TMP_Text totalText;
        public Image[] playerCandles;
        public Image[] enemyCandles;
        public Button continueButton;

        void Awake()
        {
            if (continueButton) continueButton.onClick.AddListener(() =>
            {
                Hide();
                if (GameManager.Instance) GameManager.Instance.AcknowledgeRound();
            });
        }

        public void ShowSummary(RoundSummary s, int playerMax, int enemyMax)
        {
            string hope = Palette.HexOf(Palette.Hope), blood = Palette.HexOf(Palette.BloodLight);
            titleText.text = s.playerWon
                ? $"<color=#{hope}>Une de ses bougies s'éteint</color>"
                : $"<color=#{blood}>Une de tes bougies s'éteint</color>";
            var sb = new StringBuilder();
            foreach (var l in s.lines) sb.AppendLine(l);
            bodyText.text = sb.ToString();
            totalText.text = $"+{s.bloodTotal} gouttes de sang";
            Fill(playerCandles, s.playerCandles, playerMax, Palette.Hope);
            Fill(enemyCandles, s.enemyCandles, enemyMax, Palette.BloodLight);
            Show();
        }

        static void Fill(Image[] icons, int lit, int max, Color c)
        {
            if (icons == null) return;
            for (int i = 0; i < icons.Length; i++)
            {
                if (!icons[i]) continue;
                icons[i].gameObject.SetActive(i < max);
                icons[i].color = i < lit ? c : Palette.AshDark.WithAlpha(0.4f);
            }
        }
    }
}
