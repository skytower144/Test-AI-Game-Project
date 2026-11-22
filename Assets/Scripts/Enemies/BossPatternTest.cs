using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 패턴 에디터의 "게임에서 테스트" 상태.
    /// 패턴이 지정되어 있는 동안 보스는 등장 대기 시간과 등장 연출 없이 바로 전투 위치에 나타나,
    /// 원래의 패턴 목록 대신 그 패턴만 반복 실행한다. 지정을 해제하면 원래의 패턴 목록으로 돌아간다.
    /// 테스트하는 동안 플레이어를 무적으로 둘 수 있다.
    /// (게임 쪽에서는 BossDirector, Boss, PlayerHealth가 이 상태를 읽어 따른다.)
    /// </summary>
    public static class BossPatternTest
    {
        public const float DefaultRepeatDelay = 1f;

        private static BossPattern s_Pattern;
        private static float s_RepeatDelay = DefaultRepeatDelay;
        private static bool s_PlayerInvincible = true;

        /// <summary>반복 실행할 패턴. 테스트 중이 아니면(또는 그 패턴이 파괴됐으면) null.</summary>
        public static BossPattern Pattern => s_Pattern != null ? s_Pattern : null;

        public static bool IsActive => s_Pattern != null;

        /// <summary>패턴이 끝난 뒤 다시 시작하기까지의 대기(초).</summary>
        public static float RepeatDelay => s_RepeatDelay;

        /// <summary>테스트하는 동안 플레이어가 피해를 받지 않는지. 테스트 중이 아니면 항상 false.</summary>
        public static bool PlayerInvincible => s_PlayerInvincible && IsActive;

        /// <summary>
        /// pattern만 반복 실행하는 테스트를 시작한다.
        /// 이미 테스트 중이면 대상 패턴, 대기 시간, 플레이어 무적 여부를 바꾼다.
        /// </summary>
        public static void Begin(BossPattern pattern, float repeatDelay = DefaultRepeatDelay, bool playerInvincible = true)
        {
            s_Pattern = pattern;
            s_RepeatDelay = Mathf.Max(0f, repeatDelay);
            s_PlayerInvincible = playerInvincible;
        }

        public static void End()
        {
            s_Pattern = null;
        }

        // Enter Play Mode 옵션(도메인 리로드 비활성화)에서도 이전 플레이의 테스트 상태가 남지 않도록 초기화.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            s_Pattern = null;
            s_RepeatDelay = DefaultRepeatDelay;
            s_PlayerInvincible = true;
        }
    }
}
