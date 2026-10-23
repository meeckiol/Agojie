using System.Collections.Generic;
using UnityEngine;

namespace Agojie.Audio
{
    /// <summary>
    /// Fabrique les sons avec des maths : un son = un tableau de nombres entre -1 et 1
    /// (22 050 nombres par seconde). Sinus = note pure, bruit aléatoire = souffle/impact.
    /// La musique reprend la cloche gankogui et les tambours des rythmes fon en 12/8.
    /// </summary>
    public static class ProceduralSounds
    {
        const int Rate = 22050;
        const float TwoPi = Mathf.PI * 2f;
        static readonly System.Random rng = new System.Random(1897);

        public static Dictionary<Sfx, AudioClip> CreateAll()
        {
            return new Dictionary<Sfx, AudioClip>
            {
                { Sfx.Step, Make("pas", Step()) },
                { Sfx.Swing, Make("lame", Whoosh(0.3f)) },
                { Sfx.Dodge, Make("esquive", Whoosh(0.22f)) },
                { Sfx.Hit, Make("coup", Impact(0.25f, 170f, 60f, 1.2f, 16f)) },
                { Sfx.HeavyHit, Make("coup_lourd", Impact(0.5f, 120f, 40f, 1.5f, 9f)) },
                { Sfx.Assassinate, Make("assassinat", Assassinate()) },
                { Sfx.Hurt, Make("blessure", Voice(0.35f, 260f, 120f, 8f)) },
                { Sfx.Death, Make("mort", Voice(0.9f, 200f, 140f, 3f)) },
                { Sfx.Alert, Make("alerte", Alert()) },
                { Sfx.Pickup, Make("objet", Pickup()) },
                { Sfx.QuestStart, Make("mission", Horn()) },
                { Sfx.QuestComplete, Make("victoire", Bells()) },
            };
        }

        // ------------------------------------------------------------ Bruitages
        static float[] Step()
        {
            float[] d = Buffer(0.12f);
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = Seconds(i);
                low += 0.12f * (Noise() - low); // filtre passe-bas : bruit plus sourd
                d[i] = low * Mathf.Exp(-t * 45f) * 3f + Mathf.Sin(TwoPi * 85f * t) * Mathf.Exp(-t * 40f) * 0.6f;
            }
            return d;
        }

