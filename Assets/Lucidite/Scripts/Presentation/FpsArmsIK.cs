using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Pose les bras "PSX FPS Hands" (squelette type Rigify) par cinématique inverse :
    /// chaque main se place sous la carte extrême de l'éventail (gauche / droite), paume vers la caméra,
    /// index et annulaire repliés DERRIÈRE les cartes, pouce DEVANT. Indépendant des axes locaux des os :
    /// tout est calculé en directions monde.
    /// </summary>
    public class FpsArmsIK : MonoBehaviour
    {
        [System.Serializable]
        public class Arm
        {
            public Transform upper, forearm, hand;
            public Transform index1, index2, index3, indexEnd;
            public Transform ring1, ring2, ring3, ringEnd;
            public Transform middle1, middle2, middle3, middleEnd;
            public Transform pinky1, pinky2, pinky3, pinkyEnd;
            public Transform thumb1, thumb2, thumb3, thumbEnd;
            public Transform palmIndex, palmRing;
            [HideInInspector] public float upperLen, foreLen, handLen;
            [HideInInspector] public Vector3 grip;
            [HideInInspector] public Vector3 cardUp; // repère 'space'
        }

        public HandView hand;
        [Tooltip("Repère des positions (le parent des bras, ex. 'Tremor').")]
        public Transform space;
        public Arm left = new Arm();
        public Arm right = new Arm();

        [Header("Prise (repère 'space')")]
        public Vector3 leftDefaultGrip = new Vector3(-0.16f, -0.205f, 0.4f);
        public Vector3 rightDefaultGrip = new Vector3(0.16f, -0.21f, 0.4f);
        [Tooltip("Point pincé entre pouce et index, par rapport au coin inférieur de la carte : vers l'intérieur, vers le haut, derrière.")]
        public Vector3 pinchOffset = new Vector3(0.01f, 0.012f, 0.003f);
        [Tooltip("Distance entre le point pincé et la jointure de l'index, le long de la main (plus petit = carte plus enfoncée dans le creux).")]
        public float crookDepth = 0.002f;
        [Tooltip("Inclinaison de la main par rapport à la carte tenue : vers l'arrière (loin de la caméra) et vers le centre.")]
        public float handLeanBack = 0.12f;
        public float handLeanIn = 0.1f;
        [Tooltip("Orientation de la paume : 1 = vers le centre de l'éventail (main vue de profil), 0 = vers l'arrière des cartes (dos de la main face à la caméra).")]
        [Range(0f, 1f)] public float palmInward = 0.88f;
        public float minSeparation = 0.13f;
        public float sharpness = 10f;

        [Header("Doigts (poing lâche, réf. pince pouce-index)")]
        [Tooltip("Repli des phalanges de l'index (degrés cumulés par phalange) : 1re, 2e, 3e.")]
        public Vector3 indexCurl = new Vector3(55f, 70f, 55f);
        [Tooltip("Repli des autres doigts, plus serrés dans la paume.")]
        public Vector3 fingerCurl = new Vector3(75f, 85f, 60f);

        void Awake()
        {
            if (!space) space = transform.parent ? transform.parent : transform;
            Measure(left); Measure(right);
            left.grip = leftDefaultGrip; right.grip = rightDefaultGrip;
        }

        static void Measure(Arm a)
        {
            if (!a.upper || !a.forearm || !a.hand) return;
            a.upperLen = Vector3.Distance(a.upper.position, a.forearm.position);
            a.foreLen = Vector3.Distance(a.forearm.position, a.hand.position);
            var knuckle = a.index1 ? a.index1.position : (a.palmIndex ? a.palmIndex.position : a.hand.position);
            a.handLen = Vector3.Distance(a.hand.position, knuckle);
        }

        void LateUpdate()
        {
            Vector3 lt = leftDefaultGrip, rt = rightDefaultGrip;
            Vector3 lu = space.up, ru = space.up;
            if (hand && hand.GetRestFanCorners(out var lw, out var rw))
            {
                hand.GetRestFanUps(out lu, out ru);
                // Point pincé : dans le repère de la carte extrême (suit son inclinaison),
                // un peu à l'intérieur et au-dessus de son coin inférieur extérieur
                lt = space.InverseTransformPoint(PinchPoint(lw, lu, +1f));
                rt = space.InverseTransformPoint(PinchPoint(rw, ru, -1f));
                float sep = rt.x - lt.x;
                if (sep < minSeparation)
                {
                    float mid = (lt.x + rt.x) * 0.5f;
                    lt.x = mid - minSeparation * 0.5f;
                    rt.x = mid + minSeparation * 0.5f;
                }
            }
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            left.grip = Vector3.Lerp(left.grip, lt, k);
            right.grip = Vector3.Lerp(right.grip, rt, k);
            left.cardUp = Vector3.Slerp(left.cardUp == Vector3.zero ? space.InverseTransformDirection(lu) : left.cardUp, space.InverseTransformDirection(lu), k);
            right.cardUp = Vector3.Slerp(right.cardUp == Vector3.zero ? space.InverseTransformDirection(ru) : right.cardUp, space.InverseTransformDirection(ru), k);
            Solve(left, +1f);
            Solve(right, -1f);
        }

        /// <summary>Direction "vers le centre de l'éventail" dans le plan de la carte.</summary>
        Vector3 InAlong(Vector3 cardUp, float inward)
        {
            var v = Vector3.ProjectOnPlane(space.right * inward, cardUp);
            return v.sqrMagnitude > 1e-8f ? v.normalized : space.right * inward;
        }

        Vector3 PinchPoint(Vector3 corner, Vector3 cardUp, float inward) =>
            corner + cardUp * pinchOffset.y + InAlong(cardUp, inward) * pinchOffset.x + space.forward * pinchOffset.z;

        /// <summary>inward = +1 si le centre de l'éventail est vers +X (main gauche), -1 sinon.</summary>
        void Solve(Arm a, float inward)
        {
            if (!a.upper || !a.forearm || !a.hand || a.upperLen <= 0f) return;
            var up = space.up;
            var back = space.forward;                  // derrière les cartes (loin de la caméra)
            var front = -back;
            // Repère de la carte extrême tenue : la main suit son inclinaison dans l'éventail
            var cardUp = a.cardUp.sqrMagnitude > 1e-6f ? space.TransformDirection(a.cardUp).normalized : up;
            var inDir = InAlong(cardUp, inward);       // vers le centre de l'éventail, dans le plan de la carte
            // Poing lâche dressé sous le coin de la carte, dans l'axe de celle-ci (réf. « 18hndcard ») :
            // le pouce passe devant la carte et presse le coin contre le flanc de l'index replié derrière.
            var handAxis = (cardUp + back * handLeanBack + inDir * handLeanIn).normalized;
            // Paume tournée entre le centre de l'éventail et l'arrière des cartes : vue de face, la main
            // montre son dos et son flanc (pas une tranche) ; les doigts se replient derrière la carte.
            var palmDir = Vector3.ProjectOnPlane(inDir * palmInward + back * (1f - palmInward), handAxis).normalized;
            var lateral = (Vector3.Cross(palmDir, handAxis) * inward).normalized; // de l'index vers l'annulaire

            // Le coin de la carte est pincé entre le pouce (devant) et le flanc de l'index replié (derrière) :
            // la jointure de l'index se trouve donc un peu sous le point pincé et juste derrière la carte.
            var pinch = space.TransformPoint(a.grip);
            // La carte entre dans le creux pouce-index : la jointure de l'index arrive à hauteur du coin,
            // juste derrière la carte, et le pouce remonte devant.
            var knuckle = pinch - handAxis * crookDepth - palmDir * 0.002f + back * 0.009f;
            var wristTarget = knuckle - handAxis * a.handLen;

            // --- IK deux os (épaule fixe, coude vers l'extérieur et le bas)
            var S = a.upper.position;
            var ST = wristTarget - S;
            float d = Mathf.Clamp(ST.magnitude, Mathf.Abs(a.upperLen - a.foreLen) + 1e-3f, a.upperLen + a.foreLen - 1e-3f);
            var dir = ST.normalized;
            var T = S + dir * d;
            float cosA = Mathf.Clamp((a.upperLen * a.upperLen + d * d - a.foreLen * a.foreLen) / (2f * a.upperLen * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            var poleHint = -inDir * 0.6f - up * 1f;
            var pole = Vector3.ProjectOnPlane(poleHint, dir).normalized;
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(-up, dir).normalized;
            var E = S + dir * (a.upperLen * cosA) + pole * (a.upperLen * sinA);
            Aim(a.upper, a.forearm, E - S);
            Aim(a.forearm, a.hand, T - E);

            // --- Main : jointures dans l'axe, roulis tel que l'index soit côté caméra
            // (les os "palm" partent du poignet : on vise les jointures, c.-à-d. la base des doigts)
            var kIndex = a.index1 ? a.index1 : a.palmIndex;
            var kRing = a.ring1 ? a.ring1 : a.palmRing;
            if (kIndex) Aim(a.hand, kIndex, handAxis);
            if (kIndex && kRing)
            {
                var across = Vector3.ProjectOnPlane(kRing.position - kIndex.position, handAxis);
                if (across.sqrMagnitude > 1e-8f)
                    a.hand.rotation = Quaternion.FromToRotation(across.normalized, lateral) * a.hand.rotation;
            }

            // --- Doigts : poing lâche, repliés vers la paume (vers le centre de l'éventail)
            Vector3 Bend(float deg) => (handAxis * Mathf.Cos(deg * Mathf.Deg2Rad) + palmDir * Mathf.Sin(deg * Mathf.Deg2Rad)).normalized;
            Curl(a.index1, a.index2, a.index3, a.indexEnd, Bend(indexCurl.x), Bend(indexCurl.x + indexCurl.y), Bend(indexCurl.x + indexCurl.y + indexCurl.z));
            var fc = fingerCurl;
            Curl(a.middle1, a.middle2, a.middle3, a.middleEnd, Bend(fc.x - 5f), Bend(fc.x + fc.y - 5f), Bend(fc.x + fc.y + fc.z - 5f));
            Curl(a.ring1, a.ring2, a.ring3, a.ringEnd, Bend(fc.x), Bend(fc.x + fc.y), Bend(fc.x + fc.y + fc.z));
            Curl(a.pinky1, a.pinky2, a.pinky3, a.pinkyEnd, Bend(fc.x + 5f), Bend(fc.x + fc.y + 5f), Bend(fc.x + fc.y + fc.z + 5f));

            // --- Pouce : monte devant la carte et vient presser le coin contre le flanc de l'index
            if (a.thumb1)
            {
                // Base du pouce vers un point juste devant le coin, puis la pulpe se couche sur la face de la carte
                // Le pouce passe devant la carte au niveau du creux puis se couche le long de sa face
                var toPinch = (pinch + front * 0.008f) - a.thumb1.position;
                var d1 = toPinch.sqrMagnitude > 1e-8f ? (toPinch.normalized * 0.8f + cardUp * 0.2f).normalized : handAxis;
                var d2 = (cardUp * 0.9f + inDir * 0.3f + front * 0.04f).normalized;
                var d3 = (cardUp * 0.75f + inDir * 0.55f - front * 0.05f).normalized;
                Curl(a.thumb1, a.thumb2, a.thumb3, a.thumbEnd, d1, d2, d3);
            }
        }

        static void Curl(Transform b1, Transform b2, Transform b3, Transform end, Vector3 d1, Vector3 d2, Vector3 d3)
        {
            Aim(b1, b2, d1);
            Aim(b2, b3, d2);
            Aim(b3, end, d3);
        }

        static void Aim(Transform bone, Transform child, Vector3 dir)
        {
            if (!bone || !child || dir.sqrMagnitude < 1e-8f) return;
            var cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(cur, dir.normalized) * bone.rotation;
        }

        /// <summary>Recherche des os par nom (squelette Rigify : upper_arm.L, forearm.L, hand.L, f_index.01.L...).</summary>
        public static Arm Bind(Transform root, string side)
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(t.name)) map[t.name] = t;
            Transform B(string n) => map.TryGetValue($"{n}.{side}", out var t) ? t : null;
            return new Arm
            {
                upper = B("upper_arm"), forearm = B("forearm"), hand = B("hand"),
                index1 = B("f_index.01"), index2 = B("f_index.02"), index3 = B("f_index.03"), indexEnd = B("f_index.03") ? (map.TryGetValue($"f_index.03.{side}_end", out var ie) ? ie : null) : null,
                ring1 = B("f_ring.01"), ring2 = B("f_ring.02"), ring3 = B("f_ring.03"), ringEnd = map.TryGetValue($"f_ring.03.{side}_end", out var re) ? re : null,
                middle1 = B("f_middle.01"), middle2 = B("f_middle.02"), middle3 = B("f_middle.03"), middleEnd = map.TryGetValue($"f_middle.03.{side}_end", out var me) ? me : null,
                pinky1 = B("f_pinky.01"), pinky2 = B("f_pinky.02"), pinky3 = B("f_pinky.03"), pinkyEnd = map.TryGetValue($"f_pinky.03.{side}_end", out var pe) ? pe : null,
                thumb1 = B("thumb.01"), thumb2 = B("thumb.02"), thumb3 = B("thumb.03"), thumbEnd = map.TryGetValue($"thumb.03.{side}_end", out var te) ? te : null,
                palmIndex = B("palm.01"), palmRing = B("palm.03")
            };
        }
    }
}
