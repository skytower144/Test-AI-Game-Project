using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 플레이어 위에 표시되는 체력바(칸 단위). 플레이어를 따라다니며,
    /// 감소한 칸은 초록색이 없는 바 스프라이트로 표시한다.
    /// </summary>
    public sealed class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("플레이어 중심 기준 체력바 위치")]
        [SerializeField] private Vector2 offset = new Vector2(0.06f, 0.33f);
        [Tooltip("왼쪽부터 순서대로 배치된 체력 칸")]
        [SerializeField] private SpriteRenderer[] cells = System.Array.Empty<SpriteRenderer>();
        [Tooltip("체력이 남아 있는 칸 (Crew_Counter 초록)")]
        [SerializeField] private Sprite filledSprite;
        [Tooltip("체력이 감소한 칸 (Crew_Counter 초록 없음)")]
        [SerializeField] private Sprite emptySprite;

        private bool visible = true;

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Subscribe<PlayerExplodedEvent>(OnPlayerExploded);
            EventBus.Subscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Unsubscribe<PlayerExplodedEvent>(OnPlayerExploded);
            EventBus.Unsubscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 p = target.position;
            transform.position = new Vector3(p.x + offset.x, p.y + offset.y, 0f);
        }

        private void OnHealthChanged(PlayerHealthChangedEvent evt)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                cells[i].sprite = i < evt.health ? filledSprite : emptySprite;
            }
        }

        private void OnPlayerExploded(PlayerExplodedEvent evt)
        {
            SetVisible(false);
        }

        private void OnPlayerRevived(PlayerRevivedEvent evt)
        {
            SetVisible(true);
        }

        private void OnGameReset(GameResetEvent evt)
        {
            SetVisible(true);
        }

        private void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null) cells[i].enabled = value;
            }
        }
    }
}
