using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// Hornet: 화면을 빠르게 가로지르며 45°, 0°, -45° 방향으로 총알 3발을 발사한다.
    /// </summary>
    public sealed class HornetEnemy : Enemy
    {
        [Header("Hornet Movement")]
        [Tooltip("화면 오른쪽 끝에서 왼쪽 끝까지 이동하는 데 걸리는 시간 범위(초). 값이 클수록 느리다")]
        [SerializeField] private Vector2 crossTimeRange = new Vector2(0.74f, 1.11f);

        [Header("Hornet Attack")]
        [SerializeField] private Bullet bulletPrefab;
        [Tooltip("총알 속도(유닛/초). 보스 총알과 같은 값으로 맞춘다")]
        [SerializeField, Min(0.1f)] private float bulletSpeed = 7.2f;
        [Tooltip("위/아래 총알의 벌어짐 각도(도)")]
        [SerializeField, Range(0f, 90f)] private float spreadAngle = 45f;
        [Tooltip("발사 간격 범위(초)")]
        [SerializeField] private Vector2 fireIntervalRange = new Vector2(1f, 1.5f);
        [Tooltip("화면 진입 후 첫 발사까지의 지연 범위(초)")]
        [SerializeField] private Vector2 firstShotDelayRange = new Vector2(0.15f, 0.45f);
        [Tooltip("총알 발사 위치(몸통 기준 오프셋)")]
        [SerializeField] private Vector2 muzzleOffset = new Vector2(-0.12f, 0f);

        [Header("Hornet Animation")]
        [SerializeField] private Sprite[] idleFrames;
        [Tooltip("발사 직전 예고 동작에 쓰는 프레임")]
        [SerializeField] private Sprite[] attackFrames;
        [SerializeField, Min(0.01f)] private float idleFps = 16f;
        [SerializeField, Min(0.01f)] private float attackFps = 20f;
        [SerializeField, Min(0f)] private float attackTelegraphTime = 0.18f;

        private float speed;
        private float fireTimer;
        private bool telegraphing;

        protected override void OnActivated()
        {
            float crossTime = Mathf.Max(0.1f, Random.Range(crossTimeRange.x, crossTimeRange.y));
            speed = ScreenBounds.Width / crossTime;
            fireTimer = Random.Range(firstShotDelayRange.x, firstShotDelayRange.y);
            telegraphing = false;
            PlayIdle();
        }

        protected override void Tick(float deltaTime)
        {
            Vector3 p = transform.position;
            p.x -= speed * deltaTime;
            transform.position = p;

            // 화면 안에 있을 때만 발사 타이머가 흐른다
            if (bulletPrefab == null || p.x > ScreenBounds.Right) return;

            fireTimer -= deltaTime;

            if (!telegraphing && fireTimer <= attackTelegraphTime)
            {
                telegraphing = true;
                if (Animator != null && attackFrames != null && attackFrames.Length > 0)
                    Animator.Play(attackFrames, attackFps, true);
            }

            if (fireTimer <= 0f)
            {
                if (p.x > ScreenBounds.Left) Fire();
                fireTimer = Random.Range(fireIntervalRange.x, fireIntervalRange.y);
                telegraphing = false;
                PlayIdle();
            }
        }

        private void Fire()
        {
            Vector2 origin = (Vector2)transform.position + muzzleOffset;
            // 진행 방향(왼쪽 = 180°) 기준 +45°, 0°, -45°
            for (int i = -1; i <= 1; i++)
            {
                float angle = (180f + i * spreadAngle) * Mathf.Deg2Rad;
                Bullet bullet = PoolManager.Spawn(bulletPrefab, origin, Quaternion.identity);
                if (bullet != null)
                    bullet.Launch(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), bulletSpeed);
            }
        }

        private void PlayIdle()
        {
            if (Animator != null && idleFrames != null && idleFrames.Length > 0)
                Animator.Play(idleFrames, idleFps, true);
        }
    }
}