        static float[] Whoosh(float duration)
        {
            float[] d = Buffer(duration);
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float env = Mathf.Sin(Mathf.PI * Seconds(i) / duration);
                env *= env;
                low += (0.05f + 0.35f * env) * (Noise() - low); // le souffle devient plus aigu au milieu
                d[i] = low * env;
            }
            return d;
        }

        static float[] Impact(float duration, float startFreq, float endFreq, float noise, float decay)
        {
            float[] d = Buffer(duration);
            float phase = 0f;
            float low = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = Seconds(i);
                float freq = endFreq + (startFreq - endFreq) * Mathf.Exp(-t * 25f); // la note tombe : "boum"
                phase += TwoPi * freq / Rate;
                low += 0.3f * (Noise() - low);
                d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * decay) + low * noise * Mathf.Exp(-t * decay * 2.5f);
            }
            return d;
        }

        static float[] Assassinate()
        {
            float[] d = Impact(0.45f, 150f, 50f, 1f, 12f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = Seconds(i);
                float metal = Mathf.Sin(TwoPi * 2400f * t) + 0.6f * Mathf.Sin(TwoPi * 3650f * t) + 0.4f * Mathf.Sin(TwoPi * 5100f * t);
                d[i] += metal * Mathf.Exp(-t * 9f) * 0.25f; // le "tchiiing" de la lame
            }
            return d;
        }

        static float[] Voice(float duration, float startFreq, float drop, float decay)
        {
            float[] d = Buffer(duration);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = Seconds(i);
                float freq = startFreq - drop * t / duration + 12f * Mathf.Sin(TwoPi * 30f * t);
                phase += TwoPi * freq / Rate;
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t * decay);
                d[i] = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(2f * phase)) * env + Noise() * 0.08f * env;
            }
            return d;
        }

        static float[] Alert()
        {
            float[] d = Buffer(0.5f);
            AddTone(d, 0f, 0.18f, 620f);
            AddTone(d, 0.2f, 0.25f, 930f);
            return d;
        }

        static float[] Pickup()
        {
            float[] d = Buffer(0.4f);
            float[] notes = { 880f, 1320f, 1760f };
            for (int n = 0; n < notes.Length; n++)
                AddBell(d, n * 0.07f, notes[n], 0.6f, 14f);
            return d;
        }

        static float[] Horn()
        {
            float[] d = Buffer(1.1f);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = Seconds(i);
                float freq = t < 0.45f ? 196f : 294f;
                phase += TwoPi * freq / Rate;
                float tone = 0f;
                for (int h = 1; h <= 6; h++) tone += Mathf.Sin(h * phase) / h; // timbre de corne
                float env = Mathf.Min(1f, t / 0.05f) * Mathf.Clamp01((1.1f - t) / 0.25f);
                d[i] = tone * env;
            }
            return d;
        }

        static float[] Bells()
        {
            float[] d = Buffer(1.6f);
            float[] notes = { 523f, 659f, 784f, 1047f };
            float[] starts = { 0f, 0.18f, 0.36f, 0.6f };
            for (int n = 0; n < notes.Length; n++)
                AddBell(d, starts[n], notes[n], 1f, 4f);
            return d;
        }

        // ------------------------------------------------------------ Musique
        /// <summary>Boucle de 4 mesures en 12/8 : cloche gankogui, tambour grave, hochet.</summary>
        public static AudioClip CreateAmbientLoop()
        {
            const float pulse = 0.14f;     // durée d'une croche
            const int pulsesPerBar = 12;
            const int bars = 4;
            float[] d = Buffer(pulse * pulsesPerBar * bars);
            int[] bellPattern = { 0, 2, 4, 5, 7, 9, 11 }; // la "timeline" standard ouest-africaine

            for (int bar = 0; bar < bars; bar++)
            {
                float barStart = bar * pulsesPerBar * pulse;
                foreach (int p in bellPattern)
                    AddBell(d, barStart + p * pulse, p == 0 ? 640f : 860f, 0.35f, 9f);

                AddDrum(d, barStart, 0.9f);
                AddDrum(d, barStart + 6 * pulse, 0.7f);
                if (bar == bars - 1)
                {
                    AddDrum(d, barStart + 9 * pulse, 0.5f);
                    AddDrum(d, barStart + 10 * pulse, 0.6f);
                    AddDrum(d, barStart + 11 * pulse, 0.7f);
                }
                for (int p = 0; p < pulsesPerBar; p++)
                    AddShaker(d, barStart + p * pulse, p % 3 == 0 ? 0.25f : 0.12f);
            }
            return Make("ambiance_abomey", d, 0.7f);
        }

        // ------------------------------------------------------------ Briques
        static void AddTone(float[] d, float start, float duration, float freq)
        {
            int from = Mathf.RoundToInt(start * Rate);
            int count = Mathf.RoundToInt(duration * Rate);
            for (int i = 0; i < count && from + i < d.Length; i++)
            {
                float t = Seconds(i);
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Clamp01((duration - t) / 0.05f);
                float x = TwoPi * freq * t;
                d[from + i] += (Mathf.Sin(x) + 0.35f * Mathf.Sin(3f * x)) * env;
            }
        }

        // Cloche : fréquences "non harmoniques" (x2.76, x5.4) = son métallique.
        // L'index boucle (modulo) pour que la musique se répète sans coupure.
        static void AddBell(float[] d, float start, float freq, float amp, float decay)
        {
            int from = Mathf.RoundToInt(start * Rate);
            int count = Mathf.RoundToInt(Mathf.Min(1.2f, 6f / decay) * Rate);
            for (int i = 0; i < count; i++)
            {
                float t = Seconds(i);
                float x = TwoPi * freq * t;
                float s = Mathf.Sin(x) + 0.5f * Mathf.Sin(2.76f * x) + 0.25f * Mathf.Sin(5.4f * x);
                d[(from + i) % d.Length] += s * amp * Mathf.Exp(-t * decay);
            }
        }

        static void AddDrum(float[] d, float start, float amp)
        {
            int from = Mathf.RoundToInt(start * Rate);
            int count = Mathf.RoundToInt(0.4f * Rate);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = Seconds(i);
                phase += TwoPi * (55f + 90f * Mathf.Exp(-t * 30f)) / Rate;
                d[(from + i) % d.Length] += Mathf.Sin(phase) * Mathf.Exp(-t * 9f) * amp * 1.4f;
            }
        }

        static void AddShaker(float[] d, float start, float amp)
        {
            int from = Mathf.RoundToInt(start * Rate);
            int count = Mathf.RoundToInt(0.06f * Rate);
            float low = 0f;
            for (int i = 0; i < count; i++)
            {
                float n = Noise();
                low += 0.2f * (n - low);
                float high = n - low; // passe-haut : il ne reste que le "tchk"
                d[(from + i) % d.Length] += high * Mathf.Exp(-Seconds(i) * 70f) * amp;
            }
        }

        static AudioClip Make(string name, float[] data, float peak = 0.9f)
        {
            float max = 0.0001f;
            foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
            for (int i = 0; i < data.Length; i++) data[i] = data[i] / max * peak;

            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
        static float Seconds(int sample) => sample / (float)Rate;
        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
    }
}
