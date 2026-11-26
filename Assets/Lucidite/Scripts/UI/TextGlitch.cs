using TMPro;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Texte qui "grésille" comme une bande VHS abîmée : décalages brefs, double rouge décalé,
    /// et parfois un caractère remplacé une fraction de seconde.
    /// </summary>
    public class TextGlitch : MonoBehaviour
    {
        public TMP_Text text;
        [Tooltip("Copie décalée (aberration rouge), optionnelle.")]
        public TMP_Text ghost;
        [Range(0f, 1f)] public float intensity = 0.5f;

        string _original, _shown;
        Vector2 _basePos, _ghostBase;
        float _timer;
        const string Glyphs = "#%&@$?!/\\|_-=+*~<>";

        void Start()
        {
            if (!text) text = GetComponent<TMP_Text>();
            _original = _shown = text.text;
            _basePos = text.rectTransform.anchoredPosition;
            if (ghost) _ghostBase = ghost.rectTransform.anchoredPosition;
        }

        void Update()
        {
            if (!text) return;
            if (text.text != _shown) _original = _shown = text.text; // texte changé de l'extérieur
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f || string.IsNullOrEmpty(_original)) return;

            float r = Random.value;
            if (r < 0.04f * intensity)
            {
                // burst : décalage + caractère corrompu
                var chars = _original.ToCharArray();
                int n = Mathf.Max(1, Mathf.RoundToInt(chars.Length * 0.15f));
                for (int i = 0; i < n; i++)
                {
                    int k = Random.Range(0, chars.Length);
                    if (chars[k] != ' ') chars[k] = Glyphs[Random.Range(0, Glyphs.Length)];
                }
                text.text = _shown = new string(chars);
                text.rectTransform.anchoredPosition = _basePos + new Vector2(Random.Range(-8f, 8f), Random.Range(-2f, 2f)) * intensity;
                if (ghost) ghost.rectTransform.anchoredPosition = _ghostBase + new Vector2(Random.Range(-14f, 14f), 0f) * intensity;
                _timer = Random.Range(0.04f, 0.12f);
            }
            else
            {
                text.text = _shown = _original;
                text.rectTransform.anchoredPosition = _basePos;
                if (ghost) ghost.rectTransform.anchoredPosition = _ghostBase + new Vector2(Mathf.Sin(Time.unscaledTime * 1.3f) * 3f, 0f);
                _timer = Random.Range(0.05f, 0.2f);
            }
        }
    }
}
