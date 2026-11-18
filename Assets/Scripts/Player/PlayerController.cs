using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 헬리콥터 이동(약간의 관성)과 부활 입력을 처리한다.
    /// 체력이 0이면 조작이 차단되고, 목숨이 남아 있을 때 R키로 최초 시작 위치에서 부활한다.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("최대 이동 속도(유닛/초)")]
        [SerializeField, Min(0f)] private float maxSpeed = 4.6f;
        [Tooltip("이동 시작/방향 전환 시 가속도(유닛/초²)")]
        [SerializeField, Min(0.01f)] private float acceleration = 40f;
        [Tooltip("입력을 뗐을 때 감속도(유닛/초²)")]
        [SerializeField, Min(0.01f)] private float deceleration = 32f;

        [Header("Start / Bounds")]
        [Tooltip("시작(부활) 위치. 뷰포트 좌표(0~1)")]
        [SerializeField] private Vector2 startViewportPosition = new Vector2(0.14f, 0.5f);
        [Tooltip("화면 왼쪽/아래 가장자리와 유지할 여백(유닛)")]
        [SerializeField] private Vector2 paddingMin = new Vector2(0.55f, 0.3f);
        [Tooltip("화면 오른쪽/위 가장자리와 유지할 여백(유닛)")]
        [SerializeField] private Vector2 paddingMax = new Vector2(0.55f, 0.55f);

        [Header("References")]
        [SerializeField] private PlayerHealth health;

        private Vector2 velocity;

        public Vector2 Velocity => velocity;
        public Vector2 StartPosition => ScreenBounds.FromViewport(startViewportPosition.x, startViewportPosition.y);

        private void Awake()
        {
            if (health == null) health = GetComponent<PlayerHealth>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
        }

        private void Start()
        {
            MoveToStart();
        }

        private void OnGameReset(GameResetEvent evt)
        {
            MoveToStart();
        }

        private void MoveToStart()
        {
            Vector2 start = StartPosition;
            transform.position = new Vector3(start.x, start.y, 0f);
            velocity = Vector2.zero;
        }

        private void Update()
        {
            if (health.IsDead)
            {
                velocity = Vector2.zero;
                if (GameManager.CanRevive && GameInput.RevivePressed)
                    health.Revive(StartPosition);
                return;
            }

            Vector2 input = GameManager.ControlsEnabled ? GameInput.Move : Vector2.zero;
            float dt = Time.deltaTime;

            // 목표 속도를 향해 일정 가속도로 접근 → 이동 시작/방향 전환/정지 시 미세한 가감속
            float rate = input.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            velocity = Vector2.MoveTowards(velocity, input * maxSpeed, rate * dt);

            Vector2 position = (Vector2)transform.position + velocity * dt;

            float minX = ScreenBounds.Left + paddingMin.x;
            float maxX = ScreenBounds.Right - paddingMax.x;
            float minY = ScreenBounds.Bottom + paddingMin.y;
            float maxY = ScreenBounds.Top - paddingMax.y;

            if (position.x < minX) { position.x = minX; if (velocity.x < 0f) velocity.x = 0f; }
            else if (position.x > maxX) { position.x = maxX; if (velocity.x > 0f) velocity.x = 0f; }
            if (position.y < minY) { position.y = minY; if (velocity.y < 0f) velocity.y = 0f; }
            else if (position.y > maxY) { position.y = maxY; if (velocity.y > 0f) velocity.y = 0f; }

            transform.position = new Vector3(position.x, position.y, 0f);
        }
    }
}
