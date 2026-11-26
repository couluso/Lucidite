using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lucidite
{
    public class OptionsPanel : UIPanel
    {
        public Slider musicSlider;
        public Slider sfxSlider;
        public Slider vhsSlider;
        public Slider resolutionSlider; // 0..3 -> 240/360/480/720
        public TMP_Text resolutionLabel;
        public Toggle shakeToggle;
        public Button closeButton;

        static readonly int[] Resolutions = { 240, 360, 480, 720 };

        public static bool ShakeEnabled
        {
            get => PlayerPrefs.GetInt("shake", 1) == 1;
            set => PlayerPrefs.SetInt("shake", value ? 1 : 0);
        }

        void Awake()
        {
            CameraRig.ShakeMultiplier = ShakeEnabled ? 1f : 0.15f;
            if (closeButton) closeButton.onClick.AddListener(() => { PlayerPrefs.Save(); Hide(); AudioManager.Play(Sfx.UIClick); });
            if (musicSlider) musicSlider.onValueChanged.AddListener(v => AudioManager.MusicVolume = v);
            if (sfxSlider) sfxSlider.onValueChanged.AddListener(v => { AudioManager.SfxVolume = v; });
            if (vhsSlider) vhsSlider.onValueChanged.AddListener(v => { if (PSXScreen.Instance) PSXScreen.Instance.SetIntensity(v); else PSXScreen.SavedIntensity = v; });
            if (resolutionSlider)
            {
                resolutionSlider.wholeNumbers = true;
                resolutionSlider.minValue = 0;
                resolutionSlider.maxValue = Resolutions.Length - 1;
                resolutionSlider.onValueChanged.AddListener(v =>
                {
                    int h = Resolutions[Mathf.Clamp((int)v, 0, Resolutions.Length - 1)];
                    if (PSXScreen.Instance) PSXScreen.Instance.SetResolution(h); else PSXScreen.SavedResolution = h;
                    if (resolutionLabel) resolutionLabel.text = $"Résolution interne : {h}p";
                });
            }
            if (shakeToggle) shakeToggle.onValueChanged.AddListener(on => { ShakeEnabled = on; CameraRig.ShakeMultiplier = on ? 1f : 0.15f; });
        }

        public override void Show()
        {
            base.Show();
            if (musicSlider) musicSlider.SetValueWithoutNotify(AudioManager.MusicVolume);
            if (sfxSlider) sfxSlider.SetValueWithoutNotify(AudioManager.SfxVolume);
            if (vhsSlider) vhsSlider.SetValueWithoutNotify(PSXScreen.SavedIntensity);
            if (shakeToggle) shakeToggle.SetIsOnWithoutNotify(ShakeEnabled);
            if (resolutionSlider)
            {
                int idx = System.Array.IndexOf(Resolutions, PSXScreen.SavedResolution);
                if (idx < 0) idx = 2;
                resolutionSlider.SetValueWithoutNotify(idx);
                if (resolutionLabel) resolutionLabel.text = $"Résolution interne : {Resolutions[idx]}p";
            }
        }
    }
}
