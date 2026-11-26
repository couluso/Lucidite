using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Lucidite.EditorTools.Lib;

namespace Lucidite.EditorTools
{
    /// <summary>
    /// Interface thématique : pas de fenêtres, du texte qui flotte dans le noir, menus "à l'encre" qui s'enflamment
    /// au survol, page de carnet pour les règles, affichage de magnétoscope. Polices : IM Fell (titres),
    /// Special Elite (texte), VT323 (chiffres / VHS).
    /// </summary>
    public static class UIFactory
    {
        static string P(string name) => $"{AssetFactory.UIPrefabDir}/{name}.prefab";

        // ================================================================== Helpers
        static GameObject UIObj(string name, Transform parent)
        {
            var g = new GameObject(name, typeof(RectTransform));
            g.layer = 5;
            if (parent) g.transform.SetParent(parent, false);
            return g;
        }

        static RectTransform Place(Component c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) => Place(c.gameObject, anchor, pivot, pos, size);
        static RectTransform Place(GameObject g, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)g.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(GameObject g, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            var rt = (RectTransform)g.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        static Image Img(Transform parent, string name, Color c, Sprite sprite = null, bool raycast = false)
        {
            var g = UIObj(name, parent);
            var i = g.AddComponent<Image>();
            i.sprite = sprite;
            i.color = c;
            i.raycastTarget = raycast;
            return i;
        }

        static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color c, TMP_FontAsset font,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool autoSize = false)
        {
            var g = UIObj(name, parent);
            var t = g.AddComponent<TextMeshProUGUI>();
            t.font = font ? font : TMP_Settings.defaultFontAsset;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            if (autoSize) { t.enableAutoSizing = true; t.fontSizeMin = size * 0.45f; t.fontSizeMax = size; }
            return t;
        }

        static TextMeshProUGUI Title(Transform p, string n, string text, float size, Color c, TextAlignmentOptions a = TextAlignmentOptions.Center) => Txt(p, n, text, size, c, FontTitle, a);
        static TextMeshProUGUI Body(Transform p, string n, string text, float size, Color c, TextAlignmentOptions a = TextAlignmentOptions.Center, bool auto = false) => Txt(p, n, text, size, c, FontBody, a, auto);
        static TextMeshProUGUI Mono(Transform p, string n, string text, float size, Color c, TextAlignmentOptions a = TextAlignmentOptions.Center) => Txt(p, n, text, size, c, FontMono, a);

        /// <summary>Entrée de menu en texte seul : s'enflamme au survol, trait de pinceau rouge derrière.</summary>
        static Button MenuItem(Transform parent, string name, string label, float fontSize, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Left, TMP_FontAsset font = null)
        {
            var root = UIObj(name, parent);
            ((RectTransform)root.transform).sizeDelta = size;
            var hit = root.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var marker = Img(root.transform, "Marker", Palette.Blood.WithAlpha(0.55f), SpriteScratch);
            var mrt = Stretch(marker.gameObject, -10, size.x * 0.25f, size.y * 0.25f, size.y * 0.05f);
            if (align == TextAlignmentOptions.Center) { mrt.offsetMin = new Vector2(size.x * 0.1f, size.y * 0.05f); mrt.offsetMax = new Vector2(-size.x * 0.1f, -size.y * 0.25f); }
            var t = Txt(root.transform, "Label", label, fontSize, Palette.Bone, font ? font : FontBody, align);
            Stretch(t.gameObject);
            var b = root.AddComponent<Button>();
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            var fx = root.AddComponent<MenuItemFX>();
            fx.label = t;
            fx.marker = marker;
            fx.normalColor = Palette.Bone;
            fx.hoverColor = Palette.BloodLight;
            return b;
        }

        static T Panel<T>(Transform parent, string name) where T : Component
        {
            var g = UIObj(name, parent);
            Stretch(g);
            g.AddComponent<CanvasGroup>();
            return g.AddComponent<T>();
        }

        static Image Shade(Transform parent, float alpha, bool raycast = true)
        {
            var bg = Img(parent, "Shade", Palette.Void.WithAlpha(alpha), null, raycast);
            Stretch(bg.gameObject);
            var vig = Img(parent, "Vignette", Color.white, SpriteVignette);
            Stretch(vig.gameObject, -200, -200, -200, -200);
            return bg;
        }

        static VerticalLayoutGroup VList(GameObject g, float spacing, TextAnchor align)
        {
            var l = g.AddComponent<VerticalLayoutGroup>();
            l.spacing = spacing; l.childAlignment = align;
            l.childControlWidth = false; l.childControlHeight = false;
            l.childForceExpandWidth = false; l.childForceExpandHeight = false;
            return l;
        }

        static HorizontalLayoutGroup HList(GameObject g, float spacing, TextAnchor align)
        {
            var l = g.AddComponent<HorizontalLayoutGroup>();
            l.spacing = spacing; l.childAlignment = align;
            l.childControlWidth = false; l.childControlHeight = false;
            l.childForceExpandWidth = false; l.childForceExpandHeight = false;
            return l;
        }

