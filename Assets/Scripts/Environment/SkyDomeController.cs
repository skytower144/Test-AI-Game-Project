using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// DomePrefab_Toon의 스카이돔에 하늘 텍스처/스크롤 속도/색조를 적용한다.
    /// 공유 머티리얼 에셋을 건드리지 않도록 MaterialPropertyBlock을 사용한다.
    /// </summary>
    [ExecuteAlways]
    public sealed class SkyDomeController : MonoBehaviour
    {
        [SerializeField] private Renderer domeRenderer;
        [SerializeField] private Texture2D skyTexture;
        [Tooltip("돔 둘레를 따라 텍스처가 반복되는 횟수")]
        [SerializeField] private Vector2 tiling = new Vector2(2f, 1f);
        [Tooltip("하늘이 가로로 흐르는 속도. 양수 = 오른쪽에서 왼쪽")]
        [SerializeField] private float scrollSpeed = 0.5f;
        [SerializeField] private Color tint = Color.white;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int ScrollXId = Shader.PropertyToID("_ScrollX");
        private static readonly int ColorTintId = Shader.PropertyToID("_ColorTint");

        private MaterialPropertyBlock block;

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        public void Apply()
        {
            if (domeRenderer == null) return;

            if (block == null) block = new MaterialPropertyBlock();
            domeRenderer.GetPropertyBlock(block);

            if (skyTexture != null) block.SetTexture(MainTexId, skyTexture);
            block.SetVector(MainTexStId, new Vector4(tiling.x, tiling.y, 0f, 0f));
            block.SetFloat(ScrollXId, scrollSpeed);
            block.SetColor(ColorTintId, tint);

            domeRenderer.SetPropertyBlock(block);
        }
    }
}
