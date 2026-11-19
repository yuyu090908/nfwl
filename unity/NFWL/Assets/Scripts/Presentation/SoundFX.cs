using UnityEngine;

namespace NFWL
{
    /// <summary>
    /// PHASE 13: match sound, all synthesised at start-up (no audio files, nothing copied):
    /// referee whistle, kicks / touches / bounces scaled by ball speed, post and crossbar clang,
    /// net swish, a looping crowd bed whose level follows the danger of the play, a cheer for
    /// goals and an "ooh" for saves and near misses. Event sounds are panned by where they happen
    /// relative to the camera. These are procedural placeholders: real recorded sound would need assets.
    /// M toggles the sound.
    /// </summary>
    public class SoundFX : MonoBehaviour
    {
        const int SR = 22050;

        AudioClip whistle, whistleLong, kick, bounce, post, net, crowd, cheer, ooh;
        AudioSource[] pool;
        int next;
        AudioSource crowdSrc;
        float excitement, cheerLevel;
        public bool Muted { get; private set; }
        public float master = 0.8f;

        public static SoundFX Create(Transform parent)
        {
            SoundFX fx = new GameObject("SoundFX").AddComponent<SoundFX>();
            fx.transform.SetParent(parent, false);
            fx.Build();
            return fx;
        }

        void Build()
        {
            whistle = Whistle("Whistle", new[] { 0.45f });
            whistleLong = Whistle("WhistleEnd", new[] { 0.35f, 0.35f, 0.9f });
            kick = Thud("Kick", 0.2f, 150f, 55f, 26f, 0.5f);
            bounce = Thud("Bounce", 0.12f, 210f, 95f, 34f, 0.15f);
            post = Clang();
            net = Noise("Net", 0.55f, 0.35f, 0.02f, 0.5f, 0f);
            crowd = CrowdLoop();
            cheer = Noise("Cheer", 3.2f, 0.18f, 0.35f, 2.8f, 0.6f);
            ooh = Ooh();

            pool = new AudioSource[6];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0f;
            }
            crowdSrc = gameObject.AddComponent<AudioSource>();
            crowdSrc.clip = crowd;
            crowdSrc.loop = true;
            crowdSrc.volume = 0.15f;
            crowdSrc.spatialBlend = 0f;
            crowdSrc.Play();
        }

        // ------------------------------------------------------------------ playing

        void Play(AudioClip clip, float volume, float pitch, Vector3 at)
        {
            if (Muted || clip == null || volume <= 0.001f) return;
            AudioSource s = pool[next];
            next = (next + 1) % pool.Length;
            s.pitch = pitch;
            Camera cam = Camera.main;
            s.panStereo = cam != null ? Mathf.Clamp((at.x - cam.transform.position.x) / 18f, -0.8f, 0.8f) : 0f;
            s.PlayOneShot(clip, Mathf.Clamp01(volume * master));
        }

        public void Whistle(bool end = false) => Play(end ? whistleLong : whistle, 0.55f, 1f, Camera.main != null ? Camera.main.transform.position : Vector3.zero);

        /// <summary>Ball.Impact: "Kick", "Touch", "Bounce", "Post", "Net".</summary>
        public void OnBallImpact(string kind, float speed, Vector3 at)
        {
            switch (kind)
            {
                case "Kick": Play(kick, Mathf.Lerp(0.25f, 0.9f, speed / 30f), Mathf.Lerp(1.15f, 0.85f, speed / 30f) + Random.Range(-0.04f, 0.04f), at); break;
                case "Touch": Play(kick, Mathf.Lerp(0.08f, 0.3f, speed / 12f), 1.35f + Random.Range(-0.06f, 0.06f), at); break;
                case "Bounce": Play(bounce, Mathf.Lerp(0.1f, 0.45f, speed / 20f), 1f + Random.Range(-0.05f, 0.05f), at); break;
                case "Post": Play(post, Mathf.Lerp(0.4f, 1f, speed / 25f), 1f, at); excitement = 1f; Ooh(at); break;
                case "Net": Play(net, Mathf.Lerp(0.3f, 0.8f, speed / 25f), 1f, at); break;
            }
        }

