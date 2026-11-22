using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 보스(Ship6). 등장 연출 → 행동 패턴 순환 → 처치 연출을 담당한다.
    /// 행동 패턴은 코드가 아니라 패턴 에셋(BossPattern) 목록으로 정해지며, 목록의 순서대로 반복 실행한다.
    /// 보스는 항상 오른쪽(원본 아트 방향)을 향한다. 몸통 접촉 피해는 없으며(IPlayerContact 미구현),
    /// 발사 지점의 위치는 눈알을 기준으로 한다.
    /// 패턴 테스트(BossPatternTest) 중에는 등장 연출 없이 바로 전투 위치에 나타나 지정된 패턴만 반복한다.
    /// </summary>
    public sealed class Boss : MonoBehaviour, IPoolable, IDamageable
    {
        private enum State { Inactive, FlyBy, Reveal, Fighting, Halted, Dying }

        // ───────────── 인스펙터 ─────────────

        [Header("Stats")]
        [Tooltip("5가지 패턴을 모두 경험할 수 있을 만큼의 체력")]
        [SerializeField, Min(1)] private int maxHealth = 480;
        [SerializeField, Min(0)] private int scoreValue = 1000;

        [Header("References")]
        [Tooltip("선체/부스터/눈알을 묶는 비주얼 루트(원본 아트는 오른쪽을 향함)")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer shipRenderer;
        [SerializeField] private SpriteFlasher flasher;
        [SerializeField] private Material hitFlashMaterial;
        [Tooltip("패턴의 발사 지점 위치가 기준으로 삼는 곳(Ship6의 눈알)")]
        [SerializeField] private Transform eye;
        [SerializeField] private SpriteAnimator muzzleFlash;
        [SerializeField] private BoosterFlame[] boosters = Array.Empty<BoosterFlame>();
        [SerializeField] private Collider2D[] hitColliders = Array.Empty<Collider2D>();
        [Tooltip("기본 총알. 패턴의 발사 지점에 총알 타입이 지정되지 않았을 때 쓴다")]
        [SerializeField] private Bullet bulletPrefab;

        [Header("Entrance")]
        [Tooltip("화면을 왼쪽에서 오른쪽으로 가로지르는 평균 속도(유닛/초)")]
        [SerializeField, Min(0.1f)] private float flyBySpeed = 16.8f;
        [Tooltip("가로지르는 동안의 Ease Out 세기. 1 = 등속, 클수록 처음이 빠르고 끝에서 느려진다")]
        [SerializeField, Min(1f)] private float flyByEase = 2f;
        [Tooltip("가로지른 뒤 다시 나타나기까지의 대기(초)")]
        [SerializeField, Min(0f)] private float flyByPause = 1.7f;
        [Tooltip("오른쪽 끝에서 천천히 모습을 드러내는 시간(초)")]
        [SerializeField, Min(0.1f)] private float revealDuration = 3.25f;
        [Tooltip("모습을 드러내는 동안의 Ease Out 세기. 1 = 등속, 클수록 처음이 빠르고 끝에서 느려진다")]
        [SerializeField, Min(1f)] private float revealEase = 3f;
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
        [Tooltip("제자리에서 위아래로 떠 있는 Idle 움직임의 폭(유닛)")]
        [SerializeField, Min(0f)] private float hoverAmplitude = 0.1f;
        [Tooltip("Idle 움직임의 빠르기(라디안/초)")]
        [SerializeField, Min(0f)] private float hoverFrequency = 2.7f;
        [Tooltip("화면 위/아래 가장자리와 유지할 여백(유닛)")]
        [SerializeField, Min(0f)] private float verticalPadding = 0.85f;

        [Header("Patterns")]
        [Tooltip("행동 패턴 목록. 위에서부터 순서대로 실행하고, 끝까지 가면 처음으로 돌아간다.\n" +
                 "패턴 에셋은 Tools > Chopper > Boss Pattern Editor에서 만들고 고친다")]
        [SerializeField] private BossPattern[] patterns = Array.Empty<BossPattern>();

        [Header("Feedback")]
        [SerializeField, Min(0.01f)] private float hitFlashInterval = 0.05f;

        [Header("Death 1) Shake + Red Flash (플레이어 사망 연출과 동일)")]
        [SerializeField] private Material deathFlashMaterial;
        [Tooltip("빨강 점멸 간격(초)")]
        [SerializeField, Min(0.01f)] private float deathFlashInterval = 0.05f;
        [Tooltip("처치 직후 제자리에서 흔들리는 시간(초). 흔들림은 이 시간 동안 점점 약해지다 멈춘다(Fade Out)")]
        [SerializeField, Min(0f)] private float deathShakeDuration = 1f;
        [Tooltip("흔들림이 시작될 때의 반경(유닛)")]
        [SerializeField, Min(0f)] private float deathShakeAmplitude = 0.08f;
        [Tooltip("초당 흔들림 횟수")]
        [SerializeField, Min(1f)] private float deathShakeFrequency = 50f;

        [Header("Death 2) Small Explosions")]
        [Tooltip("흔들림이 멈춘 뒤 대폭발까지의 대기 시간(초). 빨강 점멸은 계속된다")]
        [SerializeField, Min(0f)] private float deathStillDuration = 1.3f;
        [Tooltip("소규모 폭발 생성 간격 범위(초). 처치 직후(처음)부터 대폭발까지 계속 생성한다")]
        [SerializeField] private Vector2 deathSmallExplosionInterval = new Vector2(0.03f, 0.075f);
        [SerializeField] private Vector2 deathSmallExplosionScale = new Vector2(1.6f, 2.8f);
        [Tooltip("소규모 폭발이 생성되는 원의 반지름(유닛). 선체 전체를 포괄하는 크기")]
        [SerializeField, Min(0f)] private float deathSmallExplosionRadius = 1.2f;
        [Tooltip("잔해가 튀어나오는 영역(중심 기준 반크기)")]
        [SerializeField] private Vector2 hullHalfExtents = new Vector2(1.05f, 0.6f);

        [Header("Death 3) Big Explosion")]
        [Tooltip("대형 폭발 개수")]
        [SerializeField, Min(1)] private int blastCount = 3;
        [Tooltip("대형 폭발 생성 간격(초)")]
        [SerializeField, Min(0f)] private float blastInterval = 0.1f;
        [Tooltip("보스 왼쪽~오른쪽으로 대형 폭발이 퍼지는 반폭(유닛)")]
        [SerializeField, Min(0f)] private float blastSpread = 0.85f;
        [Tooltip("대형 폭발 이펙트 프리팹에 곱하는 크기 배율")]
        [SerializeField, Min(0.1f)] private float blastScale = 1f;
        [Tooltip("보스 중심 기준 함선 밑변의 높이(유닛). 대형 폭발의 밑동이 이 높이에 놓인다")]
        [SerializeField] private float hullBottomY = -0.74f;
        [Tooltip("대형 폭발 이펙트의 밑동에서 이펙트 중심까지의 높이(유닛, blastScale = 1 기준)")]
        [SerializeField, Min(0f)] private float blastBaseToCenter = 0.7f;

        [Header("Death 4) Wreckage")]
        [Tooltip("선체 파츠와 잔해에 쓰는 프리팹(스프라이트는 생성 시 지정)")]
        [SerializeField] private WreckPiece wreckPrefab;
        [Tooltip("선체를 뒤/앞 두 파츠로 나누는 위치(스프라이트 가로 비율)")]
        [SerializeField, Range(0.1f, 0.9f)] private float hullSplit = 0.5f;
        [SerializeField] private Vector2 rearPartVelocity = new Vector2(-1.4f, 2.4f);
        [SerializeField] private Vector2 frontPartVelocity = new Vector2(1.2f, 2f);
        [SerializeField] private float rearPartSpin = 70f;
        [SerializeField] private float frontPartSpin = -55f;
        [Tooltip("튀어 오르는 잔해에 쓰는 스프라이트(Ship6 Parts)")]
        [SerializeField] private Sprite[] debrisSprites = Array.Empty<Sprite>();
        [Tooltip("튀어 오르는 잔해 개수 범위")]
        [SerializeField] private Vector2Int debrisCountRange = new Vector2Int(4, 6);
        [Tooltip("잔해 초기 속도 범위(유닛/초)")]
        [SerializeField] private Vector2 debrisSpeedRange = new Vector2(3f, 6.5f);
        [Tooltip("잔해가 튀어 오르는 각도 범위(도, 90 = 수직 위)")]
        [SerializeField] private Vector2 debrisAngleRange = new Vector2(30f, 150f);
        [SerializeField] private Vector2 debrisSpinRange = new Vector2(-360f, 360f);
        [Tooltip("보스 제거 후 풀 반납까지의 대기(초)")]
        [SerializeField, Min(0f)] private float postDeathDelay = 1.2f;

        // ───────────── 런타임 상태 ─────────────

        private readonly List<PatternShot> shotBuffer = new List<PatternShot>(64);

        /// <summary>패턴 목록에 없는 패턴(패턴 테스트)을 실행할 때 이벤트에 싣는 패턴 번호.</summary>
        public const int TestPatternIndex = -1;

        private State state = State.Inactive;
        private int health;
        private bool patternActive;
        private int activePatternIndex;
        private BossPattern activePatternData;
        private BossPattern followedTest;   // 지금 따르고 있는 테스트 패턴(없으면 null)
        private bool halted;
        private Vector2 patternOffset;
        private Vector2 velocity;
        private Vector3 visualBasePosition;
        private Coroutine entranceRoutine;
        private Coroutine fightRoutine;
        private Sprite rearHullSprite;
        private Sprite frontHullSprite;
        private int[] debrisOrder;

        public int Health => health;
        public int MaxHealth => maxHealth;
        public bool IsDefeated => state == State.Dying;

        // 패턴 에디터가 미리보기를 게임과 똑같이 그리기 위해 읽는 값
        public IReadOnlyList<BossPattern> Patterns => patterns;
        public Bullet DefaultBulletPrefab => bulletPrefab;
        public Transform Eye => eye;
        public SpriteRenderer ShipRenderer => shipRenderer;
        public Vector2 AnchorViewport => anchorViewport;
        public float MoveSmoothTime => moveSmoothTime;
        public float VerticalPadding => verticalPadding;

        private Vector2 Anchor => ScreenBounds.FromViewport(anchorViewport.x, anchorViewport.y);

        private void Awake()
        {
            if (visualRoot != null) visualBasePosition = visualRoot.localPosition;
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

        private void OnDestroy()
        {
            // 런타임에 만든 선체 파츠 스프라이트 정리
            if (rearHullSprite != null) Destroy(rearHullSprite);
            if (frontHullSprite != null) Destroy(frontHullSprite);
        }

        // ───────────── 풀링 ─────────────

        public void OnSpawned()
        {
            StopAllCoroutines();
            entranceRoutine = null;
            fightRoutine = null;
            patternActive = false;
            activePatternData = null;
            halted = false;
            health = maxHealth;
            patternOffset = Vector2.zero;
            velocity = Vector2.zero;

            if (visualRoot != null) visualRoot.localPosition = visualBasePosition;
            SetVisualsVisible(true);
            if (flasher != null) flasher.Stop();
            if (muzzleFlash != null && muzzleFlash.Target != null) muzzleFlash.Target.enabled = false;

            followedTest = BossPatternTest.Pattern;
            if (followedTest != null)
            {
                GameManager.MarkRunUnranked();
                BeginInstant();
            }
            else
            {
                entranceRoutine = StartCoroutine(EntranceRoutine());
            }
        }

        public void OnDespawned()
        {
            StopAllCoroutines();
            entranceRoutine = null;
            fightRoutine = null;
            EndActivePattern();
            state = State.Inactive;
        }

        // ───────────── 등장 ─────────────

        private IEnumerator EntranceRoutine()
        {
            // 1) 화면 왼쪽 밖 → 오른쪽 밖으로 빠르게 통과(처음이 가장 빠르고 지나가며 감속)
            //    등장 연출 중에도 플레이어 총알에 피격된다.
            state = State.FlyBy;
            SetColliders(true);
            SetBoosters(BoosterLevel.Turbo);

            float y = Anchor.y;
            float fromX = ScreenBounds.Left - offscreenMargin;
            float toX = ScreenBounds.Right + offscreenMargin;
            transform.position = new Vector3(fromX, y, 0f);
            EventBus.Publish(new BossSpawnedEvent());

            float flyByDuration = Mathf.Abs(toX - fromX) / flyBySpeed;
            float t = 0f;
            while (t < flyByDuration)
            {
                t += Time.deltaTime;
                float k = EaseOut(t / flyByDuration, flyByEase);
                transform.position = new Vector3(Mathf.LerpUnclamped(fromX, toX, k), y, 0f);
                yield return null;
            }

            yield return Wait(flyByPause);

            // 2) 화면 오른쪽 끝에서 모습을 드러냄(Ease Out). 방향은 그대로 오른쪽을 향한다
            state = State.Reveal;
            SetBoosters(BoosterLevel.Normal);
            SetColliders(true);

            Vector2 from = new Vector2(ScreenBounds.Right + offscreenMargin, Anchor.y);
            transform.position = from;
            EventBus.Publish(new BossRevealStartedEvent());

            t = 0f;
            while (t < revealDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector2.LerpUnclamped(from, Anchor, EaseOut(t / revealDuration, revealEase));
                yield return null;
            }

            velocity = Vector2.zero;
            patternOffset = Vector2.zero;
            entranceRoutine = null;

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

        /// <summary>등장 연출 없이 바로 전투 위치에서 전투를 시작한다(패턴 테스트).</summary>
        private void BeginInstant()
        {
            if (entranceRoutine != null)
            {
                StopCoroutine(entranceRoutine);
                entranceRoutine = null;
            }
            StopFight();

            SetColliders(true);
            SetBoosters(BoosterLevel.Normal);

            Vector2 anchor = Anchor;
            transform.position = new Vector3(anchor.x, anchor.y, 0f);
            velocity = Vector2.zero;
            patternOffset = Vector2.zero;
            EventBus.Publish(new BossSpawnedEvent { instant = true });

            if (halted)
            {
                state = State.Halted;
                return;
            }

            state = State.Fighting;
            fightRoutine = StartCoroutine(FightRoutine());
        }

        /// <summary>처음이 빠르고 끝으로 갈수록 느려지는 보간(0~1). power = 1이면 등속.</summary>
        private static float EaseOut(float t, float power)
        {
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(t), Mathf.Max(1f, power));
        }

        // ───────────── 전투 ─────────────

        private void Update()
        {
            SyncPatternTest();
            if (state != State.Fighting && state != State.Halted) return;

            float hover = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
            Vector2 desired = Anchor + patternOffset + new Vector2(0f, hover);

            Vector2 position = StepBody(transform.position, desired, ref velocity, moveSmoothTime,
                ScreenBounds.Bottom + verticalPadding, ScreenBounds.Top - verticalPadding, Time.deltaTime);
            transform.position = new Vector3(position.x, position.y, 0f);
        }

        /// <summary>
        /// 전투 중 본체를 목표 위치(desired)로 부드럽게 한 스텝 옮긴 위치를 돌려준다.
        /// 목표의 높이는 minY~maxY로 제한한다. 게임과 패턴 에디터의 미리보기가 같은 계산을 쓴다.
        /// </summary>
        public static Vector2 StepBody(Vector2 current, Vector2 desired, ref Vector2 velocity, float smoothTime,
            float minY, float maxY, float deltaTime)
        {
            if (minY < maxY) desired.y = Mathf.Clamp(desired.y, minY, maxY);
            return Vector2.SmoothDamp(current, desired, ref velocity, smoothTime, Mathf.Infinity, deltaTime);
        }

        /// <summary>패턴 테스트(BossPatternTest)가 켜지거나, 꺼지거나, 다른 패턴으로 바뀌면 바로 따라간다.</summary>
        private void SyncPatternTest()
        {
            BossPattern test = BossPatternTest.Pattern;
            if (ReferenceEquals(test, followedTest)) return;
            followedTest = test;

            if (test != null) GameManager.MarkRunUnranked();

            switch (state)
            {
                case State.FlyBy:
                case State.Reveal:
                    // 테스트를 켜면 등장 연출을 건너뛴다(끄는 경우에는 등장 연출을 그대로 이어 간다)
                    if (test != null) BeginInstant();
                    break;

                case State.Fighting:
                    // 실행 중이던 패턴을 중단하고, 테스트 패턴(끈 경우에는 패턴 목록의 처음)부터 다시 시작한다
                    StopFight();
                    patternOffset = Vector2.zero;
                    SetBoosters(BoosterLevel.Normal);
                    fightRoutine = StartCoroutine(FightRoutine());
                    break;
            }
        }

        private IEnumerator FightRoutine()
        {
            // 패턴 테스트 중에는 기다리지 않고 바로 시작한다
            if (followedTest == null) yield return Wait(firstPatternDelay);

            int index = -1;
            while (true)
            {
                // 테스트 패턴이 바뀌거나 꺼지면 SyncPatternTest가 이 루틴을 처음부터 다시 시작한다
                BossPattern test = followedTest;
                BossPattern pattern = test;
                int patternIndex = TestPatternIndex;

                if (test == null)
                {
                    index = NextPatternIndex(index);
                    if (index < 0)
                    {
                        // 실행할 패턴이 하나도 없으면 제자리에서 기다린다
                        yield return null;
                        continue;
                    }
                    pattern = patterns[index];
                    patternIndex = index;
                }

                patternActive = true;
                activePatternIndex = patternIndex;
                activePatternData = pattern;
                EventBus.Publish(new BossPatternStartedEvent { patternIndex = patternIndex, pattern = pattern });

                yield return RunPattern(pattern);

                EndActivePattern();
                patternOffset = Vector2.zero;
                SetBoosters(BoosterLevel.Normal);

                yield return Wait(test != null
                    ? BossPatternTest.RepeatDelay
                    : UnityEngine.Random.Range(patternWaitRange.x, patternWaitRange.y));
            }
        }

        /// <summary>목록에서 current 다음에 실행할 패턴의 번호(비어 있는 칸은 건너뛴다). 실행할 패턴이 없으면 -1.</summary>
        private int NextPatternIndex(int current)
        {
            int count = patterns != null ? patterns.Length : 0;
            for (int step = 1; step <= count; step++)
            {
                int candidate = (current + step) % count;
                if (patterns[candidate] != null) return candidate;
            }
            return -1;
        }

        /// <summary>
        /// 패턴 에셋 1개를 처음부터 끝까지 실행한다.
        /// 매 프레임 "지난 프레임 시각 ~ 이번 프레임 시각" 사이에 일어난 발사를 패턴에서 받아 총알을 쏘고,
        /// 총알은 원래 발사됐어야 할 시각으로부터 지난 시간만큼 미리 전진시켜 프레임률과 무관하게 탄 간격이 균일하다.
        /// </summary>
        private IEnumerator RunPattern(BossPattern pattern)
        {
            SetBoosters(pattern.boosters);

            float t = 0f;
            while (pattern != null && t < pattern.duration) // 실행 도중 패턴이 삭제되면 그 자리에서 끝낸다
            {
                float previous = t;
                t += Time.deltaTime;

                patternOffset = pattern.BodyOffsetAt(t);
                FireShots(pattern, previous, t);
                yield return null;
            }
        }

        // ───────────── 발사 ─────────────

        private void FireShots(BossPattern pattern, float fromTime, float toTime)
        {
            shotBuffer.Clear();
            pattern.CollectShots(fromTime, toTime, shotBuffer);
            if (shotBuffer.Count == 0) return;

            Vector2 eyePosition = eye != null ? (Vector2)eye.position : (Vector2)transform.position;
            Vector2 screenCenter = ScreenBounds.Center;

            PlayerHealth player = PlayerHealth.Current;
            bool hasTarget = player != null && !player.IsDead;
            Vector2 target = hasTarget ? player.HitCenter : Vector2.zero;

            bool fired = false;
            Vector2 flashPosition = eyePosition;
            float flashAngle = 180f;

            for (int i = 0; i < shotBuffer.Count; i++)
            {
                PatternShot shot = shotBuffer[i];
                PatternEmitter emitter = pattern.emitters[shot.emitterIndex];

                Bullet prefab = emitter.bulletPrefab != null ? emitter.bulletPrefab : bulletPrefab;
                if (prefab == null) continue;

                Vector2 origin = (emitter.space == PatternSpace.Screen ? screenCenter : eyePosition) + shot.position;

                float center = shot.angle;
                if (emitter.aim == PatternAim.AtPlayer) center += AimAngle(origin, hasTarget, target);

                // 원래 발사됐어야 할 시각으로부터 지난 시간만큼 총알을 미리 전진시킨다
                float elapsed = toTime - shot.time;
                int count = emitter.BulletCount;
                for (int b = 0; b < count; b++)
                    FireBullet(prefab, origin, emitter.BulletAngle(center, b), emitter.bulletSpeed, elapsed);

                fired = true;
                flashPosition = origin;
                flashAngle = emitter.layout == ShotLayout.Ring ? 180f : center;
            }

            if (fired) FireFeedback(flashPosition, flashAngle);
        }

        /// <summary>origin에서 플레이어를 향하는 각도(도). 플레이어가 없거나 사망했으면 왼쪽(180도).</summary>
        public static float AimAngle(Vector2 origin, bool hasTarget, Vector2 target)
        {
            if (hasTarget)
            {
                Vector2 toTarget = target - origin;
                if (toTarget.sqrMagnitude > 0.01f)
                    return Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            }
            return 180f;
        }

        private static void FireBullet(Bullet prefab, Vector2 origin, float angleDegrees, float speed, float elapsed)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            Bullet bullet = PoolManager.Spawn(prefab, origin, Quaternion.identity);
            if (bullet != null)
                bullet.Launch(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)), speed, elapsed);
        }

        private void FireFeedback(Vector2 position, float angleDegrees)
        {
            if (muzzleFlash != null)
            {
                Transform flash = muzzleFlash.transform;
                flash.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angleDegrees));
                muzzleFlash.Play();
            }
            EventBus.Publish(new BossFiredEvent());
        }

        // ───────────── 피격 / 처치 ─────────────

        public HitResult TakeDamage(int amount, Vector2 hitPoint)
        {
            // 등장 연출(가로지르기, 모습 드러내기) 중에도 피격된다. 풀에 있거나 이미 처치된 뒤에는 무시
            if (state == State.Inactive || state == State.Dying) return HitResult.Ignored;

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

            // 등장 연출 도중에 처치되면 그 자리에서 멈춘다
            if (entranceRoutine != null)
            {
                StopCoroutine(entranceRoutine);
                entranceRoutine = null;
            }
            StopFight();
            SetColliders(false);
            if (flasher != null) flasher.Stop();

            EventBus.Publish(new BossDefeatedEvent { position = transform.position, score = scoreValue });
            StartCoroutine(DeathRoutine());
        }

        // 플레이어 사망 연출과 같은 구성:
        //  1) 제자리에서 심하게 흔들리며 빠르게 빨강 점멸
        //  2) 점멸 유지, 흔들림 중단 (소규모 폭발은 처치 직후인 1)부터 계속 생성)
        //  3) 대폭발 + 화면 전체 흰색 깜박임 (+ 원형 충격파, 대형 폭발 3개)
        //  4) 선체가 두 파츠로 분리되어 낙하 + 잔해가 튀어 오른 뒤 낙하
        private IEnumerator DeathRoutine()
        {
            SetBoosters(BoosterLevel.Weak);
            if (muzzleFlash != null && muzzleFlash.Target != null) muzzleFlash.Target.enabled = false;

            if (flasher != null && deathFlashMaterial != null)
                flasher.FlashContinuous(deathFlashMaterial, deathFlashInterval);

            float total = deathShakeDuration + deathStillDuration;
            float shakeStep = 1f / Mathf.Max(1f, deathShakeFrequency);
            float t = 0f, nextExplosion = 0f, shakeTimer = 0f;

            while (t < total)
            {
                float dt = Time.deltaTime;
                t += dt;

                nextExplosion -= dt;
                if (nextExplosion <= 0f)
                {
                    nextExplosion = UnityEngine.Random.Range(deathSmallExplosionInterval.x, deathSmallExplosionInterval.y);
                    // 선체를 포괄하는 원 안의 임의 지점
                    Vector2 point = (Vector2)transform.position + UnityEngine.Random.insideUnitCircle * deathSmallExplosionRadius;
                    VfxManager.Spawn(VfxId.SmallExplosion, point,
                        UnityEngine.Random.Range(deathSmallExplosionScale.x, deathSmallExplosionScale.y));
                    EventBus.Publish(new SmallExplosionEvent { position = point });
                }

                if (visualRoot != null)
                {
                    if (t < deathShakeDuration)
                    {
                        shakeTimer -= dt;
                        if (shakeTimer <= 0f)
                        {
                            shakeTimer = shakeStep;
                            // 흔들림 세기는 처음이 가장 크고 끝으로 갈수록 0까지 줄어든다(Fade Out)
                            float fade = 1f - t / deathShakeDuration;
                            visualRoot.localPosition = visualBasePosition + (Vector3)(UnityEngine.Random.insideUnitCircle.normalized *
                                (deathShakeAmplitude * fade * UnityEngine.Random.Range(0.5f, 1f)));
                        }
                    }
                    else
                    {
                        visualRoot.localPosition = visualBasePosition;
                    }
                }
                yield return null;
            }
            if (visualRoot != null) visualRoot.localPosition = visualBasePosition;

            // 대폭발: 화면 플래시 1회 + 원형 충격파 + 대형 폭발 3개(왼쪽 → 오른쪽)
            Vector2 center = transform.position;
            if (flasher != null) flasher.Stop();
            EventBus.Publish(new BossBigExplosionEvent { position = center });
            VfxManager.Spawn(VfxId.Shockwave, center);

            // 폭발 이펙트의 밑동이 함선 밑변에 놓이도록 이펙트 중심을 올려 배치한다
            float blastY = center.y + hullBottomY + blastBaseToCenter * blastScale;
            int count = Mathf.Max(1, blastCount);
            for (int i = 0; i < count; i++)
            {
                float k = count > 1 ? i / (float)(count - 1) : 0.5f;
                var point = new Vector2(center.x + Mathf.Lerp(-blastSpread, blastSpread, k), blastY);
                VfxManager.Spawn(VfxId.BossBlast, point, blastScale);
                EventBus.Publish(new BossBlastEvent { position = point, index = i });

                if (i == count - 1)
                {
                    // 마지막(세 번째) 폭발이 생성되는 순간 보스 제거 → 파츠 분리, 잔해 생성
                    SpawnWreckage();
                    SetVisualsVisible(false);
                }
                else
                {
                    yield return Wait(blastInterval);
                }
            }

            yield return Wait(postDeathDelay);

            EventBus.Publish(new BossDestroyedEvent());
            PoolManager.Despawn(gameObject);
        }

        private void SpawnWreckage()
        {
            if (wreckPrefab == null || visualRoot == null) return;

            Vector3 scale = visualRoot.lossyScale;

            // 선체를 뒤(왼쪽)/앞(오른쪽) 두 파츠로 분리
            EnsureHullParts();
            if (rearHullSprite != null && frontHullSprite != null)
            {
                float halfWidth = shipRenderer.sprite.bounds.extents.x;        // 로컬 단위
                float rearCenterX = Mathf.Lerp(-halfWidth, halfWidth, hullSplit * 0.5f);
                float frontCenterX = Mathf.Lerp(-halfWidth, halfWidth, hullSplit + (1f - hullSplit) * 0.5f);
                Transform ship = shipRenderer.transform;

                LaunchPiece(rearHullSprite, ship.TransformPoint(new Vector3(rearCenterX, 0f, 0f)), ship.rotation, scale,
                    rearPartVelocity, rearPartSpin);
                LaunchPiece(frontHullSprite, ship.TransformPoint(new Vector3(frontCenterX, 0f, 0f)), ship.rotation, scale,
                    frontPartVelocity, frontPartSpin);
            }

            // 잔해: 서로 다른 파츠 스프라이트를 골라 랜덤 방향으로 튀어 오르게 한다
            if (debrisSprites == null || debrisSprites.Length == 0) return;

            if (debrisOrder == null || debrisOrder.Length != debrisSprites.Length)
            {
                debrisOrder = new int[debrisSprites.Length];
                for (int i = 0; i < debrisOrder.Length; i++) debrisOrder[i] = i;
            }
            for (int i = debrisOrder.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (debrisOrder[i], debrisOrder[j]) = (debrisOrder[j], debrisOrder[i]);
            }

            Vector2 center = transform.position;
            int count = UnityEngine.Random.Range(debrisCountRange.x, debrisCountRange.y + 1);
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = debrisSprites[debrisOrder[i % debrisOrder.Length]];
                if (sprite == null) continue;

                float angle = UnityEngine.Random.Range(debrisAngleRange.x, debrisAngleRange.y) * Mathf.Deg2Rad;
                float speed = UnityEngine.Random.Range(debrisSpeedRange.x, debrisSpeedRange.y);
                Vector2 position = center + new Vector2(
                    UnityEngine.Random.Range(-hullHalfExtents.x, hullHalfExtents.x) * 0.6f,
                    UnityEngine.Random.Range(-hullHalfExtents.y, hullHalfExtents.y) * 0.6f);

                LaunchPiece(sprite, position, Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)), scale,
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    UnityEngine.Random.Range(debrisSpinRange.x, debrisSpinRange.y));
            }
        }

        private void LaunchPiece(Sprite sprite, Vector3 position, Quaternion rotation, Vector3 scale, Vector2 pieceVelocity, float spin)
        {
            WreckPiece piece = PoolManager.Spawn(wreckPrefab, position, rotation);
            if (piece == null) return;

            piece.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 1f);
            piece.SetSprite(sprite);
            piece.Launch(pieceVelocity, spin);
        }

        // Ship6 스프라이트를 hullSplit 위치에서 좌우로 잘라 두 파츠 스프라이트를 만든다(1회).
        private void EnsureHullParts()
        {
            if (rearHullSprite != null && frontHullSprite != null) return;
            if (shipRenderer == null || shipRenderer.sprite == null) return;

            Sprite source = shipRenderer.sprite;
            Rect rect = source.rect;
            float splitWidth = Mathf.Round(rect.width * hullSplit);
            var pivot = new Vector2(0.5f, 0.5f);

            rearHullSprite = Sprite.Create(source.texture, new Rect(rect.x, rect.y, splitWidth, rect.height),
                pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            rearHullSprite.name = source.name + " (Rear)";

            frontHullSprite = Sprite.Create(source.texture, new Rect(rect.x + splitWidth, rect.y, rect.width - splitWidth, rect.height),
                pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            frontHullSprite.name = source.name + " (Front)";
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
            if (!patternActive) return;
            int index = activePatternIndex;
            BossPattern pattern = activePatternData;
            patternActive = false;
            activePatternData = null;
            EventBus.Publish(new BossPatternEndedEvent { patternIndex = index, pattern = pattern });
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
