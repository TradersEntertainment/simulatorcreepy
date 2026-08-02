// Every sound in the game, synthesized at startup. No audio files ship with the project.
//
// The rule is the same one that governs the meshes and the HUD: nothing is authored in an
// editor, everything is built from code at runtime. A clip here is a short function of time
// written into a float buffer, so adding a sound is a method, not an asset import.
//
// The design asks for two things the mixer has to support and a one-shot bank cannot:
//   · a low string drone whose dissonance rises with total grievance
//   · a crowd murmur bed that swells before unrest
// Both are looping voices whose pitch and gain are driven every frame from GameState, which is
// the one place in this project allowed to read the truth directly — the ear is not a report,
// and a minister cannot lie about the sound of a crowd outside the window.

using UnityEngine;

namespace Mesruiyet.Core
{
    public sealed class AudioBus : MonoBehaviour
    {
        public static AudioBus Instance { get; private set; }

        const int Rate = 44100;

        // ---- the bank
        AudioClip _stamp;        // a decree, signed
        AudioClip _coin;         // money moving
        AudioClip _hammer;       // construction
        AudioClip _rustle;       // the newspaper / the turn report
        AudioClip _bell;         // a crisis arrives
        AudioClip _tick;         // a minister's telegram
        AudioClip _drum;         // the coup countdown
        AudioClip _drone;        // looping: the city's mood
        AudioClip _murmur;       // looping: the crowd

        // ---- the buses
        AudioSource _sfx;
        AudioSource _droneVoice;
        AudioSource _murmurVoice;

        GameState _state;

        [Range(0f, 1f)] public float Master = 0.7f;
        public bool Muted { get; private set; }

        /// <summary>Exposed for the agent bridge: audio cannot be verified by listening in CI.</summary>
        public float DroneLevel { get; private set; }
        public float MurmurLevel { get; private set; }
        public int ClipCount { get; private set; }

        public void Bind(GameState state) => _state = state;

        void Awake()
        {
            Instance = this;
            Build();
            Wire();
        }

