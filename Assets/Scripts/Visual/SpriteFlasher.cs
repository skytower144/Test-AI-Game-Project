using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 스프라이트의 머티리얼을 단색 머티리얼(whiteMAT / redMAT / reviveMAT)로 번갈아 교체해 점멸시킨다.
    /// </summary>
    public sealed class SpriteFlasher : MonoBehaviour
    {
        [Tooltip("점멸 대상 렌더러. 비워 두면 자식의 모든 SpriteRenderer를 사용")]
        [SerializeField] private SpriteRenderer[] renderers;

        private Material[] originalMaterials;
        private Material flashMaterial;
        private float interval;
        private float toggleTimer;
        private float timeLeft;      // 시간 기반 점멸의 남은 시간
        private int togglesLeft;     // 횟수 기반 점멸의 남은 전환 수
        private bool timeBased;
        private bool continuous;
        private bool flashOn;
        private bool active;

        public bool IsFlashing => active;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (originalMaterials != null) return;

            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);

            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                originalMaterials[i] = renderers[i] != null ? renderers[i].sharedMaterial : null;
        }

        /// <summary>count회 점멸한다. (켜짐 interval + 꺼짐 interval) × count</summary>
        public void Flash(Material material, int count, float flashInterval)
        {
            if (material == null || count <= 0) return;
            Begin(material, flashInterval);
            timeBased = false;
            continuous = false;
            togglesLeft = count * 2 - 1;
        }

        /// <summary>duration초 동안 점멸한다.</summary>
        public void FlashFor(Material material, float duration, float flashInterval)
        {
            if (material == null || duration <= 0f) return;
            Begin(material, flashInterval);
            timeBased = true;
            continuous = false;
            timeLeft = duration;
        }

        /// <summary>Stop()이 호출될 때까지 계속 점멸한다.</summary>
        public void FlashContinuous(Material material, float flashInterval)
        {
            if (material == null) return;
            Begin(material, flashInterval);
            timeBased = false;
            continuous = true;
        }

        public void Stop()
        {
            active = false;
            SetFlash(false);
        }

        private void Begin(Material material, float flashInterval)
        {
            EnsureInitialized();
            flashMaterial = material;
            interval = Mathf.Max(0.01f, flashInterval);
            toggleTimer = interval;
            active = true;
            SetFlash(true);
        }

        private void OnDisable()
        {
            if (originalMaterials != null) Stop();
        }

        private void Update()
        {
            if (!active) return;

            float dt = Time.deltaTime;

            if (timeBased)
            {
                timeLeft -= dt;
                if (timeLeft <= 0f)
                {
                    Stop();
                    return;
                }
            }

            toggleTimer -= dt;
            if (toggleTimer > 0f) return;
            toggleTimer += interval;
            if (toggleTimer < 0f) toggleTimer = 0f;

            if (!timeBased && !continuous)
            {
                if (togglesLeft <= 0)
                {
                    Stop();
                    return;
                }
                togglesLeft--;
            }

            SetFlash(!flashOn);
        }

        private void SetFlash(bool on)
        {
            flashOn = on;
            if (renderers == null || originalMaterials == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sharedMaterial = on && flashMaterial != null ? flashMaterial : originalMaterials[i];
            }
        }
    }
}
