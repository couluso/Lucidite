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

        void Start()
        {
            _playerBox = SpawnBox(playerPillBoxPoint, Side.Player);
            _enemyBox = SpawnBox(enemyPillBoxPoint, Side.Opponent);
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
            if (_playerBox)
            {
                _playerBox.Owner = gm.Player;
                // Le pilulier se referme en dehors de ton tour
                if (_playerBox.IsOpen && gm.Phase != GamePhase.PlayerTurn) _playerBox.SetOpen(false);
            }
            if (_enemyBox)
            {
                _enemyBox.Owner = gm.Enemy;
                bool show = gm.Enemy.items.Count > 0 || _enemyBox.Busy;
                if (_enemyBox.gameObject.activeSelf != show) _enemyBox.gameObject.SetActive(show);
            }
        }

        void SyncRelics(Fighter f, Dictionary<ItemData, TableItemView> views, Transform[] slots, Side side)
        {
            if (slots == null) return;
            var owned = f.relics;
            foreach (var r in new List<ItemData>(views.Keys))
                if (!owned.Contains(r)) { if (views[r]) Destroy(views[r].gameObject); views.Remove(r); }
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
        float _hover, _spawn, _swallow = -1f;
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

        public bool Clickable => Owned != null && Side == Side.Player && _pillShown;

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
            if (veiled == _veiled && buffed == _buffed && !buffed) return;
            _veiled = veiled; _buffed = buffed;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            float glow = buffed ? 0.12f + 0.08f * Mathf.Sin(Time.unscaledTime * 2.5f) : 0f;
            foreach (var r in _renderers)
            {
                if (!r) continue;
                var m = r.sharedMaterial;
                var c = m && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", veiled ? new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.2f, c.a) : c);
                _mpb.SetColor("_EmissionColor", new Color(0.85f, 0.6f, 0.25f) * glow);
                r.SetPropertyBlock(_mpb);
            }
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
            if (_spawn < 1f)
            {
                _spawn = Mathf.Min(1f, _spawn + dt * 2.5f);
                float bounce = 1f + Mathf.Sin(_spawn * Mathf.PI) * 0.15f;
                transform.localScale = _baseScale * Mathf.SmoothStep(0f, 1f, _spawn) * bounce;
            }
            if (_buffed) SetRelicState(_owner, _veiled, true); // lueur qui pulse
            float lift = _hover * (0.012f + Mathf.Sin(Time.unscaledTime * 4f) * 0.002f);
            transform.localPosition = _basePos + Vector3.up * (lift + (1f - _spawn) * 0.15f);
        }
    }
}
