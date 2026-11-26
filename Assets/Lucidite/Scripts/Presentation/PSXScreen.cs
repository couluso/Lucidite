using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    /// <summary>
    /// Rendu PS1/VHS : la caméra du monde rend dans une RenderTexture basse résolution (filtrage "point"),
    /// affichée plein écran à travers le shader VHS (scanlines, bruit, aberration chromatique, dithering).
    /// </summary>
    public class PSXScreen : MonoBehaviour
    {
        public static PSXScreen Instance { get; private set; }

        public Camera worldCamera;
        public RawImage output;
        public Material vhsMaterialTemplate;

        [Header("Réglages")]
        [Range(180, 720)] public int verticalResolution = 480;
        [Range(0f, 1f)] public float vhsIntensity = 0.55f;
        public float glitchDecay = 1.6f;
        public float flashDecay = 1.2f;

        Material _mat;
        RenderTexture _rt;
        float _glitch;
        float _flash;
        Color _flashColor = Color.red;
        float _hope;

        static readonly int GlitchId = Shader.PropertyToID("_Glitch");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int ResId = Shader.PropertyToID("_Resolution");

        public static int SavedResolution
        {
            get => PlayerPrefs.GetInt("psx_res2", 480);
            set => PlayerPrefs.SetInt("psx_res2", value);
        }
        public static float SavedIntensity
        {
            get => PlayerPrefs.GetFloat("vhs_int2", 0.55f);
            set => PlayerPrefs.SetFloat("vhs_int2", value);
        }

        void Awake()
        {
            Instance = this;
            verticalResolution = SavedResolution;
            vhsIntensity = SavedIntensity;
            if (vhsMaterialTemplate) _mat = new Material(vhsMaterialTemplate);
            if (output)
            {
                output.material = _mat;
                output.raycastTarget = false;
            }
            Rebuild();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (worldCamera) worldCamera.targetTexture = null;
            if (_rt) { _rt.Release(); Destroy(_rt); }
            if (_mat) Destroy(_mat);
        }

        public void SetResolution(int h)
        {
            verticalResolution = h;
            SavedResolution = h;
            Rebuild();
        }

        public void SetIntensity(float v)
        {
            vhsIntensity = v;
            SavedIntensity = v;
        }

        void Rebuild()
        {
            if (!worldCamera) return;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            int h = Mathf.Max(120, verticalResolution);
            int w = Mathf.Max(160, Mathf.RoundToInt(h * aspect));
            if (_rt && _rt.width == w && _rt.height == h) return;

            worldCamera.targetTexture = null;
            if (_rt) { _rt.Release(); Destroy(_rt); }
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                antiAliasing = 1,
                name = "PSX_RT"
            };
            _rt.Create();
            worldCamera.targetTexture = _rt;
            if (output) output.texture = _rt;
            if (_mat) _mat.SetVector(ResId, new Vector4(w, h, 0, 0));
        }

        void Update()
        {
            Rebuild();
            _glitch = Mathf.Max(0f, _glitch - glitchDecay * Time.unscaledDeltaTime);
            _flash = Mathf.Max(0f, _flash - flashDecay * Time.unscaledDeltaTime);
            Shader.SetGlobalFloat("_PSXSnapRes", verticalResolution * 0.45f);
            if (_mat)
            {
                _mat.SetFloat(GlitchId, _glitch);
                _mat.SetFloat(FlashId, _flash);
                _mat.SetColor(FlashColorId, _flashColor);
                _mat.SetFloat(IntensityId, vhsIntensity);
            }
        }

        public void AddGlitch(float amount) => _glitch = Mathf.Clamp(_glitch + amount, 0f, 1.5f);

        public void Flash(Color c, float amount)
        {
            _flashColor = c;
            _flash = Mathf.Clamp01(Mathf.Max(_flash, amount));
        }

        /// <summary>Convertit une position écran (souris) en rayon dans le monde.</summary>
        public Ray ScreenPointToRay(Vector2 screenPos)
        {
            var vp = new Vector3(screenPos.x / Mathf.Max(1, Screen.width), screenPos.y / Mathf.Max(1, Screen.height), 0f);
            return worldCamera.ViewportPointToRay(vp);
        }
    }
}
