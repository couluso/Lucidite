using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Pile de cartes posée sur la table (pioche ou défausse de la joueuse). Son épaisseur suit le nombre de cartes ;
    /// la défausse montre la dernière carte défaussée face visible (avec son sang). Survol : contour orange,
    /// clic : ouvre la liste des cartes de la pile (CardPicker).
    /// </summary>
    public class PileView : MonoBehaviour
    {
        public DeckPanel.Pile pile = DeckPanel.Pile.Draw;
        public Material backMaterial;
        public Material frontMaterial;
        public Material highlightMaterial;
        public CardArtLibrary artLibrary;
        [Tooltip("Taille d'une carte posée sur la table (même échelle que BoardView.tableCardScale).")]
        public float cardScale = 0.065f;
        [Tooltip("Épaisseur d'une couche de la pile.")]
        public float layerThickness = 0.0012f;
        [Tooltip("Une couche visible pour ce nombre de cartes.")]
        public int cardsPerLayer = 2;
        public int maxLayers = 14;
        [Tooltip("Désordre de la pile : 0 = cartes bien alignées, 1 = tas mal rangé (rotations, décalages, cartes qui dépassent).")]
        [Range(0f, 1f)] public float messiness = 1f;
        public Color hoverColor = new Color(0.88f, 0.45f, 0.12f);
        public float hoverGlow = 2.2f;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int DamageId = Shader.PropertyToID("_Damage");
        static readonly int DamageMapId = Shader.PropertyToID("_DamageMap");

        readonly List<Transform> _layers = new List<Transform>();
        Transform _top;
        MeshRenderer _topRenderer;
        Transform _outline;
        MeshRenderer _outlineRenderer;
        BoxCollider _collider;
        MaterialPropertyBlock _mpb;
        int _count = -1;
        CardInstance _topCard;
        float _topBlood = -1f;
        bool _hovered;

        float W => cardScale;
        float H => cardScale * 1.5f;

        void Awake()
        {
            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.isTrigger = true;
            for (int i = 0; i < maxLayers; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = "Layer" + i;
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(transform, false);
                // Tas mal rangé : chaque carte tournée et décalée, quelques-unes dépassent franchement, légère inclinaison
                bool stray = Random.value < 0.25f * messiness;
                float off = (0.002f + 0.006f * messiness) * (stray ? 2.2f : 1f);
                float yaw = (3f + 11f * messiness) * (stray ? 2f : 1f);
                g.transform.localPosition = new Vector3(Random.Range(-off, off), (i + 0.5f) * layerThickness, Random.Range(-off, off));
                g.transform.localRotation = Quaternion.Euler(Random.Range(-1.2f, 1.2f) * messiness, Random.Range(-yaw, yaw), Random.Range(-1.2f, 1.2f) * messiness);
                g.transform.localScale = new Vector3(W, layerThickness * 0.9f, H);
                var r = g.GetComponent<MeshRenderer>();
                r.sharedMaterial = backMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.SetActive(false);
                _layers.Add(g.transform);
            }
            // Dernière carte défaussée, face visible
            var top = GameObject.CreatePrimitive(PrimitiveType.Quad);
            top.name = "Top";
            Destroy(top.GetComponent<Collider>());
            top.transform.SetParent(transform, false);
            top.transform.localRotation = Quaternion.Euler(90f, Random.Range(-6f, 6f) * (0.5f + messiness), 0f);
            top.transform.localScale = new Vector3(W, H, 1f);
            _top = top.transform;
            _topRenderer = top.GetComponent<MeshRenderer>();
            _topRenderer.sharedMaterial = frontMaterial;
            _topRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            top.SetActive(false);
            // Contour de survol : halo légèrement plus grand, juste sous la carte du dessus
            var hl = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hl.name = "Outline";
            Destroy(hl.GetComponent<Collider>());
            hl.transform.SetParent(transform, false);
            hl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hl.transform.localScale = new Vector3(W * 1.15f, H * 1.1f, 1f);
            _outline = hl.transform;
            _outlineRenderer = hl.GetComponent<MeshRenderer>();
            _outlineRenderer.sharedMaterial = highlightMaterial;
            _outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hl.SetActive(false);
            _mpb = new MaterialPropertyBlock();
            Rebuild(0, null);
        }

        public void SetHovered(bool on)
        {
            _hovered = on;
            _outline.gameObject.SetActive(on);
            if (!on) return;
            _outlineRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, hoverColor * Mathf.Max(1f, hoverGlow * 0.5f));
            _mpb.SetColor(EmissionColorId, hoverColor * hoverGlow);
            _outlineRenderer.SetPropertyBlock(_mpb);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var deck = gm != null && gm.Player != null ? gm.Player.deck : null;
            int count = 0;
            CardInstance top = null;
            if (deck != null)
            {
                if (pile == DeckPanel.Pile.Discard)
                {
                    count = deck.DiscardCount;
                    if (count > 0) top = deck.DiscardPile[count - 1];
                }
                else count = deck.DrawCount;
            }
            if (count != _count || top != _topCard || (top != null && !Mathf.Approximately(top.blood, _topBlood)))
                Rebuild(count, top);
        }

        void Rebuild(int count, CardInstance top)
        {
            _count = count;
            _topCard = top;
            _topBlood = top != null ? top.blood : -1f;
            int layers = count <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(count / (float)Mathf.Max(1, cardsPerLayer)), 1, maxLayers);
            for (int i = 0; i < _layers.Count; i++) _layers[i].gameObject.SetActive(i < layers);
            float height = layers * layerThickness;

            bool showTop = top != null && frontMaterial;
            _top.gameObject.SetActive(showTop);
            if (showTop)
            {
                _top.localPosition = new Vector3(0f, height + 0.0004f, 0f);
                var face = top.Face ? top.Face : (artLibrary ? artLibrary.Face(top.data.type) : null);
                _topRenderer.GetPropertyBlock(_mpb);
                if (face) { _mpb.SetTexture(BaseMapId, face); _mpb.SetTexture(EmissionMapId, face); }
                _mpb.SetFloat(DamageId, top.blood);
                var map = artLibrary ? artLibrary.CardBlood(top.uid) : null;
                if (map) _mpb.SetTexture(DamageMapId, map);
                _topRenderer.SetPropertyBlock(_mpb);
            }
            // Le halo dépasse autour de la carte du dessus (pile vide : il dessine l'emplacement)
            _outline.localPosition = new Vector3(0f, Mathf.Max(0.0006f, height - layerThickness * 0.3f), 0f);
            _collider.center = new Vector3(0f, (height + 0.015f) * 0.5f, 0f);
            _collider.size = new Vector3(W * 1.15f, height + 0.015f, H * 1.1f);
            if (_hovered) SetHovered(true);
        }
    }
}
