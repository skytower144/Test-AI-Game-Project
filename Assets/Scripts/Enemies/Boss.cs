using System;
using System.Collections;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 보스(Ship6). 등장 연출 → 5가지 행동 패턴 순환 → 처치 연출을 담당한다.
    /// 몸통 접촉 피해는 없으며(IPlayerContact 미구현), 총알은 눈알 위치에서 발사한다.
    /// </summary>
    public sealed class Boss : MonoBehaviour, IPoolable, IDamageable
    {
        // ───────────── 패턴 설정 ─────────────

        [Serializable]
        public class FanPattern // 7.1
        {
            [Tooltip("상하 이동 폭(유닛)")] public float amplitude = 1.7f;
            [Tooltip("상하 왕복 1회에 걸리는 시간(초)")] public float period = 2.6f;
            [Tooltip("상하 왕복 횟수")] public int cycles = 2;
            [Tooltip("발사 간격(초)")] public float fireInterval = 0.75f;
            [Tooltip("한 번에 발사하는 총알 수")] public int bulletCount = 5;
            [Tooltip("총알 사이 각도(도)")] public float spreadStep = 14f;
            public float bulletSpeed = 4.2f;
        }

        [Serializable]
        public class SweepPattern // 7.2
        {
            [Tooltip("위아래 왕복 횟수")] public int sweeps = 4;
            [Tooltip("왕복 1회에 걸리는 시간(초)")] public float sweepPeriod = 1.1f;
            [Tooltip("위/아래로 꺾이는 최대 각도(도)")] public float sweepAngle = 55f;
            public float fireInterval = 0.05f;
            public float bulletSpeed = 5.5f;
        }

        [Serializable]
        public class SpiralPattern // 7.3
        {
            [Tooltip("시계방향 회전 수")] public int revolutions = 3;
            [Tooltip("1회전에 걸리는 시간(초)")] public float revolutionTime = 2f;
            [Tooltip("발사 간격(초). 값이 클수록 탄 사이 틈이 넓어 회피가 쉽다")] public float fireInterval = 0.065f;
            public float bulletSpeed = 3.2f;
        }

        [Serializable]
        public class RingPattern // 7.4
        {
            [Tooltip("단발 발사 반복 횟수")] public int volleys = 3;
            [Tooltip("단발 발사 간격(초)")] public float volleyInterval = 0.8f;
            [Tooltip("한 번에 전방위로 발사하는 총알 수")] public int bulletsPerVolley = 24;
            public float bulletSpeed = 3.2f;
            [Tooltip("좌우 이동 폭(유닛)")] public float moveAmplitude = 1.2f;
            [Tooltip("좌우 왕복 1회에 걸리는 시간(초)")] public float movePeriod = 1.6f;
            [Tooltip("첫 발사 전 대기(초)")] public float leadTime = 0.4f;
            [Tooltip("마지막 발사 후 대기(초)")] public float tailTime = 0.6f;
        }

        [Serializable]
        public class RotaryPattern // 7.5
        {
            [Tooltip("패턴 지속 시간(초)")] public float duration = 8f;
            [Tooltip("발사 방향 수(8 = 45도 간격)")] public int directions = 8;
            public float fireInterval = 0.14f;
            [Tooltip("발사 방향 전체가 시계방향으로 도는 속도(도/초)")] public float rotationSpeed = 22f;
            public float bulletSpeed = 3.4f;
        }

        private enum State { Inactive, FlyBy, Reveal, Fighting, Halted, Dying }

        // ───────────── 인스펙터 ─────────────

        [Header("Stats")]
        [Tooltip("5가지 패턴을 모두 경험할 수 있을 만큼의 체력")]
        [SerializeField, Min(1)] private int maxHealth = 480;
        [SerializeField, Min(0)] private int scoreValue = 1000;

        [Header("References")]
        [Tooltip("좌우 반전되는 비주얼 루트(원본 아트는 오른쪽을 향함)")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer shipRenderer;
        [SerializeField] private SpriteFlasher flasher;
        [SerializeField] private Material hitFlashMaterial;
        [Tooltip("총알 발사 지점(Ship6의 눈알)")]
        [SerializeField] private Transform eye;
        [SerializeField] private SpriteAnimator muzzleFlash;
        [SerializeField] private BoosterFlame[] boosters = Array.Empty<BoosterFlame>();
        [SerializeField] private Collider2D[] hitColliders = Array.Empty<Collider2D>();
        [SerializeField] private Bullet bulletPrefab;

        [Header("Entrance")]
        [Tooltip("화면을 왼쪽에서 오른쪽으로 가로지르는 속도(유닛/초)")]
        [SerializeField, Min(0.1f)] private float flyBySpeed = 16f;
        [Tooltip("가로지른 뒤 다시 나타나기까지의 대기(초)")]
        [SerializeField, Min(0f)] private float flyByPause = 0.7f;
        [Tooltip("오른쪽 끝에서 서서히 모습을 드러내는 시간(초)")]
        [SerializeField, Min(0.1f)] private float revealDuration = 2.6f;
        [Tooltip("화면 밖 대기 위치까지의 여유 거리(유닛)")]
        [SerializeField, Min(0f)] private float offscreenMargin = 2.4f;
        [Tooltip("전투 중 기준 위치(뷰포트 좌표)")]
        [SerializeField] private Vector2 anchorViewport = new Vector2(0.83f, 0.5f);
        [Tooltip("등장 완료 후 첫 패턴까지의 대기(초)")]
        [SerializeField, Min(0f)] private float firstPatternDelay = 1.2f;

        [Header("Movement")]
        [Tooltip("패턴 사이 대기 시간 범위(초)")]
        [SerializeField] private Vector2 patternWaitRange = new Vector2(3f, 4f);
        [SerializeField, Min(0.01f)] private float moveSmoothTime = 0.3f;
        [SerializeField, Min(0f)] private float hoverAmplitude = 0.1f;
        [SerializeField, Min(0f)] private float hoverFrequency = 1.8f;
        [Tooltip("화면 위/아래 가장자리와 유지할 여백(유닛)")]
        [SerializeField, Min(0f)] private float verticalPadding = 0.85f;

        [Header("Patterns")]
        [SerializeField] private FanPattern pattern1 = new FanPattern();
        [SerializeField] private SweepPattern pattern2 = new SweepPattern();
        [SerializeField] private SpiralPattern pattern3 = new SpiralPattern();
        [SerializeField] private RingPattern pattern4 = new RingPattern();
        [SerializeField] private RotaryPattern pattern5 = new RotaryPattern();

        [Header("Feedback")]
        [SerializeField, Min(0.01f)] private float hitFlashInterval = 0.05f;

        [Header("Death")]
        [Tooltip("처치 후 소규모 폭발이 이어지는 시간(초)")]
        [SerializeField, Min(0f)] private float deathSmallExplosionTime = 1.8f;
        [SerializeField] private Vector2 deathSmallExplosionInterval = new Vector2(0.06f, 0.15f);
        [SerializeField] private Vector2 deathSmallExplosionScale = new Vector2(1.6f, 2.8f);
        [Tooltip("소규모 폭발이 생성되는 영역(중심 기준 반크기)")]
        [SerializeField] private Vector2 hullHalfExtents = new Vector2(1.05f, 0.6f);
        [SerializeField, Min(0f)] private float deathShake = 0.04f;
        [Tooltip("대형 폭발 개수")]
        [SerializeField, Min(1)] private int blastCount = 3;
        [Tooltip("대형 폭발 생성 간격(초)")]
        [SerializeField, Min(0f)] private float blastInterval = 0.1f;
        [Tooltip("보스 왼쪽~오른쪽으로 대형 폭발이 퍼지는 반폭(유닛)")]
        [SerializeField, Min(0f)] private float blastSpread = 0.85f;
        [SerializeField, Min(0.1f)] private float blastScale = 1f;
        [Tooltip("보스 제거 후 풀 반납까지의 대기(초)")]
        [SerializeField, Min(0f)] private float postDeathDelay = 1.2f;

        // ───────────── 런타임 상태 ─────────────

        private State state = State.Inactive;
        private int health;
        private int activePattern = -1;
        private bool halted;
        private Vector2 patternOffset;
        private Vector2 velocity;
        private Vector3 visualBasePosition;
        private float visualScaleX = 1f;
        private Coroutine fightRoutine;

        public int Health => health;
        public int MaxHealth => maxHealth;
        public bool IsDefeated => state == State.Dying;

        private Vector2 Anchor
        {
            get
            {
                Vector2 anchor = ScreenBounds.FromViewport(anchorViewport.x, anchorViewport.y);
                return anchor;
            }
        }

        private void Awake()
        {
            if (visualRoot != null)
            {
                visualBasePosition = visualRoot.localPosition;
                visualScaleX = Mathf.Abs(visualRoot.localScale.x);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ResultsShownEvent>(OnResultsShown);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ResultsShownEvent>(OnResultsShown);
            EndActivePattern();
            state = State.Inactive;
        }

        // ───────────── 풀링 ─────────────

        public void OnSpawned()
        {
            StopAllCoroutines();
            fightRoutine = null;
            activePattern = -1;
            halted = false;
            health = maxHealth;
            patternOffset = Vector2.zero;
            velocity = Vector2.zero;

            if (visualRoot != null) visualRoot.localPosition = visualBasePosition;
            SetVisualsVisible(true);
            if (flasher != null) flasher.Stop();
            if (muzzleFlash != null && muzzleFlash.Target != null) muzzleFlash.Target.enabled = false;

            StartCoroutine(EntranceRoutine());
        }

        public void OnDespawned()
        {
            StopAllCoroutines();
            fightRoutine = null;
            EndActivePattern();
            state = State.Inactive;
        }

        // ───────────── 등장 ─────────────

        private IEnumerator EntranceRoutine()
        {
            // 1) 화면 왼쪽 밖 → 오른쪽 밖으로 빠르게 통과
            state = State.FlyBy;
            SetColliders(false);
            SetFacingRight(true);
            SetBoosters(BoosterLevel.Turbo);

            float y = Anchor.y;
            transform.position = new Vector3(ScreenBounds.Left - offscreenMargin, y, 0f);
            EventBus.Publish(new BossSpawnedEvent());

            while (transform.position.x < ScreenBounds.Right + offscreenMargin)
            {
                transform.position += Vector3.right * (flyBySpeed * Time.deltaTime);
                yield return null;
            }

            yield return Wait(flyByPause);

            // 2) 화면 오른쪽 끝에서 천천히 모습을 드러냄
            state = State.Reveal;
            SetFacingRight(false);
            SetBoosters(BoosterLevel.Normal);
            SetColliders(true);

            Vector2 from = new Vector2(ScreenBounds.Right + offscreenMargin, Anchor.y);
            transform.position = from;
            EventBus.Publish(new BossRevealStartedEvent());

            float t = 0f;
            while (t < revealDuration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / revealDuration), 3f); // ease-out
                transform.position = Vector2.LerpUnclamped(from, Anchor, k);
                yield return null;
            }

            velocity = Vector2.zero;
            patternOffset = Vector2.zero;

            if (halted)
            {
                state = State.Halted;
            }
            else
            {
                state = State.Fighting;
                fightRoutine = StartCoroutine(FightRoutine());
            }
        }

        // ───────────── 전투 ─────────────

        private void Update()
        {
            if (state != State.Fighting && state != State.Halted) return;

            float hover = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
            Vector2 desired = Anchor + patternOffset + new Vector2(0f, hover);

            float minY = ScreenBounds.Bottom + verticalPadding;
            float maxY = ScreenBounds.Top - verticalPadding;
            if (minY < maxY) desired.y = Mathf.Clamp(desired.y, minY, maxY);

            Vector2 position = Vector2.SmoothDamp(transform.position, desired, ref velocity, moveSmoothTime);
            transform.position = new Vector3(position.x, position.y, 0f);
        }

        private IEnumerator FightRoutine()
        {
            yield return Wait(firstPatternDelay);

            int index = 0;
            while (true)
            {
                activePattern = index;
                EventBus.Publish(new BossPatternStartedEvent { patternIndex = index });

                switch (index)
                {
                    case 0: yield return PatternFan(); break;
                    case 1: yield return PatternSweep(); break;
                    case 2: yield return PatternSpiral(); break;
                    case 3: yield return PatternRing(); break;
                    default: yield return PatternRotary(); break;
                }

                EndActivePattern();
                patternOffset = Vector2.zero;
                SetBoosters(BoosterLevel.Normal);

                yield return Wait(UnityEngine.Random.Range(patternWaitRange.x, patternWaitRange.y));
                index = (index + 1) % 5;
            }
        }

        // 7.1 상하로 크게 이동하면서 플레이어 방향으로 5발 부채꼴 발사
        private IEnumerator PatternFan()
        {
            FanPattern cfg = pattern1;
            SetBoosters(BoosterLevel.Turbo);

            float duration = Mathf.Max(1, cfg.cycles) * Mathf.Max(0.1f, cfg.period);
            float interval = Mathf.Max(0.05f, cfg.fireInterval);
            float t = 0f;
            float fireTimer = interval * 0.5f;

            while (t < duration)
            {
                float dt = Time.deltaTime;
                t += dt;
                patternOffset = new Vector2(0f, cfg.amplitude * Mathf.Sin(t / cfg.period * Mathf.PI * 2f));

                fireTimer -= dt;
                if (fireTimer <= 0f)
                {
                    fireTimer += interval;
                    float aim = AimAngle();
                    int count = Mathf.Max(1, cfg.bulletCount);
                    for (int i = 0; i < count; i++)
                        FireBullet(aim + (i - (count - 1) * 0.5f) * cfg.spreadStep, cfg.bulletSpeed, 0f);
                    FireFeedback(aim);
                }
                yield return null;
            }
        }

        // 7.2 위아래로 빠르게 흩뿌리는 연발, 4회 왕복 후 중단
        private IEnumerator PatternSweep()
        {
            SweepPattern cfg = pattern2;
            float period = Mathf.Max(0.1f, cfg.sweepPeriod);
            float duration = Mathf.Max(1, cfg.sweeps) * period;
            float interval = Mathf.Max(0.01f, cfg.fireInterval);
            float t = 0f, accumulator = 0f;

            while (t < duration)
            {
                float dt = Time.deltaTime;
                t += dt;
                accumulator += dt;

                bool fired = false;
                float lastAngle = 180f;
                while (accumulator >= interval)
                {
                    accumulator -= interval;
                    float shotTime = t - accumulator;
                    if (shotTime > duration) break;
                    lastAngle = 180f - cfg.sweepAngle * Mathf.Sin(shotTime / period * Mathf.PI * 2f);
                    FireBullet(lastAngle, cfg.bulletSpeed, accumulator);
                    fired = true;
                }
                if (fired) FireFeedback(lastAngle);
                yield return null;
            }
        }

        // 7.3 12시 방향부터 시계방향으로 3바퀴 회전하며 연발
        private IEnumerator PatternSpiral()
        {
            SpiralPattern cfg = pattern3;
            float revolutionTime = Mathf.Max(0.1f, cfg.revolutionTime);
            float duration = Mathf.Max(1, cfg.revolutions) * revolutionTime;
            float interval = Mathf.Max(0.01f, cfg.fireInterval);
            float t = 0f, accumulator = interval; // 시작 즉시 12시 방향 1발

            while (t < duration)
            {
                float dt = Time.deltaTime;
                t += dt;
                accumulator += dt;

                bool fired = false;
                float lastAngle = 90f;
                while (accumulator >= interval)
                {
                    accumulator -= interval;
                    float shotTime = Mathf.Max(0f, t - accumulator);
                    if (shotTime > duration) break;
                    lastAngle = 90f - 360f * (shotTime / revolutionTime); // 각도 감소 = 시계방향
                    FireBullet(lastAngle, cfg.bulletSpeed, Mathf.Min(accumulator, dt));
                    fired = true;
                }
                if (fired) FireFeedback(lastAngle);
                yield return null;
            }
        }

        // 7.4 좌우로 이동하면서 전방위 동시 단발 발사 3회
        private IEnumerator PatternRing()
        {
            RingPattern cfg = pattern4;
            SetBoosters(BoosterLevel.Turbo);

            int volleys = Mathf.Max(1, cfg.volleys);
            float duration = cfg.leadTime + (volleys - 1) * cfg.volleyInterval + cfg.tailTime;
            float period = Mathf.Max(0.1f, cfg.movePeriod);
            float t = 0f;
            int fired = 0;

            while (t < duration)
            {
                t += Time.deltaTime;
                // 기준 위치에서 왼쪽으로 나갔다가 돌아오는 부드러운 좌우 왕복
                patternOffset = new Vector2(-cfg.moveAmplitude * 0.5f * (1f - Mathf.Cos(t / period * Mathf.PI * 2f)), 0f);

                while (fired < volleys && t >= cfg.leadTime + fired * cfg.volleyInterval)
                {
                    int count = Mathf.Max(1, cfg.bulletsPerVolley);
                    float step = 360f / count;
                    float offset = (fired % 2) * step * 0.5f; // 발사마다 반 칸씩 어긋나게
                    for (int i = 0; i < count; i++)
                        FireBullet(offset + i * step, cfg.bulletSpeed, 0f);
                    FireFeedback(180f);
                    fired++;
                }
                yield return null;
            }
        }

        // 7.5 45도 간격 8방향 연발, 전체가 시계방향으로 천천히 회전 (약 8초)
        private IEnumerator PatternRotary()
        {
            RotaryPattern cfg = pattern5;
            float interval = Mathf.Max(0.02f, cfg.fireInterval);
            int directions = Mathf.Max(1, cfg.directions);
            float step = 360f / directions;
            float t = 0f, accumulator = interval;

            while (t < cfg.duration)
            {
                float dt = Time.deltaTime;
                t += dt;
                accumulator += dt;

                bool fired = false;
                while (accumulator >= interval)
                {
                    accumulator -= interval;
                    float shotTime = Mathf.Max(0f, t - accumulator);
                    if (shotTime > cfg.duration) break;
                    float baseAngle = -cfg.rotationSpeed * shotTime; // 시계방향
                    for (int i = 0; i < directions; i++)
                        FireBullet(baseAngle + i * step, cfg.bulletSpeed, Mathf.Min(accumulator, dt));
                    fired = true;
                }
                if (fired) FireFeedback(180f);
                yield return null;
            }
        }

        // ───────────── 발사 ─────────────

        private float AimAngle()
        {
            PlayerHealth player = PlayerHealth.Current;
            if (player != null && !player.IsDead && eye != null)
            {
                Vector2 toPlayer = player.HitCenter - (Vector2)eye.position;
                if (toPlayer.sqrMagnitude > 0.01f)
                    return Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            }
            return 180f;
        }

        private void FireBullet(float angleDegrees, float speed, float elapsed)
        {
            if (bulletPrefab == null || eye == null) return;

            float radians = angleDegrees * Mathf.Deg2Rad;
            Bullet bullet = PoolManager.Spawn(bulletPrefab, eye.position, Quaternion.identity);
            if (bullet != null)
                bullet.Launch(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)), speed, elapsed);
        }

        private void FireFeedback(float angleDegrees)
        {
            if (muzzleFlash != null && eye != null)
            {
                Transform flash = muzzleFlash.transform;
                flash.SetPositionAndRotation(eye.position, Quaternion.Euler(0f, 0f, angleDegrees));
                muzzleFlash.Play();
            }
            EventBus.Publish(new BossFiredEvent());
        }

        // ───────────── 피격 / 처치 ─────────────

        public HitResult TakeDamage(int amount, Vector2 hitPoint)
        {
            switch (state)
            {
                case State.Reveal:
                    return HitResult.Blocked; // 등장 중에는 무적(총알만 소멸)
                case State.Fighting:
                case State.Halted:
                    break;
                default:
                    return HitResult.Ignored;
            }

            health -= Mathf.Max(0, amount);
            if (flasher != null && hitFlashMaterial != null)
                flasher.Flash(hitFlashMaterial, 1, hitFlashInterval);

            if (health <= 0)
            {
                Die();
                return HitResult.Killed;
            }
            return HitResult.Damaged;
        }

        private void Die()
        {
            state = State.Dying;
            StopFight();
            SetColliders(false);
            if (flasher != null) flasher.Stop();

            EventBus.Publish(new BossDefeatedEvent { position = transform.position, score = scoreValue });
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            SetBoosters(BoosterLevel.Weak);

            // 처치 직후부터 소규모 폭발을 불규칙하게 생성
            float t = 0f, next = 0f;
            while (t < deathSmallExplosionTime)
            {
                float dt = Time.deltaTime;
                t += dt;
                next -= dt;
                if (next <= 0f)
                {
                    next = UnityEngine.Random.Range(deathSmallExplosionInterval.x, deathSmallExplosionInterval.y);
                    Vector2 point = (Vector2)transform.position + new Vector2(
                        UnityEngine.Random.Range(-hullHalfExtents.x, hullHalfExtents.x),
                        UnityEngine.Random.Range(-hullHalfExtents.y, hullHalfExtents.y));
                    VfxManager.Spawn(VfxId.SmallExplosion, point,
                        UnityEngine.Random.Range(deathSmallExplosionScale.x, deathSmallExplosionScale.y));
                    EventBus.Publish(new SmallExplosionEvent { position = point });
                }

                if (visualRoot != null)
                    visualRoot.localPosition = visualBasePosition + (Vector3)(UnityEngine.Random.insideUnitCircle * deathShake);
                yield return null;
            }
            if (visualRoot != null) visualRoot.localPosition = visualBasePosition;

            // 대폭발: 화면 플래시 1회 + 원형 충격파 + 대형 폭발 3개(왼쪽 → 오른쪽)
            Vector2 center = transform.position;
            EventBus.Publish(new BossBigExplosionEvent { position = center });
            VfxManager.Spawn(VfxId.Shockwave, center);

            int count = Mathf.Max(1, blastCount);
            for (int i = 0; i < count; i++)
            {
                float k = count > 1 ? i / (float)(count - 1) : 0.5f;
                Vector2 point = center + new Vector2(Mathf.Lerp(-blastSpread, blastSpread, k), 0f);
                VfxManager.Spawn(VfxId.BossBlast, point, blastScale);
                EventBus.Publish(new BossBlastEvent { position = point, index = i });

                if (i == count - 1)
                    SetVisualsVisible(false); // 마지막(세 번째) 폭발이 생성되는 순간 보스 제거
                else
                    yield return Wait(blastInterval);
            }

            yield return Wait(postDeathDelay);

            EventBus.Publish(new BossDestroyedEvent());
            PoolManager.Despawn(gameObject);
        }

        // ───────────── 기타 ─────────────

        private void OnResultsShown(ResultsShownEvent evt)
        {
            // 결과 화면이 표시되는 동안에는 행동 패턴을 중단한다.
            halted = true;
            if (state == State.Fighting)
            {
                StopFight();
                patternOffset = Vector2.zero;
                SetBoosters(BoosterLevel.Normal);
                state = State.Halted;
            }
        }

        private void StopFight()
        {
            if (fightRoutine != null)
            {
                StopCoroutine(fightRoutine);
                fightRoutine = null;
            }
            EndActivePattern();
        }

        private void EndActivePattern()
        {
            if (activePattern < 0) return;
            int index = activePattern;
            activePattern = -1;
            EventBus.Publish(new BossPatternEndedEvent { patternIndex = index });
        }

        private void SetFacingRight(bool right)
        {
            if (visualRoot == null) return;
            Vector3 scale = visualRoot.localScale;
            scale.x = right ? visualScaleX : -visualScaleX;
            visualRoot.localScale = scale;
        }

        private void SetBoosters(BoosterLevel level)
        {
            for (int i = 0; i < boosters.Length; i++)
            {
                if (boosters[i] != null) boosters[i].SetLevel(level);
            }
        }

        private void SetColliders(bool enabled)
        {
            for (int i = 0; i < hitColliders.Length; i++)
            {
                if (hitColliders[i] != null) hitColliders[i].enabled = enabled;
            }
        }

        private void SetVisualsVisible(bool visible)
        {
            if (visualRoot != null) visualRoot.gameObject.SetActive(visible);
        }

        private static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
