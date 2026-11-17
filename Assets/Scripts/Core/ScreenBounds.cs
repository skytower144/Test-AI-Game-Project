using UnityEngine;

namespace Chopper
{
    /// <summary>메인 카메라가 비추는 월드 영역(z = 0 평면). 프레임당 한 번만 계산한다.</summary>
    public static class ScreenBounds
    {
        private static Camera s_Camera;
        private static int s_Frame = -1;
        private static float s_Left = -4.8f, s_Right = 4.8f, s_Bottom = -2.7f, s_Top = 2.7f;

        public static float Left { get { Refresh(); return s_Left; } }
        public static float Right { get { Refresh(); return s_Right; } }
        public static float Bottom { get { Refresh(); return s_Bottom; } }
        public static float Top { get { Refresh(); return s_Top; } }
        public static float Width { get { Refresh(); return s_Right - s_Left; } }
        public static float Height { get { Refresh(); return s_Top - s_Bottom; } }
        public static Vector2 Center { get { Refresh(); return new Vector2((s_Left + s_Right) * 0.5f, (s_Bottom + s_Top) * 0.5f); } }

        /// <summary>뷰포트 좌표(0~1)를 월드 좌표로 변환한다.</summary>
        public static Vector2 FromViewport(float x, float y)
        {
            Refresh();
            return new Vector2(Mathf.LerpUnclamped(s_Left, s_Right, x), Mathf.LerpUnclamped(s_Bottom, s_Top, y));
        }

        /// <summary>화면 밖으로 margin 이상 벗어났는지 여부.</summary>
        public static bool IsOutside(Vector2 point, float margin)
        {
            Refresh();
            return point.x < s_Left - margin || point.x > s_Right + margin ||
                   point.y < s_Bottom - margin || point.y > s_Top + margin;
        }

        public static bool Contains(Vector2 point, float inset = 0f)
        {
            Refresh();
            return point.x >= s_Left + inset && point.x <= s_Right - inset &&
                   point.y >= s_Bottom + inset && point.y <= s_Top - inset;
        }

        private static void Refresh()
        {
            if (s_Frame == Time.frameCount && s_Camera != null) return;
            s_Frame = Time.frameCount;

            if (s_Camera == null || !s_Camera.isActiveAndEnabled) s_Camera = Camera.main;
            if (s_Camera == null) return; // 카메라가 없으면 마지막 값을 유지

            Vector3 position = s_Camera.transform.position;
            float halfHeight;
            if (s_Camera.orthographic)
                halfHeight = s_Camera.orthographicSize;
            else
                halfHeight = Mathf.Abs(position.z) * Mathf.Tan(s_Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            float halfWidth = halfHeight * s_Camera.aspect;
            s_Left = position.x - halfWidth;
            s_Right = position.x + halfWidth;
            s_Bottom = position.y - halfHeight;
            s_Top = position.y + halfHeight;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            s_Camera = null;
            s_Frame = -1;
        }
    }
}
