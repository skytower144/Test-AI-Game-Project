using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 원형 충격파. 점점 커지면서 두께가 얇아지다가 사라지고, 색은 빨강에서 노랑으로 변한다.
    /// 원 안에 들어온 모든 몬스터를 처치하고 적 총알은 충돌 이펙트와 함께 파괴한다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class Shockwave : MonoBehaviour, IPoolable
    {
        [SerializeField] private LineRenderer line;
        [SerializeField, Min(8)] private int segments = 96;
        [SerializeField, Min(0.05f)] private float duration = 0.8f;
        [SerializeField, Min(0.1f)] private float maxRadius = 11f;
        [SerializeField, Min(0f)] private float startRadius = 0.2f;
        [SerializeField, Min(0f)] private float startThickness = 0.55f;
        [SerializeField] private Color startColor = new Color(1f, 0.12f, 0.08f, 1f);
        [SerializeField] private Color endColor = new Color(1f, 0.92f, 0.2f, 1f);
        [Tooltip("진행도(0~1)에 따른 반지름 비율")]
        [SerializeField] private AnimationCurve radiusCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Vector3[] points;
        private Vector2[] unitCircle;
        private float elapsed;
        private bool running;

        public float CurrentRadius { get; private set; }

        private void Awake()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            BuildCircle();
        }

        private void BuildCircle()
        {
            int count = Mathf.Max(8, segments);
            unitCircle = new Vector2[count];
            points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                unitCircle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = count;
        }

        public void OnSpawned()
        {
            elapsed = 0f;
            running = true;
            Apply(0f);
        }

        public void OnDespawned()
        {
            running = false;
        }

        private void Update()
        {
            if (!running) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Apply(t);
            Sweep();

            if (t >= 1f)
            {
                running = false;
                PoolManager.Despawn(gameObject);
            }
        }

        private void Apply(float t)
        {
            float radius = Mathf.Lerp(startRadius, maxRadius, radiusCurve.Evaluate(t));
            CurrentRadius = radius;

            for (int i = 0; i < points.Length; i++)
                points[i] = new Vector3(unitCircle[i].x * radius, unitCircle[i].y * radius, 0f);
            line.SetPositions(points);

            // 두께는 선형으로 0까지 감소, 색은 빨강 → 노랑
            float thickness = Mathf.Lerp(startThickness, 0f, t);
            line.startWidth = thickness;
            line.endWidth = thickness;

            Color color = Color.Lerp(startColor, endColor, t);
            line.startColor = color;
            line.endColor = color;
        }

        private void Sweep()
        {
            Vector2 center = transform.position;
            float radiusSq = CurrentRadius * CurrentRadius;

            // 역순 순회: 처치/파괴 시 목록에서 제거되어도 안전
            var enemies = Enemy.Active;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (i >= enemies.Count) continue;
                Enemy enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                if (((Vector2)enemy.transform.position - center).sqrMagnitude <= radiusSq)
                    enemy.Kill();
            }

            var bullets = Bullet.ActiveEnemyBullets;
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                if (i >= bullets.Count) continue;
                Bullet bullet = bullets[i];
                if (bullet == null || !bullet.IsLive) continue;
                if (((Vector2)bullet.transform.position - center).sqrMagnitude <= radiusSq)
                    bullet.Explode();
            }
        }
    }
}
