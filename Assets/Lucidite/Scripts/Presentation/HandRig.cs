using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Cale les mains sur l'éventail : la main gauche tient la carte la plus à gauche, la droite la plus à droite.
    /// Chaque bras pivote autour de son coude (et s'allonge un peu si besoin), quel que soit le nombre de cartes.
    /// </summary>
    public class HandRig : MonoBehaviour
    {
        public HandView hand;
        [Tooltip("Repère des positions (le parent des bras, ex. 'Tremor').")]
        public Transform space;

        [Header("Bras gauche")]
        public Transform leftPivot;
        public Vector3 leftElbow;
        public Vector3 leftDefaultGrip;

        [Header("Bras droit")]
        public Transform rightPivot;
        public Vector3 rightElbow;
        public Vector3 rightDefaultGrip;

        [Header("Réglages")]
        [Tooltip("Décalage de la prise par rapport au coin inférieur de la carte (repère 'space').")]
        public Vector3 gripOffset = new Vector3(0.012f, -0.012f, 0.012f);
        public float minSeparation = 0.13f;
        public float sharpness = 10f;

        Vector3 _leftGrip, _rightGrip;

        void Awake()
        {
            _leftGrip = leftDefaultGrip;
            _rightGrip = rightDefaultGrip;
        }

        void LateUpdate()
        {
            if (!space) space = transform;
            Vector3 lt = leftDefaultGrip, rt = rightDefaultGrip;
            if (hand && hand.GetRestFanCorners(out var lw, out var rw))
            {
                lt = space.InverseTransformPoint(lw) + new Vector3(gripOffset.x, gripOffset.y, gripOffset.z);
                rt = space.InverseTransformPoint(rw) + new Vector3(-gripOffset.x, gripOffset.y, gripOffset.z);
                // Peu de cartes : on garde les mains écartées pour qu'elles ne se chevauchent pas
                float sep = rt.x - lt.x;
                if (sep < minSeparation)
                {
                    float mid = (lt.x + rt.x) * 0.5f;
                    lt.x = mid - minSeparation * 0.5f;
                    rt.x = mid + minSeparation * 0.5f;
                }
            }
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            _leftGrip = Vector3.Lerp(_leftGrip, lt, k);
            _rightGrip = Vector3.Lerp(_rightGrip, rt, k);
            Pose(leftPivot, leftElbow, leftDefaultGrip, _leftGrip);
            Pose(rightPivot, rightElbow, rightDefaultGrip, _rightGrip);
        }

        static void Pose(Transform pivot, Vector3 elbow, Vector3 defaultGrip, Vector3 grip)
        {
            if (!pivot) return;
            var from = defaultGrip - elbow;
            var to = grip - elbow;
            if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f) return;
            pivot.localRotation = Quaternion.FromToRotation(from, to);
            // Si la carte est plus loin que la longueur du bras, l'avant-bras avance (glisse) vers elle
            pivot.localPosition = elbow + to.normalized * (to.magnitude - from.magnitude);
        }
    }
}
