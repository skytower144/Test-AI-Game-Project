using UnityEngine;

namespace Chopper
{
    /// <summary>헬리콥터/보스 폭발 후 중력으로 떨어지는 파츠/잔해.</summary>
    public sealed class WreckPiece : MonoBehaviour, IPoolable
    {
        [Tooltip("표시할 스프라이트 렌더러(SetSprite로 스프라이트를 바꿀 때 사용)")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("중력 가속도(유닛/초²)")]
        [SerializeField, Min(0f)] private float gravity = 9f;
        [Tooltip("화면 밖으로 이만큼 벗어나면 풀에 반납")]
        [SerializeField, Min(0f)] private float despawnMargin = 1f;
        [Tooltip("잔해 애니메이션(선택). door_16x16 스프라이트 회전 애니메이션")]
        [SerializeField] private SpriteAnimator animator;
        [Tooltip("잔해 애니메이션 속도 범위(FPS)")]
        [SerializeField] private Vector2 animationFpsRange = new Vector2(12f, 22f);

        private Vector2 velocity;
        private float angularVelocity;
        private bool live;

        public void OnSpawned()
        {
            velocity = Vector2.zero;
            angularVelocity = 0f;
            live = true;

            if (animator != null)
            {
                animator.FramesPerSecond = Random.Range(animationFpsRange.x, animationFpsRange.y);
                animator.Play();
            }
        }

        public void OnDespawned()
        {
            live = false;
        }

        /// <summary>표시할 스프라이트를 바꾼다(하나의 프리팹으로 여러 파츠를 표현할 때).</summary>
        public void SetSprite(Sprite sprite)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.sprite = sprite;
        }

        /// <summary>초기 속도와 회전 속도(도/초)를 주고 날려 보낸다.</summary>
        public void Launch(Vector2 initialVelocity, float angularVelocityDegrees)
        {
            velocity = initialVelocity;
            angularVelocity = angularVelocityDegrees;
            live = true;
        }

        private void Update()
        {
            if (!live) return;

            float dt = Time.deltaTime;
            velocity.y -= gravity * dt;

            Vector3 position = transform.position;
            position.x += velocity.x * dt;
            position.y += velocity.y * dt;
            transform.position = position;

            if (angularVelocity != 0f)
                transform.Rotate(0f, 0f, angularVelocity * dt);

            // 위로 튀어 오르는 동안은 화면 위로 벗어나도 유지, 아래/좌우로 벗어나면 반납
            if (position.y < ScreenBounds.Bottom - despawnMargin ||
                position.x < ScreenBounds.Left - despawnMargin ||
                position.x > ScreenBounds.Right + despawnMargin)
            {
                live = false;
                PoolManager.Despawn(gameObject);
            }
        }
    }
}
