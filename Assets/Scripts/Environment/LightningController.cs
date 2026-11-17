using System;
using System.Collections;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 번개 연출. 배경 전체를 덮는 흰색 오버레이가 불규칙하게 깜박인 뒤 서서히 사라지며 배경이 다시 드러난다.
    /// 빗소리 루프(Rain-and-thunder-loop)의 천둥 시점에 맞춰 번개가 치고,
    /// 루프가 재생 중이 아니면 무작위 간격으로 번개가 친다.
    /// </summary>
    public sealed class LightningController : MonoBehaviour
    {
        [Serializable]
        public struct ThunderCue
        {
            [Tooltip("루프 내 천둥이 들리기 시작하는 시점(초)")]
            public float time;
            [Tooltip("번개 세기(0~1)")]
            [Range(0f, 1f)] public float intensity;
        }

        [SerializeField] private SpriteRenderer flashRenderer;
        [SerializeField] private Color flashColor = Color.white;

        [Header("Thunder Sync")]
        [Tooltip("빗소리 루프의 천둥 소리에 번개를 맞춘다")]
        [SerializeField] private bool syncToThunder = true;
        [SerializeField] private SoundId thunderLoop = SoundId.RainThunder;
        [Tooltip("번개가 천둥 소리보다 먼저 치는 시간(초)")]
        [SerializeField, Min(0f)] private float flashLeadTime = 0.12f;
        [Tooltip("Rain-and-thunder-loop.mp3에서 천둥이 울리는 시점들")]
        [SerializeField] private ThunderCue[] thunderCues =
        {
            new ThunderCue { time = 1.70f, intensity = 1f },
            new ThunderCue { time = 4.45f, intensity = 0.4f },
            new ThunderCue { time = 5.75f, intensity = 0.75f },
            new ThunderCue { time = 8.00f, intensity = 1f },
            new ThunderCue { time = 9.08f, intensity = 0.55f },
            new ThunderCue { time = 9.95f, intensity = 1f },
            new ThunderCue { time = 11.05f, intensity = 0.4f },
        };

        [Header("Random Fallback")]
        [Tooltip("천둥 동기화를 쓰지 않을 때 첫 번개까지의 시간(초)")]
        [SerializeField, Min(0f)] private float firstStrikeDelay = 2.5f;
        [Tooltip("천둥 동기화를 쓰지 않을 때 번개 사이 간격 범위(초)")]
        [SerializeField] private Vector2 strikeIntervalRange = new Vector2(4f, 9f);

        [Header("Flicker")]
        [Tooltip("한 번의 번개에서 깜박이는 횟수 범위")]
        [SerializeField] private Vector2Int flickerCountRange = new Vector2Int(2, 4);
        [Tooltip("밝게 켜져 있는 시간 범위(초)")]
        [SerializeField] private Vector2 flickerOnTime = new Vector2(0.03f, 0.09f);
        [Tooltip("깜박임 사이 어두워지는 시간 범위(초)")]
        [SerializeField] private Vector2 flickerOffTime = new Vector2(0.03f, 0.12f);
        [SerializeField] private Vector2 peakAlphaRange = new Vector2(0.7f, 1f);
        [SerializeField] private Vector2 dimAlphaRange = new Vector2(0.05f, 0.3f);
        [Tooltip("마지막 깜박임 후 배경이 서서히 드러나는 시간 범위(초)")]
        [SerializeField] private Vector2 fadeOutRange = new Vector2(0.8f, 1.4f);

        [Header("Coverage")]
        [Tooltip("화면보다 얼마나 더 크게 덮을지(유닛)")]
        [SerializeField, Min(0f)] private float overscan = 0.5f;

        private Vector2 spriteSize = Vector2.one;
        private Coroutine strikeRoutine;
        private float lastLoopTime = -1f;
        private float randomTimer;

        private void Awake()
        {
            if (flashRenderer == null) flashRenderer = GetComponent<SpriteRenderer>();
            if (flashRenderer != null && flashRenderer.sprite != null)
                spriteSize = flashRenderer.sprite.bounds.size;
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            lastLoopTime = -1f;
            randomTimer = firstStrikeDelay;
        }

        private void OnDisable()
        {
            strikeRoutine = null;
            SetAlpha(0f);
        }

        private void Update()
        {
            float loopTime = syncToThunder ? AudioManager.GetLoopTime(thunderLoop) : -1f;

            if (loopTime >= 0f)
            {
                // 직전 프레임과 현재 프레임 사이에 지나친 천둥 시점이 있으면 번개를 친다
                if (lastLoopTime >= 0f)
                {
                    for (int i = 0; i < thunderCues.Length; i++)
                    {
                        if (Crossed(lastLoopTime, loopTime, thunderCues[i].time - flashLeadTime))
                            Strike(thunderCues[i].intensity);
                    }
                }
                lastLoopTime = loopTime;
                return;
            }

            // 루프가 없으면 무작위 간격
            lastLoopTime = -1f;
            randomTimer -= Time.deltaTime;
            if (randomTimer <= 0f)
            {
                randomTimer = UnityEngine.Random.Range(strikeIntervalRange.x, strikeIntervalRange.y);
                Strike(UnityEngine.Random.Range(0.6f, 1f));
            }
        }

        private static bool Crossed(float previous, float current, float target)
        {
            if (current >= previous) return target > previous && target <= current;
            return target > previous || target <= current; // 루프가 처음으로 되돌아간 경우
        }

        private void LateUpdate()
        {
            // 화면 전체(배경 영역)를 항상 덮도록 크기/위치 유지
            Vector2 center = ScreenBounds.Center;
            transform.position = new Vector3(center.x, center.y, transform.position.z);
            if (spriteSize.x > 0f && spriteSize.y > 0f)
            {
                transform.localScale = new Vector3(
                    (ScreenBounds.Width + overscan * 2f) / spriteSize.x,
                    (ScreenBounds.Height + overscan * 2f) / spriteSize.y, 1f);
            }
        }

        /// <summary>번개를 한 번 친다. intensity는 0~1.</summary>
        public void Strike(float intensity)
        {
            if (!isActiveAndEnabled) return;
            if (strikeRoutine != null) StopCoroutine(strikeRoutine);
            strikeRoutine = StartCoroutine(StrikeRoutine(Mathf.Clamp01(intensity)));
        }

        private IEnumerator StrikeRoutine(float intensity)
        {
            // 약한 번개는 깜박임 횟수도 적다
            int minFlickers = Mathf.Max(1, flickerCountRange.x);
            int maxFlickers = Mathf.Max(minFlickers, flickerCountRange.y);
            int flickers = intensity < 0.5f
                ? UnityEngine.Random.Range(1, minFlickers + 1)
                : UnityEngine.Random.Range(minFlickers, maxFlickers + 1);
            float alpha = 0f;

            for (int i = 0; i < flickers; i++)
            {
                alpha = UnityEngine.Random.Range(peakAlphaRange.x, peakAlphaRange.y) * intensity;
                SetAlpha(alpha);
                yield return Wait(UnityEngine.Random.Range(flickerOnTime.x, flickerOnTime.y));

                if (i < flickers - 1)
                {
                    SetAlpha(UnityEngine.Random.Range(dimAlphaRange.x, dimAlphaRange.y) * intensity);
                    yield return Wait(UnityEngine.Random.Range(flickerOffTime.x, flickerOffTime.y));
                }
            }

            // 서서히 배경이 드러남
            float fade = Mathf.Max(0.01f, UnityEngine.Random.Range(fadeOutRange.x, fadeOutRange.y));
            float t = 0f;
            while (t < fade)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / fade);
                SetAlpha(alpha * k * k);
                yield return null;
            }

            SetAlpha(0f);
            strikeRoutine = null;
        }

        private void SetAlpha(float alpha)
        {
            if (flashRenderer == null) return;
            Color c = flashColor;
            c.a = flashColor.a * Mathf.Clamp01(alpha);
            flashRenderer.color = c;
            flashRenderer.enabled = c.a > 0.001f;
        }

        private static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