        static Slider MakeSlider(Transform parent, string name, float width)
        {
            var root = UIObj(name, parent);
            ((RectTransform)root.transform).sizeDelta = new Vector2(width, 30);
            var line = Img(root.transform, "Line", Palette.AshDark, null, true);
            Stretch(line.gameObject, 0, 0, 13, 13);
            var fillArea = UIObj("Fill Area", root.transform);
            Stretch(fillArea, 0, 0, 12, 12);
            var fill = Img(fillArea.transform, "Fill", Palette.Blood);
            Stretch(fill.gameObject);
            var handleArea = UIObj("Handle Slide Area", root.transform);
            Stretch(handleArea, 6, 6, 0, 0);
            var handle = Img(handleArea.transform, "Handle", Palette.Bone, null, true);
            ((RectTransform)handle.transform).sizeDelta = new Vector2(6, 0);
            var s = root.AddComponent<Slider>();
            s.fillRect = (RectTransform)fill.transform;
            s.handleRect = (RectTransform)handle.transform;
            s.targetGraphic = handle;
            var cb = s.colors; cb.highlightedColor = Palette.BloodLight; cb.pressedColor = Palette.Hope; s.colors = cb;
            s.minValue = 0; s.maxValue = 1; s.value = 0.8f;
            return s;
        }

        static (ScrollRect, RectTransform) ScrollGrid(Transform parent, string name, Vector2 cell, Vector2 spacing)
        {
            var root = UIObj(name, parent);
            var sr = root.AddComponent<ScrollRect>();
            var viewport = UIObj("Viewport", root.transform);
            Stretch(viewport);
            viewport.AddComponent<RectMask2D>();
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.01f);
            var content = UIObj("Content", viewport.transform);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = Vector2.zero;
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell; grid.spacing = spacing;
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.childAlignment = TextAnchor.UpperCenter;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = (RectTransform)viewport.transform;
            sr.content = crt;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            return (sr, crt);
        }

        // ================================================================== Cartes / objets UI
        public static CardUIView CardUI(string name, float s)
        {
            var root = UIObj(name, null);
            ((RectTransform)root.transform).sizeDelta = new Vector2(220, 330) * s;
            var view = root.AddComponent<CardUIView>();
            var frame = root.AddComponent<Image>();
            frame.sprite = SpriteCardFrame;
            frame.color = Color.white;
            view.frame = frame;
            var btn = root.AddComponent<Button>();
            btn.targetGraphic = frame;
            var cb = btn.colors; cb.highlightedColor = new Color(1f, 0.92f, 0.85f); cb.pressedColor = Palette.BloodLight; cb.disabledColor = new Color(0.45f, 0.42f, 0.4f); btn.colors = cb;
            view.button = btn;

            var top = new Vector2(0.5f, 1f);
            var dark = Img(root.transform, "Ink", Palette.Hex("120E0E").WithAlpha(0.86f));
            Stretch(dark.gameObject, 14 * s, 14 * s, 14 * s, 14 * s);
            view.typeBand = Img(root.transform, "TypeBand", Palette.TypeAttack);
            Place(view.typeBand, top, top, new Vector2(0, -14 * s), new Vector2(192 * s, 5 * s));
            view.nameText = Txt(root.transform, "Name", "Nom", 22 * s, Palette.Bone, FontTitle, TextAlignmentOptions.Center, true);
            Place(view.nameText, top, top, new Vector2(10 * s, -20 * s), new Vector2(160 * s, 40 * s));
            var artGo = UIObj("Art", root.transform);
            Place(artGo, top, top, new Vector2(0, -62 * s), new Vector2(150 * s, 112 * s));
            view.art = artGo.AddComponent<RawImage>();
            view.art.raycastTarget = false;
            view.art.uvRect = new Rect(0, 0.12f, 1, 0.76f);
            view.typeText = Txt(root.transform, "Type", "TYPE", 15 * s, Palette.Ash, FontMono, TextAlignmentOptions.Center);
            Place(view.typeText, top, top, new Vector2(0, -176 * s), new Vector2(190 * s, 20 * s));
            view.rulesText = Txt(root.transform, "Rules", "", 15 * s, Palette.Bone, FontBody, TextAlignmentOptions.Top, true);
            Place(view.rulesText, top, top, new Vector2(0, -198 * s), new Vector2(180 * s, 92 * s));
            view.footerText = Txt(root.transform, "Footer", "", 24 * s, Palette.BloodLight, FontMono, TextAlignmentOptions.Center);
            Place(view.footerText, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -4 * s), new Vector2(200 * s, 30 * s));

            var orb = Img(root.transform, "CostOrb", Palette.BloodDark, SpriteOrb);
            Place(orb, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(26 * s, -28 * s), new Vector2(44 * s, 44 * s));
            view.costRoot = orb.gameObject;
            view.costText = Txt(orb.transform, "Cost", "1", 28 * s, Palette.Bone, FontTitle);
            Stretch(view.costText.gameObject);

