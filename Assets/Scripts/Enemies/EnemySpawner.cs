using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 일정한 간격으로 몬스터를 스폰한다. 어떤 몬스터가 나올지는 가중치로 정한다.
    /// 보스가 있는 동안에는 Hornet 스폰을 멈추고 Wasp 스폰 빈도를 크게 낮춘다.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private Enemy waspPrefab;
        [SerializeField] private Enemy hornetPrefab;

        [Header("Normal")]
        [Tooltip("스폰 가중치. Wasp가 Hornet보다 높아야 한다")]
        [SerializeField, Min(0f)] private float waspWeight = 3f;
        [SerializeField, Min(0f)] private float hornetWeight = 1.8f;
        [Tooltip("다음 스폰까지의 간격 범위(초)")]
        [SerializeField] private Vector2 spawnIntervalRange = new Vector2(0.096f, 0.256f);
        [Tooltip("게임 시작 후 첫 스폰까지의 지연(초). 진입 연출(검은색 바)이 끝난 뒤 등장하도록 한다")]
        [SerializeField, Min(0f)] private float startDelay = 2.6f;

        [Header("While Boss Is Alive")]
        [Tooltip("보스 생존 중 Wasp 스폰 간격 범위(초)")]
        [SerializeField] private Vector2 bossSpawnIntervalRange = new Vector2(3f, 4.5f);

        [Header("Spawn Area")]
        [Tooltip("화면 오른쪽 밖으로 얼마나 떨어진 곳에서 생성할지")]
        [SerializeField, Min(0f)] private float spawnOffsetX = 0.5f;
        [Tooltip("생성 높이 범위(뷰포트 Y, 0=아래 1=위)")]
        [SerializeField] private Vector2 spawnViewportYRange = new Vector2(0.08f, 0.92f);

        private float timer;
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
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            timer -= dt;
            if (timer > 0f) return;

            Spawn();

            // 이번 프레임에서 늦어진 만큼(음수인 timer)을 다음 간격에서 빼서, 프레임률과 무관하게 평균 스폰 간격을 유지한다
            Vector2 range = bossActive ? bossSpawnIntervalRange : spawnIntervalRange;
            timer = Mathf.Max(timer, -dt) + Random.Range(range.x, range.y);
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
