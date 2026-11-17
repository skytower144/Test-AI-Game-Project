using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    public enum VfxId
    {
        BulletHit = 0,        // BulletHit.png
        SmallExplosion = 1,   // Lunar Lander Upload/Effects/Explosion.png
        BigExplosion = 2,     // Explosion SpriteSheet.png
        PlayerHit = 3,        // hit - yellow.png
        BossBlast = 4,        // explosion-4.png
        Shockwave = 5,        // 원형 충격파
    }

    /// <summary>이펙트 프리팹을 ID로 찾아 풀에서 꺼내 재생한다.</summary>
    public sealed class VfxManager : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public VfxId id;
            public GameObject prefab;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private static VfxManager s_Instance;
        private readonly Dictionary<VfxId, GameObject> lookup = new Dictionary<VfxId, GameObject>();

        private void Awake()
        {
            s_Instance = this;
            lookup.Clear();
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].prefab != null) lookup[entries[i].id] = entries[i].prefab;
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

        /// <summary>이펙트를 생성한다. scale은 프리팹 기본 크기에 곱해진다.</summary>
        public static GameObject Spawn(VfxId id, Vector2 position, float scale = 1f, float rotationDegrees = 0f)
        {
            if (s_Instance == null) return null;
            if (!s_Instance.lookup.TryGetValue(id, out GameObject prefab)) return null;

            GameObject instance = PoolManager.Spawn(prefab, new Vector3(position.x, position.y, 0f), Quaternion.Euler(0f, 0f, rotationDegrees));
            if (instance != null && !Mathf.Approximately(scale, 1f))
                instance.transform.localScale = prefab.transform.localScale * scale;
            return instance;
        }
    }
}
