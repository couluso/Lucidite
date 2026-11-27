using System.Collections.Generic;
using UnityEngine;

namespace Lucidite
{
    public enum Sfx
    {
        Hover, CardDraw, CardPlace, CardFlip, CardSlide,
        ClashWin, ClashLose, ClashDraw,
        Damage, Heartbeat, Static, OpponentHurt, Heal, Whisper,
        Victory, Defeat, RoundStart,
        CandleOut, CandleLight, BloodDrop,
        UIClick, Buy, Error,
        Hit, Blocked, Shield, Burn, Swoosh, EndTurn, Rumble, Select,
        Crumble, DebrisHit
    }

    /// <summary>
    /// Audio du jeu. Les sons sont synthétisés au lancement (<see cref="ProceduralAudio"/>) ;
    /// assignez des AudioClips dans <see cref="overrides"/> pour les remplacer par de vrais fichiers.
    /// Volumes Musique / Effets réglables et sauvegardés (couche de gain statique).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [System.Serializable]
        public struct ClipOverride { public Sfx sfx; public AudioClip clip; }

        public static AudioManager Instance { get; private set; }

        [Tooltip("Remplace un son synthétisé par un vrai fichier audio.")]
        public List<ClipOverride> overrides = new List<ClipOverride>();
        public AudioClip musicOverride;
        public AudioClip ambienceOverride;
        public AudioSource lampHumSource; // source 3D placée sur la lampe (optionnelle)

