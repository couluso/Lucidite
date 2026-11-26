using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Mise en scène physique du combat : pioche, cartes jouées, défausse, épuisement,
    /// carte d'intention posée par la Dépression, Crises glissées dans ton deck.
    /// Le GameManager attend chacune de ces routines.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Prefab")]
        public CardView cardPrefab;

        [Header("Mains")]
        public HandView playerHand;
        public HandView opponentHand;

        [Header("Points")]
        public Transform playerDeckPoint;
        public Transform playerDiscardPoint;
        public Transform exhaustPoint;
        public Transform playPoint;
        public Transform intentPoint;
        public Transform intentAttackPoint;
        [Tooltip("Centre de la rangée où tes cartes jouées sont posées, devant toi.")]
        public Transform playRowPoint;
        public float playRowSpacing = 0.082f;
        public ParticleSystem tableDust;

        [Header("Scène")]
        public CameraRig cameraRig;
        public PlayerArms playerArms;

        [Header("Réglages")]
        public float drawInterval = 0.07f;
        public float tableCardScale = 0.072f;
        public float playScale = 0.09f;
        public float intentScale = 0.082f;
        public int opponentHeldCards = 3;

        readonly Dictionary<CardInstance, CardView> _views = new Dictionary<CardInstance, CardView>();
        CardView _intentView;
        readonly List<CardView> _intentViews = new List<CardView>();
        readonly List<CardView> _opponentHeld = new List<CardView>();

        public CardView ViewOf(CardInstance c) => c != null && _views.TryGetValue(c, out var v) ? v : null;
        public CardView IntentView => _intentView;

        CardView Spawn(Transform at, float scale)
        {
            var v = Instantiate(cardPrefab, at.position, at.rotation);
            v.SetTarget(at.position, at.rotation, Vector3.one * scale, instant: true);
            return v;
        }

        CardView SpawnCard(CardInstance c, Transform at)
        {
            var v = Spawn(at, tableCardScale);
            v.name = "Card_" + c.data.cardName;
            v.Bind(c);
            v.FaceUp = true;
            _views[c] = v;
            return v;
        }

        void Forget(CardInstance c, float delay)
        {
            if (c != null && _views.TryGetValue(c, out var v))
            {
                _views.Remove(c);
                if (v) v.DestroyAfter(delay);
            }
        }

        void MoveTo(CardView v, Transform point, Quaternion localRot, float scale)
        {
            if (!v) return;
            v.transform.SetParent(point, true);
            v.SetTarget(Vector3.zero, localRot, Vector3.one * scale);
        }

        // ------------------------------------------------------------------ Manche

        public IEnumerator IntroRoutine()
        {
            if (cameraRig) cameraRig.GoSeated();
            if (playerArms) playerArms.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.6f);
        }

        public IEnumerator RoundStartRoutine(int round)
        {
            // La Dépression tient quelques cartes, dos à toi
            foreach (var v in _opponentHeld) if (v) Destroy(v.gameObject);
            _opponentHeld.Clear();
            if (opponentHand) opponentHand.Clear();
            yield return RefillOpponentRoutine();
            if (cameraRig) cameraRig.SetTableFocus(false);
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>Elle s'effondre (fin de partie) : ses cartes lui échappent et ne sont plus gérées par le plateau.</summary>
        public List<CardView> ReleaseOpponentCards()
        {
            var list = new List<CardView>();
            foreach (var v in _opponentHeld)
                if (v) { if (opponentHand) opponentHand.Remove(v); list.Add(v); }
            _opponentHeld.Clear();
            return list;
        }

        /// <summary>Elle repioche jusqu'à tenir son nombre de cartes (l'éventail s'élargit, ses mains suivent).</summary>
        IEnumerator RefillOpponentRoutine()
        {
            if (!opponentHand) yield break;
            _opponentHeld.RemoveAll(v => !v);
            while (_opponentHeld.Count < opponentHeldCards)
            {
                var v = Spawn(intentPoint ? intentPoint : opponentHand.transform, tableCardScale);
                v.FaceUp = false;
                opponentHand.Add(v);
                _opponentHeld.Add(v);
                AudioManager.Play(Sfx.CardDraw, 0.5f);
                yield return new WaitForSeconds(0.08f);
            }
        }

        public IEnumerator DrawRoutine(List<CardInstance> drawn)
        {
            foreach (var c in drawn)
            {
                var v = SpawnCard(c, playerDeckPoint);
                playerHand.Add(v);
                AudioManager.Play(Sfx.CardDraw, 0.8f);
                yield return new WaitForSeconds(drawInterval);
            }
            yield return new WaitForSeconds(0.15f);
        }

        public void RemoveFromHand(CardInstance c, bool exhausted)
        {
            var v = ViewOf(c);
            if (!v) return;
            playerHand.Remove(v);
            v.SetHighlight(false, Color.clear);
            if (exhausted) Burn(v); else MoveTo(v, playerDiscardPoint, Quaternion.Euler(0, 180, Random.Range(-12f, 12f)), tableCardScale);
            Forget(c, 0.6f);
        }

        void Burn(CardView v)
        {
            v.transform.SetParent(exhaustPoint ? exhaustPoint : null, true);
            v.SetTarget(exhaustPoint ? new Vector3(0, 0.08f, 0) : v.transform.position + Vector3.up * 0.1f, Quaternion.Euler(0, 0, Random.Range(-40f, 40f)), Vector3.one * 0.001f);
            AudioManager.Play(Sfx.Burn, 0.7f);
        }

        // ------------------------------------------------------------------ Cartes jouées (fin de tour)

        readonly List<CardView> _played = new List<CardView>();

        /// <summary>Les cartes sélectionnées sont plaquées face cachée, en rangée, devant toi.</summary>
        public IEnumerator LayDownRoutine(List<CardInstance> cards)
        {
            _played.Clear();
            if (cameraRig) cameraRig.SetTableFocus(true); // on regarde la table pendant la résolution
            int n = cards.Count;
            for (int i = 0; i < n; i++)
            {
                var v = ViewOf(cards[i]);
                if (!v) continue;
                playerHand.Remove(v);
                v.SetHighlight(false, Color.clear);
                var slot = playRowPoint ? playRowPoint : playPoint;
                v.transform.SetParent(slot, true);
                float x = (i - (n - 1) * 0.5f) * playRowSpacing;
                v.SetTarget(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 180f, Random.Range(-4f, 4f)), Vector3.one * tableCardScale);
                _played.Add(v);
                yield return new WaitForSeconds(0.16f);
                Slam(slot.TransformPoint(new Vector3(x, 0f, 0f)), 0.12f);
            }
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>Retourne la carte (face visible) au moment où elle prend effet.</summary>
        public IEnumerator RevealPlayedRoutine(CardInstance c)
        {
            var v = ViewOf(c);
            if (v)
            {
                var local = v.transform.localPosition;
                v.SetTarget(local + new Vector3(0, 0, 0.0f), Quaternion.identity, Vector3.one * tableCardScale);
                v.SetHighlight(true, Palette.Hope);
                v.Punch(0.35f);
                AudioManager.Play(Sfx.CardFlip);
                yield return new WaitForSeconds(0.28f);
                Slam(v.transform.position, 0.08f);
            }
            yield return new WaitForSeconds(0.12f);
        }

        /// <summary>Fin de la résolution : les cartes rejoignent la défausse (ou brûlent si épuisées).</summary>
        public IEnumerator ClearPlayedRoutine(List<(CardInstance card, bool exhausted)> resolved)
        {
            yield return new WaitForSeconds(0.35f);
            foreach (var (c, exhausted) in resolved)
            {
                var v = ViewOf(c);
                if (!v) continue;
                v.SetHighlight(false, Color.clear);
                if (exhausted) Burn(v); else MoveTo(v, playerDiscardPoint, Quaternion.Euler(0, 180, Random.Range(-12f, 12f)), tableCardScale);
                Forget(c, 0.7f);
                yield return new WaitForSeconds(0.06f);
            }
            _played.Clear();
            if (cameraRig) cameraRig.SetTableFocus(false);
            AudioManager.Play(Sfx.CardSlide);
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>Carte plaquée sur la table : bruit sourd, poussière, petite secousse.</summary>
        void Slam(Vector3 pos, float shake)
        {
            AudioManager.Play(Sfx.CardPlace);
            if (RoomShake.Instance) RoomShake.Instance.Shake(shake, false);
            if (tableDust)
            {
                var ep = new ParticleSystem.EmitParams { position = pos + Vector3.up * 0.01f, applyShapeToPosition = true };
                tableDust.Emit(ep, 10);
            }
        }

        public IEnumerator PlayCardRoutine(CardInstance c)
        {
            var v = ViewOf(c);
            if (v)
            {
                playerHand.Remove(v);
                v.SetHighlight(true, Palette.Hope);
                MoveTo(v, playPoint, Quaternion.identity, playScale);
            }
            AudioManager.Play(Sfx.CardPlace);
            yield return new WaitForSeconds(0.22f);
        }

        public IEnumerator PlayerHitRoutine(int lost, int blocked)
        {
            foreach (var iv in _intentViews) if (iv) StartCoroutine(iv.ShakeRoutine(0.25f, 0.01f));
            AudioManager.Play(lost > 0 ? Sfx.Hit : Sfx.Blocked);
            yield return new WaitForSeconds(0.16f);
        }

        public IEnumerator FinishCardRoutine(CardInstance c, bool exhausted)
        {
            var v = ViewOf(c);
            if (v)
            {
                v.SetHighlight(false, Color.clear);
                if (exhausted) Burn(v);
                else MoveTo(v, playerDiscardPoint, Quaternion.Euler(0, 180, Random.Range(-12f, 12f)), tableCardScale);
                AudioManager.Play(Sfx.CardSlide, 0.6f);
            }
            Forget(c, 0.6f);
            yield return null;
        }

        public IEnumerator DiscardHandRoutine(List<CardInstance> discarded, List<CardInstance> exhausted)
        {
            foreach (var c in discarded) RemoveFromHand(c, false);
            foreach (var c in exhausted) RemoveFromHand(c, true);
            if (discarded.Count + exhausted.Count > 0) AudioManager.Play(Sfx.CardSlide);
            yield return new WaitForSeconds(0.35f);
        }

        // ------------------------------------------------------------------ Dépression

        [Tooltip("Écart entre ses cartes posées (en mètres).")]
        public float intentSpacing = 0.095f;

        CardView IntentAt(int i) => i >= 0 && i < _intentViews.Count ? _intentViews[i] : null;

        void ClearIntents(float delay)
        {
            foreach (var v in _intentViews) if (v) v.DestroyAfter(delay);
            _intentViews.Clear();
            _intentView = null;
        }

        /// <summary>Elle pose ses cartes du tour, face visible, en rangée devant elle.</summary>
        /// <summary>
        /// Son plan a changé en cours de tour (pilules) : les cartes restantes sont remises à jour, celles qu'elle
        /// abandonne retournent dans sa main, les nouvelles sont posées à la suite.
        /// </summary>
        public void UpdateIntents(IReadOnlyList<EnemyIntent> intents)
        {
            _intentViews.RemoveAll(v => !v);
            while (_intentViews.Count > intents.Count)
            {
                var v = _intentViews[_intentViews.Count - 1];
                _intentViews.RemoveAt(_intentViews.Count - 1);
                v.transform.SetParent(intentPoint, true);
                v.SetTarget(new Vector3(0f, 0.25f, 0.2f), Quaternion.Euler(-40f, 0f, 0f), Vector3.one * intentScale * 0.6f);
                v.DestroyAfter(0.45f);
                AudioManager.Play(Sfx.CardSlide, 0.6f);
            }
            int n = intents.Count;
            for (int i = 0; i < n; i++)
            {
                CardView v;
                if (i < _intentViews.Count) v = _intentViews[i];
                else { v = TakeIntentCard(i); _intentViews.Add(v); AudioManager.Play(Sfx.CardFlip); }
                v.BindIntent(intents[i]);
                v.FaceUp = true;
                v.Punch(0.2f);
                float x = (i - (n - 1) * 0.5f) * intentSpacing;
                v.SetTarget(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 0f, Random.Range(-3f, 3f)), Vector3.one * intentScale);
            }
            _intentView = IntentAt(0);
        }

        CardView TakeIntentCard(int i)
        {
            CardView v = null;
            _opponentHeld.RemoveAll(c => !c);
            if (_opponentHeld.Count > 0)
            {
                int k = (i % 2 == 0) ? _opponentHeld.Count - 1 : 0;
                v = _opponentHeld[k];
                _opponentHeld.RemoveAt(k);
                if (opponentHand) opponentHand.Remove(v);
            }
            if (!v)
            {
                v = Instantiate(cardPrefab, intentPoint.position, intentPoint.rotation);
                v.SetTarget(intentPoint.position, intentPoint.rotation, Vector3.one * tableCardScale, true);
            }
            v.transform.SetParent(intentPoint, true);
            return v;
        }

        public void ShowIntents(IReadOnlyList<EnemyIntent> intents)
        {
            ClearIntents(0f);
            int n = intents.Count;
            for (int i = 0; i < n; i++)
            {
                var intent = intents[i];
                // Elle prend la carte dans sa main (les cartes extérieures d'abord) : son éventail se resserre
                CardView v = null;
                _opponentHeld.RemoveAll(c => !c);
                if (_opponentHeld.Count > 0)
                {
                    int k = (i % 2 == 0) ? _opponentHeld.Count - 1 : 0;
                    v = _opponentHeld[k];
                    _opponentHeld.RemoveAt(k);
                    if (opponentHand) opponentHand.Remove(v);
                }
                if (!v)
                {
                    v = Instantiate(cardPrefab, intentPoint.position, intentPoint.rotation);
                    v.SetTarget(intentPoint.position, intentPoint.rotation, Vector3.one * tableCardScale, true);
                }
                v.name = "Intent_" + (intent.move ? intent.move.moveName : i.ToString());
                v.BindIntent(intent);
                v.FaceUp = true;
                v.transform.SetParent(intentPoint, true);
                float x = (i - (n - 1) * 0.5f) * intentSpacing;
                v.SetTarget(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 0f, Random.Range(-3f, 3f)), Vector3.one * intentScale);
                _intentViews.Add(v);
            }
            _intentView = IntentAt(0);
            StartCoroutine(IntentSlamRoutine(n));
        }

        IEnumerator IntentSlamRoutine(int n)
        {
            for (int i = 0; i < n; i++)
            {
                AudioManager.Play(Sfx.CardFlip);
                yield return new WaitForSeconds(0.14f);
            }
        }

        public IEnumerator EnemyActRoutine(EnemyIntent intent, int index)
        {
            var v = IntentAt(index);
            if (!v) yield break;
            _intentView = v;
            v.SetHighlight(true, Palette.BloodLight);
            v.Punch(0.3f);
            if (intent.damagePerHit > 0 && intentAttackPoint)
            {
                MoveTo(v, intentAttackPoint, Quaternion.identity, intentScale * 1.25f);
                AudioManager.Play(Sfx.Swoosh);
                yield return new WaitForSeconds(0.25f);
            }
            else
            {
                AudioManager.Play(Sfx.CardFlip);
                yield return new WaitForSeconds(0.35f);
            }
        }

        public IEnumerator EnemyHitRoutine(int lost, int blocked, int index)
        {
            var v = IntentAt(index);
            if (v) StartCoroutine(v.ShakeRoutine(0.2f, 0.01f));
            yield return new WaitForSeconds(lost > 0 ? 0.45f : 0.25f);
        }

        /// <summary>Carte jouée : elle la retourne et la retire.</summary>
        public IEnumerator EnemyCardDoneRoutine(int index)
        {
            var v = IntentAt(index);
            if (v)
            {
                v.SetHighlight(false, Color.clear);
                v.transform.SetParent(intentPoint, true);
                v.SetTarget(new Vector3(0f, 0.02f, 0.12f), Quaternion.Euler(0, 180, Random.Range(-15f, 15f)), Vector3.one * tableCardScale);
                v.DestroyAfter(0.6f);
                _intentViews[index] = null;
            }
            yield return new WaitForSeconds(0.25f);
        }

        public IEnumerator EnemyEndRoutine()
        {
            ClearIntents(0.6f);
            yield return RefillOpponentRoutine();
            yield return new WaitForSeconds(0.2f);
        }

        public IEnumerator CrisisRoutine(List<CardInstance> added, bool toDraw)
        {
            var target = toDraw ? playerDeckPoint : playerDiscardPoint;
            foreach (var c in added)
            {
                var v = SpawnCard(c, intentPoint);
                MoveTo(v, playPoint, Quaternion.identity, playScale);
                AudioManager.Play(Sfx.Whisper, 0.7f);
                yield return new WaitForSeconds(0.55f);
                MoveTo(v, target, Quaternion.Euler(0, 180, Random.Range(-15f, 15f)), tableCardScale);
                Forget(c, 0.7f);
                yield return new WaitForSeconds(0.15f);
            }
        }

        public IEnumerator ClearRoundRoutine(bool playerWon)
        {
            foreach (var kv in new List<KeyValuePair<CardInstance, CardView>>(_views))
            {
                var v = kv.Value;
                if (!v) continue;
                playerHand.Remove(v);
                MoveTo(v, playerDiscardPoint, Quaternion.Euler(0, 180, Random.Range(-12f, 12f)), tableCardScale);
                v.DestroyAfter(0.7f);
            }
            _views.Clear();
            ClearIntents(0.1f);
            foreach (var v in _opponentHeld) if (v) { opponentHand.Remove(v); v.DestroyAfter(0.1f); }
            _opponentHeld.Clear();
            yield return new WaitForSeconds(1.2f);
        }
    }
}
