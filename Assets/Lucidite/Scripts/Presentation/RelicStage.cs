using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Petit studio hors champ (loin sous la pièce) qui filme les objets de la boutique : statuettes et gélules.
    /// Chaque emplacement a sa caméra et sa texture ; l'objet est recadré automatiquement et tourne lentement,
    /// éclairé par deux lampes.
    /// </summary>
    public class RelicStage : MonoBehaviour
    {
        public static RelicStage Instance { get; private set; }
        public const int Layer = 31;
        public int slots = 14;
        public int resolution = 192;
        public float turnSpeed = 35f;
        [Tooltip("Matériau des gélules (PSXLit).")]
        public Material pillMaterial;
        [Tooltip("Hauteur visée de l'objet recadré (le champ de la caméra fait environ 0,145).")]
        public float frameHeight = 0.105f;

        Transform[] _pivots;
        Camera[] _cams;
        RenderTexture[] _rts;
        GameObject[] _models;
        bool _active;

        void Awake()
        {
            Instance = this;
            transform.position = new Vector3(0f, -80f, 0f);
            _pivots = new Transform[slots];
            _cams = new Camera[slots];
            _rts = new RenderTexture[slots];
            _models = new GameObject[slots];
            for (int i = 0; i < slots; i++)
            {
                var root = new GameObject("Slot" + i).transform;
                root.SetParent(transform, false);
                root.localPosition = new Vector3(i * 4f, 0f, 0f);
                var pivot = new GameObject("Pivot").transform;
                pivot.SetParent(root, false);
                _pivots[i] = pivot;

                _rts[i] = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32) { name = "RelicStage" + i, filterMode = FilterMode.Point };
                var cg = new GameObject("Camera");
                cg.transform.SetParent(root, false);
                cg.transform.localPosition = new Vector3(0f, 0.075f, -0.27f);
                cg.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
                var cam = cg.AddComponent<Camera>();
                cam.cullingMask = 1 << Layer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 3f;
                cam.targetTexture = _rts[i];
                cam.enabled = false;
                _cams[i] = cam;

                Light(root, "Key", new Vector3(-0.18f, 0.25f, -0.2f), new Color(1f, 0.82f, 0.62f), 2.2f);
                Light(root, "Rim", new Vector3(0.2f, 0.18f, 0.22f), new Color(0.75f, 0.25f, 0.22f), 1.6f);
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; foreach (var rt in _rts) if (rt) rt.Release(); }

        static void Light(Transform parent, string name, Vector3 pos, Color c, float intensity)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 0.8f;
            l.color = c;
            l.intensity = intensity;
            l.cullingMask = 1 << Layer;
            l.shadows = LightShadows.None;
        }

        /// <summary>Filme une statuette dans l'emplacement i : renvoie la texture à afficher.</summary>
        public Texture Show(int i, GameObject model) => Place(i, model ? Instantiate(model) : null);

        /// <summary>Filme la gélule d'une pilule (avec son aspect de la partie), un peu inclinée.</summary>
        public Texture ShowPill(int i, ItemData d)
        {
            if (!d) return Place(i, null);
            var holder = new GameObject("Pill");
            var pill = PillLooks.MakePill(d, pillMaterial, 0.05f);
            pill.transform.SetParent(holder.transform, false);
            pill.transform.localRotation = Quaternion.Euler(0f, 0f, 62f);
            return Place(i, holder);
        }

        Texture Place(int i, GameObject g)
        {
            if (i < 0 || i >= slots) { if (g) Destroy(g); return null; }
            if (_models[i]) Destroy(_models[i]);
            _models[i] = null;
            if (g)
            {
                _pivots[i].localRotation = Quaternion.identity;
                g.transform.SetParent(_pivots[i], false);
                g.transform.localPosition = Vector3.zero;
                SetLayer(g.transform, Layer);
                // Recadrage : l'objet tient dans le champ, posé au centre
                var b = new Bounds(g.transform.position, Vector3.zero);
                bool any = false;
                foreach (var r in g.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (any)
                {
                    float size = Mathf.Max(b.size.y, Mathf.Max(b.size.x, b.size.z) * 0.85f);
                    float k = size > 1e-4f ? frameHeight / size : 1f;
                    g.transform.localScale *= k;
                    var center = (b.center - _pivots[i].position) * k;
                    g.transform.localPosition = new Vector3(-center.x, 0.075f - center.y, -center.z);
                }
                _models[i] = g;
            }
            _cams[i].enabled = _active && g;
            return _rts[i];
        }

        public void SetActive(bool on)
        {
            _active = on;
            for (int i = 0; i < slots; i++) _cams[i].enabled = on && _models[i];
        }

        public void Clear()
        {
            for (int i = 0; i < slots; i++) { if (_models[i]) Destroy(_models[i]); _models[i] = null; _cams[i].enabled = false; }
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }

        void Update()
        {
            if (!_active) return;
            for (int i = 0; i < slots; i++) if (_pivots[i]) _pivots[i].Rotate(0f, turnSpeed * Time.unscaledDeltaTime, 0f, Space.Self);
        }
    }
}
