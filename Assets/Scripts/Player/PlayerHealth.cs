using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 플레이어 체력/피격/무적/부활. CircleCollider2D를 피격 판정으로 사용하며,
    /// 적 몸통·아이템과의 접촉은 매 프레임 겹침 질의로 처리한다.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField, Min(1)] private int maxHealth = 5;

        [Header("References")]
        [SerializeField] private CircleCollider2D hitCollider;
        [SerializeField] private SpriteFlasher flasher;
        [SerializeField] private PlayerDeathSequence deathSequence;

        [Header("Hit Feedback")]
        [SerializeField] private Material hitFlashMaterial;
        [Tooltip("피격 시 흰색 점멸 횟수")]
        [SerializeField, Min(1)] private int hitFlashCount = 3;
        [SerializeField, Min(0.01f)] private float hitFlashInterval = 0.05f;
        [Tooltip("피격 이펙트(hit - yellow) 크기 배율")]
        [SerializeField, Min(0f)] private float hitEffectScale = 1f;

        [Header("Revive")]
        [SerializeField] private Material reviveFlashMaterial;
        [Tooltip("부활 직후 무적 + 초록 점멸 지속 시간(초)")]
        [SerializeField, Min(0f)] private float reviveInvincibleTime = 1.5f;
        [SerializeField, Min(0.01f)] private float reviveFlashInterval = 0.06f;

        [Header("Contact")]
        [Tooltip("몸통 접촉을 검사할 레이어(적, 아이템)")]
        [SerializeField] private LayerMask contactMask;

        private static readonly Collider2D[] s_Overlaps = new Collider2D[16];

        private int health;
        private bool dead;
        private float invincibleTimer;
        private ContactFilter2D contactFilter;

        /// <summary>현재 씬의 플레이어.</summary>
        public static PlayerHealth Current { get; private set; }

        public int Health => health;
        public int MaxHealth => maxHealth;
        public bool IsDead => dead;
        public bool IsInvincible => invincibleTimer > 0f;

        /// <summary>적 총알/몸통의 판정 대상이 될 수 있는 상태인지.</summary>
        public bool IsTargetable => !dead && isActiveAndEnabled && hitCollider != null && hitCollider.enabled;

        /// <summary>피격 원의 월드 중심.</summary>
        public Vector2 HitCenter => hitCollider != null
            ? (Vector2)transform.TransformPoint(hitCollider.offset)
            : (Vector2)transform.position;

        /// <summary>피격 원의 월드 반지름.</summary>
        public float HitRadius
        {
            get
            {
                if (hitCollider == null) return 0f;
                Vector3 scale = transform.lossyScale;
                return hitCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }
        }

        private void Awake()
        {
            if (hitCollider == null) hitCollider = GetComponent<CircleCollider2D>();

            contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(contactMask);
            contactFilter.useTriggers = true;

            health = maxHealth;
            Current = this;
        }

        private void OnEnable()
        {
            Current = this;
            EventBus.Subscribe<ItemPickedEvent>(OnItemPicked);
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ItemPickedEvent>(OnItemPicked);
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
            if (Current == this) Current = null;
        }

        private void Start()
        {
            EventBus.Publish(new PlayerHealthChangedEvent { health = health, maxHealth = maxHealth });
        }

        private void Update()
        {
            if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;
        }

        // 물리 동기화(GameManager.LateUpdate, 실행 순서 -100) 이후에 접촉을 검사한다.
        private void LateUpdate()
        {
            if (!IsTargetable) return;

            int count = Physics2D.OverlapCircle(HitCenter, HitRadius, contactFilter, s_Overlaps);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = s_Overlaps[i];
                if (other == null) continue;

                var contact = other.GetComponentInParent<IPlayerContact>();
                if (contact != null) contact.OnPlayerContact(this);

                if (dead) break; // 접촉 피해로 사망한 경우 이후 접촉은 무시
            }
        }

        public HitResult TakeDamage(int amount, Vector2 hitPoint)
        {
            if (dead || IsInvincible || amount <= 0) return HitResult.Ignored;

            health = Mathf.Max(0, health - amount);

            Vector2 center = HitCenter;
            VfxManager.Spawn(VfxId.PlayerHit, Vector2.Lerp(center, hitPoint, 0.5f), hitEffectScale, Random.Range(0f, 360f));

            EventBus.Publish(new PlayerHealthChangedEvent { health = health, maxHealth = maxHealth });
            EventBus.Publish(new PlayerDamagedEvent { health = health, position = center });

            if (health <= 0)
            {
                Die();
                return HitResult.Killed;
            }

            if (flasher != null && hitFlashMaterial != null)
                flasher.Flash(hitFlashMaterial, hitFlashCount, hitFlashInterval);

            return HitResult.Damaged;
        }

        public void Heal(int amount)
        {
            if (dead || amount <= 0) return;

            int before = health;
            health = Mathf.Min(maxHealth, health + amount);
            if (health != before)
                EventBus.Publish(new PlayerHealthChangedEvent { health = health, maxHealth = maxHealth });
        }

        private void Die()
        {
            dead = true;
            invincibleTimer = 0f;
            hitCollider.enabled = false;

            EventBus.Publish(new PlayerDiedEvent { position = transform.position });

            if (deathSequence != null)
                deathSequence.Play();
            else
                EventBus.Publish(new PlayerDeathSequenceFinishedEvent());
        }

        /// <summary>지정 위치에서 체력을 가득 채워 부활한다. 부활 직후 일정 시간 무적 + 초록 점멸.</summary>
        public void Revive(Vector2 position)
        {
            if (!dead) return;

            transform.position = new Vector3(position.x, position.y, 0f);
            if (deathSequence != null) deathSequence.ResetVisual();

            dead = false;
            health = maxHealth;
            hitCollider.enabled = true;
            invincibleTimer = reviveInvincibleTime;

            if (flasher != null)
            {
                flasher.Stop();
                if (reviveFlashMaterial != null && reviveInvincibleTime > 0f)
                    flasher.FlashFor(reviveFlashMaterial, reviveInvincibleTime, reviveFlashInterval);
            }

            EventBus.Publish(new PlayerHealthChangedEvent { health = health, maxHealth = maxHealth });
            EventBus.Publish(new PlayerRevivedEvent { position = position });
        }

        private void OnItemPicked(ItemPickedEvent evt)
        {
            // 구급상자: 체력바 한 칸 회복
            if (evt.type == ItemType.Medkit) Heal(1);
        }

        // RETRY: 사망 연출/점멸/무적을 모두 걷어내고 처음 상태(체력 가득)로 되돌린다.
        // 위치는 PlayerController가 시작 위치로 되돌린다.
        private void OnGameReset(GameResetEvent evt)
        {
            if (deathSequence != null) deathSequence.ResetVisual();
            if (flasher != null) flasher.Stop();

            dead = false;
            health = maxHealth;
            invincibleTimer = 0f;
            hitCollider.enabled = true;

            EventBus.Publish(new PlayerHealthChangedEvent { health = health, maxHealth = maxHealth });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Current = null;
        }
    }
}
