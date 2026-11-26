using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Bras de la Dépression (squelette importé, axes inconnus) posés par cinématique inverse :
    /// chaque main tient un bord de son éventail (première / dernière carte), coudes vers l'extérieur.
    /// Tout est calculé en directions monde ; seuls l'axe de la paume est mesuré à la construction.
    /// Ni l'éventail ni les bras ne traversent la table : l'éventail est relevé si besoin (quand elle se penche)
    /// et les coudes cherchent une position qui garde avant-bras et bras au-dessus du plateau.
    /// </summary>
    public class OpponentArmsIK : MonoBehaviour
    {
        [System.Serializable]
        public class Arm
        {
            public Transform upper, forearm, hand, handTip;
            [Tooltip("Axe de la paume dans le repère local de l'os de la main (mesuré en pose de repos).")]
            public Vector3 palmLocal = Vector3.down;
            [HideInInspector] public float upperLen, foreLen, handLen;
            [HideInInspector] public Vector3 grip;
            [HideInInspector] public bool init;
            [HideInInspector] public float elbowOut;
        }

        public HandView hand;
        [Tooltip("Repère du corps : +Z vers la joueuse, +Y vers le haut.")]
        public Transform body;
        public Arm left = new Arm();
        public Arm right = new Arm();

        [Header("Prise")]
        [Tooltip("Décalage du poignet sous le coin inférieur de la carte : vers l'intérieur, vers le haut, vers la joueuse (repère du corps).")]
        public Vector3 wristOffset = new Vector3(0.015f, 0.01f, 0.01f);
        public float minSeparation = 0.12f;
        public float sharpness = 8f;
        [Range(0f, 1.5f)] public float handTiltIn = 0.6f;

        [Header("Table (repère de cet objet : +Z vers la joueuse, +Y vers le haut)")]
        [Tooltip("Hauteur du plateau de la table.")]
        public float tableTopHeight = 0.78f;
        [Tooltip("Distance (vers la joueuse) à partir de laquelle on est au-dessus de la table, de son côté.")]
        public float tableEdge = 0.19f;
        [Tooltip("Marge minimale entre le plateau et ses bras.")]
        public float armClearance = 0.035f;
        [Tooltip("Marge minimale entre le plateau et le bas de ses cartes (laisse la place aux poignets dessous).")]
        public float fanClearance = 0.12f;

        [Header("Effondrement (fin de partie)")]
        [Tooltip("0 = tient ses cartes, 1 = bras tombés à plat sur la table.")]
        [Range(0f, 1f)] public float collapse;
        [Tooltip("Où tombent ses mains sur la table (repère de cet objet) : écart latéral, avancée depuis le bord.")]
        public Vector2 collapsedHands = new Vector2(0.24f, 0.17f);

        Vector3 _fanBase;
        bool _fanInit;

        void Awake()
        {
            if (!body) body = transform;
            Measure(left); Measure(right);
            // Gauche/droite à l'écran : celle dont l'épaule est vers -X du corps
            if (left.upper && right.upper &&
                body.InverseTransformPoint(left.upper.position).x > body.InverseTransformPoint(right.upper.position).x)
                (left, right) = (right, left);
        }

        static void Measure(Arm a)
        {
            if (!a.upper || !a.forearm || !a.hand) return;
            a.upperLen = Vector3.Distance(a.upper.position, a.forearm.position);
            a.foreLen = Vector3.Distance(a.forearm.position, a.hand.position);
            a.handLen = a.handTip ? Vector3.Distance(a.hand.position, a.handTip.position) : a.foreLen * 0.35f;
        }

        void LateUpdate()
        {
            if (!body) return;
            if (collapse > 0f) { CollapsedPose(); return; }
            if (!hand) return;
            KeepFanAboveTable();
            if (!hand.GetRestFanCorners(out var cl, out var cr)) return;
            // Coins dans le repère du corps ; la gauche du corps est -X
            var a = body.InverseTransformPoint(cl);
            var b = body.InverseTransformPoint(cr);
            if (a.x > b.x) (a, b) = (b, a);
            float sep = b.x - a.x;
            if (sep < minSeparation)
            {
                float mid = (a.x + b.x) * 0.5f;
                a.x = mid - minSeparation * 0.5f;
                b.x = mid + minSeparation * 0.5f;
            }
            var lt = a + new Vector3(wristOffset.x, wristOffset.y, wristOffset.z);
            var rt = b + new Vector3(-wristOffset.x, wristOffset.y, wristOffset.z);
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            left.grip = left.init ? Vector3.Lerp(left.grip, lt, k) : lt; left.init = true;
            right.grip = right.init ? Vector3.Lerp(right.grip, rt, k) : rt; right.init = true;
            Solve(left, +1f, k);
            Solve(right, -1f, k);
        }

        /// <summary>Morte : ses mains tombent à plat sur la table, de part et d'autre de sa tête.</summary>
        void CollapsedPose()
        {
            float k = 1f - Mathf.Exp(-sharpness * 1.6f * Time.deltaTime);
            foreach (var (arm, side) in new[] { (left, -1f), (right, 1f) })
            {
                var world = transform.TransformPoint(new Vector3(side * collapsedHands.x, tableTopHeight + 0.03f, tableEdge + collapsedHands.y));
                var target = body.InverseTransformPoint(world);
                arm.grip = arm.init ? Vector3.Lerp(arm.grip, target, k * collapse) : target;
                arm.init = true;
                Solve(arm, -side, k, true);
            }
        }

        /// <summary>Relève l'éventail (en monde) si le bas de ses cartes descend trop près du plateau.</summary>
        void KeepFanAboveTable()
        {
            var fan = hand.transform;
            if (!_fanInit) { _fanBase = fan.localPosition; _fanInit = true; }
            fan.localPosition = _fanBase;
            if (!hand.GetRestFanCorners(out var cl, out var cr)) return;
            float low = float.MaxValue;
            foreach (var c in new[] { cl, cr })
            {
                var p = transform.InverseTransformPoint(c);
                if (p.z > tableEdge - 0.05f) low = Mathf.Min(low, p.y);
            }
            float lift = tableTopHeight + fanClearance - low;
            if (low < float.MaxValue && lift > 0f) fan.position += transform.up * lift;
        }

        /// <summary>
        /// Marge minimale (m) entre le plateau et le segment a→b, sur la partie au-dessus de la table.
        /// Positif = au-dessus avec la marge voulue ; MaxValue si le segment n'est pas au-dessus de la table.
        /// </summary>
        float TableMargin(Vector3 a, Vector3 b)
        {
            float m = float.MaxValue;
            for (int i = 0; i <= 6; i++)
            {
                var p = transform.InverseTransformPoint(Vector3.Lerp(a, b, i / 6f));
                if (p.z > tableEdge - 0.03f) m = Mathf.Min(m, p.y - tableTopHeight - armClearance);
            }
            return m;
        }

        /// <summary>inward = +1 pour le bras gauche (le centre de l'éventail est vers +X du corps).</summary>
        void Solve(Arm a, float inward, float k, bool lying = false)
        {
            if (!a.upper || !a.forearm || !a.hand || a.upperLen <= 0f) return;
            var up = body.up;
            var toPlayer = body.forward;
            var inDir = body.right * inward;
            // Main : prolonge l'avant-bras vers le coin de la carte, penchée vers le centre et vers la joueuse.
            // Effondrée : main à plat sur la table, doigts vers la joueuse
            var flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var handDir = lying ? (flatFwd + inDir * 0.3f - Vector3.up * 0.1f).normalized
                                : (up * 0.8f + inDir * handTiltIn + toPlayer * 0.35f).normalized;

            // Le bout de la main s'arrête sur le coin : le poignet est en dessous, côté extérieur
            var wristTarget = body.TransformPoint(a.grip) - handDir * a.handLen * 0.8f;

            // IK deux os
            var S = a.upper.position;
            var ST = wristTarget - S;
            float d = Mathf.Clamp(ST.magnitude, Mathf.Abs(a.upperLen - a.foreLen) + 1e-3f, a.upperLen + a.foreLen - 1e-3f);
            var dir = ST.normalized;
            var T = S + dir * d;
            float cosA = Mathf.Clamp((a.upperLen * a.upperLen + d * d - a.foreLen * a.foreLen) / (2f * a.upperLen * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            // Coudes vers le bas et vers l'arrière, le long du buste (pas posés sur la table devant elle).
            // Si le bras traverse alors le plateau, le coude s'écarte et remonte jusqu'à passer au-dessus.
            // u = 0 : le long du buste ; u = 1 : coude écarté vers l'extérieur et relevé
            Vector3 Elbow(float u)
            {
                var poleHint = -inDir * Mathf.Lerp(0.45f, 1f, u) - up * Mathf.Lerp(0.8f, -0.25f, u) - toPlayer * Mathf.Lerp(0.75f, 0.3f, u);
                var pole = Vector3.ProjectOnPlane(poleHint, dir).normalized;
                if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(-up, dir).normalized;
                return S + dir * (a.upperLen * cosA) + pole * (a.upperLen * sinA);
            }
            float targetU = lying ? 0.7f : 0f, best = float.MinValue;
            for (int c = 0; c <= 8 && !lying; c++)
            {
                float u = c / 8f;
                var e = Elbow(u);
                float margin = Mathf.Min(TableMargin(S, e), TableMargin(e, T));
                if (margin >= 0f) { targetU = u; break; }
                if (margin > best) { best = margin; targetU = u; }
            }
            // Transition douce (pas de saut d'une position de coude à l'autre)
            a.elbowOut = Mathf.Lerp(a.elbowOut, targetU, k);
            var E = Elbow(a.elbowOut);
            Aim(a.upper, a.forearm, E - S);
            Aim(a.forearm, a.hand, T - E);

            // Main dans l'axe de la carte, paume tournée vers elle (les doigts derrière les cartes, côté joueuse)
            if (a.handTip) Aim(a.hand, a.handTip, handDir);
            var palm = a.hand.TransformDirection(a.palmLocal);
            var wantPalm = lying ? Vector3.down : (-toPlayer + inDir * 0.4f).normalized;
            var p0 = Vector3.ProjectOnPlane(palm, handDir);
            var p1 = Vector3.ProjectOnPlane(wantPalm, handDir);
            if (p0.sqrMagnitude > 1e-8f && p1.sqrMagnitude > 1e-8f)
                a.hand.rotation = Quaternion.FromToRotation(p0.normalized, p1.normalized) * a.hand.rotation;
        }

        static void Aim(Transform bone, Transform child, Vector3 dir)
        {
            if (!bone || !child || dir.sqrMagnitude < 1e-8f) return;
            var cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(cur, dir.normalized) * bone.rotation;
        }
    }
}
