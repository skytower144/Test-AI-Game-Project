using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 일반 몬스터(Wasp, Hornet) 공통 로직: 체력, 피격 점멸, 처치(점수/이펙트/이벤트), 몸통 접촉 피해, 화면 이탈 처리.
    /// </summary>
    public abstract class Enemy : MonoBehaviour, IPoolable, IDamageable, IPlayerContact
    {
        [Header("Stats")]
        [SerializeField] private EnemyType enemyType = EnemyType.Wasp;
        [Tooltip("처치에 필요한 피격 횟수")]
        [SerializeField, Min(1)] private int maxHealth = 1;
        [SerializeField, Min(0)] private int scoreValue = 50;
        [Tooltip("몸통 접촉 시 플레이어 체력 감소량")]
        [SerializeField, Min(0)] private int contactDamage = 1;
        [Tooltip("계속 겹쳐 있을 때 다시 피해를 주기까지의 간격(초)")]
        [SerializeField, Min(0f)] private float contactDamageInterval = 1f;

        [Header("Visual")]
        [SerializeField] private SpriteAnimator animator;
        [SerializeField] private SpriteFlasher flasher;
        [SerializeField] private bool flashOnHit;
        [SerializeField] private Material hitFlashMaterial;
        [SerializeField, Min(0.01f)] private float hitFlashInterval = 0.05f;
        [SerializeField, Min(0f)] private float deathEffectScale = 1f;

        [Header("Bounds")]
        [Tooltip("왼쪽 화면 밖으로 이만큼 벗어나면 제거")]
        [SerializeField, Min(0f)] private float despawnMargin = 0.6f;

        /// <summary>현재 활성화된 모든 몬스터.</summary>
        public static readonly List<Enemy> Active = new List<Enemy>(16);

        private int health;
        private bool alive;
        private float nextContactTime;

        public EnemyType Type => enemyType;
        public bool IsAlive => alive;
        protected SpriteAnimator Animator => animator;

        protected virtual void OnEnable()
        {
            Active.Add(this);
        }

        protected virtual void OnDisable()
        {
            Active.Remove(this);
            alive = false;
        }

        public void OnSpawned()
        {
            health = maxHealth;
            alive = true;
            nextContactTime = 0f;
            if (flasher != null) flasher.Stop();
            OnActivated();
        }

        public void OnDespawned()
        {
            alive = false;
        }

        /// <summary>풀에서 꺼내져 활성화될 때 1회 호출. 이동 파라미터 등을 초기화한다.</summary>
        protected abstract void OnActivated();

        /// <summary>매 프레임 이동/행동 갱신.</summary>
        protected abstract void Tick(float deltaTime);

        private void Update()
        {
            if (!alive) return;

            Tick(Time.deltaTime);

            // 왼쪽 화면 밖으로 나가면 제거(점수/드롭 없음)
            if (alive && transform.position.x < ScreenBounds.Left - despawnMargin)
                PoolManager.Despawn(gameObject);
        }

        public HitResult TakeDamage(int amount, Vector2 hitPoint)
        {
            if (!alive) return HitResult.Ignored;

            health -= Mathf.Max(0, amount);
            if (health <= 0)
            {
                Kill();
                return HitResult.Killed;
            }

            if (flashOnHit && flasher != null && hitFlashMaterial != null)
                flasher.Flash(hitFlashMaterial, 1, hitFlashInterval);

            return HitResult.Damaged;
        }

        /// <summary>처치: 폭발 이펙트, 점수/드롭 이벤트 발행 후 풀에 반납.</summary>
        public void Kill()
        {
            if (!alive) return;
            alive = false;

            Vector2 position = transform.position;
            VfxManager.Spawn(VfxId.SmallExplosion, position, deathEffectScale);
            EventBus.Publish(new EnemyKilledEvent { type = enemyType, position = position, score = scoreValue });
            PoolManager.Despawn(gameObject);
        }

        public void OnPlayerContact(PlayerHealth player)
        {
            if (!alive || contactDamage <= 0 || player == null) return;
            if (Time.time < nextContactTime) return;

            HitResult result = player.TakeDamage(contactDamage, transform.position);
            if (result != HitResult.Ignored)
                nextContactTime = Time.time + contactDamageInterval;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Active.Clear();
        }
    }
}
