using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Synthèse des sons du jeu (aucun fichier audio requis). Échantillonnage 22 kHz pour un rendu lo-fi.
    /// Remplacez librement par de vrais AudioClips dans l'AudioManager.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int Rate = 22050;
        const float TwoPi = Mathf.PI * 2f;

        static System.Random s_rng = new System.Random(1337);
        static float Noise() => (float)(s_rng.NextDouble() * 2.0 - 1.0);

        static float[] Buf(float seconds) => new float[Mathf.Max(1, Mathf.CeilToInt(seconds * Rate))];

        static AudioClip ToClip(string name, float[] data, bool fadeEdges = true)
        {
            if (fadeEdges)
            {
                int f = Mathf.Min(data.Length / 2, Rate / 200); // 5 ms
                for (int i = 0; i < f; i++)
                {
                    float k = i / (float)f;
                    data[i] *= k;
                    data[data.Length - 1 - i] *= k;
                }
            }
            float peak = 0f;
            foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            if (peak > 0.98f) for (int i = 0; i < data.Length; i++) data[i] *= 0.98f / peak;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Env(float t, float attack, float decay) =>
            t < attack ? t / Mathf.Max(1e-4f, attack) : Mathf.Exp(-(t - attack) / Mathf.Max(1e-4f, decay));

        static void LowPass(float[] d, float cutoff)
        {
            float rc = 1f / (TwoPi * cutoff), dt = 1f / Rate, a = dt / (rc + dt), y = 0f;
            for (int i = 0; i < d.Length; i++) { y += a * (d[i] - y); d[i] = y; }
        }

        static void HighPass(float[] d, float cutoff)
        {
            float rc = 1f / (TwoPi * cutoff), dt = 1f / Rate, a = rc / (rc + dt);
            float prevX = 0f, prevY = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float y = a * (prevY + d[i] - prevX);
                prevX = d[i]; prevY = y; d[i] = y;
            }
        }

        /// <summary>Rend une boucle sans couture en fondant la fin dans le début.</summary>
        static float[] MakeLoop(float[] longer, int loopLen)
        {
            int fade = longer.Length - loopLen;
            var o = new float[loopLen];
            for (int i = 0; i < loopLen; i++) o[i] = longer[i];
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                o[i] = longer[loopLen + i] * (1f - k) + longer[i] * k;
            }
            return o;
        }

        static float Snap(float f, float loopSeconds) => Mathf.Max(1f, Mathf.Round(f * loopSeconds)) / loopSeconds;

        // ------------------------------------------------------------------ SFX

        public static AudioClip Tick()
        {
            var d = Buf(0.03f);
            for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = Noise() * Env(t, 0.001f, 0.006f) * 0.5f; }
            HighPass(d, 2000f);
            return ToClip("tick", d);
        }

        public static AudioClip Swish(float dur = 0.2f)
        {
            var d = Buf(dur);
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, p = t / dur;
                float cutoff = Mathf.Lerp(800f, 4000f, Mathf.Sin(p * Mathf.PI));
                float a = 1f / (1f + Rate / (TwoPi * cutoff));
                y += a * (Noise() - y);
                d[i] = y * Mathf.Sin(p * Mathf.PI) * 0.9f;
            }
            return ToClip("swish", d);
        }

        public static AudioClip Thump(float freq = 110f, float dur = 0.18f)
        {
            var d = Buf(dur);
            float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(freq, freq * 0.5f, t / dur);
                ph += TwoPi * f / Rate;
                d[i] = (Mathf.Sin(ph) * 0.8f + Noise() * 0.25f * Env(t, 0.001f, 0.01f)) * Env(t, 0.002f, dur * 0.3f);
            }
            return ToClip("thump", d);
        }

        public static AudioClip Flip()
        {
            var d = Buf(0.12f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = Noise() * (Env(t, 0.002f, 0.012f) + Env(Mathf.Max(0, t - 0.05f), 0.002f, 0.015f) * (t > 0.05f ? 0.8f : 0f)) * 0.6f;
            }
            HighPass(d, 900f);
            return ToClip("flip", d);
        }

        public static AudioClip Slide()
        {
            var d = Buf(0.3f);
            for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = Noise() * Mathf.Sin(t / 0.3f * Mathf.PI) * 0.5f; }
            LowPass(d, 1800f);
            HighPass(d, 300f);
            return ToClip("slide", d);
        }

        public static AudioClip Bell(float[] freqs, float dur, float decay, float gain = 0.35f)
        {
            var d = Buf(dur);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, s = 0f;
                for (int k = 0; k < freqs.Length; k++)
                    s += Mathf.Sin(TwoPi * freqs[k] * t) * Mathf.Exp(-t / (decay * (1f - k * 0.12f))) / (1 + k * 0.4f);
                d[i] = s * gain * Env(t, 0.004f, 99f);
            }
            return ToClip("bell", d);
        }

        public static AudioClip Arpeggio(float[] freqs, float step, float decay, float gain = 0.3f)
        {
            float dur = step * freqs.Length + decay * 3f;
            var d = Buf(dur);
            for (int n = 0; n < freqs.Length; n++)
            {
                int start = Mathf.RoundToInt(n * step * Rate);
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    d[i] += (Mathf.Sin(TwoPi * freqs[n] * t) + 0.3f * Mathf.Sin(TwoPi * freqs[n] * 2f * t)) * Env(t, 0.01f, decay) * gain;
                }
            }
            return ToClip("arp", d);
        }

        public static AudioClip Cluster()
        {
            var d = Buf(1.3f);
            float[] f = { 98f, 103.8f, 146.8f, 155.6f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, s = 0f;
                foreach (var fr in f) s += (Mathf.Repeat(fr * t, 1f) * 2f - 1f) * 0.25f;
                d[i] = s * Env(t, 0.02f, 0.45f) * 0.7f;
            }
            LowPass(d, 900f);
            return ToClip("cluster", d);
        }

        public static AudioClip Impact()
        {
            var d = Buf(1.2f);
            float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                ph += TwoPi * Mathf.Lerp(80f, 32f, Mathf.Clamp01(t / 0.6f)) / Rate;
                float body = Mathf.Sin(ph) * Env(t, 0.003f, 0.35f);
                float crunch = Mathf.Clamp(Noise() * 3f, -1f, 1f) * Env(t, 0.001f, 0.08f) * 0.5f;
                d[i] = Mathf.Clamp(body * 1.4f + crunch, -1f, 1f) * 0.9f;
            }
            LowPass(d, 2500f);
            return ToClip("impact", d);
        }

        public static AudioClip Heartbeat()
        {
            var d = Buf(1.8f);
            float[] beats = { 0f, 0.16f, 0.85f, 1.01f };
            float[] amps = { 1f, 0.7f, 0.8f, 0.55f };
            for (int b = 0; b < beats.Length; b++)
            {
                int s0 = Mathf.RoundToInt(beats[b] * Rate);
                for (int i = s0; i < d.Length; i++)
                {
                    float t = (i - s0) / (float)Rate;
                    if (t > 0.3f) break;
                    d[i] += Mathf.Sin(TwoPi * Mathf.Lerp(65f, 40f, t / 0.3f) * t) * Env(t, 0.005f, 0.06f) * amps[b];
                }
            }
            LowPass(d, 300f);
            for (int i = 0; i < d.Length; i++) d[i] *= 2.2f;
            return ToClip("heartbeat", d);
        }

        public static AudioClip Static(float dur = 0.6f)
        {
            var d = Buf(dur);
            float gate = 1f;
            for (int i = 0; i < d.Length; i++)
            {
                if (i % 300 == 0) gate = s_rng.NextDouble() < 0.6 ? Random.Range(0.3f, 1f) : 0.05f;
                float t = i / (float)Rate;
                d[i] = Noise() * gate * Env(t, 0.005f, dur * 0.5f) * 0.45f;
            }
            HighPass(d, 1200f);
            return ToClip("static", d);
        }

        public static AudioClip Growl()
        {
            var d = Buf(0.9f);
            float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                ph += Mathf.Lerp(95f, 45f, t / 0.9f) / Rate;
                float saw = Mathf.Repeat(ph, 1f) * 2f - 1f;
                float am = 0.6f + 0.4f * Mathf.Sin(TwoPi * 13f * t);
                d[i] = (saw * am + Noise() * 0.3f) * Env(t, 0.05f, 0.35f) * 0.6f;
            }
            LowPass(d, 700f);
            return ToClip("growl", d);
        }

        public static AudioClip Whisper()
        {
            var d = Buf(1.4f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float flutter = 0.5f + 0.5f * Mathf.Sin(TwoPi * (5f + 3f * Mathf.Sin(t * 2f)) * t);
                d[i] = Noise() * flutter * Mathf.Sin(t / 1.4f * Mathf.PI) * 0.4f;
            }
            HighPass(d, 1500f);
            LowPass(d, 5000f);
            return ToClip("whisper", d);
        }

        public static AudioClip Swell(float[] freqs, float dur, bool falling)
        {
            var d = Buf(dur);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, p = t / dur, s = 0f;
                float bend = falling ? Mathf.Lerp(1f, 0.7f, p * p) : 1f;
                foreach (var f in freqs)
                    s += Mathf.Sin(TwoPi * f * bend * t) + 0.25f * Mathf.Sin(TwoPi * f * 2.003f * bend * t);
                float env = falling ? Env(t, 0.05f, dur * 0.45f) : Mathf.Sin(p * Mathf.PI);
                d[i] = (s / freqs.Length + (falling ? Noise() * 0.15f : 0f)) * env * 0.5f;
            }
            LowPass(d, falling ? 1200f : 4000f);
            return ToClip(falling ? "defeat" : "victory", d);
        }

        /// <summary>Grondement de bâtiment qui craque : bruit très grave + craquements.</summary>
        public static AudioClip Rumble()
        {
            var d = Buf(2.2f);
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                y += 0.01f * (Noise() - y);
                float crack = s_rng.NextDouble() < 0.0015 ? Noise() * 1.5f : 0f;
                d[i] = (y * 6f + Mathf.Sin(TwoPi * 38f * t) * 0.3f) * Env(t, 0.05f, 0.7f) + crack * Env(t, 0.1f, 0.6f);
            }
            LowPass(d, 900f);
            return ToClip("rumble", d);
        }

        /// <summary>Plâtre qui craque puis s'effrite : craquements secs de plus en plus espacés sur un grondement sourd.</summary>
        public static AudioClip Crumble()
        {
            float dur = 1.6f;
            var d = Buf(dur);
            // Grondement grave filtré
            var low = Buf(dur);
            for (int i = 0; i < low.Length; i++) { float t = i / (float)Rate; low[i] = Noise() * Env(t, 0.02f, 0.45f); }
            LowPass(low, 180f);
            LowPass(low, 180f);
            // Craquements : petites salves de bruit aigu, denses au début puis clairsemées (morceaux qui tombent)
            var crack = Buf(dur);
            int n = 70;
            for (int k = 0; k < n; k++)
            {
                float u = (float)s_rng.NextDouble();
                float start = u * u * dur * 0.9f;
                float len = 0.004f + (float)s_rng.NextDouble() * 0.02f;
                float amp = (0.25f + (float)s_rng.NextDouble() * 0.75f) * (1f - start / dur);
                int i0 = (int)(start * Rate), i1 = Mathf.Min(crack.Length, i0 + (int)(len * Rate));
                for (int i = i0; i < i1; i++) crack[i] += Noise() * amp * (1f - (i - i0) / (float)(i1 - i0));
            }
            HighPass(crack, 900f);
            LowPass(crack, 6000f);
            for (int i = 0; i < d.Length; i++) d[i] = low[i] * 2.2f + crack[i] * 0.8f;
            return ToClip("crumble", d);
        }

        /// <summary>Choc d'un gravat sur le bois ou le sol : clic sourd et bref.</summary>
        public static AudioClip Knock()
        {
            var d = Buf(0.09f);
            float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                ph += TwoPi * Mathf.Lerp(520f, 260f, t / 0.09f) / Rate;
                d[i] = (Noise() * Env(t, 0.0005f, 0.006f) * 0.9f + Mathf.Sin(ph) * Env(t, 0.001f, 0.02f) * 0.6f);
            }
            LowPass(d, 2500f);
            return ToClip("knock", d);
        }

        public static AudioClip Puff()
        {
            var d = Buf(0.25f);
            for (int i = 0; i < d.Length; i++) { float t = i / (float)Rate; d[i] = Noise() * Env(t, 0.01f, 0.06f) * 0.5f; }
            LowPass(d, 600f);
            return ToClip("puff", d);
        }

        public static AudioClip Strike()
        {
            var d = Buf(0.5f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float crackle = s_rng.NextDouble() < 0.02 ? Noise() * 2f : 0f;
                d[i] = (Noise() * Env(t, 0.002f, 0.05f) + crackle * Env(t, 0.05f, 0.2f)) * 0.4f;
            }
            HighPass(d, 1500f);
            return ToClip("strike", d);
        }

        public static AudioClip Drop()
        {
            var d = Buf(0.2f);
            float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                ph += TwoPi * Mathf.Lerp(1200f, 380f, Mathf.Clamp01(t / 0.05f)) / Rate;
                d[i] = Mathf.Sin(ph) * Env(t, 0.001f, 0.04f) * 0.5f;
            }
            return ToClip("drop", d);
        }

        public static AudioClip Buzz()
        {
            var d = Buf(0.2f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Mathf.Sin(TwoPi * 110f * t) > 0 ? 0.3f : -0.3f) * Env(t, 0.003f, 0.08f);
            }
            LowPass(d, 1500f);
            return ToClip("buzz", d);
        }

        // ------------------------------------------------------------------ Boucles

        /// <summary>Nappe sombre (musique de fond), bouclable.</summary>
        public static AudioClip DroneLoop(float seconds = 16f)
        {
            int loop = Mathf.RoundToInt(seconds * Rate);
            var d = new float[loop + Rate];
            float[] f = { Snap(55f, seconds), Snap(55.3f, seconds), Snap(82.4f, seconds), Snap(110.4f, seconds), Snap(164.8f, seconds) };
            float lfo = Snap(0.125f, seconds);
            var rng = new System.Random(7);
            float noiseY = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, s = 0f;
                for (int k = 0; k < f.Length; k++)
                    s += Mathf.Sin(TwoPi * f[k] * t + k) * (0.6f + 0.4f * Mathf.Sin(TwoPi * lfo * (k + 1) * 0.5f * t + k)) / (k + 1.5f);
                noiseY += 0.02f * ((float)(rng.NextDouble() * 2 - 1) - noiseY);
                d[i] = s * 0.4f + noiseY * 0.6f;
            }
            // Quelques notes de piano désaccordé, éparses
            float[] notes = { 220f, 207.7f, 174.6f, 261.6f, 196f };
            for (int n = 0; n < 5; n++)
            {
                int start = Mathf.RoundToInt((1.5f + n * 3.1f) * Rate);
                float fr = notes[n];
                for (int i = start; i < d.Length && i < start + Rate * 3; i++)
                {
                    float t = (i - start) / (float)Rate;
                    d[i] += (Mathf.Sin(TwoPi * fr * t) + 0.4f * Mathf.Sin(TwoPi * fr * 2.01f * t) + 0.2f * Mathf.Sin(TwoPi * fr * 3.02f * t)) * Env(t, 0.005f, 0.9f) * 0.12f;
                }
            }
            return ToClip("drone", MakeLoop(d, loop), false);
        }

        /// <summary>Couche d'angoisse (volume piloté par la Lucidité perdue).</summary>
        public static AudioClip DreadLoop(float seconds = 8f)
        {
            int loop = Mathf.RoundToInt(seconds * Rate);
            var d = new float[loop + Rate / 2];
            float a = Snap(466.2f, seconds), b = Snap(493.9f, seconds), trem = Snap(6f, seconds), low = Snap(41.2f, seconds);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float tr = 0.7f + 0.3f * Mathf.Sin(TwoPi * trem * t);
                d[i] = (Mathf.Sin(TwoPi * a * t) + Mathf.Sin(TwoPi * b * t)) * 0.08f * tr + Mathf.Sin(TwoPi * low * t) * 0.25f;
            }
            return ToClip("dread", MakeLoop(d, loop), false);
        }

        /// <summary>Ambiance de pièce (bruit brun + craquements).</summary>
        public static AudioClip RoomToneLoop(float seconds = 10f)
        {
            int loop = Mathf.RoundToInt(seconds * Rate);
            var d = new float[loop + Rate];
            float brown = 0f;
            var rng = new System.Random(3);
            for (int i = 0; i < d.Length; i++)
            {
                brown = Mathf.Clamp(brown + (float)(rng.NextDouble() * 2 - 1) * 0.02f, -1f, 1f) * 0.998f;
                d[i] = brown * 0.5f;
            }
            // Gouttes d'eau / craquements
            for (int n = 0; n < 6; n++)
            {
                int start = rng.Next(0, loop - Rate / 4);
                float fr = 600f + (float)rng.NextDouble() * 900f;
                for (int i = start; i < start + Rate / 5; i++)
                {
                    float t = (i - start) / (float)Rate;
                    d[i] += Mathf.Sin(TwoPi * fr * (1f - t * 2f) * t) * Env(t, 0.001f, 0.03f) * 0.15f;
                }
            }
            return ToClip("roomtone", MakeLoop(d, loop), false);
        }

        /// <summary>Bourdonnement électrique de l'ampoule.</summary>
        public static AudioClip HumLoop(float seconds = 2f)
        {
            int loop = Mathf.RoundToInt(seconds * Rate);
            var d = new float[loop];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                d[i] = Mathf.Sin(TwoPi * 50f * t) * 0.3f + Mathf.Sin(TwoPi * 100f * t) * 0.25f + Mathf.Sin(TwoPi * 150f * t) * 0.12f
                     + Mathf.Sin(TwoPi * 250f * t) * 0.05f;
            }
            return ToClip("hum", d, false);
        }
    }
}
