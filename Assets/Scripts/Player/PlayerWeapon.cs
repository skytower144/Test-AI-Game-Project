using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 발사키를 누르고 있는 동안 총알을 연발한다.
    /// 기본은 첫 번째 총알타입 1발, 라이플 탄창을 획득하면 일정 시간 동안 두 번째 총알타입을 함께 쏴 한 번에 2발을 발사한다.
    /// </summary>
    public sealed class PlayerWeapon : MonoBehaviour
    {
        [Header("Bullets")]
        [Tooltip("첫 번째 총알타입 (Fx_02)")]
        [SerializeField] private Bullet primaryBulletPrefab;
        [Tooltip("두 번째 총알타입 (Fx_01)")]
        [SerializeField] private Bullet rifleBulletPrefab;

        [Header("Firing")]
        [Tooltip("발사 간격(초). 두 총알타입에 동일하게 적용")]
        [SerializeField, Min(0.01f)] private float fireInterval = 0.1125f;
        [Tooltip("라이플 탄창 지속 시간(초). 이 시간 동안 두 번째 총알타입이 함께 발사된다. 재획득 시 이 값으로 초기화")]
        [SerializeField, Min(0f)] private float rifleDuration = 7f;

        [Header("References")]
        [Tooltip("첫 번째 총알타입 발사 지점(헬리콥터의 문)")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private SpriteAnimator muzzleFlash;
        [Tooltip("두 번째 총알타입 발사 지점(문 오른쪽 위 꼭짓점의 수직선이 천장과 만나는 위치)")]
        [SerializeField] private Transform rifleFirePoint;
        [Tooltip("두 번째 총알타입의 Muzzle Flash(헬리콥터 이미지 뒤에 그려진다)")]
        [SerializeField] private SpriteAnimator rifleMuzzleFlash;
        [SerializeField] private PlayerHealth health;

        private float cooldown;
        private float rifleTimeLeft;
        private bool wasFiring;

        public float RifleTimeLeft => rifleTimeLeft;

        /// <summary>라이플 탄창 효과(두 번째 총알타입 동시 발사)가 켜져 있는지.</summary>
        public bool RifleActive => rifleTimeLeft > 0f;

        private void Awake()
        {
            if (health == null) health = GetComponent<PlayerHealth>();
            if (firePoint == null) firePoint = transform;
            if (rifleFirePoint == null) rifleFirePoint = firePoint;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ItemPickedEvent>(OnItemPicked);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ItemPickedEvent>(OnItemPicked);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (rifleTimeLeft > 0f) rifleTimeLeft -= dt;
            cooldown -= dt;

            bool canFire = health != null && !health.IsDead && GameManager.ControlsEnabled && GameInput.FireHeld;
            if (!canFire)
            {
                if (cooldown < 0f) cooldown = 0f;
                wasFiring = false;
                return;
            }

            if (cooldown > 0f) return;

            // 연사 중에는 cooldown(음수)의 크기만큼 "원래 발사됐어야 할 시각"이 지난 것이므로
            // 그만큼 총알을 미리 전진시켜 프레임률과 무관하게 탄 간격을 균일하게 유지한다.
            float overdue = wasFiring ? Mathf.Min(-cooldown, dt) : 0f;
            wasFiring = true;
            Fire(overdue);
            cooldown = fireInterval - overdue;
        }

        private void Fire(float overdue)
        {
            // 첫 번째 총알타입은 항상 발사
            Shoot(primaryBulletPrefab, firePoint, muzzleFlash, BulletType.Primary, overdue);

            // 라이플 탄창 지속 중에는 두 번째 총알타입을 함께 발사 → 한 번에 2발
            if (RifleActive)
                Shoot(rifleBulletPrefab, rifleFirePoint, rifleMuzzleFlash, BulletType.Rifle, overdue);
        }

        private void Shoot(Bullet prefab, Transform origin, SpriteAnimator flash, BulletType type, float overdue)
        {
            if (prefab == null || origin == null) return;

            Bullet bullet = PoolManager.Spawn(prefab, origin.position, Quaternion.identity);
            if (bullet != null) bullet.Launch(Vector2.right, 0f, overdue);

            if (flash != null) flash.Play();
            EventBus.Publish(new PlayerFiredEvent { bulletType = type, interval = fireInterval });
        }

        private void OnItemPicked(ItemPickedEvent evt)
        {
            switch (evt.type)
            {
                case ItemType.RifleAmmo:
                    // 두 번째 총알타입 활성화. 재획득 시 지속시간 초기화
                    rifleTimeLeft = rifleDuration;
                    break;

                case ItemType.ShotgunAmmo:
                    // 플레이어 중심에서 원형 충격파 생성
                    Vector2 center = health != null ? health.HitCenter : (Vector2)transform.position;
                    VfxManager.Spawn(VfxId.Shockwave, center);
                    break;
            }
        }

        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            rifleTimeLeft = 0f;
        }

        private void OnGameReset(GameResetEvent evt)
        {
            rifleTimeLeft = 0f;
            cooldown = 0f;
            wasFiring = false;
            HideFlash(muzzleFlash);
            HideFlash(rifleMuzzleFlash);
        }

        private static void HideFlash(SpriteAnimator flash)
        {
            if (flash == null) return;
            flash.Stop();
            if (flash.Target != null) flash.Target.enabled = false;
        }
    }
}
