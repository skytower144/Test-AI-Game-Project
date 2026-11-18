using UnityEngine;

namespace Chopper
{
    /// <summary>씬 전환 사이에 유지되는 세션 데이터(유저네임).</summary>
    public static class GameSession
    {
        public const string TitleSceneName = "Title";
        public const string GameSceneName = "Game";
        public const string DefaultPlayerName = "PLAYER";

        private static string s_PlayerName = DefaultPlayerName;

        public static string PlayerName
        {
            get => string.IsNullOrEmpty(s_PlayerName) ? DefaultPlayerName : s_PlayerName;
            set => s_PlayerName = value;
        }

        /// <summary>
        /// VSync가 꺼져 있을 때 프레임률이 무제한으로 치솟지 않도록 제한한다.
        /// target이 0 이하면 모니터 주사율(최소 60)을 사용한다.
        /// </summary>
        public static void ApplyFrameRateLimit(int target = 0)
        {
            if (QualitySettings.vSyncCount != 0)
            {
                Application.targetFrameRate = -1; // VSync가 프레임률을 결정
                return;
            }

            int refresh = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = target > 0 ? target : Mathf.Max(60, refresh);
        }
    }
}
