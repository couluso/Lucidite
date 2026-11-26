using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Lucidite
{
    /// <summary>Messages courts au centre de l'écran (file d'attente, fondu).</summary>
    public class ToastFeed : MonoBehaviour
    {
        public TMP_Text text;
        public float duration = 2.4f;

        readonly Queue<string> _queue = new Queue<string>();
        float _timer;

        public void Push(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            _queue.Enqueue(msg);
        }

        void Update()
        {
            if (!text) return;
            if (_timer <= 0f && _queue.Count > 0)
            {
                text.text = _queue.Dequeue();
                _timer = duration;
            }
            if (_timer > 0f)
            {
                _timer -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(_timer / 0.4f) * Mathf.Clamp01((duration - _timer) / 0.2f);
                text.alpha = a;
            }
            else text.alpha = 0f;
        }
    }
}
