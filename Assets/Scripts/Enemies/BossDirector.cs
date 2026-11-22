using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 보스 등장 시점 관리. 게임 시작(또는 직전 보스 처치) 후 일정 시간이 지나고 플레이어가 살아 있을 때 보스를 등장시킨다.
    /// 패턴 테스트(BossPatternTest) 중에는 등장 대기 시간을 기다리지 않고 바로 등장시킨다.
    /// </summary>
    public sealed class BossDirector : MonoBehaviour
    {
        [SerializeField] private Boss bossPrefab;
        [Tooltip("게임 시작 또는 보스 처치 후 다음 보스 등장까지의 시간(초)")]
        [SerializeField, Min(0f)] private float spawnDelay = 40f;

        private float timer;
        private bool bossAlive;     // 등장한 뒤 처치되기 전
        private bool bossPresent;   // 등장한 뒤 처치 연출이 끝나 완전히 사라지기 전
        private bool gameOver;

        /// <summary>다음 보스 등장까지 남은 시간(초).</summary>
        public float TimeUntilSpawn => Mathf.Max(0f, spawnDelay - timer);

        private void OnEnable()
        {
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<BossDestroyedEvent>(OnBossDestroyed);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<BossDestroyedEvent>(OnBossDestroyed);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
        }

        private void Update()
        {
            if (bossAlive || gameOver || bossPrefab == null) return;

            if (BossPatternTest.IsActive)
            {
                // 패턴 테스트: 등장 대기 시간도, 플레이어의 생존 여부도 기다리지 않는다(사망한 채로 패턴만 지켜볼 수도 있다).
                // 직전 보스가 처치됐다면 그 처치 연출이 끝나 완전히 사라진 뒤에 등장시킨다
                if (bossPresent) return;
            }
            else
            {
                timer += Time.deltaTime;
                if (timer < spawnDelay) return;

                // 플레이어가 살아 있을 때만 등장
                PlayerHealth player = PlayerHealth.Current;
                if (player == null || player.IsDead) return;
            }

            Boss boss = PoolManager.Spawn(bossPrefab, new Vector3(ScreenBounds.Left - 10f, 0f, 0f), Quaternion.identity);
            if (boss != null)
            {
                bossAlive = true;
                bossPresent = true;
            }
        }

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            // 처치 시 등장 쿨타임 초기화
            bossAlive = false;
            timer = 0f;
        }

        private void OnBossDestroyed(BossDestroyedEvent evt)
        {
            bossPresent = false;
        }

        private void OnGameOver(GameOverEvent evt)
        {
            gameOver = true;
        }

        // RETRY: 보스는 이미 풀로 반납된 상태. 등장 타이머를 처음부터 다시 센다
        private void OnGameReset(GameResetEvent evt)
        {
            bossAlive = false;
            bossPresent = false;
            gameOver = false;
            timer = 0f;
        }
    }
}
