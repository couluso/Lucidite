using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>
    /// Petit affichage accroché au-dessus de la Dépression (suit sa tête à l'écran) :
    /// Emprise (ses PV), bouclier, statuts et intention du tour.
    /// </summary>
    public class EnemyOverlay : MonoBehaviour
    {
        public RectTransform root;
        public TMP_Text hpText;
        public RectTransform hpBar;
        public TMP_Text blockText;
        public GameObject blockRoot;
        public TMP_Text statusText;
        public Image intentIcon;
        public TMP_Text intentText;
        public Sprite iconAttack, iconDefend, iconBuff, iconCurse;
        public Vector3 worldOffset = new Vector3(0, 0.38f, 0);

        Transform _anchor;
        Canvas _canvas;

        void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
            var opp = FindAnyObjectByType<OpponentView>();
            if (opp) _anchor = opp.head ? opp.head : opp.transform;
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            bool show = gm && gm.Enemy != null && (gm.Phase == GamePhase.PlayerTurn || gm.Phase == GamePhase.EnemyTurn || gm.Phase == GamePhase.RoundIntro);
            if (root) root.gameObject.SetActive(show);
            if (!show) return;

            var e = gm.Enemy;
            if (hpText) hpText.text = $"EMPRISE  {e.hp}/{e.maxHp}";
            if (hpBar) hpBar.localScale = new Vector3(e.maxHp > 0 ? e.hp / (float)e.maxHp : 0f, 1f, 1f);
            if (blockRoot) blockRoot.SetActive(e.block > 0);
            if (blockText) blockText.text = e.block.ToString();
            if (statusText) statusText.text = e.StatusText();

            var it = gm.Intent;
            bool hasIntent = it != null && gm.Phase == GamePhase.PlayerTurn;
            if (intentIcon)
            {
                intentIcon.gameObject.SetActive(hasIntent);
                if (hasIntent)
                {
                    switch (it.move.Intent)
                    {
                        case IntentKind.Attaque:
                        case IntentKind.AttaqueDefense:
                        case IntentKind.AttaqueMalediction: intentIcon.sprite = iconAttack; intentIcon.color = Palette.BloodLight; break;
                        case IntentKind.Defense: intentIcon.sprite = iconDefend; intentIcon.color = Palette.Ash; break;
                        case IntentKind.Malediction: intentIcon.sprite = iconCurse; intentIcon.color = Palette.Hex("8A5CA8"); break;
                        default: intentIcon.sprite = iconBuff; intentIcon.color = Palette.Hope; break;
                    }
                    float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f);
                    intentIcon.rectTransform.localScale = Vector3.one * pulse;
                }
            }
            if (intentText) intentText.text = hasIntent ? EnemyIntent.Summary(gm.Intents, e, gm.Player) : "";
            if (statusText)
            {
                string energy = $"<color=#C9A227>énergie {e.maxEnergy}</color>";
                string st = e.StatusText();
                statusText.text = string.IsNullOrEmpty(st) ? energy : $"{st}  {energy}";
            }

            // Suivi de la tête à l'écran
            if (_anchor && root && PSXScreen.Instance && PSXScreen.Instance.worldCamera && _canvas)
            {
                var vp = PSXScreen.Instance.worldCamera.WorldToViewportPoint(_anchor.position + worldOffset);
                var canvasRt = (RectTransform)_canvas.transform;
                var size = canvasRt.rect.size;
                root.anchorMin = root.anchorMax = Vector2.zero;
                root.anchoredPosition = Vector2.Lerp(root.anchoredPosition, new Vector2(vp.x * size.x, vp.y * size.y), 0.5f);
            }
        }
    }
}
