using UnityEngine;
using UnityEngine.InputSystem;

namespace Lucidite
{
    /// <summary>
    /// Caméra première personne : pose "menu" (lent travelling), pose "assise", focus sur la table,
    /// respiration, léger regard vers la souris et tremblements (modèle "trauma").
    /// Hiérarchie : CameraRig (pose) > Shake (offsets) > Camera.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform shakeRoot;
        public Transform titlePose;
        public Transform seatedPose;
        public Transform tablePose;

        [Header("Mouvements")]
        public float poseSharpness = 3f;
        public float breathAmplitude = 0.004f;
        public float breathFrequency = 0.22f;
        public float mouseLookYaw = 4f;
        public float mouseLookPitch = 2.5f;
        public float titleDrift = 3f;

        [Header("Tremblements")]
        public float maxShakeAngle = 4f;
        public float maxShakeOffset = 0.035f;
        public float traumaDecay = 1.3f;

        public static float ShakeMultiplier = 1f; // option d'accessibilité

        Transform _current;
        bool _tableFocus;
        float _trauma;
        float _seed;

        void Awake()
        {
            _seed = Random.value * 100f;
            _current = titlePose ? titlePose : seatedPose;
            if (_current) { transform.SetPositionAndRotation(_current.position, _current.rotation); }
        }

        public void GoSeated() { _current = seatedPose; _tableFocus = false; }
        public void GoTitle() { _current = titlePose; _tableFocus = false; }
        public void SetTableFocus(bool on) { _tableFocus = on; }

        public void AddTrauma(float amount) { _trauma = Mathf.Clamp01(_trauma + amount * ShakeMultiplier); }

        // ------------------------------------------------------------------ Coup violent (ressort amorti)
        [Header("Coup violent")]
        public float joltStiffness = 260f;
        public float joltDamping = 14f;
        Vector3 _joltPos, _joltPosVel, _joltRot, _joltRotVel;

        /// <summary>
        /// Choc brutal : la caméra est projetée (comme si la pièce avait été frappée) puis revient en oscillant.
        /// amount ~ 0..1.
        /// </summary>
        // Coup d'œil : le regard se tourne un peu vers un point de la scène (ex. elle avale une pilule)
        Vector3 _glanceTarget;
        float _glanceUntil, _glance;
        [Header("Coup d'œil")]
        [Range(0f, 1f)] public float glanceStrength = 0.45f;

        /// <summary>Pendant <paramref name="duration"/> secondes, le regard glisse vers ce point puis revient.</summary>
        public void Glance(Vector3 world, float duration)
        {
            _glanceTarget = world;
            _glanceUntil = Time.unscaledTime + duration;
        }

        /// <summary>Déglutition : la tête plonge légèrement puis revient (ressort du coup violent, vers le bas).</summary>
        public void Nod(float amount)
        {
            _joltRotVel += new Vector3(70f * amount, 0f, 0f);
            _joltPosVel += new Vector3(0f, -0.06f * amount, 0f);
        }

        public void Jolt(float amount)
        {
            amount *= ShakeMultiplier;
            var dir = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, -0.3f), Random.Range(-0.6f, 0.2f)).normalized;
            _joltPosVel += dir * (0.9f * amount);
            _joltRotVel += new Vector3(Random.Range(-1f, 1f) * 0.6f - 0.6f, Random.Range(-1f, 1f) * 0.6f, Random.Range(-1f, 1f)) * (260f * amount);
        }

        void UpdateJolt(float dt)
        {
            dt = Mathf.Min(dt, 0.05f);
            _joltPosVel += (-joltStiffness * _joltPos - joltDamping * _joltPosVel) * dt;
            _joltPos += _joltPosVel * dt;
            _joltRotVel += (-joltStiffness * _joltRot - joltDamping * _joltRotVel) * dt;
            _joltRot += _joltRotVel * dt;
        }

        void LateUpdate()
        {
            var target = _tableFocus && tablePose ? tablePose : _current;
            if (!target) return;
            float k = 1f - Mathf.Exp(-poseSharpness * Time.unscaledDeltaTime);

            Quaternion targetRot = target.rotation;
            bool seated = target != titlePose;
            if (!seated)
            {
                // Lent balancement sur l'écran titre
                float t = Time.unscaledTime * 0.15f;
                targetRot *= Quaternion.Euler(Mathf.Sin(t) * titleDrift * 0.4f, Mathf.Sin(t * 0.7f) * titleDrift, 0f);
            }
            else if (Mouse.current != null)
            {
                Vector2 m = Mouse.current.position.ReadValue();
                float nx = Mathf.Clamp(m.x / Mathf.Max(1, Screen.width) * 2f - 1f, -1f, 1f);
                float ny = Mathf.Clamp(m.y / Mathf.Max(1, Screen.height) * 2f - 1f, -1f, 1f);
                targetRot *= Quaternion.Euler(-ny * mouseLookPitch, nx * mouseLookYaw, 0f);
            }

            // Coup d'œil vers un point (montée et retour en douceur)
            _glance = Mathf.MoveTowards(_glance, Time.unscaledTime < _glanceUntil ? 1f : 0f, Time.unscaledDeltaTime * 2.2f);
            if (_glance > 0f && seated)
            {
                var look = Quaternion.LookRotation(_glanceTarget - transform.position, Vector3.up);
                targetRot = Quaternion.Slerp(targetRot, look, glanceStrength * Mathf.SmoothStep(0f, 1f, _glance));
            }

            transform.position = Vector3.Lerp(transform.position, target.position, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);

            if (shakeRoot)
            {
                float time = Time.time;
                float breath = Mathf.Sin(time * breathFrequency * Mathf.PI * 2f) * breathAmplitude;
                float s = _trauma * _trauma;
                float n1 = Mathf.PerlinNoise(_seed, time * 25f) * 2f - 1f;
                float n2 = Mathf.PerlinNoise(_seed + 10f, time * 25f) * 2f - 1f;
                float n3 = Mathf.PerlinNoise(_seed + 20f, time * 25f) * 2f - 1f;
                UpdateJolt(Time.deltaTime);
                shakeRoot.localPosition = new Vector3(n1 * maxShakeOffset * s, breath + n2 * maxShakeOffset * s, 0f) + _joltPos;
                shakeRoot.localRotation = Quaternion.Euler(n2 * maxShakeAngle * s, n3 * maxShakeAngle * s, n1 * maxShakeAngle * s * 1.5f) * Quaternion.Euler(_joltRot);
                _trauma = Mathf.Max(0f, _trauma - traumaDecay * Time.deltaTime);
            }
        }
    }
}
