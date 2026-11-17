using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 최전방 안개(FogMAT 파티클). 방출 위치를 화면 오른쪽 밖에 맞추고 시작 시 화면 전체에 안개가 깔려 있도록 미리 시뮬레이션한다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class FogController : MonoBehaviour
    {
        [SerializeField] private ParticleSystem particles;
        [Tooltip("화면 오른쪽 가장자리에서 방출 위치까지의 거리(유닛)")]
        [SerializeField, Min(0f)] private float spawnMargin = 3f;
        [Tooltip("안개가 깔리는 세로 범위(뷰포트 Y)")]
        [SerializeField] private Vector2 viewportYRange = new Vector2(-0.05f, 0.62f);
        [Tooltip("시작 시 미리 진행시켜 둘 시간(초)")]
        [SerializeField, Min(0f)] private float prewarmTime = 5f;

        private void Awake()
        {
            if (particles == null) particles = GetComponent<ParticleSystem>();
        }

        private void Start()
        {
            float yMin = ScreenBounds.FromViewport(0f, viewportYRange.x).y;
            float yMax = ScreenBounds.FromViewport(0f, viewportYRange.y).y;

            transform.position = new Vector3(ScreenBounds.Right + spawnMargin, (yMin + yMax) * 0.5f, 0f);

            ParticleSystem.ShapeModule shape = particles.shape;
            Vector3 scale = shape.scale;
            scale.y = Mathf.Abs(yMax - yMin);
            shape.scale = scale;

            particles.Clear(true);
            if (prewarmTime > 0f) particles.Simulate(prewarmTime, true, true);
            particles.Play(true);
        }
    }
}
