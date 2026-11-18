using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    /// <summary>풀에서 꺼내질 때/반납될 때 호출되는 콜백.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>풀링된 인스턴스에 자동으로 부착되어 소속 풀 정보를 보관한다.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class PooledObject : MonoBehaviour
    {
        internal GameObject SourcePrefab;
        internal bool IsInPool;

        private IPoolable[] poolables;

        internal IPoolable[] Poolables
        {
            get
            {
                if (poolables == null) poolables = GetComponentsInChildren<IPoolable>(true);
                return poolables;
            }
        }

        /// <summary>자신을 풀에 반납한다.</summary>
        public void Release()
        {
            PoolManager.Despawn(gameObject);
        }
    }

    /// <summary>
    /// 프리팹 단위 오브젝트 풀. 총알, 적, 이펙트, 아이템, 잔해 등 반복 생성되는 모든 오브젝트를 관리한다.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class PoolManager : MonoBehaviour
    {
        [Serializable]
        public struct PrewarmEntry
        {
            public GameObject prefab;
            [Min(0)] public int count;
        }

        [Tooltip("씬 시작 시 미리 생성해 둘 프리팹과 개수")]
        [SerializeField] private PrewarmEntry[] prewarm = Array.Empty<PrewarmEntry>();

        private static PoolManager s_Instance;

        private readonly Dictionary<GameObject, Stack<PooledObject>> pools = new Dictionary<GameObject, Stack<PooledObject>>();
        private readonly Dictionary<GameObject, Transform> roots = new Dictionary<GameObject, Transform>();

        public static bool IsAvailable => s_Instance != null;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;

            for (int i = 0; i < prewarm.Length; i++)
            {
                GameObject prefab = prewarm[i].prefab;
                if (prefab == null) continue;

                Stack<PooledObject> stack = GetStack(prefab);
                for (int n = stack.Count; n < prewarm[i].count; n++)
                {
                    PooledObject instance = CreateInstance(prefab, Vector3.zero, Quaternion.identity);
                    instance.IsInPool = true;
                    instance.gameObject.SetActive(false);
                    stack.Push(instance);
                }
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            s_Instance = null;
        }

        // ───────────── 공개 API ─────────────

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null || s_Instance == null) return null;
            return s_Instance.SpawnInternal(prefab, position, rotation).gameObject;
        }

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            if (prefab == null || s_Instance == null) return null;
            return s_Instance.SpawnInternal(prefab.gameObject, position, rotation).GetComponent<T>();
        }

        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;

            var pooled = instance.GetComponent<PooledObject>();
            if (pooled == null || pooled.SourcePrefab == null || s_Instance == null)
            {
                // 풀 소속이 아니거나 씬이 내려가는 중이면 그냥 파괴한다.
                Destroy(instance);
                return;
            }

            if (pooled.IsInPool) return; // 중복 반납 방지
            pooled.IsInPool = true;

            IPoolable[] poolables = pooled.Poolables;
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnDespawned();

            instance.SetActive(false);
            s_Instance.GetStack(pooled.SourcePrefab).Push(pooled);
        }

        /// <summary>현재 사용 중인 모든 풀링 오브젝트를 풀에 반납한다(RETRY로 인게임을 초기화할 때 사용).</summary>
        public static void DespawnAll()
        {
            if (s_Instance == null) return;

            // 모든 인스턴스는 프리팹별 루트의 자식으로 만들어진다
            foreach (Transform root in s_Instance.roots.Values)
            {
                if (root == null) continue;
                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    var pooled = root.GetChild(i).GetComponent<PooledObject>();
                    if (pooled != null && !pooled.IsInPool) Despawn(pooled.gameObject);
                }
            }
        }

        // ───────────── 내부 구현 ─────────────

        private PooledObject SpawnInternal(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            Stack<PooledObject> stack = GetStack(prefab);

            PooledObject pooled = null;
            while (stack.Count > 0 && pooled == null)
                pooled = stack.Pop(); // 외부에서 파괴된 항목은 건너뛴다

            if (pooled == null)
            {
                pooled = CreateInstance(prefab, position, rotation);
            }
            else
            {
                Transform t = pooled.transform;
                t.SetPositionAndRotation(position, rotation);
                t.localScale = prefab.transform.localScale;
                pooled.gameObject.SetActive(true);
            }

            pooled.IsInPool = false;

            IPoolable[] poolables = pooled.Poolables;
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnSpawned();

            return pooled;
        }

        private PooledObject CreateInstance(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject instance = Instantiate(prefab, position, rotation, GetRoot(prefab));
            instance.name = prefab.name;

            var pooled = instance.GetComponent<PooledObject>();
            if (pooled == null) pooled = instance.AddComponent<PooledObject>();
            pooled.SourcePrefab = prefab;
            return pooled;
        }

        private Stack<PooledObject> GetStack(GameObject prefab)
        {
            if (!pools.TryGetValue(prefab, out Stack<PooledObject> stack))
            {
                stack = new Stack<PooledObject>();
                pools.Add(prefab, stack);
            }
            return stack;
        }

        private Transform GetRoot(GameObject prefab)
        {
            if (!roots.TryGetValue(prefab, out Transform root) || root == null)
            {
                root = new GameObject("Pool - " + prefab.name).transform;
                root.SetParent(transform, false);
                roots[prefab] = root;
            }
            return root;
        }
    }
}
