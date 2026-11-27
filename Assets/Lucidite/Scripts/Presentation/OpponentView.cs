using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// La Dépression : version grisâtre et abîmée de l'étudiante, assise en face.
    /// Respire, penche la tête, tressaille quand elle perd de la Lucidité (des fissures apparaissent)
    /// et se penche en avant quand elle gagne. Sa dernière bougie éteinte, elle s'effondre sur la table :
    /// ses cartes lui échappent, sa tête et ses bras tombent sur le plateau et le sang s'étale sous elle.
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

        [Header("Effondrement (dernière bougie)")]
        [Tooltip("Hauteur (au-dessus de l'origine du corps) du point autour duquel le buste bascule en avant.")]
        public float collapsePivot = 0.2f;
        [Tooltip("Recul du corps pendant la chute (la chaise glisse) : le buste passe au-dessus du bord de la table.")]
        public float collapseBack = 0.05f;
        [Tooltip("Distance entre l'os de la tête et le plateau quand elle repose dessus.")]
        public float headRestHeight = 0.09f;
        public float maxCollapseAngle = 72f;

        [Header("Pilule")]
        [Tooltip("0 → 1 : elle relève le menton pour avaler.")]
        [Range(0f, 1f)] public float gulp;
        [Tooltip("Position de la bouche par rapport à l'os de la tête (repère du corps : x côté, y haut, z vers la joueuse).")]
        public Vector3 mouthOffset = new Vector3(0f, 0.035f, 0.075f);

        /// <summary>Position (monde) de sa bouche.</summary>
        public Vector3 MouthPosition
        {
            get
            {
                var basis = body ? body : transform;
                var h = head ? head.position : basis.position + basis.up * 1.2f;
                return h + basis.right * mouthOffset.x + basis.up * mouthOffset.y + basis.forward * mouthOffset.z;
            }
        }

        public OpponentArmsIK ArmsIK => _ik ? _ik : (_ik = GetComponent<OpponentArmsIK>());

        /// <summary>Frisson bref (ex. après avoir avalé une pilule).</summary>
        public void Shudder(float amount) => _flinch = Mathf.Max(_flinch, Mathf.Clamp01(amount));

        bool _dead;
        float _deadT;   // 0 -> 1 : abandon de la posture (regard, respiration, yeux)
        float _fall;    // bascule en avant (degrés)
        OpponentArmsIK _ik;

        Vector3 _bodyBase;
        Quaternion _headBase;
        Vector3 _bodyScaleBase;
        float _lean;        // >0 penché en avant
        float _flinch;
        float _slump;
        int _cracksShown;
        float _fade = 1f;
        MaterialPropertyBlock _mpb;
        DamageVisuals _dv;

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _dv = GetComponent<DamageVisuals>();
            _ik = GetComponent<OpponentArmsIK>();
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
            if (side == Side.Opponent && lost > 0) { _flinch = Mathf.Clamp01(0.3f + FeedbackDirector.HitPower(lost) * 0.9f); Bleed(lost); }
            else if (side == Side.Player && lost > 0) StartCoroutine(LeanRoutine(0.4f));
        }

        void HandleCandle(Side side, int remaining)
        {
            if (side != Side.Opponent) { if (!_dead) StartCoroutine(LeanRoutine(1.5f)); return; }
            _flinch = 1f;
            if (remaining <= 0) StartCoroutine(CollapseRoutine());
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
            EnsureEmitters();
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

        readonly System.Collections.Generic.List<CardView> _stained = new System.Collections.Generic.List<CardView>();

        void Bleed(int lost)
        {
            float power = FeedbackDirector.HitPower(lost);
            var bs = BloodSystem.Instance;
            // Saignement (reliques, pilules, cartes) : deux fois plus ou deux fois moins de projections
            var gm = GameManager.Instance;
            float bleed = gm ? gm.BleedMultiplier(Side.Opponent) : 1f;
            // Giclée projetée vers la joueuse, sur la table (depuis son buste)
            if (bs && head)
            {
                var toPlayer = body ? body.forward : -transform.forward;
                var chest = head.position - Vector3.up * 0.22f + Vector3.ProjectOnPlane(toPlayer, Vector3.up).normalized * 0.1f;
                bs.Spray(chest, toPlayer, Mathf.Clamp01(power * Mathf.Min(1f, bleed)));
                if (bleed > 1.5f) bs.Spray(chest, toPlayer, power);
                bs.Splatter(head.position, Mathf.RoundToInt((2 + power * 10f) * bleed), 0.4f + power * 0.4f);
                // Une partie de la giclée retombe sur les cartes que la joueuse a posées sur la table
                CardView.PlayerTableCards(_stained);
                foreach (var c in _stained) bs.SprayOnto(chest, c, Mathf.RoundToInt((2 + power * 4f) * bleed));
            }
            EnsureEmitters();
            if (_emitters.Count == 0) return;
            int n = Mathf.RoundToInt((3 + power * 14f) * bleed);
            _emitters[Random.Range(0, _emitters.Count)].Burst(n);
            if (power > 0.6f) _emitters[Random.Range(0, _emitters.Count)].Burst(n / 2);
        }

        void HandleEnd(bool playerWon)
        {
            if (playerWon) { if (!_dead) StartCoroutine(CollapseRoutine()); }
            else StartCoroutine(LeanRoutine(4f));
        }

        float TableWorldY() => _ik ? _ik.transform.TransformPoint(new Vector3(0f, _ik.tableTopHeight, 0f)).y : transform.position.y + 0.78f;

        /// <summary>Morte : lâche ses cartes, bascule en avant jusqu'à ce que sa tête heurte la table, puis saigne.</summary>
        IEnumerator CollapseRoutine()
        {
            if (_dead) yield break;
            _dead = true;
            _flinch = 1f;
            AudioManager.Play(Sfx.OpponentHurt, 1f);
            // Récupère ses cartes tout de suite : le plateau les détruit en fin de manche
            var board = FindAnyObjectByType<BoardView>();
            var cards = board ? board.ReleaseOpponentCards() : new System.Collections.Generic.List<CardView>();
            yield return new WaitForSeconds(0.4f);

            // Ses cartes lui échappent et tombent sur la table
            {
                var fwd = Vector3.ProjectOnPlane(body ? body.forward : transform.forward, Vector3.up).normalized;
                var side = Vector3.Cross(Vector3.up, fwd);
                for (int i = 0; i < cards.Count; i++)
                    FallingCard.Drop(cards[i], fwd * Random.Range(0.2f, 0.6f) + side * Random.Range(-0.45f, 0.45f) + Vector3.up * Random.Range(0.15f, 0.5f), i);
            }

            // Bascule en avant sous son propre poids jusqu'au choc de la tête sur le plateau, petits rebonds
            float tableY = TableWorldY();
            float vel = 0f;
            int bounces = 0;
            bool hit = false;
            while (true)
            {
                float dt = Time.deltaTime;
                _deadT = Mathf.MoveTowards(_deadT, 1f, dt * 2f);
                if (_ik) _ik.collapse = Mathf.MoveTowards(_ik.collapse, 1f, dt * 2.5f);
                vel += 260f * dt;
                _fall = Mathf.Min(maxCollapseAngle, _fall + vel * dt);
                yield return null; // la pose est appliquée dans Update : on mesure ensuite la tête
                bool contact = head && head.position.y - headRestHeight <= tableY;
                if ((contact || _fall >= maxCollapseAngle) && vel > 0f)
                {
                    if (!hit) { hit = true; Impact(tableY); }
                    bounces++;
                    vel = -vel * 0.2f;
                    if (bounces >= 3 || Mathf.Abs(vel) < 8f) break;
                }
            }
            while (_deadT < 1f || (_ik && _ik.collapse < 1f))
            {
                _deadT = Mathf.MoveTowards(_deadT, 1f, Time.deltaTime * 2f);
                if (_ik) _ik.collapse = Mathf.MoveTowards(_ik.collapse, 1f, Time.deltaTime * 2.5f);
                yield return null;
            }

            // Derniers spasmes, puis le sang coule du bord de la table jusqu'au sol, sous elle
            yield return new WaitForSeconds(0.6f);
            _flinch = 0.45f;
            yield return new WaitForSeconds(0.5f);
            _flinch = 0.25f;
            var bs = BloodSystem.Instance;
            if (bs)
            {
                bs.Pool(transform.position + transform.forward * 0.05f, 0.52f, 4.2f, false);
                var edge = _ik ? _ik.transform.TransformPoint(new Vector3(0f, _ik.tableTopHeight + 0.004f, _ik.tableEdge - 0.012f))
                               : transform.position + Vector3.up * 0.79f;
                float until = Time.time + 6f;
                while (Time.time < until)
                {
                    bs.SpawnDrop(edge + transform.right * Random.Range(-0.08f, 0.08f), Random.Range(0.8f, 1.3f), -transform.forward * 0.05f);
                    yield return new WaitForSeconds(Random.Range(0.06f, 0.18f));
                }
            }
        }

        /// <summary>Choc de la tête sur la table : bruit sourd, la pièce tremble, giclée et flaque qui s'étale.</summary>
        void Impact(float tableY)
        {
            AudioManager.Play(Sfx.Hit, 1f);
            AudioManager.Play(Sfx.Rumble, 0.6f);
            var shake = FindAnyObjectByType<RoomShake>();
            if (shake) shake.Shake(0.35f, false);
            var bs = BloodSystem.Instance;
            if (!bs || !head) return;
            var p = head.position;
            bs.Splatter(p, 18, 0.7f);
            bs.Pool(new Vector3(p.x, tableY, p.z), 0.32f, 3f, true);
            EnsureEmitters();
            foreach (var e in _emitters) if (e) e.Burst(12);
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
                var want = Vector3.Slerp(face, toCam, lookAtPlayer * _fade * (1f - _deadT));
                head.rotation = Quaternion.FromToRotation(face, want) * head.rotation;
                face = want;
            }
            var side = Vector3.Cross(face, Vector3.up);
            if (side.sqrMagnitude < 1e-6f) return;
            side.Normalize();
            float nod = (_slump * 14f - _lean * 6f + _flinch * 12f) * Mathf.Lerp(1f, 0.5f, headMotion); // >0 : baisse le menton
            nod -= gulp * 22f; // elle avale : le menton se relève
            float tilt = Mathf.Sin(t * 0.13f) * headTilt * 0.6f + (Mathf.PerlinNoise(t * 0.4f, 3f) - 0.5f) * 5f;
            float twitch = _flinch > 0f ? Mathf.Sin(t * 60f) * _flinch * 5f : 0f;
            head.rotation = Quaternion.AngleAxis(tilt * (1f - _deadT), face) * Quaternion.AngleAxis(twitch, Vector3.up) * Quaternion.AngleAxis(-nod * (1f - _deadT), side) * head.rotation;
            // Morte : la tête roule sur le côté, joue contre la table
            if (_deadT > 0f) head.rotation = Quaternion.AngleAxis(70f * _deadT, face) * Quaternion.AngleAxis(-15f * _deadT, side) * head.rotation;
        }

        void Update()
        {
            // Plus elle est blessée, plus elle saigne (blessures durables : DamageVisuals)
            float level = _dv ? _dv.Level : _slump;
            if (_dead) level = 1f; // morte : saigne abondamment
            foreach (var e in _emitters) if (e) e.rate = level < 0.1f ? 0f : 0.4f + level * 3.5f;
            float t = Time.time;
            _flinch = Mathf.MoveTowards(_flinch, 0f, Time.deltaTime * 2.5f);
            if (body)
            {
                float breath = Mathf.Sin(t * breathFrequency * Mathf.PI * 2f) * (1f - _deadT);
                body.localScale = new Vector3(_bodyScaleBase.x, _bodyScaleBase.y * (1f + breath * breathAmplitude), _bodyScaleBase.z) * Mathf.Lerp(0.6f, 1f, _fade);
                float sink = (1f - _fade) * 0.6f + _slump * 0.04f;
                float alive = 1f - _deadT;
                // Effondrement : le buste bascule autour d'un point à hauteur de poitrine (pas des hanches)
                var fall = Quaternion.Euler(_fall, 0f, 0f);
                var pivot = new Vector3(0f, collapsePivot, 0f);
                body.localPosition = _bodyBase + new Vector3(0f, -sink, _lean * 0.06f * alive - collapseBack * _deadT) // +Z local = vers le joueur
                                     + (pivot - fall * pivot);
                body.localRotation = fall * Quaternion.Euler((_lean * 8f + _slump * 6f - _flinch * 8f) * alive, 0f, 0f);
            }
            if (head && faceAxis.sqrMagnitude < 1e-6f)
            {
                float tilt = Mathf.Sin(t * 0.13f) * headTilt + Mathf.PerlinNoise(t * 0.4f, 3f) * 4f;
                float twitch = _flinch > 0f ? Mathf.Sin(t * 60f) * _flinch * 6f : 0f;
                head.localRotation = _headBase * Quaternion.Euler((10f + _slump * 15f - _lean * 10f - _flinch * 15f) * headMotion * (1f - _deadT), twitch * headMotion, tilt * headMotion * (1f - _deadT))
                                     * Quaternion.Euler(20f * _deadT, 0f, 70f * _deadT); // morte : la joue contre la table
            }
            if (head && faceAxis.sqrMagnitude >= 1e-6f) PoseImportedHead(t);

            // Les yeux luisent plus fort quand elle domine
            foreach (var r in eyeRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", Palette.BloodLight * (0.3f + _lean * 2.5f) * (1f - _deadT)); // morte : les yeux s'éteignent
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
