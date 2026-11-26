using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// L'unique ampoule au-dessus de la table : balancement pendulaire, grésillement,
    /// couleur qui suit l'ambiance (ambre = espoir, rouge = détresse).
    /// Le pivot de l'objet doit être au point d'accroche (plafond).
    /// </summary>
    public class HangingLamp : MonoBehaviour
    {
        public Light spotLight;
        public Light fillLight;
        public Renderer bulbRenderer;

        [Header("Pendule")]
        public float cordLength = 1.2f;
        public float damping = 0.5f;
        public float idleSway = 1.2f;

        [Header("Lumière")]
        public float spotIntensity = 6f;
        public float fillIntensity = 0.6f;
        [Range(0f, 1f)] public float idleFlicker = 0.08f;

        Vector2 _angle = Vector2.zero; // degrés (x, z)
        Vector2 _velocity;
        float _burst;
        float _mood;         // -1 détresse .. +1 espoir
        float _moodTarget;
        float _dim = 1f;
        float _dimTarget = 1f;
        MaterialPropertyBlock _mpb;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake() { _mpb = new MaterialPropertyBlock(); }

        public void Impulse(float strength)
        {
            var dir = Random.insideUnitCircle.normalized;
            _velocity += dir * strength;
        }

        public void FlickerBurst(float duration) => _burst = Mathf.Max(_burst, duration);
        public void SetMood(float mood) => _moodTarget = Mathf.Clamp(mood, -1f, 1f);
        public void SetDim(float target) => _dimTarget = Mathf.Clamp01(target);

        void Update()
        {
            float dt = Time.deltaTime;
            // Pendule simple sur deux axes
            float omega2 = 9.81f / Mathf.Max(0.2f, cordLength);
            for (int i = 0; i < 2; i++)
            {
                // approximation petits angles : a = -(g/L)·θ - amortissement·ω   (en degrés)
                float acc = -omega2 * _angle[i] - damping * _velocity[i];
                _velocity[i] += acc * dt;
                _angle[i] += _velocity[i] * dt;
            }
            float t = Time.time;
            var idle = new Vector2(Mathf.Sin(t * 0.7f), Mathf.Sin(t * 0.53f + 1.3f)) * idleSway;
            transform.localRotation = Quaternion.Euler(_angle.x + idle.x, 0f, _angle.y + idle.y);

            // Grésillement
            _burst = Mathf.Max(0f, _burst - dt);
            float flicker = 1f;
            if (Random.value < idleFlicker * dt * 30f) flicker = Random.Range(0.55f, 0.9f);
            if (_burst > 0f) flicker = Random.value < 0.5f ? Random.Range(0.05f, 0.4f) : Random.Range(0.8f, 1.3f);

            _mood = Mathf.MoveTowards(_mood, _moodTarget, dt * 0.5f);
            _dim = Mathf.MoveTowards(_dim, _dimTarget, dt * 0.4f);
            Color c = _mood >= 0f
                ? Color.Lerp(Palette.Bulb, Palette.Hope, _mood)
                : Color.Lerp(Palette.Bulb, Palette.BloodLight, -_mood * 0.8f);

            float k = flicker * _dim;
            if (spotLight) { spotLight.color = c; spotLight.intensity = spotIntensity * k; }
            if (fillLight) { fillLight.color = c; fillLight.intensity = fillIntensity * k; }
            if (bulbRenderer)
            {
                bulbRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, c * (3f * k));
                bulbRenderer.SetPropertyBlock(_mpb);
            }
            AudioManager.SetLampHum(k);
        }
    }
}
