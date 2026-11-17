using UnityEngine;

namespace Chopper
{
    /// <summary>배경에서 오른쪽 → 왼쪽으로 떠밀려가는 잔해 하나.</summary>
    public sealed class DriftingDebris : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private float speed;
        private float spin;
        private float halfWidth;
        private bool live;

        public float HalfWidth => halfWidth;

        public void OnSpawned()
        {
            live = true;
        }

        public void OnDespawned()
        {
            live = false;
        }

        public void Setup(Sprite sprite, float scale, float driftSpeed, float spinDegrees, Color color, bool flipX, int sortingOrder)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.flipX = flipX;
            spriteRenderer.sortingOrder = sortingOrder;

            transform.localScale = new Vector3(scale, scale, 1f);
            speed = driftSpeed;
            spin = spinDegrees;
            // 회전해도 가려지지 않도록 대각선 길이 기준으로 화면 이탈 판정
            halfWidth = sprite != null ? sprite.bounds.extents.magnitude * scale : scale;
            live = true;
        }

        private void Update()
        {
            if (!live) return;

            float dt = Time.deltaTime;
            Vector3 p = transform.position;
            p.x -= speed * dt;
            transform.position = p;

            if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);

            if (p.x < ScreenBounds.Left - halfWidth)
            {
                live = false;
                PoolManager.Despawn(gameObject);
            }
        }
    }
}
