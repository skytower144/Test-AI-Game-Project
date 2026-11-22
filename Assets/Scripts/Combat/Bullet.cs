using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    public enum BulletTeam { Player = 0, Enemy = 1 }

    /// <summary>
    /// 풀링되는 직선 총알. 매 프레임 이동 구간 전체를 스윕 판정해 빠른 속도에서도 관통(터널링)이 없다.
    /// 플레이어 총알은 Physics2D CircleCast로 적 콜라이더를, 적 총알은 플레이어의 CircleCollider2D를 판정한다.
    /// </summary>
    public sealed class Bullet : MonoBehaviour, IPoolable
    {
        [SerializeField] private BulletTeam team = BulletTeam.Player;
        [SerializeField, Min(0f)] private float speed = 14f;
        [SerializeField, Min(0)] private int damage = 1;
        [Tooltip("충돌 판정 반지름(월드 단위)")]
        [SerializeField, Min(0.001f)] private float hitRadius = 0.06f;
        [Tooltip("플레이어 총알이 충돌할 레이어(적)")]
        [SerializeField] private LayerMask targetMask;
        [Tooltip("진행 방향으로 스프라이트를 회전")]
        [SerializeField] private bool rotateToDirection = true;
        [Tooltip("화면 밖으로 이만큼 벗어나면 풀에 반납")]
        [SerializeField, Min(0f)] private float despawnMargin = 0.4f;
        [SerializeField, Min(0f)] private float hitEffectScale = 1f;

        /// <summary>현재 활성화된 모든 적 총알(충격파 판정용).</summary>
        public static readonly List<Bullet> ActiveEnemyBullets = new List<Bullet>(256);

        private static readonly RaycastHit2D[] s_Hits = new RaycastHit2D[8];

        private Vector2 direction = Vector2.right;
        private float currentSpeed;
        private float firstStepTime;
        private bool firstStep;
        private bool live;
        private ContactFilter2D filter;

        public BulletTeam Team => team;
        public float DefaultSpeed => speed;
        public float HitRadius => hitRadius;
        public float DespawnMargin => despawnMargin;
        public bool RotatesToDirection => rotateToDirection;
        public bool IsLive => live;

        private void Awake()
        {
            filter = new ContactFilter2D();
            filter.SetLayerMask(targetMask);
            filter.useTriggers = true;
        }

        private void OnEnable()
        {
            if (team == BulletTeam.Enemy) ActiveEnemyBullets.Add(this);
        }

        private void OnDisable()
        {
            if (team == BulletTeam.Enemy) ActiveEnemyBullets.Remove(this);
            live = false;
        }

        public void OnSpawned()
        {
            Launch(team == BulletTeam.Player ? Vector2.right : Vector2.left);
        }

        public void OnDespawned()
        {
            live = false;
        }

        /// <summary>
        /// 총알을 발사한다.
        /// speedOverride가 0 이하면 프리팹 기본 속도를 사용한다.
        /// elapsed는 "원래 발사됐어야 할 시각"으로부터 지난 시간으로, 그만큼 미리 전진시켜 탄 간격을 균일하게 만든다.
        /// </summary>
        public void Launch(Vector2 launchDirection, float speedOverride = 0f, float elapsed = 0f)
        {
            direction = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : Vector2.right;
            currentSpeed = speedOverride > 0f ? speedOverride : speed;
            firstStepTime = Mathf.Max(0f, elapsed);
            firstStep = true;
            live = true;

            if (rotateToDirection)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        // 이동/판정은 모든 Update(적·플레이어 이동)가 끝난 뒤, GameManager의 물리 동기화 이후에 수행한다.
        private void LateUpdate()
        {
            if (!live) return;

            float dt = Time.deltaTime;
            if (firstStep)
            {
                firstStep = false;
                dt = Mathf.Min(firstStepTime, dt);
            }

            float distance = currentSpeed * dt;
            Vector2 from = transform.position;
            Vector2 to = from + direction * distance;

            bool consumed = team == BulletTeam.Player ? HitEnemies(from, distance) : HitPlayer(from, to);
            if (consumed) return;

            transform.position = new Vector3(to.x, to.y, 0f);

            if (ScreenBounds.IsOutside(to, despawnMargin))
                PoolManager.Despawn(gameObject);
        }

        private bool HitEnemies(Vector2 from, float distance)
        {
            int count = Physics2D.CircleCast(from, hitRadius, direction, filter, s_Hits, distance);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = s_Hits[i].collider;
                if (other == null) continue;

                var target = other.GetComponentInParent<IDamageable>();
                if (target == null) continue;

                // 시작 지점부터 겹쳐 있던 경우 hit.point가 유효하지 않을 수 있어 총알 위치를 사용한다.
                Vector2 point = s_Hits[i].distance > 0.0001f ? s_Hits[i].point : from;

                HitResult result = target.TakeDamage(damage, point);
                if (result == HitResult.Ignored) continue;

                VfxManager.Spawn(VfxId.BulletHit, point, hitEffectScale);
                EventBus.Publish(new BulletImpactEvent { position = point });
                PoolManager.Despawn(gameObject);
                return true;
            }
            return false;
        }

        private bool HitPlayer(Vector2 from, Vector2 to)
        {
            PlayerHealth player = PlayerHealth.Current;
            if (player == null || !player.IsTargetable) return false;

            Vector2 center = player.HitCenter;
            float radius = player.HitRadius + hitRadius;

            // 이동 선분(from→to)과 플레이어 원 중심 사이의 최단 거리로 판정
            Vector2 segment = to - from;
            float lengthSq = segment.sqrMagnitude;
            float t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(center - from, segment) / lengthSq) : 0f;
            Vector2 closest = from + segment * t;

            if ((center - closest).sqrMagnitude > radius * radius) return false;

            HitResult result = player.TakeDamage(damage, closest);
            if (result == HitResult.Ignored) return false;

            PoolManager.Despawn(gameObject);
            return true;
        }

        /// <summary>충돌 이펙트를 재생하고 소멸한다(충격파에 휩쓸렸을 때).</summary>
        public void Explode()
        {
            if (!live) return;
            live = false;
            VfxManager.Spawn(VfxId.BulletHit, transform.position, hitEffectScale);
            PoolManager.Despawn(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ActiveEnemyBullets.Clear();
        }
    }
}
