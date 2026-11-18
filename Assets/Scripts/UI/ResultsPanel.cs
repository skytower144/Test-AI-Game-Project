using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 결과 화면: 역대 최고 점수 1~5위(현재 플레이 포함), RETRY / QUIT 아이콘.
    /// 결과 화면이 표시될 때만 마우스 커서가 활성화되고, 아이콘은 마우스 클릭으로 동작한다.
    /// </summary>
    public sealed class ResultsPanel : MonoBehaviour
    {
        [Serializable]
        public struct Row
        {
            public GameObject root;
            public TMP_Text nameText;
            public TMP_Text scoreText;
        }

        [Header("References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Row[] rows = Array.Empty<Row>();
        [Tooltip("RETRY 아이콘 + 이름을 감싸는 클릭 영역")]
        [SerializeField] private RectTransform retryButton;
        [Tooltip("QUIT 아이콘 + 이름을 감싸는 클릭 영역")]
        [SerializeField] private RectTransform quitButton;
        [Tooltip("결과 화면에서만 활성화되는 커서 오브젝트")]
        [SerializeField] private GameObject cursorObject;

        [Header("Colors")]
        [SerializeField] private Color rowColor = new Color(0.3f, 1f, 0.45f, 1f);
        [Tooltip("현재 플레이어의 점수가 1~5위에 포함되면 해당 행에 적용")]
        [SerializeField] private Color playerRowColor = Color.white;

        [Header("Behaviour")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;
        [Tooltip("표시 직후 오클릭을 막기 위한 입력 잠금 시간(초)")]
        [SerializeField, Min(0f)] private float inputLockTime = 0.35f;
        [SerializeField, Min(1f)] private float hoverScale = 1.12f;
        [SerializeField, Min(0f)] private float hoverLerpSpeed = 16f;

        private bool shown;
        private bool actionTaken;
        private float shownTime;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (cursorObject != null) cursorObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ResultsShownEvent>(OnResultsShown);
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ResultsShownEvent>(OnResultsShown);
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
        }

        // RETRY: 결과 화면과 커서를 닫는다
        private void OnGameReset(GameResetEvent evt)
        {
            shown = false;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (cursorObject != null) cursorObject.SetActive(false);
            if (retryButton != null) retryButton.localScale = Vector3.one;
            if (quitButton != null) quitButton.localScale = Vector3.one;
        }

        private void OnResultsShown(ResultsShownEvent evt)
        {
            Populate(evt.entries, evt.playerRank);

            if (panelRoot != null) panelRoot.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = fadeInDuration > 0f ? 0f : 1f;
            if (cursorObject != null) cursorObject.SetActive(true); // 결과 화면에서만 커서 활성화

            shown = true;
            actionTaken = false;
            shownTime = 0f;
        }

        private void Populate(List<ScoreEntry> entries, int playerRank)
        {
            int count = entries != null ? entries.Count : 0;
            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                bool hasEntry = i < count && entries[i] != null;
                if (row.root != null) row.root.SetActive(hasEntry);
                if (!hasEntry) continue;

                Color color = i == playerRank ? playerRowColor : rowColor;
                if (row.nameText != null)
                {
                    row.nameText.text = entries[i].name;   // 좌측 정렬
                    row.nameText.color = color;
                }
                if (row.scoreText != null)
                {
                    row.scoreText.text = entries[i].score.ToString(); // 우측 정렬
                    row.scoreText.color = color;
                }
            }
        }

        private void Update()
        {
            if (!shown) return;

            float dt = Time.unscaledDeltaTime;
            shownTime += dt;

            if (canvasGroup != null && fadeInDuration > 0f)
                canvasGroup.alpha = Mathf.Clamp01(shownTime / fadeInDuration);

            bool hasPointer = GameInput.TryGetPointer(out Vector2 pointer);
            bool overRetry = hasPointer && Contains(retryButton, pointer);
            bool overQuit = hasPointer && Contains(quitButton, pointer);

            AnimateHover(retryButton, overRetry, dt);
            AnimateHover(quitButton, overQuit, dt);

            if (actionTaken || shownTime < inputLockTime || !GameInput.PointerPressed) return;

            if (overRetry)
            {
                actionTaken = true;
                GameManager.Retry();
            }
            else if (overQuit)
            {
                actionTaken = true;
                GameManager.Quit();
            }
        }

        private static bool Contains(RectTransform rect, Vector2 screenPoint)
        {
            // Screen Space - Overlay 캔버스이므로 카메라는 null
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);
        }

        private void AnimateHover(RectTransform rect, bool hovered, float dt)
        {
            if (rect == null) return;
            float target = hovered ? hoverScale : 1f;
            float current = rect.localScale.x;
            float next = Mathf.Lerp(current, target, 1f - Mathf.Exp(-hoverLerpSpeed * dt));
            rect.localScale = new Vector3(next, next, 1f);
        }
    }
}
