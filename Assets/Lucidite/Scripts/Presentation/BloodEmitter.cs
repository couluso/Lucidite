using UnityEngine;

namespace Lucidite
{
    /// <summary>Fait goutter du sang depuis un point (carte sélectionnée, blessure, main...).</summary>
    public class BloodEmitter : MonoBehaviour
    {
        public Transform source;
        public Vector3 localOffset;
        [Tooltip("Gouttes par seconde.")]
        public float rate = 2f;
        public float jitter = 0.004f;
        public float dropSize = 1f;

        float _acc;

        void Update()
        {
            var bs = BloodSystem.Instance;
            if (!bs || rate <= 0f) return;
            var src = source ? source : transform;
            _acc += rate * Time.deltaTime * Random.Range(0.6f, 1.4f);
            while (_acc >= 1f)
            {
                _acc -= 1f;
                bs.SpawnDrop(src.TransformPoint(localOffset) + Random.insideUnitSphere * jitter, dropSize);
            }
        }

        public void Burst(int count)
        {
            var bs = BloodSystem.Instance;
            if (!bs) return;
            var src = source ? source : transform;
            for (int i = 0; i < count; i++) bs.SpawnDrop(src.TransformPoint(localOffset) + Random.insideUnitSphere * jitter * 2f, dropSize * Random.Range(0.8f, 1.5f));
        }
    }
}
