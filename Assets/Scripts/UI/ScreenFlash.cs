using UnityEngine;
using UnityEngine.UI;

namespace Chopper
{
    /// <summary>화면 전체를 잠깐 단색(흰색)으로 깜박이게 한다. 헬리콥터/보스 대폭발 시 사용.</summary>
    public sealed class ScreenFlash : MonoBehaviour
    {
        [SerializeField] private Graphic overlay;

        [Header("Boss Big Explosion")]
        [SerializeField] private Color bossFlashColor = Color.white;
        [SerializeField, Min(0f)] private float bossFlashHold = 0.06f;
        [SerializeField, Min(0f)] private float bossFlashFade = 0.4f;

        private Color color = Color.white;
        private float hold;
        private float fade;
        private float timer;
        private bool active;

        private void Awake()
        {
            if (overlay == null) overlay = GetComponent<Graphic>();
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ScreenFlashEvent>(OnScreenFlash);
            EventBus.Subscribe<BossBigExplosionEvent>(OnBossBigExplosion);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ScreenFlashEvent>(OnScreenFlash);
            EventBus.Unsubscribe<BossBigExplosionEvent>(OnBossBigExplosion);
        }

        private void OnScreenFlash(ScreenFlashEvent evt)
        {
            Flash(evt.color, evt.holdTime, evt.fadeTime);
        }

        private void OnBossBigExplosion(BossBigExplosionEvent evt)
        {
            Flash(bossFlashColor, bossFlashHold, bossFlashFade);
        }

        public void Flash(Color flashColor, float holdTime, float fadeTime)
        {
            color = flashColor;
            hold = Mathf.Max(0f, holdTime);
            fade = Mathf.Max(0.01f, fadeTime);
            timer = 0f;
            active = true;
            SetAlpha(1f);
        }

        private void Update()
        {
            if (!active) return;

            timer += Time.unscaledDeltaTime;
            if (timer <= hold)
            {
                SetAlpha(1f);
                return;
            }

            float k = (timer - hold) / fade;
            if (k >= 1f)
            {
                active = false;
                SetAlpha(0f);
            }
            else
            {
                SetAlpha(1f - k);
            }
        }

        private void SetAlpha(float alpha)
        {
            if (overlay == null) return;
            Color c = color;
            c.a = color.a * alpha;
            overlay.color = c;
            overlay.enabled = c.a > 0.001f;
        }
    }
}
