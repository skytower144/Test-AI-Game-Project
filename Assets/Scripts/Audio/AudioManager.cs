using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 효과음(풀링된 AudioSource 보이스)과 루프 사운드를 재생한다.
    /// 각 사운드의 볼륨과 중복재생 정책(동시 재생 수, 초과 시 처리, 최소 간격)은 SoundLibrary에서 정의한다.
    /// 루프는 필요 시 두 개의 소스를 교차 페이드해 MP3 앞뒤 무음으로 인한 끊김 없이 이어 붙인다.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private SoundLibrary library;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float loopVolume = 1f;
        [Tooltip("동시에 재생 가능한 효과음 보이스 수")]
        [SerializeField, Range(4, 64)] private int voiceCount = 28;
        [Tooltip("시작 시 오디오 데이터를 미리 로드해 첫 재생 끊김을 방지")]
        [SerializeField] private bool preloadClips = true;

        private sealed class Voice
        {
            public AudioSource source;
            public SoundId id;
            public float startTime;
        }

        private sealed class LoopChannel
        {
            public readonly AudioSource[] sources = new AudioSource[2];
            public int active;
            public SoundDefinition definition;
            public float gain;
            public float targetGain;
            public float fadeSpeed;
            public float pitch = 1f;
            public bool playing;
            public bool stopWhenSilent;
            public bool crossfading;

            public AudioSource Active => sources[active];
            public AudioSource Standby => sources[1 - active];
        }

        private static AudioManager s_Instance;

        private Voice[] voices;
        private readonly Dictionary<SoundId, float> lastPlayTimes = new Dictionary<SoundId, float>();
        private readonly Dictionary<SoundId, LoopChannel> loops = new Dictionary<SoundId, LoopChannel>();
        private readonly List<LoopChannel> loopList = new List<LoopChannel>();

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;

            voices = new Voice[voiceCount];
            for (int i = 0; i < voices.Length; i++)
                voices[i] = new Voice { source = CreateSource("Voice " + i) };

            if (preloadClips && library != null)
            {
                IReadOnlyList<SoundDefinition> sounds = library.Sounds;
                for (int i = 0; i < sounds.Count; i++)
                {
                    AudioClip clip = sounds[i] != null ? sounds[i].clip : null;
                    if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                        clip.LoadAudioData();
                }
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            s_Instance = null;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            return source;
        }

        // ───────────── 효과음 ─────────────

        /// <summary>효과음을 1회 재생한다. 중복재생 정책에 따라 무시되거나 기존 재생을 대체할 수 있다.</summary>
        public static void Play(SoundId id, float volumeScale = 1f)
        {
            if (s_Instance != null) s_Instance.PlayInternal(id, volumeScale);
        }

        private void PlayInternal(SoundId id, float volumeScale)
        {
            if (library == null || !library.TryGet(id, out SoundDefinition def) || def.clip == null) return;

            float now = Time.unscaledTime;
            if (def.minInterval > 0f && lastPlayTimes.TryGetValue(id, out float last) && now - last < def.minInterval)
                return;

            int free = -1;
            int sameCount = 0, sameOldest = -1, anyOldest = 0;
            float sameOldestTime = float.MaxValue, anyOldestTime = float.MaxValue;

            for (int i = 0; i < voices.Length; i++)
            {
                Voice v = voices[i];
                if (!v.source.isPlaying)
                {
                    if (free < 0) free = i;
                    continue;
                }

                if (v.startTime < anyOldestTime) { anyOldestTime = v.startTime; anyOldest = i; }
                if (v.id == id)
                {
                    sameCount++;
                    if (v.startTime < sameOldestTime) { sameOldestTime = v.startTime; sameOldest = i; }
                }
            }

            int index;
            if (sameCount >= Mathf.Max(1, def.maxVoices))
            {
                if (def.overflow == SoundOverflowPolicy.SkipNew) return;
                index = sameOldest;
            }
            else
            {
                index = free >= 0 ? free : anyOldest;
            }

            Voice voice = voices[index];
            AudioSource source = voice.source;
            source.Stop();
            source.clip = def.clip;
            source.volume = Mathf.Clamp01(def.volume * volumeScale * effectsVolume * masterVolume);
            source.pitch = Random.Range(Mathf.Min(def.pitchRange.x, def.pitchRange.y), Mathf.Max(def.pitchRange.x, def.pitchRange.y));
            // 앞쪽 무음(MP3 인코더 지연 등)을 건너뛰어 입력/타격과 소리가 어긋나지 않게 한다
            source.time = Mathf.Clamp(def.startOffset, 0f, Mathf.Max(0f, def.clip.length - 0.01f));
            source.Play();

            voice.id = id;
            voice.startTime = now;
            lastPlayTimes[id] = now;
        }

        // ───────────── 루프 ─────────────

        /// <summary>루프 사운드를 재생한다. 이미 재생 중이면 볼륨만 복구한다.</summary>
        public static void PlayLoop(SoundId id, float fadeIn = 0f)
        {
            if (s_Instance != null) s_Instance.PlayLoopInternal(id, fadeIn);
        }

        public static void StopLoop(SoundId id, float fadeOut = 0f)
        {
            if (s_Instance != null && s_Instance.loops.TryGetValue(id, out LoopChannel channel))
                s_Instance.StopChannel(channel, fadeOut);
        }

        public static void StopAllLoops(float fadeOut = 0f)
        {
            if (s_Instance == null) return;
            for (int i = 0; i < s_Instance.loopList.Count; i++)
                s_Instance.StopChannel(s_Instance.loopList[i], fadeOut);
        }

        public static void SetLoopPitch(SoundId id, float pitch)
        {
            if (s_Instance == null || !s_Instance.loops.TryGetValue(id, out LoopChannel channel)) return;

            channel.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            channel.sources[0].pitch = channel.pitch;
            channel.sources[1].pitch = channel.pitch;
        }

        public static bool IsLoopPlaying(SoundId id)
        {
            return s_Instance != null && s_Instance.loops.TryGetValue(id, out LoopChannel channel) &&
                   channel.playing && !channel.stopWhenSilent;
        }

        /// <summary>루프의 현재 재생 위치(초). 재생 중이 아니면 -1.</summary>
        public static float GetLoopTime(SoundId id)
        {
            if (s_Instance == null || !s_Instance.loops.TryGetValue(id, out LoopChannel channel)) return -1f;
            if (!channel.playing || !channel.Active.isPlaying) return -1f;
            return channel.Active.time;
        }

        private void PlayLoopInternal(SoundId id, float fadeIn)
        {
            if (!loops.TryGetValue(id, out LoopChannel channel))
            {
                if (library == null || !library.TryGet(id, out SoundDefinition def) || def.clip == null) return;

                channel = new LoopChannel { definition = def };
                channel.sources[0] = CreateSource("Loop " + id + " A");
                channel.sources[1] = CreateSource("Loop " + id + " B");
                loops.Add(id, channel);
                loopList.Add(channel);
            }

            channel.stopWhenSilent = false;
            channel.targetGain = 1f;
            channel.fadeSpeed = fadeIn > 0f ? 1f / fadeIn : float.MaxValue;

            if (!channel.playing)
            {
                SoundDefinition def = channel.definition;
                bool nativeLoop = def.loopCrossfade <= 0f;

                channel.active = 0;
                channel.crossfading = false;
                channel.pitch = 1f;
                channel.gain = fadeIn > 0f ? 0f : 1f;
                channel.playing = true;

                channel.Standby.Stop();
                AudioSource source = channel.Active;
                source.Stop();
                source.clip = def.clip;
                source.loop = nativeLoop;
                source.pitch = 1f;
                source.time = nativeLoop ? 0f : LoopStart(def);
                ApplyLoopVolume(channel);
                source.Play();
            }
            else if (fadeIn <= 0f)
            {
                channel.gain = 1f;
                ApplyLoopVolume(channel);
            }
        }

        private void StopChannel(LoopChannel channel, float fadeOut)
        {
            if (!channel.playing) return;

            if (fadeOut <= 0f)
            {
                HaltChannel(channel);
                return;
            }

            channel.targetGain = 0f;
            channel.fadeSpeed = 1f / fadeOut;
            channel.stopWhenSilent = true;
        }

        private static void HaltChannel(LoopChannel channel)
        {
            channel.sources[0].Stop();
            channel.sources[1].Stop();
            channel.playing = false;
            channel.crossfading = false;
            channel.stopWhenSilent = false;
            channel.gain = 0f;
            channel.targetGain = 0f;
        }

        private static float LoopStart(SoundDefinition def)
        {
            return Mathf.Clamp(def.loopStartTrim, 0f, Mathf.Max(0f, def.clip.length - 0.05f));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < loopList.Count; i++)
            {
                LoopChannel channel = loopList[i];
                if (!channel.playing) continue;

                if (!Mathf.Approximately(channel.gain, channel.targetGain))
                {
                    channel.gain = Mathf.MoveTowards(channel.gain, channel.targetGain, channel.fadeSpeed * dt);
                    if (channel.stopWhenSilent && channel.gain <= 0.0001f)
                    {
                        HaltChannel(channel);
                        continue;
                    }
                }

                UpdateCrossfade(channel);
                ApplyLoopVolume(channel);
            }
        }

        // 루프 끝에 가까워지면 대기 소스를 루프 시작점에서 재생해 두 소스를 교차 페이드한다.
        private static void UpdateCrossfade(LoopChannel channel)
        {
            SoundDefinition def = channel.definition;
            float crossfade = def.loopCrossfade;
            if (crossfade <= 0f) return;

            float end = Mathf.Max(LoopStart(def) + crossfade * 2f, def.clip.length - Mathf.Max(0f, def.loopEndTrim));
            AudioSource active = channel.Active;

            if (!channel.crossfading)
            {
                if (!active.isPlaying || active.time >= end - crossfade)
                {
                    AudioSource next = channel.Standby;
                    next.Stop();
                    next.clip = def.clip;
                    next.loop = false;
                    next.pitch = channel.pitch;
                    next.time = LoopStart(def);
                    next.volume = 0f;
                    next.Play();
                    channel.crossfading = true;
                }
                return;
            }

            // 나가는 소스의 실제 재생 위치를 기준으로 진행도를 계산(피치가 바뀌어도 정확)
            if (!active.isPlaying || active.time >= end)
            {
                active.Stop();
                channel.active = 1 - channel.active;
                channel.crossfading = false;
            }
        }

        private void ApplyLoopVolume(LoopChannel channel)
        {
            SoundDefinition def = channel.definition;
            float volume = Mathf.Clamp01(def.volume * channel.gain * loopVolume * masterVolume);

            if (!channel.crossfading)
            {
                channel.Active.volume = volume;
                return;
            }

            float crossfade = Mathf.Max(0.001f, def.loopCrossfade);
            float end = Mathf.Max(LoopStart(def) + crossfade * 2f, def.clip.length - Mathf.Max(0f, def.loopEndTrim));
            float k = channel.Active.isPlaying ? Mathf.InverseLerp(end - crossfade, end, channel.Active.time) : 1f;

            // 등전력(equal-power) 교차 페이드
            channel.Active.volume = volume * Mathf.Cos(k * Mathf.PI * 0.5f);
            channel.Standby.volume = volume * Mathf.Sin(k * Mathf.PI * 0.5f);
        }
    }
}
