using TMPro;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 화면 중앙 안내 문구: GAME OVER 표시, 그리고 목숨이 남아 있을 때의 부활 안내(R키).
    /// </summary>
    public sealed class GameOverBanner : MonoBehaviour
    {
        [Header("Game Over")]
        [SerializeField] private GameObject gameOverRoot;

        [Header("Revive Prompt")]
        [SerializeField] private GameObject revivePromptRoot;
        [SerializeField] private TMP_Text revivePromptText;
        [Tooltip("부활 안내 문구가 깜박이는 주기(초)")]
        [SerializeField, Min(0.05f)] private float promptBlinkPeriod = 1f;
        [SerializeField, Range(0f, 1f)] private float promptMinAlpha = 0.25f;

        private float promptTimer;

        private void Awake()
        {
            if (gameOverRoot != null) gameOverRoot.SetActive(false);
            if (revivePromptRoot != null) revivePromptRoot.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerDeathSequenceFinishedEvent>(OnDeathSequenceFinished);
            EventBus.Subscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<ResultsShownEvent>(OnResultsShown);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerDeathSequenceFinishedEvent>(OnDeathSequenceFinished);
            EventBus.Unsubscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<ResultsShownEvent>(OnResultsShown);
        }

        private void Update()
        {
            if (revivePromptRoot == null || !revivePromptRoot.activeSelf || revivePromptText == null) return;

            promptTimer += Time.unscaledDeltaTime;
            float k = 0.5f + 0.5f * Mathf.Cos(promptTimer / promptBlinkPeriod * Mathf.PI * 2f);
            Color color = revivePromptText.color;
            color.a = Mathf.Lerp(promptMinAlpha, 1f, k);
            revivePromptText.color = color;
        }

        private void OnDeathSequenceFinished(PlayerDeathSequenceFinishedEvent evt)
        {
            // 목숨은 사망 시점(체력 0)에 이미 차감되어 있다
            if (GameManager.Lives > 0 && revivePromptRoot != null)
            {
                promptTimer = 0f;
                revivePromptRoot.SetActive(true);
            }
        }

        private void OnPlayerRevived(PlayerRevivedEvent evt)
        {
            if (revivePromptRoot != null) revivePromptRoot.SetActive(false);
        }

        private void OnGameOver(GameOverEvent evt)
        {
            if (revivePromptRoot != null) revivePromptRoot.SetActive(false);
            if (gameOverRoot != null) gameOverRoot.SetActive(true);
        }

        private void OnResultsShown(ResultsShownEvent evt)
        {
            if (gameOverRoot != null) gameOverRoot.SetActive(false);
        }
    }
}
