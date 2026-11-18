using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 인게임 사운드 연출. 게임 로직은 사운드를 직접 재생하지 않고,
    /// 이 컴포넌트가 이벤트 버스의 이벤트를 받아 해당하는 사운드를 재생한다.
    /// </summary>
    public sealed class GameAudioDirector : MonoBehaviour
    {
        [Header("Ambient")]
        [SerializeField, Min(0f)] private float ambientFadeIn = 1.2f;

        [Header("Gun")]
        [Tooltip("두 번째 총알타입의 발사음을 늦추는 정도(발사 간격 대비 비율). 0.5 = 첫 번째 총알 소리 사이의 정확히 가운데")]
        [SerializeField, Range(0f, 1f)] private float rifleSoundOffset = 0.5f;

        [Header("Chopper Loop")]
        [SerializeField, Min(0f)] private float chopperFadeIn = 0.25f;
        [Tooltip("체력 0 이후 대폭발까지 로터 소리의 피치가 떨어지는 최종값")]
        [SerializeField, Range(0.1f, 1f)] private float dyingChopperPitch = 0.6f;
        [Tooltip("피치가 떨어지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float dyingPitchTime = 2f;

        [Header("Boss")]
        [SerializeField, Min(0f)] private float bossEngineFadeIn = 0.4f;
        [SerializeField, Min(0f)] private float bossEngineFadeOut = 0.6f;
        [SerializeField, Min(0f)] private float portalFadeOut = 0.35f;
        [Tooltip("portal_idle 루프를 재생할 패턴 번호(0부터). 4 = 5번 패턴")]
        [SerializeField] private int portalPatternIndex = 4;

        private bool playerDying;
        private float dyingTimer;

        private void OnEnable()
        {
            EventBus.Subscribe<GameStartedEvent>(OnGameStarted);
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
            EventBus.Subscribe<PlayerFiredEvent>(OnPlayerFired);
            EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Subscribe<PlayerExplodedEvent>(OnPlayerExploded);
            EventBus.Subscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Subscribe<BulletImpactEvent>(OnBulletImpact);
            EventBus.Subscribe<SmallExplosionEvent>(OnSmallExplosion);
            EventBus.Subscribe<ItemPickedEvent>(OnItemPicked);
            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Subscribe<BossSpawnedEvent>(OnBossSpawned);
            EventBus.Subscribe<BossRevealStartedEvent>(OnBossRevealStarted);
            EventBus.Subscribe<BossFiredEvent>(OnBossFired);
            EventBus.Subscribe<BossPatternStartedEvent>(OnBossPatternStarted);
            EventBus.Subscribe<BossPatternEndedEvent>(OnBossPatternEnded);
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<BossBigExplosionEvent>(OnBossBigExplosion);
            EventBus.Subscribe<BossBlastEvent>(OnBossBlast);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStartedEvent>(OnGameStarted);
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
            EventBus.Unsubscribe<PlayerFiredEvent>(OnPlayerFired);
            EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Unsubscribe<PlayerExplodedEvent>(OnPlayerExploded);
            EventBus.Unsubscribe<PlayerRevivedEvent>(OnPlayerRevived);
            EventBus.Unsubscribe<BulletImpactEvent>(OnBulletImpact);
            EventBus.Unsubscribe<SmallExplosionEvent>(OnSmallExplosion);
            EventBus.Unsubscribe<ItemPickedEvent>(OnItemPicked);
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
            EventBus.Unsubscribe<BossRevealStartedEvent>(OnBossRevealStarted);
            EventBus.Unsubscribe<BossFiredEvent>(OnBossFired);
            EventBus.Unsubscribe<BossPatternStartedEvent>(OnBossPatternStarted);
            EventBus.Unsubscribe<BossPatternEndedEvent>(OnBossPatternEnded);
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<BossBigExplosionEvent>(OnBossBigExplosion);
            EventBus.Unsubscribe<BossBlastEvent>(OnBossBlast);
        }

        private void Update()
        {
            if (!playerDying) return;

            dyingTimer += Time.deltaTime;
            AudioManager.SetLoopPitch(SoundId.Chopper, Mathf.Lerp(1f, dyingChopperPitch, dyingTimer / dyingPitchTime));
        }

        // ───────────── 게임 흐름 ─────────────

        private void OnGameStarted(GameStartedEvent evt)
        {
            AudioManager.PlayLoop(SoundId.GameMusic, ambientFadeIn);
            AudioManager.PlayLoop(SoundId.RainThunder, ambientFadeIn);
            AudioManager.PlayLoop(SoundId.Blizzard, ambientFadeIn);
            AudioManager.PlayLoop(SoundId.Chopper, chopperFadeIn);

            if (evt.isRetry) AudioManager.Play(SoundId.Revive); // Revive.mp3: 부활 및 RETRY
        }

        // RETRY: 배경음은 그대로 이어가고, 보스/사망 연출에 딸린 루프와 피치만 처음 상태로 되돌린다.
        private void OnGameReset(GameResetEvent evt)
        {
            playerDying = false;
            AudioManager.StopLoop(SoundId.BossEngine, bossEngineFadeOut);
            AudioManager.StopLoop(SoundId.PortalIdle, portalFadeOut);
            AudioManager.SetLoopPitch(SoundId.Chopper, 1f);
        }

        // ───────────── 플레이어 ─────────────

        private void OnPlayerFired(PlayerFiredEvent evt)
        {
            // Gun.wav: 첫 번째 총알타입, Gun2.mp3: 두 번째 총알타입.
            // 두 총알은 같은 순간에 발사되지만, 두 번째 총알의 소리는 발사 간격의 일부만큼 늦춰
            // 두 총이 번갈아 가며 쏘는 것처럼 들리게 한다.
            bool rifle = evt.bulletType == BulletType.Rifle;
            float delay = rifle ? Mathf.Max(0f, evt.interval) * rifleSoundOffset : 0f;

            AudioManager.Play(rifle ? SoundId.Gun2 : SoundId.Gun, 1f, delay);
            AudioManager.Play(SoundId.GunLayer, 1f, delay); // 발사음에 공통으로 얹는 낮은 볼륨의 소리
        }

        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            AudioManager.Play(SoundId.PlayerHurt);
        }

        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            // 체력 0 도달 순간
            AudioManager.Play(SoundId.ImminentExplosion);
            AudioManager.Play(SoundId.Charge);
            playerDying = true;
            dyingTimer = 0f;
        }

        private void OnPlayerExploded(PlayerExplodedEvent evt)
        {
            playerDying = false;
            AudioManager.StopLoop(SoundId.Chopper, 0.05f);
            AudioManager.Play(SoundId.Mayday);
            AudioManager.Play(SoundId.BombExplosion);
            AudioManager.Play(SoundId.BombExplosion2);
        }

        private void OnPlayerRevived(PlayerRevivedEvent evt)
        {
            playerDying = false;
            AudioManager.Play(SoundId.Revive);
            AudioManager.PlayLoop(SoundId.Chopper, chopperFadeIn);
            AudioManager.SetLoopPitch(SoundId.Chopper, 1f);
        }

        private void OnBulletImpact(BulletImpactEvent evt)
        {
            AudioManager.Play(SoundId.BulletHit);
            AudioManager.Play(SoundId.Ricochet);
        }

        private void OnSmallExplosion(SmallExplosionEvent evt)
        {
            AudioManager.Play(SoundId.SmallExplosion);
        }

        // ───────────── 아이템 / 몬스터 ─────────────

        private void OnItemPicked(ItemPickedEvent evt)
        {
            switch (evt.type)
            {
                case ItemType.RifleAmmo:
                    AudioManager.Play(SoundId.Reload);
                    break;
                case ItemType.ShotgunAmmo:
                    AudioManager.Play(SoundId.Blank);
                    AudioManager.Play(SoundId.Blank2);
                    AudioManager.Play(SoundId.Blank3);
                    break;
                case ItemType.Medkit:
                    AudioManager.Play(SoundId.IronHit);
                    AudioManager.Play(SoundId.Health);
                    break;
            }
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (evt.type != EnemyType.Boss) AudioManager.Play(SoundId.BoxExplosion);
        }

        // ───────────── 보스 ─────────────

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            AudioManager.Play(SoundId.BossArrival);
            AudioManager.PlayLoop(SoundId.BossEngine, bossEngineFadeIn);
        }

        private void OnBossRevealStarted(BossRevealStartedEvent evt)
        {
            AudioManager.Play(SoundId.BossSlowArrival);
        }

        private void OnBossFired(BossFiredEvent evt)
        {
            AudioManager.Play(SoundId.BossBullet);
        }

        private void OnBossPatternStarted(BossPatternStartedEvent evt)
        {
            if (evt.patternIndex == portalPatternIndex) AudioManager.PlayLoop(SoundId.PortalIdle, 0.2f);
        }

        private void OnBossPatternEnded(BossPatternEndedEvent evt)
        {
            if (evt.patternIndex == portalPatternIndex) AudioManager.StopLoop(SoundId.PortalIdle, portalFadeOut);
        }

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            AudioManager.StopLoop(SoundId.BossEngine, bossEngineFadeOut);
            AudioManager.StopLoop(SoundId.PortalIdle, portalFadeOut);
        }

        private void OnBossBigExplosion(BossBigExplosionEvent evt)
        {
            AudioManager.Play(SoundId.Explosion4);
        }

        private void OnBossBlast(BossBlastEvent evt)
        {
            AudioManager.Play(SoundId.Explosion2);
            AudioManager.Play(SoundId.Explosion3);
        }
    }
}
