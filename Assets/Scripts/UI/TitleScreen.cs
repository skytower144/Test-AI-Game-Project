using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Chopper
{
    /// <summary>
    /// 타이틀 화면: 유저네임 입력(영문 대문자/숫자), 플레이스홀더 표시, Enter로 인게임 전환.
    /// IME 상태와 무관하게 동작하도록 텍스트 이벤트 대신 물리 키 입력을 읽는다.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("입력값이 비어 있을 때 보이는 'INSERT NAME'")]
        [SerializeField] private TMP_Text placeholderText;
        [SerializeField] private RectTransform caret;
        [SerializeField] private Graphic fadeOverlay;

        [Header("Input")]
        [SerializeField, Range(1, 16)] private int maxNameLength = 10;
        [Tooltip("Backspace를 누르고 있을 때 연속 삭제가 시작되기까지의 시간(초)")]
        [SerializeField, Min(0f)] private float eraseRepeatDelay = 0.4f;
        [SerializeField, Min(0.01f)] private float eraseRepeatInterval = 0.06f;

        [Header("Visual")]
        [SerializeField, Range(0f, 1f)] private float placeholderAlpha = 0.3f;
        [SerializeField, Min(0.05f)] private float caretBlinkInterval = 0.4f;
        [Tooltip("이름 끝과 캐럿 사이 간격(px)")]
        [SerializeField] private float caretSpacing = 8f;

        [Header("Transition")]
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;
        [SerializeField, Min(0f)] private float musicFadeIn = 0.6f;

        private static readonly Key[] LetterKeys =
        {
            Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J, Key.K, Key.L, Key.M,
            Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T, Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z,
        };

        private static readonly Key[] DigitKeys =
        {
            Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        private static readonly Key[] NumpadKeys =
        {
            Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
            Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9,
        };

        private readonly StringBuilder buffer = new StringBuilder(16);
        private float eraseTimer;
        private float caretTimer;
        private float placeholderWarnTimer;
        private bool transitioning;
        private Graphic caretGraphic;

        private void Start()
        {
            Time.timeScale = 1f;
            GameSession.ApplyFrameRateLimit();
            if (caret != null) caretGraphic = caret.GetComponent<Graphic>();
            buffer.Clear();
            Refresh();
            SetOverlayAlpha(0f);
            AudioManager.PlayLoop(SoundId.TitleMusic, musicFadeIn);
        }

        private void Update()
        {
            if (transitioning) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                ReadCharacters(keyboard);
                ReadErase(keyboard);

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    if (buffer.Length > 0)
                    {
                        StartCoroutine(StartGameRoutine());
                        return;
                    }
                    placeholderWarnTimer = 0.6f; // 이름이 비어 있으면 플레이스홀더를 잠깐 강조
                }
            }

            Animate();
        }

        private void ReadCharacters(Keyboard keyboard)
        {
            for (int i = 0; i < LetterKeys.Length; i++)
            {
                KeyControl control = keyboard[LetterKeys[i]];
                if (control != null && control.wasPressedThisFrame)
                    Append(ResolveLetter(control, (char)('A' + i)));
            }

            for (int i = 0; i < DigitKeys.Length; i++)
            {
                KeyControl top = keyboard[DigitKeys[i]];
                KeyControl pad = keyboard[NumpadKeys[i]];
                if ((top != null && top.wasPressedThisFrame) || (pad != null && pad.wasPressedThisFrame))
                    Append((char)('0' + i));
            }
        }

        // 키보드 배열(AZERTY 등)에 맞는 글자를 쓰되, 영문자가 아니면 물리 키 기준 글자로 대체한다.
        private static char ResolveLetter(KeyControl control, char fallback)
        {
            string display = control.displayName;
            if (!string.IsNullOrEmpty(display) && display.Length == 1)
            {
                char c = char.ToUpperInvariant(display[0]);
                if (c >= 'A' && c <= 'Z') return c;
            }
            return fallback;
        }

        private void ReadErase(Keyboard keyboard)
        {
            KeyControl backspace = keyboard.backspaceKey;
            KeyControl delete = keyboard.deleteKey;
            bool pressed = backspace.wasPressedThisFrame || delete.wasPressedThisFrame;
            bool held = backspace.isPressed || delete.isPressed;

            if (pressed)
            {
                Erase();
                eraseTimer = eraseRepeatDelay;
            }
            else if (held)
            {
                eraseTimer -= Time.unscaledDeltaTime;
                if (eraseTimer <= 0f)
                {
                    Erase();
                    eraseTimer = eraseRepeatInterval;
                }
            }
        }

        private void Append(char c)
        {
            if (buffer.Length >= maxNameLength) return;
            buffer.Append(c);
            AudioManager.Play(SoundId.KeyInput);
            caretTimer = 0f;
            Refresh();
        }

        private void Erase()
        {
            if (buffer.Length == 0) return;
            buffer.Length -= 1;
            AudioManager.Play(SoundId.EraseInput);
            caretTimer = 0f;
            Refresh();
        }

        private void Refresh()
        {
            bool empty = buffer.Length == 0;
            if (nameText != null) nameText.text = buffer.ToString();
            if (placeholderText != null) placeholderText.gameObject.SetActive(empty); // 입력이 시작되면 숨김

            if (caret != null)
            {
                caret.gameObject.SetActive(!empty);
                if (!empty && nameText != null)
                {
                    nameText.ForceMeshUpdate();
                    float width = nameText.preferredWidth;
                    Vector2 position = caret.anchoredPosition;
                    position.x = width * 0.5f + caretSpacing;
                    caret.anchoredPosition = position;
                }
            }
        }

        private void Animate()
        {
            float dt = Time.unscaledDeltaTime;

            if (caret != null && caret.gameObject.activeSelf)
            {
                caretTimer += dt;
                if (caretGraphic != null)
                    caretGraphic.enabled = Mathf.Repeat(caretTimer, caretBlinkInterval * 2f) < caretBlinkInterval;
            }

            if (placeholderText != null && placeholderText.gameObject.activeSelf)
            {
                float alpha = placeholderAlpha;
                if (placeholderWarnTimer > 0f)
                {
                    placeholderWarnTimer -= dt;
                    // 빠르게 깜박여 입력이 필요함을 알린다
                    alpha = Mathf.Repeat(placeholderWarnTimer, 0.2f) < 0.1f ? 1f : placeholderAlpha;
                }
                Color color = placeholderText.color;
                if (!Mathf.Approximately(color.a, alpha))
                {
                    color.a = alpha;
                    placeholderText.color = color;
                }
            }
        }

        private IEnumerator StartGameRoutine()
        {
            transitioning = true;
            GameSession.PlayerName = buffer.ToString();
            GameSession.IsRetry = false;

            if (caret != null) caret.gameObject.SetActive(false);
            AudioManager.StopAllLoops(fadeOutDuration);

            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                SetOverlayAlpha(Mathf.Clamp01(t / fadeOutDuration));
                yield return null;
            }
            SetOverlayAlpha(1f);

            SceneManager.LoadScene(GameSession.GameSceneName);
        }

        private void SetOverlayAlpha(float alpha)
        {
            if (fadeOverlay == null) return;
            Color color = fadeOverlay.color;
            color.a = alpha;
            fadeOverlay.color = color;
            fadeOverlay.enabled = alpha > 0.001f;
        }
    }
}
