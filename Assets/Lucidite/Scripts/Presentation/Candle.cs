using UnityEngine;

namespace Lucidite
{
    /// <summary>Bougie de Lucidité : une flamme = un point de Lucidité.</summary>
    public class Candle : MonoBehaviour
    {
        public GameObject flame;
        public Light flameLight;
        public ParticleSystem smoke;
        public float lightIntensity = 0.06f;

        bool _lit = true;
        float _seed;

        void Awake() { _seed = Random.value * 50f; }

        public bool Lit => _lit;

        public void SetLit(bool lit, bool animate = true)
        {
            if (_lit == lit) return;
            _lit = lit;
            if (flame) flame.SetActive(lit);
            if (flameLight) flameLight.enabled = lit;
            if (!lit && animate && smoke) smoke.Play();
            if (animate) AudioManager.Play(lit ? Sfx.CandleLight : Sfx.CandleOut);
        }

        void Update()
        {
            if (!_lit) return;
            float n = Mathf.PerlinNoise(_seed, Time.time * 6f);
            if (flameLight) flameLight.intensity = lightIntensity * (0.7f + n * 0.6f);
            if (flame) flame.transform.localScale = new Vector3(1f, 0.85f + n * 0.3f, 1f);
        }
    }
}
