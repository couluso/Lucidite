using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    public class TitlePanel : UIPanel
    {
        public TMP_Text titleText;
        public TMP_Text subtitleText;
        public TMP_Text promptText;
        public Button playButton;
        public Button rulesButton;
        public Button optionsButton;
        public Button quitButton;

        protected override void Update()
        {
            base.Update();
            if (promptText)
            {
                // Pulsation douce (0.5 <-> 1 sur ~1.6 s)
                float a = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.6f);
                promptText.alpha = a;
            }
            if (titleText)
            {
                // Léger tremblement du titre
                float j = Mathf.PerlinNoise(Time.unscaledTime * 3f, 0f) > 0.85f ? Random.Range(-3f, 3f) : 0f;
                titleText.rectTransform.anchoredPosition = new Vector2(j, titleText.rectTransform.anchoredPosition.y);
            }
        }
    }
}
