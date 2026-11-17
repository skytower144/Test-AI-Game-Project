using UnityEngine;

namespace Chopper
{
    public enum BoosterLevel { Weak = 0, Normal = 1, Turbo = 2 }

    /// <summary>
    /// 보스 부스터의 로켓 불꽃. Turbo_flight 폴더의 세 가지 불꽃 세트(Exhaust1~3)를 출력 단계별로 재생한다.
    /// Exhaust1 = 최대 출력, Exhaust2 = 평상시, Exhaust3 = 약한 불꽃.
    /// </summary>
    public sealed class BoosterFlame : MonoBehaviour
    {
        [SerializeField] private SpriteAnimator animator;
        [SerializeField] private Sprite[] turboFrames;
        [SerializeField] private Sprite[] normalFrames;
        [SerializeField] private Sprite[] weakFrames;
        [SerializeField, Min(0.01f)] private float framesPerSecond = 14f;
        [SerializeField] private BoosterLevel initialLevel = BoosterLevel.Normal;

        private BoosterLevel level;
        private bool initialized;

        private void OnEnable()
        {
            // 상시 재생: 활성화될 때마다 초기 단계로 다시 시작
            initialized = false;
            SetLevel(initialLevel);
        }

        public void SetLevel(BoosterLevel newLevel)
        {
            if (initialized && newLevel == level) return;
            initialized = true;
            level = newLevel;

            Sprite[] frames = newLevel == BoosterLevel.Turbo ? turboFrames
                            : newLevel == BoosterLevel.Normal ? normalFrames
                            : weakFrames;

            if (animator != null && frames != null && frames.Length > 0)
                animator.Play(frames, framesPerSecond, true, true);
        }
    }
}
