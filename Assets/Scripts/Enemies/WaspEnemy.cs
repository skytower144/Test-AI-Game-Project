using UnityEngine;

namespace Chopper
{
    /// <summary>Wasp: 사인파(SineWave) 궤도로 느리게 왼쪽으로 이동한다. 속도와 궤도는 생성 시 랜덤.</summary>
    public sealed class WaspEnemy : Enemy
    {
        [Header("Wasp Movement")]
        [Tooltip("왼쪽으로 이동하는 속도 범위(유닛/초)")]
        [SerializeField] private Vector2 speedRange = new Vector2(1.3f, 2.3f);
        [Tooltip("사인파 진폭 범위(유닛)")]
        [SerializeField] private Vector2 amplitudeRange = new Vector2(0.25f, 0.85f);
        [Tooltip("사인파 각속도 범위(라디안/초)")]
        [SerializeField] private Vector2 frequencyRange = new Vector2(1.6f, 3.6f);
        [Tooltip("화면 위/아래 가장자리와 유지할 여백")]
        [SerializeField, Min(0f)] private float verticalPadding = 0.25f;

        private float speed;
        private float amplitude;
        private float frequency;
        private float phase;
        private float baseY;
        private float elapsed;

        protected override void OnActivated()
        {
            speed = Random.Range(speedRange.x, speedRange.y);
            amplitude = Random.Range(amplitudeRange.x, amplitudeRange.y);
            frequency = Random.Range(frequencyRange.x, frequencyRange.y);
            phase = Random.Range(0f, Mathf.PI * 2f);
            elapsed = 0f;

            // 파동 전체가 화면 안에 들어오도록 기준 높이를 보정
            float min = ScreenBounds.Bottom + verticalPadding + amplitude;
            float max = ScreenBounds.Top - verticalPadding - amplitude;
            float y = transform.position.y;
            baseY = min < max ? Mathf.Clamp(y, min, max) : (ScreenBounds.Bottom + ScreenBounds.Top) * 0.5f;

            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, baseY + Mathf.Sin(phase) * amplitude, 0f);
        }

        protected override void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            Vector3 p = transform.position;
            p.x -= speed * deltaTime;
            p.y = baseY + Mathf.Sin(phase + elapsed * frequency) * amplitude;
            transform.position = p;
        }
    }
}
