using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// La Dépression : version grisâtre et abîmée de l'étudiante, assise en face.
    /// Respire, penche la tête, tressaille quand elle perd de la Lucidité (des fissures apparaissent)
    /// et se penche en avant quand elle gagne.
    /// </summary>
    public class OpponentView : MonoBehaviour
    {
        public Transform body;
        public Transform head;
        public Transform leftArm;
        public Transform rightArm;
        public List<GameObject> cracks = new List<GameObject>();
        public List<Renderer> eyeRenderers = new List<Renderer>();

        [Header("Idle")]
        public float breathAmplitude = 0.012f;
        public float breathFrequency = 0.18f;
        public float headTilt = 9f;
        [Tooltip("Amplitude des mouvements de tête (réduire pour un squelette importé aux axes inconnus).")]
        [Range(0f, 1f)] public float headMotion = 1f;
        [Tooltip("Axe du visage dans le repère local de l'os de tête (zéro : modèle généré, axes connus).")]
        public Vector3 faceAxis;
        [Tooltip("Part du regard tournée vers la caméra (0 = droit devant).")]
        [Range(0f, 1f)] public float lookAtPlayer = 0.75f;

        Vector3 _bodyBase;
        Quaternion _headBase;
        Vector3 _bodyScaleBase;
        float _lean;        // >0 penché en avant
        float _flinch;
        float _slump;
        int _cracksShown;
        float _fade = 1f;
        MaterialPropertyBlock _mpb;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (body) { _bodyBase = body.localPosition; _bodyScaleBase = body.localScale; }
            if (head) _headBase = head.localRotation;
            foreach (var c in cracks) if (c) c.SetActive(false);
        }

        void OnEnable() { Subscribe(); }
        void Start() { Subscribe(); }
        void OnDisable()
        {
            var gm = GameManager.Instance;
            if (!gm) return;
            gm.OnDamaged -= HandleDamaged;
            gm.OnCandleLost -= HandleCandle;
            gm.OnStatsChanged -= HandleStats;
            gm.OnMatchEnded -= HandleEnd;
        }

        void Subscribe()
        {
            var gm = GameManager.Instance;
            if (!gm) return;
            gm.OnDamaged -= HandleDamaged; gm.OnDamaged += HandleDamaged;
            gm.OnCandleLost -= HandleCandle; gm.OnCandleLost += HandleCandle;
            gm.OnStatsChanged -= HandleStats; gm.OnStatsChanged += HandleStats;
            gm.OnMatchEnded -= HandleEnd; gm.OnMatchEnded += HandleEnd;
        }

        void HandleDamaged(Side side, int lost, int blocked)
        {
            if (side == Side.Opponent && lost > 0) { _flinch = Mathf.Clamp01(0.4f + lost / 12f); Bleed(lost); }
            else if (side == Side.Player && lost > 0) StartCoroutine(LeanRoutine(0.4f));
        }

        void HandleCandle(Side side, int remaining)
        {
            if (side != Side.Opponent) { StartCoroutine(LeanRoutine(1.5f)); return; }
            _flinch = 1f;
            // Chaque manche perdue la fissure un peu plus
            int target = Mathf.Min(cracks.Count, _cracksShown + Mathf.Max(1, cracks.Count / 3));
            while (_cracksShown < target)
            {
                if (cracks[_cracksShown]) cracks[_cracksShown].SetActive(true);
                _cracksShown++;
            }
        }

        void HandleStats()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Enemy == null || gm.Enemy.maxHp <= 0) return;
            _slump = Mathf.Clamp01(1f - gm.Enemy.hp / (float)gm.Enemy.maxHp);
            // Plus elle est blessée, plus elle saigne
            EnsureEmitters();
            foreach (var e in _emitters) if (e) e.rate = _slump < 0.05f ? 0f : 0.4f + _slump * 3f;
        }

        [Header("Sang")]
        [Tooltip("Points d'où elle saigne (tête, mains...).")]
        public List<Transform> bleedPoints = new List<Transform>();
        readonly List<BloodEmitter> _emitters = new List<BloodEmitter>();

        void EnsureEmitters()
        {
            if (_emitters.Count > 0 || bleedPoints.Count == 0) return;
            foreach (var p in bleedPoints)
            {
                if (!p) continue;
                var e = gameObject.AddComponent<BloodEmitter>();
                e.source = p;
                e.rate = 0f;
                e.jitter = 0.01f;
                e.dropSize = 1.1f;
                _emitters.Add(e);
            }
        }

        void Bleed(int lost)
        {
            EnsureEmitters();
            if (_emitters.Count == 0) return;
            int n = Mathf.Clamp(lost / 2 + 2, 2, 12);
            _emitters[Random.Range(0, _emitters.Count)].Burst(n);
            if (BloodSystem.Instance && head) BloodSystem.Instance.Splatter(head.position, Mathf.Clamp(lost / 3, 1, 6), 0.5f);
        }

        void HandleEnd(bool playerWon)
        {
            if (playerWon) StartCoroutine(FadeRoutine());
            else StartCoroutine(LeanRoutine(4f));
        }

        IEnumerator LeanRoutine(float hold = 0.9f)
        {
            float t = 0f;
            while (t < 0.4f) { t += Time.deltaTime; _lean = t / 0.4f; yield return null; }
            yield return new WaitForSeconds(hold);
            while (_lean > 0f) { _lean -= Time.deltaTime * 1.5f; yield return null; }
            _lean = 0f;
        }

        IEnumerator FadeRoutine()
        {
            yield return new WaitForSeconds(0.8f);
            while (_fade > 0f)
            {
                _fade -= Time.deltaTime * 0.25f;
                yield return null;
            }
            if (body) body.gameObject.SetActive(false);
        }

        /// <summary>
        /// Squelette importé : la tête regarde la joueuse, puis s'incline (abattement, tressaillement)
        /// autour d'axes monde, sans dépendre des axes locaux de l'os.
        /// </summary>
        void PoseImportedHead(float t)
        {
            head.localRotation = _headBase;
            var cam = PSXScreen.Instance && PSXScreen.Instance.worldCamera ? PSXScreen.Instance.worldCamera : Camera.main;
            var face = head.TransformDirection(faceAxis).normalized;
            if (cam)
            {
                var toCam = (cam.transform.position - head.position).normalized;
                var want = Vector3.Slerp(face, toCam, lookAtPlayer * _fade);
                head.rotation = Quaternion.FromToRotation(face, want) * head.rotation;
                face = want;
            }
            var side = Vector3.Cross(face, Vector3.up);
            if (side.sqrMagnitude < 1e-6f) return;
            side.Normalize();
            float nod = (_slump * 14f - _lean * 6f + _flinch * 12f) * Mathf.Lerp(1f, 0.5f, headMotion); // >0 : baisse le menton
            float tilt = Mathf.Sin(t * 0.13f) * headTilt * 0.6f + (Mathf.PerlinNoise(t * 0.4f, 3f) - 0.5f) * 5f;
            float twitch = _flinch > 0f ? Mathf.Sin(t * 60f) * _flinch * 5f : 0f;
            head.rotation = Quaternion.AngleAxis(tilt, face) * Quaternion.AngleAxis(twitch, Vector3.up) * Quaternion.AngleAxis(-nod, side) * head.rotation;
        }

        void Update()
        {
            float t = Time.time;
            _flinch = Mathf.MoveTowards(_flinch, 0f, Time.deltaTime * 2.5f);
            if (body)
            {
                float breath = Mathf.Sin(t * breathFrequency * Mathf.PI * 2f);
                body.localScale = new Vector3(_bodyScaleBase.x, _bodyScaleBase.y * (1f + breath * breathAmplitude), _bodyScaleBase.z) * Mathf.Lerp(0.6f, 1f, _fade);
                float sink = (1f - _fade) * 0.6f + _slump * 0.04f;
                body.localPosition = _bodyBase + new Vector3(0f, -sink, _lean * 0.12f); // +Z local = vers le joueur
                body.localRotation = Quaternion.Euler(_lean * 12f + _slump * 6f - _flinch * 8f, 0f, 0f);
            }
            if (head && faceAxis.sqrMagnitude < 1e-6f)
            {
                float tilt = Mathf.Sin(t * 0.13f) * headTilt + Mathf.PerlinNoise(t * 0.4f, 3f) * 4f;
                float twitch = _flinch > 0f ? Mathf.Sin(t * 60f) * _flinch * 6f : 0f;
                head.localRotation = _headBase * Quaternion.Euler((10f + _slump * 15f - _lean * 10f - _flinch * 15f) * headMotion, twitch * headMotion, tilt * headMotion);
            }
            if (head && faceAxis.sqrMagnitude >= 1e-6f) PoseImportedHead(t);

            // Les yeux luisent plus fort quand elle domine
            foreach (var r in eyeRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", Palette.BloodLight * (0.3f + _lean * 2.5f));
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
