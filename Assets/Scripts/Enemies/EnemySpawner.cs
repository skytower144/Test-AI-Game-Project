using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 화면에 항상 약 5~6마리의 몬스터가 존재하도록 스폰을 관리한다.
    /// 보스가 있는 동안에는 Hornet 스폰을 멈추고 Wasp 스폰 빈도를 크게 낮춘다.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private Enemy waspPrefab;
        [SerializeField] private Enemy hornetPrefab;

        [Header("Normal")]
        [Tooltip("화면에 유지할 몬스터 수 범위")]
        [SerializeField] private Vector2Int targetCountRange = new Vector2Int(5, 6);
        [Tooltip("스폰 가중치. Wasp가 Hornet보다 높아야 한다")]
        [SerializeField, Min(0f)] private float waspWeight = 3f;
        [SerializeField, Min(0f)] private float hornetWeight = 1f;
        [Tooltip("몬스터가 부족할 때 다음 스폰까지의 간격 범위(초)")]
        [SerializeField] private Vector2 spawnIntervalRange = new Vector2(0.15f, 0.4f);
        [Tooltip("게임 시작 후 첫 스폰까지의 지연(초). 진입 연출(검은색 바)이 끝난 뒤 등장하도록 한다")]
        [SerializeField, Min(0f)] private float startDelay = 2.6f;

        [Header("While Boss Is Alive")]
        [SerializeField, Min(0)] private int bossTargetCount = 2;
        [Tooltip("보스 생존 중 Wasp 스폰 간격 범위(초)")]
        [SerializeField] private Vector2 bossSpawnIntervalRange = new Vector2(3f, 4.5f);

        [Header("Spawn Area")]
        [Tooltip("화면 오른쪽 밖으로 얼마나 떨어진 곳에서 생성할지")]
        [SerializeField, Min(0f)] private float spawnOffsetX = 0.5f;
        [Tooltip("생성 높이 범위(뷰포트 Y, 0=아래 1=위)")]
        [SerializeField] private Vector2 spawnViewportYRange = new Vector2(0.08f, 0.92f);

        private float timer;
        private int targetCount;
        private bool bossActive;

        private void OnEnable()
        {
            EventBus.Subscribe<BossSpawnedEvent>(OnBossSpawned);
            EventBus.Subscribe<BossDestroyedEvent>(OnBossDestroyed);
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
            EventBus.Unsubscribe<BossDestroyedEvent>(OnBossDestroyed);
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
        }

        private void Start()
        {
            ResetState();
        }

        private void OnGameReset(GameResetEvent evt)
        {
            ResetState();
        }

        private void ResetState()
        {
            bossActive = false;
            timer = startDelay;
            RollTargetCount();
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            int alive = CountAlive();
            int target = bossActive ? bossTargetCount : targetCount;

            if (alive < target)
            {
                Spawn();
                RollTargetCount();
            }

            Vector2 range = bossActive ? bossSpawnIntervalRange : spawnIntervalRange;
            timer = Random.Range(range.x, range.y);
        }

        private void Spawn()
        {
            Enemy prefab = waspPrefab;
            if (!bossActive && hornetPrefab != null)
            {
                float total = waspWeight + hornetWeight;
                if (total > 0f && Random.value * total >= waspWeight) prefab = hornetPrefab;
            }
            if (prefab == null) return;

            float x = ScreenBounds.Right + spawnOffsetX;
            float y = ScreenBounds.FromViewport(0f, Random.Range(spawnViewportYRange.x, spawnViewportYRange.y)).y;
            PoolManager.Spawn(prefab, new Vector3(x, y, 0f), Quaternion.identity);
        }

        private void RollTargetCount()
        {
            targetCount = Random.Range(targetCountRange.x, targetCountRange.y + 1);
        }

        private static int CountAlive()
        {
            int count = 0;
            var list = Enemy.Active;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].IsAlive) count++;
            }
            return count;
        }

        private void OnBossSpawned(BossSpawnedEvent evt)
        {
            bossActive = true;
            timer = Mathf.Max(timer, bossSpawnIntervalRange.x);
        }

        private void OnBossDestroyed(BossDestroyedEvent evt)
        {
            bossActive = false;
            timer = spawnIntervalRange.y;
        }
    }
}
