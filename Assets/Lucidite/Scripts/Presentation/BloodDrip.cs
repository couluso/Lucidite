using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Filet de sang qui coule lentement d'un point (blessure) jusqu'à la table, puis s'étale en flaque.
    /// </summary>
    public class BloodDrip : MonoBehaviour
    {
        public Transform source;     // point de départ (suit le bras)
        public Transform stream;     // cube fin étiré
        public Transform pool;       // flaque (cylindre aplati)
        public float tableHeight = 0.78f;
        public float flowSpeed = 0.06f;    // m/s
        public float poolMaxRadius = 0.07f;
        public float poolGrowth = 0.006f;  // m/s

        float _length;
        float _poolRadius;
        Vector3 _poolPoint;
        bool _reached;

        public void Init(Transform src, float tableY, float maxRadius)
        {
            source = src;
            tableHeight = tableY;
            poolMaxRadius = maxRadius;
            _length = 0f;
            _poolRadius = 0f;
            _reached = false;
            if (pool) pool.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!source) return;
            Vector3 top = source.position;
            float maxLen = Mathf.Max(0.001f, top.y - tableHeight);
            _length = Mathf.Min(maxLen, _length + flowSpeed * Time.deltaTime);

            if (stream)
            {
                stream.position = top + Vector3.down * (_length * 0.5f);
                stream.rotation = Quaternion.identity;
                float wobble = 1f + Mathf.Sin(Time.time * 3f) * 0.15f;
                stream.localScale = new Vector3(0.0025f * wobble, _length, 0.0025f * wobble);
            }

            if (!_reached && _length >= maxLen - 0.001f)
            {
                _reached = true;
                _poolPoint = new Vector3(top.x, tableHeight + 0.0015f, top.z);
                if (pool) pool.gameObject.SetActive(true);
                AudioManager.Play(Sfx.BloodDrop, 0.6f);
            }

            if (_reached && pool)
            {
                _poolRadius = Mathf.Min(poolMaxRadius, _poolRadius + poolGrowth * Time.deltaTime);
                pool.position = _poolPoint;
                pool.rotation = Quaternion.identity;
                pool.localScale = new Vector3(_poolRadius * 2f, 0.0008f, _poolRadius * 2.4f);
            }
        }
    }
}
