using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Reliques (statuettes) et piluliers posés sur la table : les tiens de ton côté, les siens du sien.
    /// Une statuette achetée tombe à sa place. Les pilules dorment dans leur pilulier : clic sur le tien, il s'ouvre et
    /// les déroule en rangée sur la table (survol : description, clic : l'avaler). Le sien ne s'ouvre que lorsqu'elle
    /// en avale une. Survol et clics : CardPicker.
    /// </summary>
    public class TableItems : MonoBehaviour
    {
        public Transform[] playerRelicSlots;
        public Transform[] enemyRelicSlots;
        [Tooltip("Emplacements des piluliers (le sien est tourné vers toi).")]
        public Transform playerPillBoxPoint;
        public Transform enemyPillBoxPoint;
        public GameObject pillBoxPrefab;
        [Tooltip("Matériau des gélules (texture générée à chaque partie, PillLooks).")]
        public Material pillMaterial;
        [Tooltip("Longueur d'une gélule posée sur la table (plus grande que nature pour rester lisible).")]
        public float pillLength = 0.04f;
        [Tooltip("Écart entre deux pilules sorties du pilulier (repère du pilulier : -x).")]
        public float pillSpacing = 0.05f;

        readonly Dictionary<ItemData, TableItemView> _playerRelics = new Dictionary<ItemData, TableItemView>();
        readonly Dictionary<ItemData, TableItemView> _enemyRelics = new Dictionary<ItemData, TableItemView>();
        readonly Dictionary<OwnedItem, TableItemView> _playerPills = new Dictionary<OwnedItem, TableItemView>();
        readonly Dictionary<OwnedItem, TableItemView> _enemyPills = new Dictionary<OwnedItem, TableItemView>();
        PillBoxView _playerBox, _enemyBox;

        // Reliques qui font effet : file d'attente (statuette pas encore posée), délai entre deux, anti-répétition
        readonly List<(Side side, ItemData relic, string label, float time)> _pendingJuice = new List<(Side, ItemData, string, float)>();
        readonly Dictionary<ItemData, float> _lastJuice = new Dictionary<ItemData, float>();
        float _nextJuiceSlot;
        [Tooltip("Une même relique ne ressaute pas avant ce délai (évite le spam sur les effets répétés).")]
        public float juiceCooldown = 0.6f;
        [Tooltip("Décalage entre deux reliques qui font effet au même moment.")]
        public float juiceStagger = 0.2f;

        public static TableItems Instance { get; private set; }

        void Awake() { Instance = this; }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var gm = GameManager.Instance;
            if (gm) { gm.OnRelicTriggered -= HandleRelicTriggered; gm.OnRoundStarted -= HandleRoundStarted; }
        }

        void Start()
        {
            _playerBox = SpawnBox(playerPillBoxPoint, Side.Player);
            _enemyBox = SpawnBox(enemyPillBoxPoint, Side.Opponent);
            var gm = GameManager.Instance;
            if (gm)
            {
                gm.OnRelicTriggered += HandleRelicTriggered;
                gm.OnRoundStarted += HandleRoundStarted;
            }
        }

        /// <summary>Chaque manche passée entre ses mains, ses statuettes se couvrent un peu plus de sang.</summary>
        void HandleRoundStarted(int round)
        {
            foreach (var kv in _enemyRelics) if (kv.Value) kv.Value.Bleed(0.1f);
        }

        void HandleRelicTriggered(Side side, ItemData relic, string label)
        {
            if (!relic) return;
            float now = Time.unscaledTime;
            if (_lastJuice.TryGetValue(relic, out var last) && now - last < juiceCooldown) return;
            _lastJuice[relic] = now;
            _pendingJuice.Add((side, relic, label, now));
        }

        void FlushJuice()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _pendingJuice.Count; i++)
            {
                var (side, relic, label, time) = _pendingJuice[i];
                var views = side == Side.Player ? _playerRelics : _enemyRelics;
                if (views.TryGetValue(relic, out var v) && v)
                {
                    _nextJuiceSlot = Mathf.Max(now, _nextJuiceSlot);
                    v.Juice(label, _nextJuiceSlot - now);
                    _nextJuiceSlot += juiceStagger;
                }
                else if (now - time < 3f) continue; // statuette pas encore posée : on réessaie
                _pendingJuice.RemoveAt(i--);
            }
        }

        PillBoxView SpawnBox(Transform point, Side side)
        {
            if (!point) return null;
            GameObject g;
            if (pillBoxPrefab) g = Instantiate(pillBoxPrefab, point, false);
            else
            {
                g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(point, false);
                g.transform.localScale = new Vector3(0.05f, 0.02f, 0.035f);
                g.transform.localPosition = Vector3.up * 0.01f;
            }
            g.name = side == Side.Player ? "TonPilulier" : "SonPilulier";
            var box = g.AddComponent<PillBoxView>();
            box.side = side;
            return box;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (!gm || gm.Player == null) return;
            SyncRelics(gm.Player, _playerRelics, playerRelicSlots, Side.Player);
            SyncRelics(gm.Enemy, _enemyRelics, enemyRelicSlots, Side.Opponent);
            SyncPills(gm.Player, _playerPills, _playerBox, Side.Player);
            SyncPills(gm.Enemy, _enemyPills, _enemyBox, Side.Opponent);
            FlushJuice();
            if (_playerBox)
            {
                _playerBox.Owner = gm.Player;
                // Le pilulier se referme en dehors de ton tour
                if (_playerBox.IsOpen && gm.Phase != GamePhase.PlayerTurn) _playerBox.SetOpen(false);
            }
            // Son pilulier reste toujours visible de son côté (même vide) : on sait qu'elle en a un
            if (_enemyBox) _enemyBox.Owner = gm.Enemy;
        }

        /// <summary>
        /// Ses achats se font entre les manches, pendant que la boutique couvre l'écran : ses nouvelles statuettes
        /// n'apparaissent qu'au début de la manche suivante, pour qu'on les voie tomber sur la table.
        /// </summary>
        static bool CanRevealEnemyPurchases(GameManager gm) =>
            gm.Phase != GamePhase.Shop && gm.Phase != GamePhase.RoundEnd;

        void SyncRelics(Fighter f, Dictionary<ItemData, TableItemView> views, Transform[] slots, Side side)
        {
            if (slots == null) return;
            var gm = GameManager.Instance;
            var owned = f.relics;
            foreach (var r in new List<ItemData>(views.Keys))
                if (!owned.Contains(r)) { if (views[r]) Destroy(views[r].gameObject); views.Remove(r); }
            if (side == Side.Opponent && gm && !CanRevealEnemyPurchases(gm)) return;
            for (int i = 0; i < owned.Count && i < slots.Length; i++)
            {
                var r = owned[i];
                if (!r || views.ContainsKey(r) || !r.model || !slots[i]) continue;
                var g = Instantiate(r.model, slots[i], false);
                g.name = r.itemName;
                // Elle : ses statuettes regardent la joueuse
                g.transform.localRotation = Quaternion.Euler(0f, (side == Side.Opponent ? 180f : 0f) + Random.Range(-20f, 20f), 0f);
                var v = g.AddComponent<TableItemView>();
                v.Init(r, null, side, true);
                views[r] = v;
                // Les siennes sont délabrées et sanglantes, et s'annoncent : chute, éclaboussures, nom au-dessus
                if (side == Side.Opponent)
                {
                    v.Defile(0.6f);
                    v.Juice($"Elle a acquis : {r.itemName}", 0.35f);
                }
            }
            foreach (var kv in views) if (kv.Value) kv.Value.SetRelicState(f, f.veiled == kv.Key, f.IsBuffed(kv.Key));
        }

        void SyncPills(Fighter f, Dictionary<OwnedItem, TableItemView> views, PillBoxView box, Side side)
        {
            if (!box) return;
            var bt = box.transform;
            Vector3 BoxPoint(Vector3 local) => bt.parent.TransformPoint(bt.localPosition + bt.localRotation * local);
            // Pilules avalées (ou volées) : elles sortent du pilulier et disparaissent
            foreach (var o in new List<OwnedItem>(views.Keys))
                if (!f.items.Contains(o))
                {
                    var v = views[o];
                    views.Remove(o);
                    if (!v) continue;
                    box.Pulse();
                    v.Swallow(BoxPoint(Vector3.up * 0.05f));
                }
            for (int i = 0; i < f.items.Count; i++)
            {
                var o = f.items[i];
                if (o == null) continue;
                if (!views.TryGetValue(o, out var v) || !v) { v = SpawnPill(o, side); views[o] = v; }
                // Ouvert : rangée sur la table à côté du pilulier ; fermé : rangées dedans (invisibles)
                bool shown = box.IsOpen && side == Side.Player;
                var local = shown ? new Vector3(-(i + 1) * pillSpacing - 0.02f, 0f, -0.01f * i) : new Vector3(0f, 0.006f, 0f);
                v.SetPillTarget(BoxPoint(local), shown);
            }
        }

        // ================================================================== Avaler une pilule

        /// <summary>
        /// Prend en charge la pilule <paramref name="o"/> (à appeler AVANT de la retirer du pilulier) et renvoie l'animation
        /// à jouer : la joueuse la porte à sa bouche (la pilule monte vers la caméra, la tête plonge en avalant) ;
        /// la Dépression la sort de sa boîte, la saisit, la porte à ses lèvres et relève le menton pour l'avaler.
        /// </summary>
        public System.Collections.IEnumerator BeginSwallow(Side side, OwnedItem o)
        {
            var views = side == Side.Player ? _playerPills : _enemyPills;
            var box = side == Side.Player ? _playerBox : _enemyBox;
            views.TryGetValue(o, out var v);
            views.Remove(o);
            if (!v) v = SpawnPill(o, side);
            v.TakeOver();
            Vector3 boxTop = box ? box.transform.position + Vector3.up * 0.02f : transform.position + Vector3.up * 0.8f;
            Vector3 from = v.PillShown ? v.transform.position : boxTop;
            if (!v.PillShown) { v.transform.position = from; v.transform.localScale = Vector3.zero; }
            if (box) { box.Pulse(); AudioManager.Play(Sfx.CardFlip, 0.45f); }
            return side == Side.Player ? SwallowPlayer(v, from, o.data) : SwallowEnemy(v, from, o.data);
        }

        static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        System.Collections.IEnumerator SwallowPlayer(TableItemView v, Vector3 from, ItemData d)
        {
            var cam = PSXScreen.Instance && PSXScreen.Instance.worldCamera ? PSXScreen.Instance.worldCamera : Camera.main;
            var tr = v.transform;
            var s1 = v.BaseScale;
            // 1) Elle la saisit : la pilule se soulève en tournant
            var lift = from + Vector3.up * 0.07f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                tr.position = Vector3.Lerp(from, lift, Ease(t));
                tr.localScale = s1 * Ease(t * 1.5f);
                tr.Rotate(0f, 500f * Time.unscaledDeltaTime, 0f, Space.World);
                yield return null;
            }
            // 2) Portée à la bouche : courbe vers le bas de l'écran, juste devant la caméra
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
            {
                var mouth = cam ? cam.transform.position + cam.transform.forward * 0.12f - cam.transform.up * 0.075f : lift + Vector3.up * 0.3f;
                var ctrl = Vector3.Lerp(lift, mouth, 0.5f) + Vector3.up * 0.12f;
                float e = Ease(t);
                tr.position = Vector3.Lerp(Vector3.Lerp(lift, ctrl, e), Vector3.Lerp(ctrl, mouth, e), e);
                tr.localScale = s1 * (1f - 0.35f * Mathf.Clamp01((t - 0.7f) / 0.3f));
                tr.Rotate(0f, 300f * Time.unscaledDeltaTime, 120f * Time.unscaledDeltaTime, Space.World);
                yield return null;
            }
            // 3) Avalée : glouc, la tête plonge, un voile de la couleur de la gélule
            Destroy(v.gameObject);
            AudioManager.Play(Sfx.Gulp, 0.9f);
            var rig = FindAnyObjectByType<CameraRig>();
            if (rig) rig.Nod(0.7f);
            if (PSXScreen.Instance) PSXScreen.Instance.Flash(PillLooks.Get(d).a, 0.14f);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        System.Collections.IEnumerator SwallowEnemy(TableItemView v, Vector3 from, ItemData d)
        {
            var opp = FindAnyObjectByType<OpponentView>();
            var ik = opp ? opp.ArmsIK : null;
            var tr = v.transform;
            var s1 = v.BaseScale;
            // 1) La pilule saute hors de sa boîte
            var hold = from + Vector3.up * 0.11f;
            if (ik) ik.BeginGesture(hold);
            // Ton regard est attiré vers elle pendant qu'elle avale
            var rig = FindAnyObjectByType<CameraRig>();
            if (rig && opp) rig.Glance(Vector3.Lerp(hold, opp.MouthPosition, 0.6f), 2.6f);
            Vector3 Chest() => opp && opp.body ? opp.body.position + opp.body.up * 0.35f : hold;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                tr.position = Vector3.Lerp(from, hold, Ease(t)) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.02f;
                tr.localScale = s1 * Ease(t * 1.4f);
                tr.Rotate(0f, 420f * Time.unscaledDeltaTime, 0f, Space.World);
                if (ik) { ik.gesture = Ease(t) * 0.5f; ik.SetGestureTarget(hold, hold - Chest()); }
                yield return null;
            }
            if (ik && opp)
            {
                // 2) Sa main vient la cueillir
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
                {
                    ik.gesture = Mathf.Lerp(0.5f, 1f, Ease(t));
                    ik.SetGestureTarget(hold, hold - Chest());
                    yield return null;
                }
                // 3) Elle la porte à ses lèvres (la pilule suit le bout de sa main)
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.7f)
                {
                    var mouth = opp.MouthPosition;
                    var target = Vector3.Lerp(hold, mouth, Ease(t));
                    ik.SetGestureTarget(target, target - Chest());
                    var tip = ik.GestureTip;
                    tr.position = tip ? tip.position : target;
                    yield return null;
                }
            }
            else
            {
                // Sans bras animables : la pilule vole jusqu'à sa bouche
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f)
                {
                    tr.position = Vector3.Lerp(hold, opp ? opp.MouthPosition : hold + Vector3.up * 0.4f, Ease(t));
                    yield return null;
                }
            }
            // 4) Elle relève le menton et avale
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                if (opp) opp.gulp = Ease(t);
                tr.localScale = s1 * (1f - Ease(t));
                if (ik && ik.GestureTip) tr.position = ik.GestureTip.position;
                yield return null;
            }
            Destroy(v.gameObject);
            AudioManager.Play(Sfx.Gulp, 0.85f);
            // La pilule passe : un frisson la traverse, l'écran prend un instant la teinte de la gélule
            if (opp) opp.Shudder(0.6f);
            if (PSXScreen.Instance) PSXScreen.Instance.Flash(PillLooks.Get(d).a, 0.08f);
            yield return new WaitForSecondsRealtime(0.4f);
            // 5) La main retourne à ses cartes, la tête revient
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
            {
                if (opp) opp.gulp = 1f - Ease(t);
                if (ik) ik.gesture = 1f - Ease(t);
                yield return null;
            }
            if (opp) opp.gulp = 0f;
            if (ik) ik.gesture = 0f;
        }

        TableItemView SpawnPill(OwnedItem o, Side side)
        {
            var g = PillLooks.MakePill(o.data, pillMaterial, pillLength);
            g.transform.SetParent(transform, false);
            g.transform.localRotation = Quaternion.Euler(0f, Random.Range(-25f, 25f), 90f);
            var v = g.AddComponent<TableItemView>();
            v.Init(o.data, o, side, false);
            return v;
        }
    }

    /// <summary>Le pilulier : petite boîte dont le couvercle (enfant « Lid ») bascule à l'ouverture.</summary>
    public class PillBoxView : MonoBehaviour
    {
        public Side side;
        public Fighter Owner { get; set; }
        public bool IsOpen { get; private set; }
        public bool Busy => _pulse > 0f;

        Transform _lid;
        Quaternion _lidClosed;
        float _open, _hover, _pulse;
        bool _hovered;
        Vector3 _basePos;

        void Awake()
        {
            _lid = transform.Find("Lid");
            if (_lid) _lidClosed = _lid.localRotation;
            _basePos = transform.localPosition;
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var s = transform.lossyScale;
            col.center = transform.InverseTransformPoint(b.center);
            col.size = new Vector3(b.size.x / Mathf.Max(1e-4f, s.x), b.size.y / Mathf.Max(1e-4f, s.y), b.size.z / Mathf.Max(1e-4f, s.z)) * 1.4f;
        }

        public void SetHovered(bool on) => _hovered = on;

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            AudioManager.Play(open ? Sfx.CardFlip : Sfx.CardPlace, 0.6f);
        }

        /// <summary>Une pilule en sort : le couvercle s'entrouvre un instant.</summary>
        public void Pulse() => _pulse = 1.2f;

        public string Title => side == Side.Player ? "Ton pilulier" : "Son pilulier";
        public string Subtitle => side == Side.Player ? "PILULES" : "PILULES  ·  À ELLE";

        public string Body
        {
            get
            {
                var sb = new StringBuilder();
                if (Owner == null || Owner.items.Count == 0) sb.AppendLine(side == Side.Player ? "Vide. Les pilules s'achètent en boutique." : "Vide.");
                else foreach (var o in Owner.items) sb.AppendLine($"• {o.data.itemName}");
                if (side == Side.Player)
                    sb.Append(IsOpen ? "\n<color=#D9A441>Clic : refermer. Survole une pilule pour la lire, clique dessus pour l'avaler.</color>"
                                     : "\n<color=#D9A441>Clic : ouvrir.</color>");
                else sb.Append("\n<color=#8E8A86>Elle en avale parfois une au début de ton tour.</color>");
                return sb.ToString().TrimEnd();
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _pulse = Mathf.Max(0f, _pulse - dt);
            float target = IsOpen || _pulse > 0f ? 1f : 0f;
            _open = Mathf.MoveTowards(_open, target, dt * 4f);
            if (_lid) _lid.localRotation = _lidClosed * Quaternion.Euler(105f * Mathf.SmoothStep(0f, 1f, _open), 0f, 0f);
            _hover = Mathf.MoveTowards(_hover, _hovered ? 1f : 0f, dt * 6f);
            transform.localPosition = _basePos + Vector3.up * (_hover * 0.006f);
        }
    }

    /// <summary>Une relique ou une pilule posée sur la table.</summary>
    public class TableItemView : MonoBehaviour
    {
        public ItemData Data { get; private set; }
        public OwnedItem Owned { get; private set; }
        public Side Side { get; private set; }

        Vector3 _basePos, _baseScale;
        Quaternion _baseRot;
        float _hover, _spawn, _swallow = -1f;
        // Juice : la relique fait effet
        float _juice = -1f, _juiceDelay, _flash;
        string _juiceLabel;
        Light _flashLight;
        static BoardView s_board;
        static readonly Color JuiceGold = new Color(1f, 0.72f, 0.32f);
        bool _hovered, _relic;
        // Pilules : position (monde) et visibilité voulues
        Vector3 _pillTarget, _swallowFrom;
        bool _pillShown, _pillPlaced;
        Collider _col;
        // Reliques : voile / reliquaire
        Fighter _owner;
        bool _veiled, _buffed;
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;

        public void Init(ItemData d, OwnedItem o, Side side, bool relic)
        {
            Data = d; Owned = o; Side = side; _relic = relic;
            _basePos = transform.localPosition;
            _baseScale = transform.localScale;
            _baseRot = transform.localRotation;
            _renderers = GetComponentsInChildren<Renderer>();
            // Zone de survol à peine plus grande que l'objet
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in _renderers) b.Encapsulate(r.bounds);
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var lossy = transform.lossyScale;
            col.center = transform.InverseTransformPoint(b.center);
            col.size = new Vector3(b.size.x / Mathf.Max(1e-4f, lossy.x), b.size.y / Mathf.Max(1e-4f, lossy.y), b.size.z / Mathf.Max(1e-4f, lossy.z)) * (relic ? 1.1f : 1.6f);
            _col = col;
            _spawn = 0f;
            transform.localScale = Vector3.zero;
            if (relic) AudioManager.Play(Sfx.CardPlace, 0.6f);
            else col.enabled = false;
        }

        public bool Clickable => Owned != null && Side == Side.Player && _pillShown && !_manual;
        public bool PillShown => _pillShown;
        public Vector3 BaseScale => _baseScale;

        bool _manual;
        /// <summary>Une animation (déglutition) prend la main sur la pilule : plus de survol ni de placement automatique.</summary>
        public void TakeOver()
        {
            _manual = true;
            _hovered = false;
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
        }

        public void SetHovered(bool on) { _hovered = on; }

        public void SetPillTarget(Vector3 world, bool shown)
        {
            _pillTarget = world;
            if (!_pillPlaced) { transform.position = world; _pillPlaced = true; }
            if (shown && !_pillShown) AudioManager.Play(Sfx.CardSlide, 0.3f);
            _pillShown = shown;
            if (_col) _col.enabled = shown;
        }

        public void SetRelicState(Fighter owner, bool veiled, bool buffed)
        {
            _owner = owner;
            if (veiled == _veiled && buffed == _buffed && !buffed && _flash <= 0f) return;
            _veiled = veiled; _buffed = buffed;
            ApplyLook();
        }

        /// <summary>
        /// Teinte de la statuette : assombrie si voilée, éclaircie et dorée quand elle fait effet (flash) ou qu'elle est
        /// magnifiée (lueur qui pulse). Les matériaux PSX n'ont pas d'émission : on éclaircit la couleur de base.
        /// Les siennes sont délabrées : ternies, salies, couvertes de sang (carte de blessures du shader PSX).
        /// </summary>
        void ApplyLook()
        {
            if (_renderers == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            float glow = Mathf.Max(_flash, _buffed ? 0.12f + 0.08f * Mathf.Sin(Time.unscaledTime * 2.5f) : 0f);
            var flashColor = _decay > 0f ? BloodRed : JuiceGold;
            foreach (var r in _renderers)
            {
                if (!r) continue;
                var m = r.sharedMaterial;
                var c = m && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                if (_decay > 0f)
                {
                    // Délabrée : couleurs ternies, assombries, virant au brun
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    var dull = Color.Lerp(c, new Color(lum, lum, lum, c.a), 0.6f * _decay);
                    c = new Color(dull.r * Mathf.Lerp(1f, 0.62f, _decay), dull.g * Mathf.Lerp(1f, 0.55f, _decay), dull.b * Mathf.Lerp(1f, 0.5f, _decay), c.a);
                }
                var col = _veiled ? new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.2f, c.a) : c;
                var lit = col * 1.9f + flashColor * 0.45f;
                col = Color.Lerp(col, new Color(lit.r, lit.g, lit.b, col.a), glow);
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", col);
                _mpb.SetColor("_EmissionColor", flashColor * glow);
                if (_decay > 0f)
                {
                    _mpb.SetTexture("_DamageMap", BloodTex);
                    _mpb.SetFloat("_Damage", _blood);
                    _mpb.SetFloat("_DamageMix", 1f);
                    _mpb.SetFloat("_DamageGlow", 0.35f);
                    _mpb.SetVector("_BaseMap_ST", _bloodST);
                }
                r.SetPropertyBlock(_mpb);
            }
        }

        // ------------------------------------------------------------------ Ses reliques : délabrées, sanglantes

        static readonly Color BloodRed = new Color(0.75f, 0.06f, 0.05f);
        float _decay, _blood;
        Vector4 _bloodST = new Vector4(3f, 10f, 0f, 0f);
        BloodEmitter _weep;
        bool _pooled;

        /// <summary>
        /// Relique de la Dépression : ternie, penchée, couverte de sang (de plus en plus au fil des manches) ; elle pleure
        /// du sang qui goutte sur la table et laisse une flaque à son pied.
        /// </summary>
        public void Defile(float blood)
        {
            _decay = 1f;
            _blood = Mathf.Clamp01(blood);
            _bloodST = new Vector4(Random.Range(2.5f, 4f), Random.Range(8f, 12f), Random.value, Random.value);
            // Penchée, de travers, comme cognée
            _baseRot = _baseRot * Quaternion.Euler(Random.Range(-9f, 9f), 0f, Random.Range(-9f, 9f));
            // Le sang suinte du haut de la statuette et coule sur la table
            var b = StatueBounds();
            var src = new GameObject("Weep").transform;
            src.SetParent(transform, false);
            src.position = new Vector3(b.center.x + b.extents.x * 0.8f * Mathf.Sign(Random.value - 0.5f), b.max.y * 0.6f + b.min.y * 0.4f, b.center.z + b.extents.z * 0.6f * Mathf.Sign(Random.value - 0.5f));
            _weep = gameObject.AddComponent<BloodEmitter>();
            _weep.source = src;
            _weep.jitter = 0.004f;
            _weep.dropSize = 0.85f;
            _weep.rate = WeepRate;
            _lookDirty = true;
        }

        float WeepRate => 0.08f + 0.22f * _blood;

        /// <summary>Une manche de plus entre ses mains : un peu plus de sang, et une coulée fraîche.</summary>
        public void Bleed(float more)
        {
            if (_decay <= 0f) return;
            _blood = Mathf.Clamp01(_blood + more);
            if (_weep) { _weep.rate = WeepRate; _weep.Burst(3); }
            _lookDirty = true;
        }

        Bounds StatueBounds()
        {
            var b = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in _renderers)
            {
                if (!r) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static Texture2D s_bloodTex;
        /// <summary>
        /// Taches de sang pour la carte de blessures du shader : RGB = sang (frais ou séché), A = seuil d'apparition
        /// (plus _Damage monte, plus les taches s'étendent). Coulées verticales + éclaboussures.
        /// </summary>
        static Texture2D BloodTex
        {
            get
            {
                if (s_bloodTex) return s_bloodTex;
                const int N = 128;
                var px = new Color[N * N];
                var rng = new System.Random(4242);
                float R() => (float)rng.NextDouble();
                void Blob(float cx, float cy, float rad, float thr, Color col)
                {
                    int x0 = Mathf.FloorToInt((cx - rad) * N), x1 = Mathf.CeilToInt((cx + rad) * N);
                    int y0 = Mathf.FloorToInt((cy - rad) * N), y1 = Mathf.CeilToInt((cy + rad) * N);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            float dx = (x / (float)N - cx) / rad, dy = (y / (float)N - cy) / rad;
                            float d = dx * dx + dy * dy;
                            if (d > 1f) continue;
                            int i = ((y % N + N) % N) * N + ((x % N + N) % N);
                            // Le centre apparaît en premier, les bords plus tard
                            float a = Mathf.Lerp(thr, thr * 0.55f, d);
                            if (a > px[i].a) px[i] = new Color(col.r, col.g, col.b, a);
                        }
                }
                for (int k = 0; k < 26; k++)
                {
                    float x = R(), y = R(), thr = Mathf.Lerp(0.35f, 1f, R());
                    var col = Color.Lerp(new Color(0.42f, 0.02f, 0.03f), new Color(0.2f, 0.05f, 0.03f), R()); // frais -> séché
                    Blob(x, y, Mathf.Lerp(0.03f, 0.09f, R()), thr, col);
                    // Coulée qui descend depuis la tache
                    int len = Mathf.RoundToInt(Mathf.Lerp(6f, 40f, R()));
                    float w = Mathf.Lerp(0.008f, 0.018f, R());
                    for (int s = 0; s < len; s++)
                        Blob(x + Mathf.Sin(s * 0.3f + k) * 0.004f, y - s / (float)N, w * (1f - s / (float)len * 0.6f), thr * (1f - s / (float)len * 0.35f), col * 0.9f);
                }
                for (int k = 0; k < 60; k++) Blob(R(), R(), Mathf.Lerp(0.006f, 0.016f, R()), Mathf.Lerp(0.3f, 0.9f, R()), new Color(0.35f, 0.03f, 0.03f));
                s_bloodTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "RelicBlood" };
                s_bloodTex.SetPixels(px);
                s_bloodTex.Apply();
                return s_bloodTex;
            }
        }

        /// <summary>
        /// La relique fait effet : elle sursaute (rebond, petite rotation), s'illumine d'un éclat doré, soulève un peu de
        /// poussière, tinte, et son effet s'affiche au-dessus d'elle. Plusieurs déclenchements rapprochés se fondent en un.
        /// </summary>
        public void Juice(string label, float delay = 0f)
        {
            if (!_relic) return;
            // Déjà en train de sursauter : l'effet suivant passera juste après (au plus deux en attente)
            if (_juice >= 0f)
            {
                if (_juiceQueue.Count < 2 && !_juiceQueue.Contains(label)) _juiceQueue.Enqueue(label);
                return;
            }
            _juice = 0f;
            _juiceDelay = delay;
            _juiceLabel = label;
        }

        readonly Queue<string> _juiceQueue = new Queue<string>();

        void StartJuiceFx()
        {
            var pos = transform.position;
            bool hers = _decay > 0f;
            AudioManager.Play(Sfx.Relic, Side == Side.Player ? 0.75f : 0.6f);
            if (!s_board) s_board = FindAnyObjectByType<BoardView>();
            if (s_board && s_board.tableDust)
            {
                var ep = new ParticleSystem.EmitParams { position = pos + Vector3.up * 0.01f, applyShapeToPosition = true };
                s_board.tableDust.Emit(ep, hers ? 8 : 14);
            }
            if (hers && BloodSystem.Instance)
            {
                // Les siennes crachent du sang quand elles agissent : éclaboussures sur la table, coulée fraîche
                var top = StatueBounds();
                BloodSystem.Instance.Splatter(new Vector3(top.center.x, top.max.y, top.center.z), 9, 0.55f);
                if (_weep) _weep.Burst(4);
                AudioManager.Play(Sfx.BloodDrop, 0.5f);
            }
            if (!string.IsNullOrEmpty(_juiceLabel) && DamagePopups.Instance)
                DamagePopups.Instance.SpawnWorld(pos + Vector3.up * 0.13f, _juiceLabel, hers ? Palette.BloodLight : Palette.Hope, Side == Side.Player ? 0.62f : 0.55f);
            if (!_flashLight)
            {
                var lg = new GameObject("RelicFlash");
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = Vector3.up * 0.06f;
                _flashLight = lg.AddComponent<Light>();
                _flashLight.type = LightType.Point;
                _flashLight.color = JuiceGold;
                _flashLight.range = 0.28f;
                _flashLight.shadows = LightShadows.None;
            }
            _flashLight.color = hers ? BloodRed : JuiceGold;
            _flashLight.enabled = true;
            _flashLight.transform.position = pos + Vector3.up * 0.06f;
        }

        /// <summary>Animation du juice (p : 0 → 1). Renvoie (rebond d'échelle, saut, rotation).</summary>
        (float scale, float hop, float wobble) JuiceCurve(float p)
        {
            float s = 1f + 0.45f * Mathf.Exp(-5.5f * p) * Mathf.Sin(p * 19f);
            float hop = 0.035f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(p * 1.7f));
            float wob = 28f * Mathf.Exp(-4.5f * p) * Mathf.Sin(p * 23f);
            return (s, hop, wob);
        }

        public string Subtitle => ItemData.KindLabel(Data.kind).ToUpper() + (Side == Side.Player ? "" : "  ·  À ELLE");

        public string Body
        {
            get
            {
                var sb = new StringBuilder(Data.Describe());
                if (_veiled) sb.Append("\n<color=#8E8A86>Voilée : sans effet jusqu'à la fin de la manche.</color>");
                if (_buffed) sb.Append("\n<color=#D9A441>Magnifiée par le Reliquaire : effet doublé.</color>");
                if (Data.effect == ItemEffect.Reliquaire && _owner != null)
                    sb.Append(_owner.buffTarget ? $"\n<color=#D9A441>Magnifie : « {_owner.buffTarget.itemName} ».</color>" : "\n<color=#8E8A86>Ne magnifie rien pour l'instant.</color>");
                if (Clickable)
                {
                    var gm = GameManager.Instance;
                    string why = gm ? gm.ItemBlockReason(Owned) : null;
                    sb.Append(why == null ? "\n\n<color=#D9A441>Clic : l'avaler.</color>" : $"\n\n<color=#8E8A86>{why}</color>");
                }
                sb.Append($"\n\n<i><color=#{Palette.HexOf(Palette.Ash)}>{Data.flavorText}</color></i>");
                return sb.ToString();
            }
        }

        /// <summary>Avalée : la pilule se soulève (au-dessus du pilulier si elle y dormait), rétrécit et disparaît.</summary>
        public void Swallow(Vector3 popFrom)
        {
            if (_swallow >= 0f) return;
            _swallow = 0f;
            _swallowFrom = _pillShown ? transform.position : popFrom;
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
        }

        void Update()
        {
            if (_manual) return;
            float dt = Time.unscaledDeltaTime;
            if (_swallow >= 0f)
            {
                _swallow += dt * 1.6f;
                transform.position = _swallowFrom + Vector3.up * (0.14f * Mathf.SmoothStep(0f, 1f, _swallow));
                float s = _swallow < 0.3f ? Mathf.SmoothStep(0f, 1f, _swallow / 0.3f) : Mathf.Max(0f, 1f - (_swallow - 0.3f) / 0.7f);
                transform.localScale = _baseScale * s;
                transform.Rotate(0f, 400f * dt, 0f, Space.World);
                if (_swallow >= 1f) Destroy(gameObject);
                return;
            }
            _hover = Mathf.MoveTowards(_hover, _hovered ? 1f : 0f, dt * 6f);
            if (!_relic)
            {
                // Pilule : glisse hors du pilulier ou y retourne
                transform.position = Vector3.Lerp(transform.position, _pillTarget + Vector3.up * (_hover * 0.01f), 1f - Mathf.Exp(-dt * 12f));
                transform.localScale = Vector3.Lerp(transform.localScale, _pillShown ? _baseScale * (1f + _hover * 0.25f) : Vector3.zero, 1f - Mathf.Exp(-dt * 14f));
                return;
            }
            // Apparition : la statuette tombe à sa place avec un petit rebond
            float spawnScale = 1f;
            if (_spawn < 1f)
            {
                _spawn = Mathf.Min(1f, _spawn + dt * 2.5f);
                float bounce = 1f + Mathf.Sin(_spawn * Mathf.PI) * 0.15f;
                spawnScale = Mathf.SmoothStep(0f, 1f, _spawn) * bounce;
            }
            else if (_decay > 0f && !_pooled && BloodSystem.Instance)
            {
                // Posée : une flaque s'étale à son pied (elle reste toute la partie)
                _pooled = true;
                var b = StatueBounds();
                BloodSystem.Instance.Pool(new Vector3(b.center.x, b.min.y, b.center.z), Mathf.Max(b.extents.x, b.extents.z) * 0.9f + 0.02f + 0.02f * _blood, 2.5f, true);
                BloodSystem.Instance.Splatter(new Vector3(b.center.x, b.max.y, b.center.z), 6, 0.4f);
            }

            // Juice : la relique fait effet
            float jScale = 1f, jHop = 0f, jWob = 0f;
            if (_juice >= 0f)
            {
                if (_juiceDelay > 0f) _juiceDelay -= dt;
                else
                {
                    if (_juice == 0f) StartJuiceFx();
                    _juice = Mathf.Min(1f, _juice + dt / 0.75f);
                    (jScale, jHop, jWob) = JuiceCurve(_juice);
                    _flash = Mathf.Pow(1f - _juice, 1.5f);
                    if (_flashLight) _flashLight.intensity = 0.9f * _flash * _flash;
                    if (_juice >= 1f)
                    {
                        _juice = -1f; _flash = 0f;
                        if (_flashLight) _flashLight.enabled = false;
                        if (_juiceQueue.Count > 0) Juice(_juiceQueue.Dequeue(), 0.05f);
                    }
                }
            }
            // Lueur qui pulse (magnifiée) ou éclat (effet) ; une dernière passe remet la teinte normale ensuite
            if (_buffed || _flash > 0f) { ApplyLook(); _lookDirty = true; }
            else if (_lookDirty) { ApplyLook(); _lookDirty = false; }

            transform.localScale = _baseScale * spawnScale * jScale;
            transform.localRotation = _baseRot * Quaternion.Euler(0f, jWob, jWob * 0.25f);
            float lift = _hover * (0.012f + Mathf.Sin(Time.unscaledTime * 4f) * 0.002f);
            transform.localPosition = _basePos + Vector3.up * (lift + jHop + (1f - _spawn) * 0.15f);
        }

        bool _lookDirty;
    }
}
