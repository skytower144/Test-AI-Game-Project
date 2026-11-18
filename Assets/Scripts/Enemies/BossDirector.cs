using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 보스 등장 시점 관리. 게임 시작(또는 직전 보스 처치) 후 일정 시간이 지나고 플레이어가 살아 있을 때 보스를 등장시킨다.
    /// </summary>
    public sealed class BossDirector : MonoBehaviour
    {
        [SerializeField] private Boss bossPrefab;
        [Tooltip("게임 시작 또는 보스 처치 후 다음 보스 등장까지의 시간(초)")]
        [SerializeField, Min(0f)] private float spawnDelay = 40f;

        private float timer;
        private bool bossAlive;
        private bool gameOver;

        /// <summary>다음 보스 등장까지 남은 시간(초).</summary>
        public float TimeUntilSpawn => Mathf.Max(0f, spawnDelay - timer);

        private void OnEnable()
        {
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
        }

        private void Update()
        {
            if (bossAlive || gameOver || bossPrefab == null) return;

            timer += Time.deltaTime;
            if (timer < spawnDelay) return;

            // 플레이어가 살아 있을 때만 등장
            PlayerHealth player = PlayerHealth.Current;
            if (player == null || player.IsDead) return;

            Boss boss = PoolManager.Spawn(bossPrefab, new Vector3(ScreenBounds.Left - 10f, 0f, 0f), Quaternion.identity);
            if (boss != null) bossAlive = true;
        }

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            // 처치 시 등장 쿨타임 초기화
            bossAlive = false;
            timer = 0f;
        }

        private void OnGameOver(GameOverEvent evt)
        {
            gameOver = true;
        }

        // RETRY: 보스는 이미 풀로 반납된 상태. 등장 타이머를 처음부터 다시 센다
        private void OnGameReset(GameResetEvent evt)
        {
            bossAlive = false;
            gameOver = false;
            timer = 0f;
        }
    }
}
