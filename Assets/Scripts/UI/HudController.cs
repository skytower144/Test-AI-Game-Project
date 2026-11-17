using TMPro;
using UnityEngine;

namespace Chopper
{
    /// <summary>인게임 HUD: 왼쪽 상단 점수와 목숨 아이콘.</summary>
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;
        [Tooltip("점수 앞에 붙는 문자열")]
        [SerializeField] private string scorePrefix = "SCORE: ";
        [Tooltip("왼쪽부터 순서대로 배치된 목숨 아이콘. 목숨이 줄면 오른쪽부터 비활성화된다")]
        [SerializeField] private GameObject[] lifeIcons = System.Array.Empty<GameObject>();

        private int shownScore = int.MinValue;

        private void OnEnable()
        {
            EventBus.Subscribe<ScoreChangedEvent>(OnScoreChanged);
            EventBus.Subscribe<LivesChangedEvent>(OnLivesChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ScoreChangedEvent>(OnScoreChanged);
            EventBus.Unsubscribe<LivesChangedEvent>(OnLivesChanged);
        }

        private void OnScoreChanged(ScoreChangedEvent evt)
        {
            if (scoreText == null || evt.score == shownScore) return;
            shownScore = evt.score;
            scoreText.text = scorePrefix + evt.score;
        }

        private void OnLivesChanged(LivesChangedEvent evt)
        {
            for (int i = 0; i < lifeIcons.Length; i++)
            {
                if (lifeIcons[i] != null) lifeIcons[i].SetActive(i < evt.lives);
            }
        }
    }
}
