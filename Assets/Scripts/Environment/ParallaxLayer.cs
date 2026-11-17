using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 가로로 타일링된 스프라이트 한 줄을 오른쪽 → 왼쪽으로 무한 스크롤한다(패럴랙스 레이어 1개).
    /// SpriteRenderer의 Draw Mode는 Tiled여야 한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer tiledRenderer;
        [Tooltip("오른쪽에서 왼쪽으로 흐르는 속도(유닛/초)")]
        [SerializeField] private float scrollSpeed = 1f;

        private float tileLocalWidth = 1f;
        private float offset;

        public float ScrollSpeed { get => scrollSpeed; set => scrollSpeed = value; }

        private void Awake()
        {
            if (tiledRenderer == null) tiledRenderer = GetComponent<SpriteRenderer>();
            if (tiledRenderer.sprite != null)
                tileLocalWidth = Mathf.Max(0.01f, tiledRenderer.sprite.bounds.size.x);
        }

        private void Update()
        {
            float tileWorldWidth = tileLocalWidth * Mathf.Abs(transform.lossyScale.x);
            if (tileWorldWidth <= 0.0001f) return;

            // 화면 폭 + 스크롤 여유분을 항상 덮도록 타일 개수를 맞춘다(화면 비율이 달라져도 대응)
            int tiles = Mathf.CeilToInt((ScreenBounds.Width + tileWorldWidth * 2f) / tileWorldWidth);
            float wantedWidth = tiles * tileLocalWidth;
            if (!Mathf.Approximately(tiledRenderer.size.x, wantedWidth))
                tiledRenderer.size = new Vector2(wantedWidth, tiledRenderer.size.y);

            // 타일 한 칸만큼 이동하면 같은 그림이 되므로 오프셋을 타일 폭으로 반복
            offset = Mathf.Repeat(offset + scrollSpeed * Time.deltaTime, tileWorldWidth);

            Vector3 p = transform.position;
            p.x = ScreenBounds.Center.x - offset;
            transform.position = p;
        }
    }
}