        [Header("Niveaux")]
        public float musicLevel = 0.45f;
        public float ambienceLevel = 0.35f;
        public float dreadLevel = 0.35f;
        public float humLevel = 0.12f;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("vol_music", 0.7f);
            set { PlayerPrefs.SetFloat("vol_music", value); }
        }
        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat("vol_sfx", 0.9f);
            set { PlayerPrefs.SetFloat("vol_sfx", value); }
        }

        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        AudioSource _music, _ambience, _dread;
        float _dreadTarget, _dreadCurrent, _hum = 1f;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildClips();
            _music = MakeLoopSource("Music", musicOverride ? musicOverride : ProceduralAudio.DroneLoop());
            _ambience = MakeLoopSource("Ambience", ambienceOverride ? ambienceOverride : ProceduralAudio.RoomToneLoop());
            _dread = MakeLoopSource("Dread", ProceduralAudio.DreadLoop());
            if (lampHumSource)
            {
                lampHumSource.clip = ProceduralAudio.HumLoop();
                lampHumSource.loop = true;
                lampHumSource.spatialBlend = 1f;
                lampHumSource.minDistance = 0.5f;
                lampHumSource.maxDistance = 6f;
                lampHumSource.Play();
            }
            for (int i = 0; i < 12; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _pool.Add(s);
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        AudioSource MakeLoopSource(string n, AudioClip clip)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.Play();
            return s;
        }

        void BuildClips()
        {
            _clips[Sfx.Hover] = ProceduralAudio.Tick();
            _clips[Sfx.CardDraw] = ProceduralAudio.Swish(0.22f);
            _clips[Sfx.CardPlace] = ProceduralAudio.Thump(140f, 0.16f);
            _clips[Sfx.CardFlip] = ProceduralAudio.Flip();
            _clips[Sfx.CardSlide] = ProceduralAudio.Slide();
            _clips[Sfx.ClashWin] = ProceduralAudio.Bell(new[] { 261.6f, 392f, 523.3f }, 1.6f, 0.5f);
            _clips[Sfx.ClashLose] = ProceduralAudio.Cluster();
            _clips[Sfx.ClashDraw] = ProceduralAudio.Bell(new[] { 196f, 293.7f }, 0.8f, 0.25f, 0.25f);
            _clips[Sfx.Damage] = ProceduralAudio.Impact();
            _clips[Sfx.Heartbeat] = ProceduralAudio.Heartbeat();
            _clips[Sfx.Static] = ProceduralAudio.Static();
            _clips[Sfx.OpponentHurt] = ProceduralAudio.Growl();
            _clips[Sfx.Heal] = ProceduralAudio.Arpeggio(new[] { 523.3f, 659.3f, 784f }, 0.09f, 0.35f);
            _clips[Sfx.Whisper] = ProceduralAudio.Whisper();
            _clips[Sfx.Victory] = ProceduralAudio.Swell(new[] { 220f, 277.2f, 329.6f, 440f }, 3.5f, false);
            _clips[Sfx.Defeat] = ProceduralAudio.Swell(new[] { 73.4f, 77.8f, 110f }, 3.5f, true);
            _clips[Sfx.RoundStart] = ProceduralAudio.Bell(new[] { 146.8f, 293.7f * 1.01f, 440f * 1.03f, 587f * 0.98f }, 2.4f, 0.9f, 0.3f);
            _clips[Sfx.CandleOut] = ProceduralAudio.Puff();
            _clips[Sfx.CandleLight] = ProceduralAudio.Strike();
            _clips[Sfx.BloodDrop] = ProceduralAudio.Drop();
            _clips[Sfx.UIClick] = ProceduralAudio.Tick();
            _clips[Sfx.Buy] = ProceduralAudio.Drop();
            _clips[Sfx.Error] = ProceduralAudio.Buzz();
            _clips[Sfx.Hit] = ProceduralAudio.Thump(90f, 0.22f);
            _clips[Sfx.Blocked] = ProceduralAudio.Bell(new[] { 880f, 1320f }, 0.25f, 0.06f, 0.2f);
            _clips[Sfx.Shield] = ProceduralAudio.Swish(0.15f);
            _clips[Sfx.Burn] = ProceduralAudio.Static(0.35f);
            _clips[Sfx.Swoosh] = ProceduralAudio.Swish(0.3f);
            _clips[Sfx.EndTurn] = ProceduralAudio.Bell(new[] { 196f, 392f }, 0.9f, 0.3f, 0.25f);
            _clips[Sfx.Rumble] = ProceduralAudio.Rumble();
            _clips[Sfx.Select] = ProceduralAudio.Drop();
            _clips[Sfx.Crumble] = ProceduralAudio.Crumble();
            _clips[Sfx.DebrisHit] = ProceduralAudio.Knock();
            foreach (var o in overrides) if (o.clip) _clips[o.sfx] = o.clip;
        }

        public static void Play(Sfx sfx, float volume = 1f)
        {
            var am = Instance;
            if (!am || !am._clips.TryGetValue(sfx, out var clip) || !clip) return;
            AudioSource src = null;
            foreach (var s in am._pool) if (!s.isPlaying) { src = s; break; }
            if (!src) src = am._pool[0];
            src.clip = clip;
            src.volume = volume * SfxVolume;
            src.pitch = (sfx == Sfx.Hover || sfx == Sfx.CardDraw || sfx == Sfx.CardPlace || sfx == Sfx.BloodDrop || sfx == Sfx.UIClick)
                ? Random.Range(0.92f, 1.08f)
                : sfx == Sfx.DebrisHit ? Random.Range(0.75f, 1.35f)
                : sfx == Sfx.Crumble ? Random.Range(0.85f, 1.1f) : 1f;
            src.Play();
        }

        public static void SetDread(float d) { if (Instance) Instance._dreadTarget = Mathf.Clamp01(d); }
        public static void SetLampHum(float k) { if (Instance) Instance._hum = k; }

        void Update()
        {
            float music = MusicVolume;
            _dreadCurrent = Mathf.MoveTowards(_dreadCurrent, _dreadTarget, Time.unscaledDeltaTime * 0.3f);
            bool paused = GameManager.Instance && GameManager.Instance.IsPaused;
            float duck = paused ? 0.5f : 1f; // ~ -6 dB en pause
            if (_music) _music.volume = musicLevel * music * duck;
            if (_ambience) _ambience.volume = ambienceLevel * SfxVolume * duck;
            if (_dread) _dread.volume = dreadLevel * music * _dreadCurrent * duck;
            if (lampHumSource) lampHumSource.volume = humLevel * SfxVolume * Mathf.Clamp01(_hum);
        }
    }
}
