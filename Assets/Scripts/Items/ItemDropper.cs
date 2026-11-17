using System;
using UnityEngine;

namespace Chopper
{
    /// <summary>몬스터 처치 이벤트를 받아 확률에 따라 아이템을 드롭한다.</summary>
    public sealed class ItemDropper : MonoBehaviour
    {
        [Serializable]
        public struct DropEntry
        {
            public ItemType type;
            public ItemPickup prefab;
            [Range(0f, 1f)] public float chance;
        }

        [Tooltip("드롭 테이블. 확률의 합이 1 미만이면 나머지는 드롭 없음")]
        [SerializeField] private DropEntry[] drops = Array.Empty<DropEntry>();

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (evt.type == EnemyType.Boss) return;

            float roll = UnityEngine.Random.value;
            float cumulative = 0f;
            for (int i = 0; i < drops.Length; i++)
            {
                cumulative += drops[i].chance;
                if (roll < cumulative)
                {
                    if (drops[i].prefab != null)
                        PoolManager.Spawn(drops[i].prefab, new Vector3(evt.position.x, evt.position.y, 0f), Quaternion.identity);
                    return;
                }
            }
        }
    }
}
