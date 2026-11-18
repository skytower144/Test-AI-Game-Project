using UnityEngine;

namespace Chopper
{
    /// <summary>배경에서 오른쪽 → 왼쪽으로 떠밀려가는 잔해 하나. 회전 없이 수평으로만 이동한다.</summary>
    public sealed class DriftingDebris : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private float speed;
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

        public void Setup(Sprite sprite, float scale, float driftSpeed, Color color, bool flipX, int sortingOrder)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.flipX = flipX;
            spriteRenderer.sortingOrder = sortingOrder;

            transform.localScale = new Vector3(scale, scale, 1f);
            speed = driftSpeed;
            halfWidth = sprite != null ? sprite.bounds.extents.x * scale : scale;
            live = true;
        }

        private void Update()
        {
            if (!live) return;

            Vector3 p = transform.position;
            p.x -= speed * Time.deltaTime;
            transform.position = p;

            if (p.x < ScreenBounds.Left - halfWidth)
            {
                live = false;
                PoolManager.Despawn(gameObject);
            }
        }
    }
}