        public void Goal()
        {
            if (Muted) return;
            cheerLevel = 1f;
            Play(cheer, 0.9f, 1f, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }

        public void Ooh(Vector3 at) => Play(ooh, 0.55f, Random.Range(0.95f, 1.05f), at);

        public void ToggleMute()
        {
            Muted = !Muted;
            if (crowdSrc != null) crowdSrc.mute = Muted;
        }

        /// <summary>Danger 0..1 (ball near a goal, moving fast toward it): the crowd rises with it.</summary>
        public void SetDanger(float danger)
        {
            excitement = Mathf.Max(excitement * 0.995f, danger);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            excitement = Mathf.MoveTowards(excitement, 0f, dt * 0.25f);
            cheerLevel = Mathf.MoveTowards(cheerLevel, 0f, dt * 0.2f);
            if (crowdSrc != null)
            {
                float target = (0.13f + 0.22f * excitement + 0.25f * cheerLevel) * master;
                crowdSrc.volume = Mathf.MoveTowards(crowdSrc.volume, target, dt * 0.5f);
                crowdSrc.pitch = 1f + 0.06f * excitement;
            }
        }

        // ------------------------------------------------------------------ synthesis

        static AudioClip Make(string name, float[] data)
        {
            AudioClip c = AudioClip.Create(name, data.Length, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Pea whistle: ~2.9 kHz tone with a fast warble from the pea, one or more blasts.</summary>
        static AudioClip Whistle(string name, float[] blasts)
        {
            float gap = 0.12f;
            float total = 0f;
            foreach (float b in blasts) total += b + gap;
            float[] d = new float[(int)(total * SR)];
            System.Random rng = new System.Random(7);
            int pos = 0;
            float phase = 0f;
            foreach (float b in blasts)
            {
                int n = (int)(b * SR);
                for (int i = 0; i < n && pos + i < d.Length; i++)
                {
                    float t = i / (float)SR;
                    float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((b - t) / 0.05f);
                    float f = 2900f + 120f * Mathf.Sin(2f * Mathf.PI * 32f * t);
                    phase += 2f * Mathf.PI * f / SR;
                    float tone = Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 2f) * 0.12f;
                    float breath = ((float)rng.NextDouble() * 2f - 1f) * 0.06f;
                    d[pos + i] = (tone + breath) * env * 0.6f;
                }
                pos += n + (int)(gap * SR);
            }
            return Make(name, d);
        }

        /// <summary>Boot on ball: a pitch-dropping low thump plus a short leather click.</summary>
        static AudioClip Thud(string name, float len, float f0, float f1, float decay, float click)
        {
            float[] d = new float[(int)(len * SR)];
            System.Random rng = new System.Random(11);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(f1, f0, Mathf.Exp(-t * 40f));
                phase += 2f * Mathf.PI * f / SR;
                float body = Mathf.Sin(phase) * Mathf.Exp(-t * decay);
                float c = t < 0.008f ? ((float)rng.NextDouble() * 2f - 1f) * click * (1f - t / 0.008f) : 0f;
                d[i] = (body * 0.8f + c) * 0.9f;
            }
            return Make(name, d);
        }

        /// <summary>Metal post: inharmonic partials with different decays.</summary>
        static AudioClip Clang()
        {
            float[] f = { 523f, 1391f, 2412f, 3530f, 4900f };
            float[] k = { 3.5f, 5f, 7f, 9f, 14f };
            float[] a = { 0.5f, 0.35f, 0.25f, 0.15f, 0.08f };
            float[] d = new float[(int)(1.0f * SR)];
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float s = 0f;
                for (int j = 0; j < f.Length; j++) s += Mathf.Sin(2f * Mathf.PI * f[j] * t) * a[j] * Mathf.Exp(-t * k[j]);
                d[i] = s * 0.6f * Mathf.Clamp01(t / 0.002f);
            }
            return Make("Post", d);
        }

        /// <summary>Filtered noise with an attack / decay envelope (net swish, cheer).</summary>
        static AudioClip Noise(string name, float len, float cutoff, float attack, float decayTime, float swell)
        {
            float[] d = new float[(int)(len * SR)];
            System.Random rng = new System.Random(name.GetHashCode());
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float w = (float)rng.NextDouble() * 2f - 1f;
                lp += (w - lp) * cutoff;
                lp2 += (lp - lp2) * cutoff;
                float env = Mathf.Clamp01(t / Mathf.Max(attack, 0.001f)) * Mathf.Exp(-Mathf.Max(0f, t - attack) / decayTime * 2.2f);
                float mod = 1f + swell * 0.3f * Mathf.Sin(2f * Mathf.PI * 2.3f * t) * Mathf.Sin(2f * Mathf.PI * 0.7f * t);
                d[i] = lp2 * env * mod * 2.2f;
            }
            return Make(name, d);
        }

        /// <summary>Crowd bed: dark noise with slow swells, cross-faded so it loops without a click.</summary>
        static AudioClip CrowdLoop()
        {
            float len = 6f, fade = 0.5f;
            int n = (int)(len * SR), nf = (int)(fade * SR);
            float[] raw = new float[n + nf];
            System.Random rng = new System.Random(99);
            float lp = 0f, lp2 = 0f, hp = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SR;
                float w = (float)rng.NextDouble() * 2f - 1f;
                lp += (w - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.08f;
                hp += (lp2 - hp) * 0.002f; // remove rumble
                float swell = 0.75f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.21f * t) + 0.1f * Mathf.Sin(2f * Mathf.PI * 0.53f * t + 1f);
                raw[i] = (lp2 - hp) * swell * 3f;
            }
            float[] d = new float[n];
            for (int i = 0; i < n; i++) d[i] = raw[i];
            for (int i = 0; i < nf; i++)
            {
                float f = i / (float)nf;
                d[i] = raw[i] * f + raw[n + i] * (1f - f);
            }
            return Make("Crowd", d);
        }

        /// <summary>"Ooh": noise through a resonance around a vowel formant, rising then falling.</summary>
        static AudioClip Ooh()
        {
            float len = 1.4f;
            float[] d = new float[(int)(len * SR)];
            System.Random rng = new System.Random(5);
            float y1 = 0f, y2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float fc = 380f + 90f * Mathf.Sin(Mathf.PI * t / len);
                float r = 0.995f;
                float w0 = 2f * Mathf.PI * fc / SR;
                float x = (float)rng.NextDouble() * 2f - 1f;
                float y = x * (1f - r) + 2f * r * Mathf.Cos(w0) * y1 - r * r * y2;
                y2 = y1; y1 = y;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / len)) * Mathf.Clamp01(t / 0.15f);
                d[i] = Mathf.Clamp(y * env * 1.2f, -1f, 1f);
            }
            return Make("Ooh", d);
        }
    }
}
