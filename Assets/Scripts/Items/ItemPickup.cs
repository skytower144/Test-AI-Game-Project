using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 몬스터 처치 시 드롭되는 아이템. 천천히 왼쪽으로 떠내려가며, 플레이어가 접촉하면 제거되고 효과 이벤트를 발행한다.
    /// </summary>
    public sealed class ItemPickup : MonoBehaviour, IPoolable, IPlayerContact
    {
        [SerializeField] private ItemType itemType = ItemType.RifleAmmo;
        [Tooltip("왼쪽으로 떠내려가는 속도(유닛/초)")]
        [SerializeField, Min(0f)] private float driftSpeed = 1.1f;
        [Tooltip("위아래 흔들림 폭(유닛)")]
        [SerializeField, Min(0f)] private float bobAmplitude = 0.07f;
        [Tooltip("위아래 흔들림 각속도(라디안/초)")]
        [SerializeField, Min(0f)] private float bobFrequency = 3.5f;
        [Tooltip("드롭 직후 살짝 튀어 오르는 연출 시간(초)")]
        [SerializeField, Min(0f)] private float popDuration = 0.18f;
        [Tooltip("왼쪽 화면 밖으로 이만큼 벗어나면 제거")]
        [SerializeField, Min(0f)] private float despawnMargin = 0.5f;
        [Tooltip("화면 위/아래 가장자리와 유지할 여백(유닛)")]
        [SerializeField, Min(0f)] private float verticalPadding = 0.3f;

        private float baseY;
        private float elapsed;
        private float phase;
        private bool collected;
        private Vector3 baseScale = Vector3.one;

        public ItemType Type => itemType;

        public void OnSpawned()
        {
            collected = false;
            elapsed = 0f;
            phase = Random.Range(0f, Mathf.PI * 2f);
            baseScale = transform.localScale;

            // 화면 가장자리에서 드롭되어도 집을 수 있는 높이로 보정
            float min = ScreenBounds.Bottom + verticalPadding;
            float max = ScreenBounds.Top - verticalPadding;
            baseY = min < max ? Mathf.Clamp(transform.position.y, min, max) : transform.position.y;

            if (popDuration > 0f) transform.localScale = baseScale * 0.4f;
        }

        public void OnDespawned()
        {
            collected = true;
        }

        private void Update()
        {
            if (collected) return;

            float dt = Time.deltaTime;
            elapsed += dt;

            Vector3 p = transform.position;
            p.x -= driftSpeed * dt;
            p.y = baseY + Mathf.Sin(phase + elapsed * bobFrequency) * bobAmplitude;
            transform.position = p;

            if (popDuration > 0f)
            {
                float k = Mathf.Clamp01(elapsed / popDuration);
                transform.localScale = baseScale * Mathf.LerpUnclamped(0.4f, 1f, 1f - (1f - k) * (1f - k));
            }

            if (p.x < ScreenBounds.Left - despawnMargin)
                PoolManager.Despawn(gameObject);
        }

        public void OnPlayerContact(PlayerHealth player)
        {
            if (collected) return;
            collected = true;

            EventBus.Publish(new ItemPickedEvent { type = itemType, position = transform.position });
            PoolManager.Despawn(gameObject);
        }
    }
}
