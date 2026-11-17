using UnityEngine;
using UnityEngine.InputSystem;

namespace Chopper
{
    /// <summary>Input System 키보드 입력을 게임 동작 단위로 읽는다.</summary>
    public static class GameInput
    {
        /// <summary>방향키(보조: WASD) 이동 입력. 대각선은 길이 1로 정규화.</summary>
        public static Vector2 Move
        {
            get
            {
                Keyboard k = Keyboard.current;
                if (k == null) return Vector2.zero;

                float x = 0f, y = 0f;
                if (k.leftArrowKey.isPressed || k.aKey.isPressed) x -= 1f;
                if (k.rightArrowKey.isPressed || k.dKey.isPressed) x += 1f;
                if (k.downArrowKey.isPressed || k.sKey.isPressed) y -= 1f;
                if (k.upArrowKey.isPressed || k.wKey.isPressed) y += 1f;

                var move = new Vector2(x, y);
                return move.sqrMagnitude > 1f ? move.normalized : move;
            }
        }

        /// <summary>발사키(Space, 보조: Z) 홀드 여부.</summary>
        public static bool FireHeld
        {
            get
            {
                Keyboard k = Keyboard.current;
                return k != null && (k.spaceKey.isPressed || k.zKey.isPressed);
            }
        }

        /// <summary>부활키(R)가 이번 프레임에 눌렸는지.</summary>
        public static bool RevivePressed
        {
            get
            {
                Keyboard k = Keyboard.current;
                return k != null && k.rKey.wasPressedThisFrame;
            }
        }

        /// <summary>마우스 포인터의 화면 좌표. 마우스가 없으면 false.</summary>
        public static bool TryGetPointer(out Vector2 screenPosition)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                screenPosition = default;
                return false;
            }
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        /// <summary>마우스 왼쪽 버튼이 이번 프레임에 눌렸는지.</summary>
        public static bool PointerPressed
        {
            get
            {
                Mouse mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
            }
        }
    }
}