        // ---------------------------------------------------------------- synthesis

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> voice)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = Mathf.Clamp(voice(t, seconds), -1f, 1f);
            }

            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Exponential decay, the shape almost every percussive sound in here wants.</summary>
        static float Decay(float t, float rate) => Mathf.Exp(-t * rate);

        static float Noise(float t) => Mathf.PerlinNoise(t * 7000f, 0.37f) * 2f - 1f;

        void Build()
        {
            // A stamp is a low thud plus the wooden knock of the handle hitting the desk.
            _stamp = Make("stamp", 0.28f, (t, d) =>
                Mathf.Sin(t * 2f * Mathf.PI * 90f) * Decay(t, 26f) * 0.9f +
                Noise(t) * Decay(t, 60f) * 0.35f);

            // Two struck partials, slightly detuned, which is what makes metal sound like metal.
            _coin = Make("coin", 0.5f, (t, d) =>
                (Mathf.Sin(t * 2f * Mathf.PI * 2400f) * 0.6f +
                 Mathf.Sin(t * 2f * Mathf.PI * 3170f) * 0.4f) * Decay(t, 11f) * 0.5f);

            _hammer = Make("hammer", 0.22f, (t, d) =>
                Noise(t) * Decay(t, 42f) * 0.5f +
                Mathf.Sin(t * 2f * Mathf.PI * 220f) * Decay(t, 34f) * 0.4f);

            // Paper is filtered noise with an envelope that opens and closes rather than snapping.
            _rustle = Make("rustle", 0.42f, (t, d) =>
                Noise(t * 1.6f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / d)) * 0.32f);

            // A bell is a hum tone plus a minor third above it — the interval does the dread.
            _bell = Make("bell", 2.4f, (t, d) =>
                (Mathf.Sin(t * 2f * Mathf.PI * 320f) * 0.5f +
                 Mathf.Sin(t * 2f * Mathf.PI * 381f) * 0.3f +
                 Mathf.Sin(t * 2f * Mathf.PI * 640f) * 0.2f) * Decay(t, 1.7f) * 0.45f);

            _tick = Make("tick", 0.07f, (t, d) =>
                Mathf.Sin(t * 2f * Mathf.PI * 1800f) * Decay(t, 120f) * 0.3f);

            _drum = Make("drum", 0.6f, (t, d) =>
                Mathf.Sin(t * 2f * Mathf.PI * (58f - t * 18f)) * Decay(t, 6f) * 0.75f +
                Noise(t) * Decay(t, 30f) * 0.12f);

            // ---- the two looping voices
            //
            // The drone is written as a perfect fifth. The dissonance the design asks for is not
            // baked in: it is applied at play time by detuning the whole clip against a second
            // pitch, so one clip covers a calm city and a city about to burn.
            _drone = Make("drone", 4f, (t, d) =>
            {
                float a = Mathf.Sin(t * 2f * Mathf.PI * 55f);
                float b = Mathf.Sin(t * 2f * Mathf.PI * 82.5f);
                float swell = 0.75f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * 0.25f);
                return (a * 0.6f + b * 0.4f) * swell * 0.5f;
            });

            // A crowd is many voices none of which you can pick out: band-limited noise wobbling
            // slowly, with no pitch centre to catch the ear.
            _murmur = Make("murmur", 6f, (t, d) =>
            {
                float body = Mathf.PerlinNoise(t * 40f, 1.7f) * 2f - 1f;
                float wobble = 0.6f + 0.4f * Mathf.PerlinNoise(t * 1.3f, 5.1f);
                return body * wobble * 0.4f;
            });

            ClipCount = 9;
        }

        void Wire()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;

            _droneVoice = gameObject.AddComponent<AudioSource>();
            _droneVoice.clip = _drone;
            _droneVoice.loop = true;
            _droneVoice.spatialBlend = 0f;
            _droneVoice.volume = 0f;
            _droneVoice.Play();

            _murmurVoice = gameObject.AddComponent<AudioSource>();
            _murmurVoice.clip = _murmur;
            _murmurVoice.loop = true;
            _murmurVoice.spatialBlend = 0f;
            _murmurVoice.volume = 0f;
            _murmurVoice.Play();
        }

        // ---------------------------------------------------------------- the one-shots

        void Shot(AudioClip clip, float gain, float pitch = 1f)
        {
            if (Muted || clip == null || _sfx == null) return;
            _sfx.pitch = pitch;
            _sfx.PlayOneShot(clip, gain * Master);
        }

        public void Stamp()      => Shot(_stamp, 0.9f);
        public void Coin()       => Shot(_coin, 0.5f);
        public void Hammer()     => Shot(_hammer, 0.7f, Random.Range(0.92f, 1.09f));
        public void Rustle()     => Shot(_rustle, 0.6f);
        public void Bell()       => Shot(_bell, 0.8f);
        public void Telegraph()  => Shot(_tick, 0.45f, Random.Range(0.96f, 1.05f));
        public void Drum()       => Shot(_drum, 0.85f);

        public void SetMuted(bool muted)
        {
            Muted = muted;
            if (_droneVoice != null) _droneVoice.mute = muted;
            if (_murmurVoice != null) _murmurVoice.mute = muted;
            if (_sfx != null) _sfx.mute = muted;
        }

        public void ToggleMute() => SetMuted(!Muted);

        // ---------------------------------------------------------------- the ambience

        void Update()
        {
            // M is the mute toggle. Deliberately not on the HUD: the design's bottom bar is
            // already at its density limit and this is the one control a player sets once.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame) ToggleMute();

            if (_state == null || _droneVoice == null) return;

            // Reads GameState rather than Reporting, deliberately. The ministers shade the
            // figures on the desk; they do not get to shade the noise coming off the street.
            float grievance = _state.AverageGrievance / 100f;
            float worst = 0f;
            foreach (var d in _state.Districts) worst = Mathf.Max(worst, d.Grievance);
            worst /= 100f;

            // The drone is always there and always quiet. Its dissonance, not its volume, is the
            // channel: a calm city sits on a clean fifth, an angry one drifts a semitone off it.
            DroneLevel = Mathf.Lerp(DroneLevel, 0.10f + grievance * 0.14f, Time.deltaTime * 0.7f);
            _droneVoice.volume = DroneLevel * Master;
            _droneVoice.pitch = 1f - grievance * 0.06f;

            // The crowd is silent until a district is genuinely close to acting, then it swells
            // under everything else. It arrives before the unrest event does, which is the point.
            float target = worst < 0.45f ? 0f : Mathf.InverseLerp(0.45f, 0.9f, worst) * 0.34f;
            MurmurLevel = Mathf.Lerp(MurmurLevel, target, Time.deltaTime * 0.5f);
            _murmurVoice.volume = MurmurLevel * Master;
            _murmurVoice.pitch = 0.9f + worst * 0.2f;
        }
    }
}
