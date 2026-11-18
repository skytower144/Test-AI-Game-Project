using System.Collections;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 인게임 진입 연출. 화면 위/아래를 덮고 있던 검은색 바 2개가 서서히 화면 밖으로 이동하며 화면을 드러낸다.
    /// 씬 진입 시 그리고 RETRY로 인게임이 초기화될 때마다 재생된다.
    /// </summary>
    public sealed class IntroBars : MonoBehaviour
    {
        [SerializeField] private RectTransform topBar;
        [SerializeField] private RectTransform bottomBar;
        [Tooltip("바가 움직이기 전 대기 시간(초)")]
        [SerializeField, Min(0f)] private float holdTime = 0.4f;
        [Tooltip("바가 화면 밖으로 완전히 나가는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.05f)] private float openDuration = 1.8f;
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("두 바가 가운데에서 겹치는 정도(화면 높이 비율). 틈이 보이지 않게 한다")]
        [SerializeField, Range(0f, 0.05f)] private float overlap = 0.005f;

        private Coroutine routine;

        private void Awake()
        {
            Cover();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
            routine = null;
        }

        private void Start()
        {
            Play();
        }

        private void OnGameReset(GameResetEvent evt)
        {
            Play();
        }

        /// <summary>화면을 바로 덮은 뒤 바를 열어 화면을 드러낸다.</summary>
        public void Play()
        {
            if (routine != null) StopCoroutine(routine);
            Cover();
            routine = StartCoroutine(OpenRoutine());
        }

        private void Cover()
        {
            if (topBar != null) topBar.gameObject.SetActive(true);
            if (bottomBar != null) bottomBar.gameObject.SetActive(true);
            SetProgress(0f);
        }

        private IEnumerator OpenRoutine()
        {
            // 씬 로드/초기화 직후의 불안정한 프레임을 건너뛴다
            yield return null;

            float t = 0f;
            while (t < holdTime)
            {
                t += Time.deltaTime;
                yield return null;
            }

            t = 0f;
            while (t < openDuration)
            {
                // 한 프레임이 길게 튀어도 바가 순간이동하지 않도록 제한
                t += Mathf.Min(Time.deltaTime, 0.05f);
                SetProgress(curve.Evaluate(Mathf.Clamp01(t / openDuration)));
                yield return null;
            }

            SetProgress(1f);
            if (topBar != null) topBar.gameObject.SetActive(false);
            if (bottomBar != null) bottomBar.gameObject.SetActive(false);
            routine = null;
        }

        /// <summary>0 = 화면을 완전히 덮음, 1 = 완전히 화면 밖.</summary>
        private void SetProgress(float k)
        {
            float shift = k * (0.5f + overlap);

            if (topBar != null)
            {
                topBar.anchorMin = new Vector2(0f, 0.5f - overlap + shift);
                topBar.anchorMax = new Vector2(1f, 1f + shift);
                topBar.offsetMin = Vector2.zero;
                topBar.offsetMax = Vector2.zero;
            }

            if (bottomBar != null)
            {
                bottomBar.anchorMin = new Vector2(0f, -shift);
                bottomBar.anchorMax = new Vector2(1f, 0.5f + overlap - shift);
                bottomBar.offsetMin = Vector2.zero;
                bottomBar.offsetMax = Vector2.zero;
            }
        }
    }
}
