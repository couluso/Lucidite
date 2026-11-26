using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>Fait vaciller l'opacité d'un élément comme une flamme ou un néon fatigué.</summary>
    public class UIFlicker : MonoBehaviour
    {
        public Graphic target;
        public float minAlpha = 0.6f;
        public float maxAlpha = 1f;
        public float speed = 3f;

        float _seed;

        void Awake()
        {
            if (!target) target = GetComponent<Graphic>();
            _seed = Random.value * 10f;
        }

        void Update()
        {
            if (!target) return;
            float n = Mathf.PerlinNoise(_seed, Time.unscaledTime * speed);
            if (Random.value < 0.01f) n = 0f;
            target.canvasRenderer.SetAlpha(Mathf.Lerp(minAlpha, maxAlpha, n));
        }
    }
}
