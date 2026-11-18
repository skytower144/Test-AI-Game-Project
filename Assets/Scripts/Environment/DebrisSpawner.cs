using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// debris.png의 모든 스프라이트를 순환 사용해 배경 잔해를 생성한다.
    /// 잔해는 화면 오른쪽 밖에서 생성되어 각자 랜덤한 속도로 왼쪽 밖까지 떠밀려간다.
    /// </summary>
    public sealed class DebrisSpawner : MonoBehaviour
    {
        [SerializeField] private DriftingDebris debrisPrefab;
        [Tooltip("debris.png에 포함된 모든 스프라이트")]
        [SerializeField] private Sprite[] sprites = System.Array.Empty<Sprite>();

        [Header("Randomization")]
        [Tooltip("이동 속도 범위(유닛/초). 잔해마다 이 범위에서 랜덤하게 정해진다")]
        [SerializeField] private Vector2 speedRange = new Vector2(0.35f, 2.1f);
        [Tooltip("크기 배율 범위. 빠른 잔해일수록 크게(가깝게) 보인다")]
        [SerializeField] private Vector2 scaleRange = new Vector2(1.2f, 2.6f);
        [Tooltip("생성 간격 범위(초)")]
        [SerializeField] private Vector2 spawnIntervalRange = new Vector2(1.5f, 3.5f);
        [Tooltip("생성 높이 범위(뷰포트 Y). 배경은 화면 위쪽 절반")]
        [SerializeField] private Vector2 viewportYRange = new Vector2(0.55f, 0.97f);
        [SerializeField] private Color nearColor = Color.white;
        [SerializeField] private Color farColor = new Color(1f, 1f, 1f, 0.6f);

        [Header("Start")]
        [Tooltip("시작 시 화면에 미리 배치할 잔해 수")]
        [SerializeField, Min(0)] private int initialCount = 4;
        [SerializeField] private int baseSortingOrder = 10;

        private float timer;
        private int[] bag;
        private int bagIndex;

        private void OnEnable()
        {
            EventBus.Subscribe<GameResetEvent>(OnGameReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameResetEvent>(OnGameReset);
        }

        private void Start()
        {
            Populate();
        }

        // RETRY: 기존 잔해는 풀로 반납된 상태이므로 처음처럼 다시 배치한다
        private void OnGameReset(GameResetEvent evt)
        {
            Populate();
        }

        private void Populate()
        {
            for (int i = 0; i < initialCount; i++)
                Spawn(Random.Range(ScreenBounds.Left, ScreenBounds.Right));

            timer = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y) * 0.5f;
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            timer = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
            Spawn(float.NaN);
        }

        /// <summary>x가 NaN이면 화면 오른쪽 밖에서 생성한다.</summary>
        private void Spawn(float x)
        {
            if (debrisPrefab == null || sprites == null || sprites.Length == 0) return;

            Sprite sprite = NextSprite();
            if (sprite == null) return;

            float depth = Random.value; // 0 = 멀다(작고 느림), 1 = 가깝다(크고 빠름)
            float speed = Mathf.Lerp(speedRange.x, speedRange.y, depth);
            float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, depth) * Random.Range(0.9f, 1.1f);
            float y = ScreenBounds.FromViewport(0f, Random.Range(viewportYRange.x, viewportYRange.y)).y;

            // 회전은 적용하지 않는다(원본 스프라이트 방향 그대로)
            DriftingDebris debris = PoolManager.Spawn(debrisPrefab, Vector3.zero, Quaternion.identity);
            if (debris == null) return;

            debris.Setup(sprite, scale, speed, Color.Lerp(farColor, nearColor, depth), Random.value < 0.5f,
                baseSortingOrder + Mathf.RoundToInt(depth * 10f));

            if (float.IsNaN(x)) x = ScreenBounds.Right + debris.HalfWidth;
            debris.transform.position = new Vector3(x, y, 0f);
        }

        // 셔플 백: 모든 스프라이트가 빠짐없이 한 번씩 쓰인 뒤 다시 섞는다.
        private Sprite NextSprite()
        {
            if (bag == null || bag.Length != sprites.Length)
            {
                bag = new int[sprites.Length];
                for (int i = 0; i < bag.Length; i++) bag[i] = i;
                bagIndex = bag.Length;
            }

            if (bagIndex >= bag.Length)
            {
                for (int i = bag.Length - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (bag[i], bag[j]) = (bag[j], bag[i]);
                }
                bagIndex = 0;
            }

            return sprites[bag[bagIndex++]];
        }
    }
}
