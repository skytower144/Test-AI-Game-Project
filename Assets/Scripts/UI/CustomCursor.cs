using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// mouse-pointer.png를 사용하는 커서. 시스템 커서는 숨기고 UI 이미지를 마우스 위치에 따라 움직인다.
    /// (시스템 커서는 크기 조절이 불가능하므로 UI로 구현해 입력창 높이 기준 크기를 맞춘다.)
    /// 이 오브젝트가 활성화되어 있는 동안에만 커서가 보인다.
    /// </summary>
    public sealed class CustomCursor : MonoBehaviour
    {
        [SerializeField] private RectTransform cursor;

        private void Awake()
        {
            if (cursor == null) cursor = transform as RectTransform;
        }

        private void OnEnable()
        {
            Cursor.visible = false;
            Follow();
        }

        private void LateUpdate()
        {
            if (Cursor.visible) Cursor.visible = false;
            Follow();
        }

        private void Follow()
        {
            // Screen Space - Overlay 캔버스에서는 월드 좌표 = 화면 픽셀 좌표
            if (cursor != null && GameInput.TryGetPointer(out Vector2 position))
                cursor.position = new Vector3(position.x, position.y, 0f);
        }
    }
}
