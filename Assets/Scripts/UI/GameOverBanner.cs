using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 안내 문구: 화면 중앙의 GAME OVER, 그리고 목숨이 남아 있을 때 화면 오른쪽 위에 깜박이는 부활 안내(R키).
    /// </summary>
    public sealed class GameOverBanner : MonoBehaviour
    {
        [Header("Game Over")]
        [SerializeField] private GameObject gameOverRoot;

        [Header("Revive Prompt")]
        [SerializeField] private GameObject revivePromptRoot;
        [Tooltip("깜박일 때 켜고 끄는 텍스트(또는 그래픽)")]
        [SerializeField] private Behaviour revivePromptGraphic;
        [Tooltip("부활 안내 문구가 깜박이는 간격(초). 이 시간마다 한 번씩 꺼졌다 켜진다")]
        [SerializeField, Min(0.05f)] private float promptBlinkInterval = 0.8f;

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
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerDeathSequenceFinishedEvent>(OnDeathSequenceFinished);
            EventBus.Unsubscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<ResultsShownEvent>(OnResultsShown);
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
        }

        private void Update()
        {
            if (revivePromptRoot == null || !revivePromptRoot.activeSelf || revivePromptGraphic == null) return;

            // 간격의 앞쪽 절반은 켜짐, 뒤쪽 절반은 꺼짐
            promptTimer += Time.unscaledDeltaTime;
            revivePromptGraphic.enabled = Mathf.Repeat(promptTimer, promptBlinkInterval) < promptBlinkInterval * 0.5f;
        }

        private void OnDeathSequenceFinished(PlayerDeathSequenceFinishedEvent evt)
        {
            // 목숨은 사망 시점(체력 0)에 이미 차감되어 있다
            if (GameManager.Lives > 0) SetPromptVisible(true);
        }

        private void OnPlayerRevived(PlayerRevivedEvent evt)
        {
            SetPromptVisible(false);
        }

        private void OnGameOver(GameOverEvent evt)
        {
            SetPromptVisible(false);
            if (gameOverRoot != null) gameOverRoot.SetActive(true);
        }

        private void OnResultsShown(ResultsShownEvent evt)
        {
            if (gameOverRoot != null) gameOverRoot.SetActive(false);
        }

        private void OnGameReset(GameResetEvent evt)
        {
            SetPromptVisible(false);
            if (gameOverRoot != null) gameOverRoot.SetActive(false);
        }

        private void SetPromptVisible(bool visible)
        {
            if (revivePromptRoot == null) return;

            promptTimer = 0f;
            if (revivePromptGraphic != null) revivePromptGraphic.enabled = true;
            revivePromptRoot.SetActive(visible);
        }
    }
}
