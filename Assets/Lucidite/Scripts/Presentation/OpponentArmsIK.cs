using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Bras de la Dépression (squelette importé, axes inconnus) posés par cinématique inverse :
    /// chaque main tient un bord de son éventail (première / dernière carte), coudes vers l'extérieur.
    /// Tout est calculé en directions monde ; seuls l'axe de la paume est mesuré à la construction.
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
            if (!body || !hand) return;
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
            Solve(left, +1f);
            Solve(right, -1f);
        }

        /// <summary>inward = +1 pour le bras gauche (le centre de l'éventail est vers +X du corps).</summary>
        void Solve(Arm a, float inward)
        {
            if (!a.upper || !a.forearm || !a.hand || a.upperLen <= 0f) return;
            var up = body.up;
            var toPlayer = body.forward;
            var inDir = body.right * inward;
            // Main : prolonge l'avant-bras vers le coin de la carte, penchée vers le centre et vers la joueuse
            var handDir = (up * 0.8f + inDir * handTiltIn + toPlayer * 0.35f).normalized;

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
            // Coudes vers le bas et vers l'arrière, le long du buste (pas posés sur la table devant elle)
            var poleHint = -inDir * 0.45f - up * 0.8f - toPlayer * 0.75f;
            var pole = Vector3.ProjectOnPlane(poleHint, dir).normalized;
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(-up, dir).normalized;
            var E = S + dir * (a.upperLen * cosA) + pole * (a.upperLen * sinA);
            Aim(a.upper, a.forearm, E - S);
            Aim(a.forearm, a.hand, T - E);

            // Main dans l'axe de la carte, paume tournée vers elle (les doigts derrière les cartes, côté joueuse)
            if (a.handTip) Aim(a.hand, a.handTip, handDir);
            var palm = a.hand.TransformDirection(a.palmLocal);
            var wantPalm = (-toPlayer + inDir * 0.4f).normalized;
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
