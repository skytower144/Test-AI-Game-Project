using System.Collections;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 체력 0 도달 후의 사망 연출.
    /// 1) 제자리에서 심하게 흔들리며 빨강 점멸 → 2) 점멸 유지, 주변에 소규모 폭발 →
    /// 3) 대폭발 + 화면 플래시 → 4) 두 파츠로 분리되어 낙하 + 잔해가 튀어 오른 뒤 낙하.
    /// </summary>
    public sealed class PlayerDeathSequence : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("흔들릴 비주얼 루트")]
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteFlasher flasher;
        [SerializeField] private Material deathFlashMaterial;

        [Header("1) Shake")]
        [SerializeField, Min(0f)] private float shakeDuration = 1f;
        [Tooltip("흔들림 반경(유닛)")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.08f;
        [Tooltip("초당 흔들림 횟수")]
        [SerializeField, Min(1f)] private float shakeFrequency = 50f;
        [Tooltip("빨강 점멸 간격(초)")]
        [SerializeField, Min(0.01f)] private float flashInterval = 0.05f;

        [Header("2) Small Explosions")]
        [SerializeField, Min(0f)] private float smallExplosionDuration = 1f;
        [Tooltip("소규모 폭발 생성 간격 범위(초)")]
        [SerializeField] private Vector2 smallExplosionInterval = new Vector2(0.035f, 0.1f);
        [Tooltip("소규모 폭발이 생성되는 원의 반지름(유닛). 헬리콥터 몸통 전체를 포괄하는 크기")]
        [SerializeField, Min(0f)] private float smallExplosionRadius = 0.6f;
        [SerializeField] private Vector2 smallExplosionScale = new Vector2(0.9f, 1.6f);

        [Header("3) Big Explosion")]
        [SerializeField, Min(0f)] private float bigExplosionScale = 1f;
        [Tooltip("대폭발 애니메이션 길이를 알 수 없을 때 사용할 대기 시간(초)")]
        [SerializeField, Min(0f)] private float bigExplosionFallbackDuration = 0.9f;
        [SerializeField] private Color screenFlashColor = Color.white;
        [SerializeField, Min(0f)] private float screenFlashHold = 0.05f;
        [SerializeField, Min(0f)] private float screenFlashFade = 0.35f;

        [Header("4) Wreckage")]
        [SerializeField] private WreckPiece cabinPrefab;
        [SerializeField] private WreckPiece tailPrefab;
        [SerializeField] private WreckPiece debrisPrefab;
        [Tooltip("헬리콥터 중심 기준 캐빈 파츠 위치")]
        [SerializeField] private Vector2 cabinOffset = new Vector2(0.16f, 0f);
        [Tooltip("헬리콥터 중심 기준 꼬리 파츠 위치")]
        [SerializeField] private Vector2 tailOffset = new Vector2(-0.32f, 0f);
        [SerializeField] private Vector2 cabinVelocity = new Vector2(1.1f, 2.2f);
        [SerializeField] private Vector2 tailVelocity = new Vector2(-1.6f, 2.8f);
        [SerializeField] private float cabinSpin = -140f;
        [SerializeField] private float tailSpin = 260f;
        [Tooltip("튀어 오르는 잔해 개수 범위")]
        [SerializeField] private Vector2Int debrisCountRange = new Vector2Int(4, 6);
        [Tooltip("잔해 초기 속도 범위(유닛/초)")]
        [SerializeField] private Vector2 debrisSpeedRange = new Vector2(2.6f, 5.2f);
        [Tooltip("잔해가 튀어 오르는 각도 범위(도, 90 = 수직 위)")]
        [SerializeField] private Vector2 debrisAngleRange = new Vector2(30f, 150f);
        [SerializeField] private Vector2 debrisSpinRange = new Vector2(-360f, 360f);

        private Vector3 visualBasePosition;
        private bool initialized;

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            if (visual != null) visualBasePosition = visual.localPosition;
        }

        public void Play()
        {
            EnsureInitialized();
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        /// <summary>부활 시 헬리콥터 비주얼을 원상 복구한다.</summary>
        public void ResetVisual()
        {
            EnsureInitialized();
            StopAllCoroutines();
            IsPlaying = false;

            if (visual != null) visual.localPosition = visualBasePosition;
            if (flasher != null) flasher.Stop();
            if (bodyRenderer != null) bodyRenderer.enabled = true;
        }

        private IEnumerator Run()
        {
            IsPlaying = true;

            if (flasher != null && deathFlashMaterial != null)
                flasher.FlashContinuous(deathFlashMaterial, flashInterval);

            // 1) 제자리에서 심하게 흔들림 + 빨강 점멸
            float t = 0f, shakeTimer = 0f;
            float shakeStep = 1f / Mathf.Max(1f, shakeFrequency);
            while (t < shakeDuration)
            {
                float dt = Time.deltaTime;
                t += dt;
                shakeTimer -= dt;
                if (shakeTimer <= 0f && visual != null)
                {
                    shakeTimer = shakeStep;
                    visual.localPosition = visualBasePosition + (Vector3)(Random.insideUnitCircle.normalized * (shakeAmplitude * Random.Range(0.5f, 1f)));
                }
                yield return null;
            }
            if (visual != null) visual.localPosition = visualBasePosition;

            // 2) 점멸 유지, 흔들림 중단, 주변에 소규모 폭발을 불규칙하게 생성
            t = 0f;
            float next = 0f;
            while (t < smallExplosionDuration)
            {
                float dt = Time.deltaTime;
                t += dt;
                next -= dt;
                if (next <= 0f)
                {
                    next = Random.Range(smallExplosionInterval.x, smallExplosionInterval.y);
                    // 헬리콥터 몸통을 포괄하는 원 안의 임의 지점
                    Vector2 point = (Vector2)transform.position + Random.insideUnitCircle * smallExplosionRadius;
                    VfxManager.Spawn(VfxId.SmallExplosion, point, Random.Range(smallExplosionScale.x, smallExplosionScale.y));
                    EventBus.Publish(new SmallExplosionEvent { position = point });
                }
                yield return null;
            }

            // 3) 대폭발 + 화면 전체 흰색 깜박임
            Vector2 center = transform.position;
            if (flasher != null) flasher.Stop();
            if (bodyRenderer != null) bodyRenderer.enabled = false;

            float explosionTime = bigExplosionFallbackDuration;
            GameObject explosion = VfxManager.Spawn(VfxId.BigExplosion, center, bigExplosionScale);
            if (explosion != null)
            {
                var explosionAnimator = explosion.GetComponent<SpriteAnimator>();
                if (explosionAnimator != null && explosionAnimator.Duration > 0f)
                    explosionTime = explosionAnimator.Duration;
            }

            EventBus.Publish(new ScreenFlashEvent { color = screenFlashColor, holdTime = screenFlashHold, fadeTime = screenFlashFade });
            EventBus.Publish(new PlayerExplodedEvent { position = center });

            // 4) 파츠 분리 + 잔해
            SpawnWreckage(center);

            // 대폭발 애니메이션이 완전히 끝날 때까지 대기
            t = 0f;
            while (t < explosionTime)
            {
                t += Time.deltaTime;
                yield return null;
            }

            IsPlaying = false;
            EventBus.Publish(new PlayerDeathSequenceFinishedEvent());
        }

        private void SpawnWreckage(Vector2 center)
        {
            // 두 파츠는 기울어진 헬리콥터 이미지와 같은 각도에서 분리되기 시작한다
            Quaternion tilt = visual != null ? visual.rotation : Quaternion.identity;

            if (cabinPrefab != null)
            {
                WreckPiece cabin = PoolManager.Spawn(cabinPrefab, center + cabinOffset, tilt);
                if (cabin != null) cabin.Launch(cabinVelocity, cabinSpin);
            }

            if (tailPrefab != null)
            {
                WreckPiece tail = PoolManager.Spawn(tailPrefab, center + tailOffset, tilt);
                if (tail != null) tail.Launch(tailVelocity, tailSpin);
            }

            if (debrisPrefab != null)
            {
                int count = Random.Range(debrisCountRange.x, debrisCountRange.y + 1);
                for (int i = 0; i < count; i++)
                {
                    float angle = Random.Range(debrisAngleRange.x, debrisAngleRange.y) * Mathf.Deg2Rad;
                    float speed = Random.Range(debrisSpeedRange.x, debrisSpeedRange.y);
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                    WreckPiece debris = PoolManager.Spawn(debrisPrefab, center + Random.insideUnitCircle * 0.15f,
                        Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                    if (debris != null)
                        debris.Launch(direction * speed, Random.Range(debrisSpinRange.x, debrisSpinRange.y));
                }
            }
        }
    }
}
