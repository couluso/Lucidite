using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Rangée de bougies : une bougie = une manche qu'on peut encore perdre.
    /// Perdre une manche en éteint une ; à zéro, la partie est perdue.
    /// </summary>
    public class LucidityCandles : MonoBehaviour
    {
        public Side side;
        public Candle[] candles;

        void Start()
        {
            var gm = GameManager.Instance;
            if (!gm) return;
            gm.OnMatchStarted += HandleMatchStarted;
            gm.OnCandleLost += HandleCandleLost;
        }

        void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (!gm) return;
            gm.OnMatchStarted -= HandleMatchStarted;
            gm.OnCandleLost -= HandleCandleLost;
        }

        void HandleMatchStarted()
        {
            var gm = GameManager.Instance;
            var f = side == Side.Player ? gm.Player : gm.Enemy;
            for (int i = 0; i < candles.Length; i++)
            {
                if (!candles[i]) continue;
                candles[i].gameObject.SetActive(i < f.maxCandles);
                candles[i].SetLit(true, false);
            }
        }

        void HandleCandleLost(Side s, int remaining)
        {
            if (s != side) return;
            for (int i = 0; i < candles.Length; i++)
                if (candles[i] && candles[i].gameObject.activeSelf) candles[i].SetLit(i < remaining, true);
        }
    }
}
