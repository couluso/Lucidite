using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lucidite
{
    /// <summary>
    /// Réactions audio-visuelles. Encaisser une attaque : la pièce tremble (ce qui balance la lampe),
    /// la caméra est secouée, l'image VHS grésille, les bords rougissent, de la poussière tombe.
    /// L'intensité suit la quantité de Lucidité perdue.
    /// </summary>
    public class FeedbackDirector : MonoBehaviour
    {
        public CameraRig cameraRig;
        public HangingLamp lamp;
        public RoomShake roomShake;
        public ParticleSystem ceilingDust;
        public Volume postVolume;

        Vignette _vignette;
        ChromaticAberration _chroma;
        float _pulse;
        float _baseVignette = 0.3f;
        float _baseChroma = 0.06f;

        void Start()
        {
            var gm = GameManager.Instance;
            if (gm)
            {
                gm.OnDamaged += HandleDamaged;
                gm.OnHealed += HandleHealed;
                gm.OnBlockGained += HandleBlock;
                gm.OnCandleLost += HandleCandle;
                gm.OnCrisisInjected += HandleCrisis;
                gm.OnMatchEnded += HandleEnd;
                gm.OnRoundStarted += HandleRound;
                gm.OnStatsChanged += HandleStats;
            }
            if (postVolume && postVolume.profile)
            {
                postVolume.profile.TryGet(out _vignette);
                postVolume.profile.TryGet(out _chroma);
                if (_vignette) _baseVignette = _vignette.intensity.value;
                if (_chroma) _baseChroma = _chroma.intensity.value;
            }
        }

        void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (!gm) return;
            gm.OnDamaged -= HandleDamaged;
            gm.OnHealed -= HandleHealed;
            gm.OnBlockGained -= HandleBlock;
            gm.OnCandleLost -= HandleCandle;
            gm.OnCrisisInjected -= HandleCrisis;
            gm.OnMatchEnded -= HandleEnd;
            gm.OnRoundStarted -= HandleRound;
            gm.OnStatsChanged -= HandleStats;
        }

        void HandleRound(int round)
        {
            if (lamp) { lamp.Impulse(6f); lamp.SetMood(0f); lamp.SetDim(1f); }
            AudioManager.Play(Sfx.RoundStart);
        }

        void HandleDamaged(Side side, int lost, int blocked)
        {
            var gm = GameManager.Instance;
            if (side == Side.Player)
            {
                if (lost <= 0) { AudioManager.Play(Sfx.Blocked); if (cameraRig) cameraRig.AddTrauma(0.15f); return; }
                float k = Mathf.Clamp01(lost / 14f);
                if (roomShake) roomShake.Shake(0.55f + k * 0.65f);
                else if (lamp) { lamp.Impulse(20f + 25f * k); lamp.FlickerBurst(0.6f); }
                if (cameraRig) { cameraRig.AddTrauma(0.45f + k * 0.5f); cameraRig.Jolt(0.45f + k * 0.55f); }
                if (PSXScreen.Instance) { PSXScreen.Instance.AddGlitch(0.6f + k * 0.9f); PSXScreen.Instance.Flash(Palette.BloodLight, 0.25f + k * 0.35f); }
                if (ceilingDust) ceilingDust.Play();
                _pulse = Mathf.Max(_pulse, 0.5f + k * 0.5f);
                AudioManager.Play(Sfx.Damage, 0.6f + k * 0.4f);
                AudioManager.Play(Sfx.Static, 0.5f + k * 0.4f);
                if (k > 0.4f || gm.Player.IsLowLucidity) AudioManager.Play(Sfx.Heartbeat);
                if (k > 0.35f) StartCoroutine(HitStop(0.05f + k * 0.08f));
            }
            else
            {
                if (lost <= 0) return;
                float k = Mathf.Clamp01(lost / 14f);
                if (cameraRig) cameraRig.AddTrauma(0.1f + k * 0.15f);
                if (PSXScreen.Instance) PSXScreen.Instance.AddGlitch(0.15f + k * 0.2f);
                if (lamp) lamp.Impulse(4f + 6f * k);
                AudioManager.Play(Sfx.OpponentHurt, 0.4f + k * 0.5f);
            }
        }

        void HandleHealed(Side side, int amount)
        {
            if (side == Side.Player && PSXScreen.Instance) PSXScreen.Instance.Flash(Palette.Hope, 0.25f);
            AudioManager.Play(Sfx.Heal);
        }

        void HandleBlock(Side side, int amount) => AudioManager.Play(Sfx.Shield, side == Side.Player ? 0.8f : 0.5f);

        void HandleCandle(Side side, int remaining)
        {
            if (side == Side.Player)
            {
                if (roomShake) roomShake.Shake(1f);
                if (cameraRig) { cameraRig.AddTrauma(0.9f); cameraRig.Jolt(1f); }
                if (PSXScreen.Instance) PSXScreen.Instance.AddGlitch(1.4f);
                if (lamp) { lamp.FlickerBurst(1.5f); lamp.SetMood(-0.7f); }
                AudioManager.Play(Sfx.ClashLose);
            }
            else
            {
                if (lamp) lamp.SetMood(0.6f);
                if (PSXScreen.Instance) PSXScreen.Instance.Flash(Palette.Hope, 0.35f);
                AudioManager.Play(Sfx.ClashWin);
            }
        }

        void HandleStats()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null || gm.Player.maxHp <= 0) return;
            float p = gm.Player.hp / (float)gm.Player.maxHp;
            AudioManager.SetDread(1f - p);
            if (lamp && gm.Phase != GamePhase.RoundEnd)
            {
                float e = gm.Enemy.maxHp > 0 ? gm.Enemy.hp / (float)gm.Enemy.maxHp : 1f;
                lamp.SetMood(Mathf.Clamp((e - p) * -1.2f, -0.8f, 0.6f));
            }
        }

        void HandleCrisis(CardData card, int count)
        {
            if (PSXScreen.Instance) PSXScreen.Instance.AddGlitch(0.5f);
            GameManager.Instance?.Message($"« {card.cardName} » s'est glissée dans ton deck...");
        }

        void HandleEnd(bool playerWon)
        {
            if (playerWon)
            {
                if (lamp) lamp.SetMood(1f);
                if (PSXScreen.Instance) PSXScreen.Instance.Flash(Palette.Hope, 0.6f);
                AudioManager.Play(Sfx.Victory);
                AudioManager.SetDread(0f);
            }
            else
            {
                if (lamp) { lamp.SetMood(-1f); lamp.SetDim(0.12f); lamp.FlickerBurst(2f); }
                if (roomShake) roomShake.Shake(1f);
                if (cameraRig) cameraRig.AddTrauma(1f);
                if (PSXScreen.Instance) PSXScreen.Instance.AddGlitch(1.5f);
                AudioManager.Play(Sfx.Defeat);
            }
        }

        /// <summary>Arrêt sur image très bref : donne du poids aux gros coups.</summary>
        System.Collections.IEnumerator HitStop(float duration)
        {
            var gm = GameManager.Instance;
            if (gm && gm.IsPaused) yield break;
            Time.timeScale = 0.04f;
            yield return new WaitForSecondsRealtime(duration);
            if (!gm || !gm.IsPaused) Time.timeScale = 1f;
        }

        void Update()
        {
            _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 0.9f);
            var gm = GameManager.Instance;
            float dread = 0f;
            if (gm && gm.Player != null && gm.Player.maxHp > 0) dread = 1f - gm.Player.hp / (float)gm.Player.maxHp;
            if (_vignette)
            {
                _vignette.intensity.value = _baseVignette + dread * 0.1f + _pulse * 0.2f;
                _vignette.color.value = Color.Lerp(Color.black, Palette.BloodDark, Mathf.Clamp01(_pulse + dread * 0.5f));
            }
            if (_chroma) _chroma.intensity.value = _baseChroma + dread * 0.15f + _pulse * 0.5f;
        }
    }
}
