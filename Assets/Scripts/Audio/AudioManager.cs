using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 효과음(풀링된 AudioSource 보이스)과 루프 사운드를 재생한다.
    /// 각 사운드의 볼륨과 중복재생 정책(동시 재생 수, 초과 시 처리, 최소 간격)은 SoundLibrary에서 정의한다.
    /// 루프는 필요 시 두 개의 소스를 교차 페이드해 MP3 앞뒤 무음으로 인한 끊김 없이 이어 붙인다.
    /// 지연 재생은 무음을 앞에 붙인 사본으로 처리해, 프레임률과 무관하게 정확한 간격으로 소리가 나게 한다.
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

        // 최소 간격을 비교할 때 부동소수점 오차를 흡수하는 여유(초). 지연이 최소 간격과 정확히 같을 때 결과가 흔들리지 않게 한다.
        private const double IntervalEpsilon = 1e-5;
        // 지연 재생용 사본 앞에 붙이는 무음의 최소 길이(초)
        private const float MinDelayPadding = 0.2f;

        private sealed class Voice
        {
            public AudioSource source;
            public SoundId id;
            public double startTime; // 소리가 나기 시작하는 시각(지연 재생이면 지연이 끝나는 시각)
            public int playId;       // 같은 재생 요청으로 시작한 보이스들이 공유하는 번호
            public bool extra;       // 1을 넘는 볼륨을 만들기 위해 겹쳐 재생하는 추가 보이스
        }

        // 지연 재생용 사본: [무음][시작 오프셋 이후의 원본]
        private sealed class DelayClip
        {
            public AudioClip original;
            public float startOffset; // 사본을 만들 때 적용한 시작 오프셋
            public AudioClip clip;    // 사본
            public int padding;       // 앞에 붙인 무음 길이(샘플)
            public bool unavailable;  // 샘플 데이터를 읽을 수 없어 사본을 만들 수 없는 클립
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
        private int playCounter;
        private readonly Dictionary<SoundId, double> lastPlayTimes = new Dictionary<SoundId, double>();
        private readonly Dictionary<SoundId, DelayClip> delayClips = new Dictionary<SoundId, DelayClip>();
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
            foreach (DelayClip entry in delayClips.Values)
            {
                if (entry.clip != null) Destroy(entry.clip);
            }
            delayClips.Clear();

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

        /// <summary>
        /// 효과음을 1회 재생한다. 중복재생 정책에 따라 무시되거나 기존 재생을 대체할 수 있다.
        /// delay(초)를 주면 그만큼 늦춰 재생한다(여러 소리를 엇박으로 배치할 때 사용).
        /// 같은 프레임에 재생한 다른 소리와의 간격은 프레임률과 무관하게 delay로 일정하다.
        /// </summary>
        public static void Play(SoundId id, float volumeScale = 1f, float delay = 0f)
        {
            if (s_Instance != null) s_Instance.PlayInternal(id, volumeScale, Mathf.Max(0f, delay));
        }

        /// <summary>효과음의 재생 길이(초): 시작 오프셋을 뺀, 피치 1 기준의 길이. 라이브러리에 없으면 0.</summary>
        public static float GetDuration(SoundId id)
        {
            if (s_Instance == null || s_Instance.library == null) return 0f;
            if (!s_Instance.library.TryGet(id, out SoundDefinition def) || def.clip == null) return 0f;
            return Mathf.Max(0f, def.clip.length - def.startOffset);
        }

        /// <summary>지연 재생을 기다리고 있어 아직 소리가 나지 않은 효과음을 취소한다. 이미 들리는 소리는 그대로 둔다.</summary>
        public static void CancelDelayed(SoundId id)
        {
            if (s_Instance == null) return;

            double now = Time.unscaledTimeAsDouble;
            Voice[] voices = s_Instance.voices;
            for (int i = 0; i < voices.Length; i++)
            {
                Voice v = voices[i];
                if (v.id == id && v.startTime > now && v.source.isPlaying) v.source.Stop();
            }
        }

        private void PlayInternal(SoundId id, float volumeScale, float delay)
        {
            if (library == null || !library.TryGet(id, out SoundDefinition def) || def.clip == null) return;

            // 중복재생 정책은 요청한 시각이 아니라 소리가 실제로 시작될 시각을 기준으로 판단한다.
            // (지연 재생도 요청한 프레임에 바로 결정되므로 결과가 프레임률에 따라 달라지지 않는다)
            double startTime = Time.unscaledTimeAsDouble + delay;
            if (def.minInterval > 0f && lastPlayTimes.TryGetValue(id, out double last) &&
                System.Math.Abs(startTime - last) + IntervalEpsilon < def.minInterval)
                return;

            int free = -1;
            int sameCount = 0, sameOldest = -1, anyOldest = 0;
            double sameOldestTime = double.MaxValue, anyOldestTime = double.MaxValue;

            for (int i = 0; i < voices.Length; i++)
            {
                Voice v = voices[i];
                if (!v.source.isPlaying)
                {
                    if (free < 0) free = i;
                    continue;
                }

                if (v.startTime < anyOldestTime) { anyOldestTime = v.startTime; anyOldest = i; }
                if (v.id == id && !v.extra) // 추가 보이스는 동시 재생 수에 세지 않는다
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

            float pitch = Random.Range(Mathf.Min(def.pitchRange.x, def.pitchRange.y), Mathf.Max(def.pitchRange.x, def.pitchRange.y));

            // 지연은 "앞에 무음을 붙인 사본을 지금 재생"해서 만든다. 재생 자체는 같은 프레임의 다른 소리와 같은
            // 믹스 블록에서 시작하므로, 프레임 시간이나 오디오 버퍼 크기와 무관하게 정확히 delay 뒤에 소리가 난다.
            DelayClip delayed = delay > 0f ? GetDelayClip(def, delay * pitch) : null;

            // AudioSource의 볼륨은 1이 한계다. 1을 넘는 볼륨은 같은 소리를 여러 보이스로 겹쳐 재생해서 만든다.
            // (같은 프레임에 시작한 보이스들은 같은 믹스 블록에서 출발하므로 파형이 그대로 더해진다)
            float volume = Mathf.Max(0f, def.volume * volumeScale * effectsVolume * masterVolume);
            int layers = Mathf.Max(1, Mathf.CeilToInt(volume - 0.0001f));
            int playId = ++playCounter;

            for (int layer = 0; layer < layers; layer++)
            {
                if (layer > 0) index = FindExtraVoice(playId);
                if (index < 0) break;

                Voice voice = voices[index];
                AudioSource source = voice.source;
                if (source.isPlaying && !voice.extra) StopExtraVoices(voice.playId); // 밀려나는 소리의 추가 보이스도 함께 멈춘다

                source.Stop();
                source.volume = volume / layers;
                source.pitch = pitch;

                if (delayed != null)
                {
                    source.clip = delayed.clip;
                    source.timeSamples = delayed.padding - Mathf.RoundToInt(delay * pitch * delayed.clip.frequency);
                    source.Play();
                }
                else
                {
                    source.clip = def.clip;
                    // 앞쪽 무음(MP3 인코더 지연 등)을 건너뛰어 입력/타격과 소리가 어긋나지 않게 한다
                    source.time = Mathf.Clamp(def.startOffset, 0f, Mathf.Max(0f, def.clip.length - 0.01f));

                    // 사본을 만들 수 없는 클립의 지연은 오디오 시계에 예약한다.
                    // (믹서가 블록을 처리하는 도중에 예약되면 한 블록만큼 일찍 시작할 수 있어 사본 방식보다 덜 정확하다)
                    if (delay > 0f) source.PlayDelayed(delay);
                    else source.Play();
                }

                voice.id = id;
                voice.startTime = startTime;
                voice.playId = playId;
                voice.extra = layer > 0;
            }

            lastPlayTimes[id] = startTime;
        }

        // 겹쳐 재생할 추가 보이스를 고른다: 비어 있는 보이스, 없으면 다른 소리의 가장 오래된 보이스
        private int FindExtraVoice(int playId)
        {
            int oldest = -1;
            double oldestTime = double.MaxValue;
            for (int i = 0; i < voices.Length; i++)
            {
                Voice v = voices[i];
                if (!v.source.isPlaying) return i;
                if (v.playId != playId && v.startTime < oldestTime) { oldestTime = v.startTime; oldest = i; }
            }
            return oldest;
        }

        private void StopExtraVoices(int playId)
        {
            for (int i = 0; i < voices.Length; i++)
            {
                Voice v = voices[i];
                if (v.extra && v.playId == playId) v.source.Stop();
            }
        }

        // 지연 재생용 사본([무음][시작 오프셋 이후의 원본])을 돌려준다. 없으면 만들고, 만들 수 없는 클립이면 null.
        private DelayClip GetDelayClip(SoundDefinition def, float paddingSeconds)
        {
            AudioClip original = def.clip;
            int needed = Mathf.CeilToInt(paddingSeconds * original.frequency);

            if (delayClips.TryGetValue(def.id, out DelayClip entry))
            {
                bool upToDate = entry.original == original && Mathf.Approximately(entry.startOffset, def.startOffset);
                if (upToDate && entry.unavailable) return null;
                if (upToDate && entry.clip != null && entry.padding >= needed) return entry;

                // 인스펙터에서 클립이나 시작 오프셋이 바뀌었거나 무음이 모자라면 다시 만든다
                if (entry.clip != null) Destroy(entry.clip);
                delayClips.Remove(def.id);
            }

            if (original.loadState == AudioDataLoadState.Unloaded) original.LoadAudioData();
            if (original.loadState != AudioDataLoadState.Loaded) return null; // 아직 로드 중이면 다음 재생 때 다시 시도

            entry = new DelayClip { original = original, startOffset = def.startOffset, unavailable = true };
            delayClips.Add(def.id, entry);

            // 샘플 데이터를 읽을 수 있는(메모리에 풀어 둔) 클립만 사본을 만들 수 있다
            if (original.loadType != AudioClipLoadType.DecompressOnLoad) return null;

            int channels = original.channels;
            int skip = Mathf.Clamp(Mathf.RoundToInt(def.startOffset * original.frequency), 0, original.samples - 1);
            int count = original.samples - skip;
            int padding = Mathf.Max(needed, Mathf.CeilToInt(MinDelayPadding * original.frequency));

            var content = new float[count * channels];
            if (!original.GetData(content, skip)) return null;

            var data = new float[(padding + count) * channels];
            System.Array.Copy(content, 0, data, padding * channels, content.Length);

            AudioClip clip = AudioClip.Create(original.name + " (delayed)", padding + count, channels, original.frequency, false);
            clip.SetData(data, 0);

            entry.clip = clip;
            entry.padding = padding;
            entry.unavailable = false;
            return entry;
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