            var sel = Img(root.transform, "Selected", Palette.Hope.WithAlpha(0.15f));
            Stretch(sel.gameObject);
            view.selectedMark = sel.gameObject;
            sel.gameObject.SetActive(false);
            view.artLibrary = ArtLibrary;
            return SavePrefab(root, P(name)).GetComponent<CardUIView>();
        }

        public static ItemSlotUI ItemUI(string name, bool compact)
        {
            var root = UIObj(name, null);
            var size = compact ? new Vector2(72, 72) : new Vector2(300, 96);
            ((RectTransform)root.transform).sizeDelta = size;
            var hit = root.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, compact ? 0.01f : 0.35f);
            var ui = root.AddComponent<ItemSlotUI>();
            var btn = root.AddComponent<Button>();
            btn.targetGraphic = hit;
            var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1, 0.8f, 0.8f); cb.disabledColor = new Color(1, 1, 1, 0.35f); btn.colors = cb;
            ui.button = btn;

            ui.icon = Img(root.transform, "Icon", Palette.Blood, SpriteOrb);
            ui.icon.preserveAspect = true;
            if (compact)
            {
                Place(ui.icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(46, 46));
                ui.nameText = Txt(root.transform, "Name", "", 14, Palette.Ash, FontMono, TextAlignmentOptions.Bottom, true);
                Place(ui.nameText, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(110, 18));
            }
            else
            {
                Place(ui.icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(54, 54));
                ui.nameText = Txt(root.transform, "Name", "", 22, Palette.Bone, FontTitle, TextAlignmentOptions.TopLeft, true);
                Place(ui.nameText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(76, -8), new Vector2(210, 30));
                ui.descText = Txt(root.transform, "Desc", "", 14, Palette.Ash, FontBody, TextAlignmentOptions.TopLeft, true);
                Place(ui.descText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(76, -38), new Vector2(160, 52));
                ui.footerText = Txt(root.transform, "Footer", "", 26, Palette.BloodLight, FontMono, TextAlignmentOptions.Right);
                Place(ui.footerText, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, -14), new Vector2(70, 30));
            }
            return SavePrefab(root, P(name)).GetComponent<ItemSlotUI>();
        }

        // ================================================================== Écran PSX
        public static GameObject PSXOutput()
        {
            var root = new GameObject("PSXOutput", typeof(RectTransform));
            root.layer = 5;
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var raw = UIObj("Screen", root.transform);
            Stretch(raw);
            var ri = raw.AddComponent<RawImage>();
            ri.raycastTarget = false;
            var psx = root.AddComponent<PSXScreen>();
            psx.output = ri;
            psx.vhsMaterialTemplate = VHSMaterial;
            return SavePrefab(root, P("PSXOutput"));
        }

        // ================================================================== Interface complète
        public static UIManager GameUI(CardUIView offerCard, CardUIView smallCard, ItemSlotUI itemOffer, ItemSlotUI itemButton)
        {
            var root = new GameObject("GameUI", typeof(RectTransform));
            root.layer = 5;
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var ui = root.AddComponent<UIManager>();
            var t = root.transform;

            BuildCombatLayer(t);
            ui.hud = BuildHUD(t, itemButton);
            ui.inspector = BuildInspector(t);
            ui.toast = BuildToast(t, "Toast", 30, new Vector2(0, 170), Palette.Bone, FontBody, 2.6f);
            ui.banner = BuildToast(t, "Banner", 64, new Vector2(0, 60), Palette.BloodLight, FontMono, 1.3f);
            ui.title = BuildTitle(t);
            ui.shop = BuildShop(t, offerCard, itemOffer);
            ui.deckPicker = BuildDeckPicker(t, smallCard);
            ui.shop.deckPicker = ui.deckPicker;
            ui.deck = BuildDeckViewer(t, smallCard);
            ui.roundSummary = BuildSummary(t);
            ui.end = BuildEnd(t);
            ui.pause = BuildPause(t);
            ui.rules = BuildRules(t);
            ui.options = BuildOptions(t);
            var fader = Img(t, "Fader", Palette.Void);
            Stretch(fader.gameObject);
            ui.fader = fader;
            return SavePrefab(root, P("GameUI")).GetComponent<UIManager>();
        }

        static void BuildCombatLayer(Transform parent)
        {
            var layer = UIObj("CombatLayer", parent);
            Stretch(layer);
            // Infos de la Dépression accrochées au-dessus de sa tête
            var ov = layer.AddComponent<EnemyOverlay>();
            var root = UIObj("EnemyInfo", layer.transform);
            ((RectTransform)root.transform).sizeDelta = new Vector2(260, 120);
            ((RectTransform)root.transform).pivot = new Vector2(0.5f, 0f);
            ov.root = (RectTransform)root.transform;
            ov.iconAttack = SpriteSword; ov.iconDefend = SpriteShield; ov.iconBuff = SpriteUp; ov.iconCurse = SpriteEye;
            ov.intentIcon = Img(root.transform, "Intent", Palette.BloodLight, SpriteSword);
            Place(ov.intentIcon, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-18, 0), new Vector2(46, 46));
            ov.intentText = Mono(root.transform, "IntentValue", "", 40, Palette.Bone, TextAlignmentOptions.Left);
            Place(ov.intentText, new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(10, 2), new Vector2(120, 46));
            var barBg = Img(root.transform, "HpBack", Palette.Void.WithAlpha(0.7f));
            Place(barBg, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(220, 6));
            var bar = Img(barBg.transform, "Hp", Palette.Blood);
            var brt = Stretch(bar.gameObject);
            brt.pivot = new Vector2(0, 0.5f);
            ov.hpBar = brt;
            ov.hpText = Mono(root.transform, "HpText", "", 24, Palette.Ash);
            Place(ov.hpText, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(260, 26));
            var blk = Img(root.transform, "Block", Palette.Ash, SpriteShield);
            Place(blk, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(-126, 44), new Vector2(30, 30));
            ov.blockRoot = blk.gameObject;
            ov.blockText = Mono(blk.transform, "Value", "", 22, Palette.Void);
            Stretch(ov.blockText.gameObject);
            ov.statusText = Mono(root.transform, "Status", "", 20, Palette.Hex("A88AC0"));
            Place(ov.statusText, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 36), new Vector2(360, 24));

            // Chiffres flottants
            var pops = layer.AddComponent<DamagePopups>();
            var prefab = Mono(layer.transform, "PopupTemplate", "-0", 54, Palette.BloodLight);
            prefab.gameObject.AddComponent<Shadow>().effectColor = Palette.Void;
            prefab.gameObject.SetActive(false);
            pops.popupPrefab = prefab;
            var anchor = UIObj("PlayerAnchor", layer.transform);
            Place(anchor, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(300, 300), new Vector2(10, 10));
            pops.playerAnchor = (RectTransform)anchor.transform;
        }

        static HUDPanel BuildHUD(Transform parent, ItemSlotUI itemButton)
        {
            var hud = Panel<HUDPanel>(parent, "HUD");
            var t = hud.transform;
            var bl = new Vector2(0, 0);
            var br = new Vector2(1, 0);

            // Lucidité (bas gauche)
            Mono(t, "LucidityLabel", "LUCIDITÉ", 24, Palette.Ash, TextAlignmentOptions.Left).rectTransform.Apply(r => Place(r, bl, bl, new Vector2(40, 118), new Vector2(200, 26)));
            hud.lucidityText = Txt(t, "Lucidity", "50/50", 64, Palette.Bone, FontTitle, TextAlignmentOptions.Left);
            Place(hud.lucidityText, bl, bl, new Vector2(40, 46), new Vector2(300, 72));
            var barBg = Img(t, "BarBack", Palette.Void.WithAlpha(0.6f));
            Place(barBg, bl, bl, new Vector2(40, 34), new Vector2(300, 6));
            var ghost = Img(barBg.transform, "Ghost", Palette.Bone.WithAlpha(0.6f));
            var grt = Stretch(ghost.gameObject); grt.pivot = new Vector2(0, 0.5f);
            var bar = Img(barBg.transform, "Bar", Palette.BloodLight);
            var brt = Stretch(bar.gameObject); brt.pivot = new Vector2(0, 0.5f);
            hud.lucidityBar = brt; hud.lucidityBarGhost = grt;
            var blk = Img(t, "Block", Palette.Ash, SpriteShield);
            Place(blk, bl, new Vector2(0.5f, 0.5f), new Vector2(370, 80), new Vector2(54, 54));
            hud.blockRoot = blk.gameObject;
            hud.blockText = Mono(blk.transform, "Value", "0", 32, Palette.Void);
            Stretch(hud.blockText.gameObject);
            hud.statusText = Mono(t, "Status", "", 22, Palette.Hex("A88AC0"), TextAlignmentOptions.Left);
            Place(hud.statusText, bl, bl, new Vector2(40, 8), new Vector2(500, 24));

            // Énergie
            hud.energyOrb = Img(t, "EnergyOrb", Palette.Hope, SpriteOrb);
            Place(hud.energyOrb, bl, new Vector2(0.5f, 0.5f), new Vector2(110, 230), new Vector2(110, 110));
            hud.energyOrb.gameObject.AddComponent<UIFlicker>().minAlpha = 0.85f;
            hud.energyText = Txt(hud.energyOrb.transform, "Value", "3/3", 40, Palette.Void, FontTitle);
            Stretch(hud.energyText.gameObject);

            // Objets
            var items = UIObj("Items", t);
            Place(items, bl, bl, new Vector2(180, 196), new Vector2(400, 72));
            HList(items, 6, TextAnchor.MiddleLeft);
            hud.itemsContainer = items.transform;
            hud.itemButtonPrefab = itemButton;

            // Fin du tour et piles (bas droite)
            hud.endTurnButton = MenuItem(t, "EndTurn", "FINIR LE TOUR  [E]", 34, new Vector2(420, 64), TextAlignmentOptions.Right, FontMono);
            Place(hud.endTurnButton, br, br, new Vector2(-40, 70), new Vector2(420, 64));
            hud.endTurnLabel = hud.endTurnButton.GetComponentInChildren<TMP_Text>();
            hud.drawPileButton = MenuItem(t, "DrawPile", "pioche 0", 22, new Vector2(200, 30), TextAlignmentOptions.Right, FontMono);
            Place(hud.drawPileButton, br, br, new Vector2(-250, 24), new Vector2(200, 30));
            hud.drawPileText = hud.drawPileButton.GetComponentInChildren<TMP_Text>();
            hud.discardPileButton = MenuItem(t, "DiscardPile", "défausse 0", 22, new Vector2(200, 30), TextAlignmentOptions.Right, FontMono);
            Place(hud.discardPileButton, br, br, new Vector2(-40, 24), new Vector2(200, 30));
            hud.discardPileText = hud.discardPileButton.GetComponentInChildren<TMP_Text>();
            return hud;
        }

        static void Apply(this RectTransform rt, System.Action<RectTransform> a) => a(rt);

        static CardInspectorPanel BuildInspector(Transform parent)
        {
            var p = Panel<CardInspectorPanel>(parent, "CardInspector");
            p.fadeTime = 0.1f;
            var glow = Img(p.transform, "Backdrop", Color.white, SpriteGradient);
            Place(glow, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 60), new Vector2(620, 760));
            glow.color = Palette.Void.WithAlpha(0.85f);
            var b = p.transform;
            var anchor = new Vector2(1, 0.5f);
            var art = UIObj("Art", b);
            Place(art, anchor, new Vector2(1, 0.5f), new Vector2(-60, 300), new Vector2(160, 160));
            p.art = art.AddComponent<RawImage>();
            p.art.raycastTarget = false;
            var orb = Img(b, "CostOrb", Palette.BloodDark, SpriteOrb);
            Place(orb, anchor, new Vector2(0.5f, 0.5f), new Vector2(-440, 330), new Vector2(64, 64));
            p.costRoot = orb.gameObject;
            p.costText = Txt(orb.transform, "Cost", "1", 40, Palette.Bone, FontTitle);
            Stretch(p.costText.gameObject);
            p.titleText = Title(b, "Title", "", 46, Palette.Bone, TextAlignmentOptions.Right);
            Place(p.titleText, anchor, new Vector2(1, 0.5f), new Vector2(-60, 180), new Vector2(480, 60));
            p.subtitleText = Mono(b, "Subtitle", "", 22, Palette.Ash, TextAlignmentOptions.Right);
            Place(p.subtitleText, anchor, new Vector2(1, 0.5f), new Vector2(-60, 140), new Vector2(480, 26));
            p.rulesText = Body(b, "Rules", "", 28, Palette.Bone, TextAlignmentOptions.TopRight, true);
            Place(p.rulesText, anchor, new Vector2(1, 1), new Vector2(-60, 110), new Vector2(470, 190));
            p.keywordsText = Body(b, "Keywords", "", 19, Palette.Ash, TextAlignmentOptions.TopRight, true);
            Place(p.keywordsText, anchor, new Vector2(1, 1), new Vector2(-60, -90), new Vector2(470, 120));
            p.flavorText = Body(b, "Flavor", "", 20, Palette.Hex("8E7A6A"), TextAlignmentOptions.TopRight, true);
            p.flavorText.fontStyle = FontStyles.Italic;
            Place(p.flavorText, anchor, new Vector2(1, 1), new Vector2(-60, -220), new Vector2(470, 70));
            p.artLibrary = ArtLibrary;
            return p;
        }

        static ToastFeed BuildToast(Transform parent, string name, float size, Vector2 pos, Color color, TMP_FontAsset font, float duration)
        {
            var g = UIObj(name, parent);
            Stretch(g);
            var toast = g.AddComponent<ToastFeed>();
            toast.duration = duration;
            toast.text = Txt(g.transform, "Text", "", size, color, font);
            Place(toast.text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(1300, size * 1.6f));
            toast.text.alpha = 0f;
            toast.text.gameObject.AddComponent<Shadow>().effectColor = Palette.Void;
            return toast;
        }

        static TitlePanel BuildTitle(Transform parent)
        {
            var p = Panel<TitlePanel>(parent, "Title");
            var vig = Img(p.transform, "Vignette", Color.white, SpriteVignette);
            Stretch(vig.gameObject, -300, -300, -300, -300);
            var left = Img(p.transform, "LeftShade", Palette.Void.WithAlpha(0.75f), SpriteGradient);
            Place(left, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(550, 0), new Vector2(1100, 1200));
            left.rectTransform.localScale = new Vector3(-1, 1, 1); // sombre à gauche, s'estompe vers la droite

            var ghost = Title(p.transform, "TitleGhost", "Lucidité", 190, Palette.BloodLight.WithAlpha(0.45f), TextAlignmentOptions.Left);
            Place(ghost, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(124, 220), new Vector2(1200, 220));
            p.titleText = Title(p.transform, "TitleText", "Lucidité", 190, Palette.Bone, TextAlignmentOptions.Left);
            Place(p.titleText, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 220), new Vector2(1200, 220));
            var glitch = p.titleText.gameObject.AddComponent<TextGlitch>();
            glitch.text = p.titleText; glitch.ghost = ghost; glitch.intensity = 0.7f;
            p.subtitleText = Body(p.transform, "Subtitle", "joue tes cartes contre ta dépression", 32, Palette.Ash, TextAlignmentOptions.Left);
            Place(p.subtitleText, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, 100), new Vector2(1000, 44));

            var menu = UIObj("Menu", p.transform);
            Place(menu, new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(130, 30), new Vector2(520, 320));
            VList(menu, 6, TextAnchor.UpperLeft);
            p.playButton = MenuItem(menu.transform, "Play", "Allumer la bougie", 44, new Vector2(520, 70));
            p.rulesButton = MenuItem(menu.transform, "Rules", "Règles", 36, new Vector2(520, 58));
            p.optionsButton = MenuItem(menu.transform, "Options", "Options", 36, new Vector2(520, 58));
            p.quitButton = MenuItem(menu.transform, "Quit", "Quitter", 36, new Vector2(520, 58));

            var osd = p.gameObject.AddComponent<VhsOsd>();
            osd.modeText = Mono(p.transform, "OsdMode", "> LECTURE", 40, Palette.Bone, TextAlignmentOptions.Left);
            Place(osd.modeText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -40), new Vector2(400, 50));
            osd.counterText = Mono(p.transform, "OsdCounter", "SP 00:00:00", 40, Palette.Bone, TextAlignmentOptions.Right);
            Place(osd.counterText, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 40), new Vector2(500, 50));
            p.promptText = Body(p.transform, "Warning",
                "Ce jeu aborde la dépression, l'automutilation et les idées suicidaires.\nBesoin de parler ? 3114 (France, 24h/24, gratuit).",
                18, Palette.AshDark, TextAlignmentOptions.Left);
            Place(p.promptText, new Vector2(0, 0), new Vector2(0, 0), new Vector2(130, 40), new Vector2(900, 50));
            return p;
        }

        static ShopPanel BuildShop(Transform parent, CardUIView offerCard, ItemSlotUI itemOffer)
        {
            var p = Panel<ShopPanel>(parent, "Shop");
            Shade(p.transform, 0.78f);
            p.offerCardPrefab = offerCard;
            p.itemOfferPrefab = itemOffer;
            var t = p.transform;
            var top = new Vector2(0.5f, 1);

            var title = Title(t, "Header", "Entre deux manches", 64, Palette.Bone);
            Place(title, top, top, new Vector2(0, -50), new Vector2(1200, 80));
            p.bloodText = Mono(t, "Blood", "0 gouttes de sang", 34, Palette.BloodLight);
            Place(p.bloodText, top, top, new Vector2(0, -130), new Vector2(800, 40));

            var cards = UIObj("CardOffers", t);
            Place(cards, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, 60), new Vector2(800, 380));
            HList(cards, 40, TextAnchor.MiddleCenter);
            p.cardOffersContainer = cards.transform;

            var items = UIObj("ItemOffers", t);
            Place(items, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(170, 60), new Vector2(300, 260));
            VList(items, 24, TextAnchor.MiddleCenter);
            p.itemOffersContainer = items.transform;

            var services = UIObj("Services", t);
            Place(services, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-440, 70), new Vector2(700, 190));
            VList(services, 4, TextAnchor.LowerLeft);
            p.upgradeButton = MenuItem(services.transform, "Upgrade", "Faire évoluer une carte", 28, new Vector2(700, 48));
            p.removeButton = MenuItem(services.transform, "Remove", "Brûler une carte", 28, new Vector2(700, 48));
            p.transformButton = MenuItem(services.transform, "Transform", "Transformer une Crise", 28, new Vector2(700, 48));

            p.continueButton = MenuItem(t, "Continue", "Manche suivante  ›", 40, new Vector2(500, 64), TextAlignmentOptions.Right);
            Place(p.continueButton, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80, 80), new Vector2(500, 64));
            p.feedbackText = Body(t, "Feedback", "", 24, Palette.Hope, TextAlignmentOptions.Center);
            p.feedbackText.fontStyle = FontStyles.Italic;
            p.feedbackText.enableAutoSizing = true;
            p.feedbackText.fontSizeMin = 14; p.feedbackText.fontSizeMax = 24;
            Place(p.feedbackText, top, top, new Vector2(0, -178), new Vector2(1500, 52));
            return p;
        }

        static DeckPickerPanel BuildDeckPicker(Transform parent, CardUIView card)
        {
            var p = Panel<DeckPickerPanel>(parent, "DeckPicker");
            Shade(p.transform, 0.9f);
            p.titleText = Title(p.transform, "Title", "", 52, Palette.Bone);
            Place(p.titleText, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1200, 70));
            var (scroll, content) = ScrollGrid(p.transform, "Scroll", new Vector2(176, 264), new Vector2(18, 18));
            Stretch(scroll.gameObject, 160, 160, 130, 120);
            p.container = content;
            p.cardPrefab = card;
            p.cancelButton = MenuItem(p.transform, "Cancel", "Annuler", 34, new Vector2(300, 56), TextAlignmentOptions.Center);
            Place(p.cancelButton, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(300, 56));
            return p;
        }

        static DeckPanel BuildDeckViewer(Transform parent, CardUIView card)
        {
            var p = Panel<DeckPanel>(parent, "DeckViewer");
            Shade(p.transform, 0.88f);
            p.headerText = Title(p.transform, "Header", "Pioche", 48, Palette.Bone);
            Place(p.headerText, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1200, 64));
            var (scroll, content) = ScrollGrid(p.transform, "Scroll", new Vector2(176, 264), new Vector2(18, 18));
            Stretch(scroll.gameObject, 160, 160, 120, 120);
            p.container = content;
            p.cardPrefab = card;
            p.closeButton = MenuItem(p.transform, "Close", "Fermer", 34, new Vector2(300, 56), TextAlignmentOptions.Center);
            Place(p.closeButton, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(300, 56));
            return p;
        }

        static RoundSummaryPanel BuildSummary(Transform parent)
        {
            var p = Panel<RoundSummaryPanel>(parent, "RoundSummary");
            Shade(p.transform, 0.6f);
            var c = new Vector2(0.5f, 0.5f);
            p.titleText = Title(p.transform, "Title", "", 72, Palette.Bone);
            Place(p.titleText, c, c, new Vector2(0, 250), new Vector2(1600, 100));
            p.playerCandles = CandleRow(p.transform, "PlayerCandles", new Vector2(-300, 90));
            p.enemyCandles = CandleRow(p.transform, "EnemyCandles", new Vector2(300, 90));
            Mono(p.transform, "YouLabel", "toi", 26, Palette.Ash).rectTransform.Apply(r => Place(r, c, c, new Vector2(-300, 20), new Vector2(300, 30)));
            Mono(p.transform, "HerLabel", "elle", 26, Palette.Ash).rectTransform.Apply(r => Place(r, c, c, new Vector2(300, 20), new Vector2(300, 30)));
            p.bodyText = Body(p.transform, "Body", "", 26, Palette.Ash);
            Place(p.bodyText, c, c, new Vector2(0, -80), new Vector2(900, 120));
            p.totalText = Mono(p.transform, "Total", "", 44, Palette.BloodLight);
            Place(p.totalText, c, c, new Vector2(0, -175), new Vector2(900, 54));
            p.continueButton = MenuItem(p.transform, "Continue", "Continuer  ›", 38, new Vector2(380, 60), TextAlignmentOptions.Center);
            Place(p.continueButton, c, c, new Vector2(0, -280), new Vector2(380, 60));
            return p;
        }

        static Image[] CandleRow(Transform parent, string name, Vector2 pos)
        {
            var row = UIObj(name, parent);
            Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(400, 110));
            HList(row, 18, TextAnchor.MiddleCenter);
            var arr = new Image[6];
            for (int i = 0; i < arr.Length; i++)
            {
                var im = Img(row.transform, "Flame" + i, Palette.Hope, SpriteFlame);
                im.preserveAspect = true;
                ((RectTransform)im.transform).sizeDelta = new Vector2(48, 90);
                im.gameObject.AddComponent<UIFlicker>().minAlpha = 0.7f;
                arr[i] = im;
            }
            return arr;
        }

        static EndPanel BuildEnd(Transform parent)
        {
            var p = Panel<EndPanel>(parent, "End");
            Shade(p.transform, 0.82f);
            var c = new Vector2(0.5f, 0.5f);
            p.titleText = Title(p.transform, "Title", "", 96, Palette.Bone);
            Place(p.titleText, c, c, new Vector2(0, 230), new Vector2(1700, 120));
            var g = p.titleText.gameObject.AddComponent<TextGlitch>(); g.text = p.titleText; g.intensity = 0.4f;
            p.messageText = Body(p.transform, "Message", "", 30, Palette.Bone);
            Place(p.messageText, c, c, new Vector2(0, 90), new Vector2(1400, 110));
            p.statsText = Mono(p.transform, "Stats", "", 30, Palette.Ash);
            Place(p.statsText, c, c, new Vector2(0, -40), new Vector2(1400, 120));
            var row = UIObj("Buttons", p.transform);
            Place(row, c, c, new Vector2(0, -190), new Vector2(900, 64));
            HList(row, 60, TextAnchor.MiddleCenter);
            p.restartButton = MenuItem(row.transform, "Restart", "Rallumer une bougie", 36, new Vector2(420, 60), TextAlignmentOptions.Center);
            p.menuButton = MenuItem(row.transform, "Menu", "Menu principal", 36, new Vector2(340, 60), TextAlignmentOptions.Center);
            p.helpText = Body(p.transform, "Help", "", 22, Palette.Ash);
            Place(p.helpText, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(1400, 30));
            return p;
        }

        static PausePanel BuildPause(Transform parent)
        {
            var p = Panel<PausePanel>(parent, "Pause");
            Shade(p.transform, 0.72f);
            var osd = Mono(p.transform, "PauseOsd", "|| PAUSE", 64, Palette.Bone, TextAlignmentOptions.Left);
            Place(osd, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -40), new Vector2(600, 70));
            osd.gameObject.AddComponent<UIFlicker>().minAlpha = 0.4f;
            var menu = UIObj("Menu", p.transform);
            Place(menu, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, 0), new Vector2(520, 360));
            VList(menu, 6, TextAnchor.MiddleLeft);
            p.resumeButton = MenuItem(menu.transform, "Resume", "Reprendre", 40, new Vector2(520, 62));
            p.rulesButton = MenuItem(menu.transform, "Rules", "Règles", 34, new Vector2(520, 56));
            p.optionsButton = MenuItem(menu.transform, "Options", "Options", 34, new Vector2(520, 56));
            p.restartButton = MenuItem(menu.transform, "Restart", "Recommencer", 34, new Vector2(520, 56));
            p.menuButton = MenuItem(menu.transform, "Menu", "Menu principal", 34, new Vector2(520, 56));
            return p;
        }

        static RulesPanel BuildRules(Transform parent)
        {
            var p = Panel<RulesPanel>(parent, "Rules");
            Shade(p.transform, 0.8f);
            var page = Img(p.transform, "Page", Color.white, SpritePage, true);
            Place(page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1000, 920));
            page.rectTransform.localEulerAngles = new Vector3(0, 0, -1.5f);
            var ink = Palette.Hex("231A1A");
            p.headerText = Title(page.transform, "Header", "", 58, Palette.Hex("6E0B0E"), TextAlignmentOptions.Left);
            Place(p.headerText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -50), new Vector2(820, 70));
            p.bodyText = Body(page.transform, "Body", "", 27, ink, TextAlignmentOptions.TopLeft, true);
            Stretch(p.bodyText.gameObject, 110, 70, 140, 130);
            p.pageText = Mono(page.transform, "Page", "", 26, ink);
            Place(p.pageText, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(300, 30));
            p.prevButton = MenuItem(page.transform, "Prev", "‹ précédente", 28, new Vector2(240, 50), TextAlignmentOptions.Left);
            Place(p.prevButton, new Vector2(0, 0), new Vector2(0, 0), new Vector2(100, 40), new Vector2(240, 50));
            p.nextButton = MenuItem(page.transform, "Next", "suivante ›", 28, new Vector2(240, 50), TextAlignmentOptions.Right);
            Place(p.nextButton, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 40), new Vector2(240, 50));
            foreach (var b in new[] { p.prevButton, p.nextButton }) b.GetComponent<MenuItemFX>().normalColor = ink;
            p.closeButton = MenuItem(p.transform, "Close", "Fermer le carnet", 32, new Vector2(400, 54), TextAlignmentOptions.Center);
            Place(p.closeButton, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(400, 54));
            return p;
        }

        static OptionsPanel BuildOptions(Transform parent)
        {
            var p = Panel<OptionsPanel>(parent, "Options");
            Shade(p.transform, 0.85f);
            var title = Title(p.transform, "Title", "Options", 72, Palette.Bone, TextAlignmentOptions.Left);
            Place(title, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, 300), new Vector2(800, 90));
            float y = 180;
            p.musicSlider = OptionRow(p.transform, "Musique", ref y, out _);
            p.sfxSlider = OptionRow(p.transform, "Effets sonores", ref y, out _);
            p.vhsSlider = OptionRow(p.transform, "Intensité VHS", ref y, out _);
            p.resolutionSlider = OptionRow(p.transform, "Résolution interne : 480p", ref y, out var resLabel);
            p.resolutionLabel = resLabel;
            var shakeLbl = Body(p.transform, "ShakeLabel", "Tremblements de caméra", 30, Palette.Bone, TextAlignmentOptions.Left);
            Place(shakeLbl, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, y), new Vector2(460, 44));
            var toggleRoot = UIObj("ShakeToggle", p.transform);
            Place(toggleRoot, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(620, y), new Vector2(34, 34));
            var bg = Img(toggleRoot.transform, "Background", Palette.AshDark, null, true);
            Stretch(bg.gameObject);
            var check = Img(bg.transform, "Check", Palette.BloodLight);
            Stretch(check.gameObject, 6, 6, 6, 6);
            var tog = toggleRoot.AddComponent<Toggle>();
            tog.targetGraphic = bg; tog.graphic = check; tog.isOn = true;
            p.shakeToggle = tog;
            p.closeButton = MenuItem(p.transform, "Close", "‹ Retour", 36, new Vector2(400, 56));
            Place(p.closeButton, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, y - 110), new Vector2(400, 56));
            return p;
        }

        static Slider OptionRow(Transform parent, string label, ref float y, out TextMeshProUGUI lbl)
        {
            lbl = Body(parent, label + "Label", label, 30, Palette.Bone, TextAlignmentOptions.Left);
            Place(lbl, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(130, y), new Vector2(460, 44));
            var s = MakeSlider(parent, label + "Slider", 380);
            Place(s, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(620, y), new Vector2(380, 30));
            y -= 76;
            return s;
        }
    }
}
