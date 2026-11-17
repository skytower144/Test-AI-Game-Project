using UnityEngine;
using UnityEngine.UI;

namespace Chopper
{
    /// <summary>UI 그래픽의 투명도를 부드럽게 증가/감소시키는 반복 애니메이션(잠수함 레이더 느낌).</summary>
    public sealed class UIAlphaPulse : MonoBehaviour
    {
        [SerializeField] private Graphic target;
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.25f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;
        [Tooltip("투명도가 한 번 증가했다 감소하는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.05f)] private float period = 2.6f;

        private float time;

        private void Awake()
        {
            if (target == null) target = GetComponent<Graphic>();
        }

        private void OnEnable()
        {
            time = 0f;
            Apply();
        }

        private void Update()
        {
            time += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            if (target == null) return;
            // 코사인 곡선: 양 끝에서 속도가 0이 되어 부드럽게 반전
            float k = 0.5f - 0.5f * Mathf.Cos(time / period * Mathf.PI * 2f);
            Color color = target.color;
            color.a = Mathf.Lerp(minAlpha, maxAlpha, k);
            target.color = color;
        }
    }
}
