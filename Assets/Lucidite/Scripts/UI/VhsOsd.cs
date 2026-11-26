using TMPro;
using UnityEngine;

namespace Lucidite
{
    /// <summary>Affichage façon magnétoscope : "▶ LECTURE" clignotant et compteur de bande.</summary>
    public class VhsOsd : MonoBehaviour
    {
        public TMP_Text modeText;
        public TMP_Text counterText;
        public string mode = "LECTURE";

        float _start;

        void OnEnable() { _start = Time.unscaledTime; }

        void Update()
        {
            float t = Time.unscaledTime - _start;
            if (modeText) modeText.text = (Mathf.Repeat(t, 1.2f) < 0.8f ? "> " : "  ") + mode;
            if (counterText)
            {
                int s = (int)t;
                counterText.text = $"SP  {s / 3600:00}:{s / 60 % 60:00}:{s % 60:00}";
            }
        }
    }
}
