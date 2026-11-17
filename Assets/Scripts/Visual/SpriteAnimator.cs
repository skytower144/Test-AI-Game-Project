using System;
using UnityEngine;

namespace Chopper
{
    public enum AnimationEndAction
    {
        HoldLastFrame = 0,
        Hide = 1,
        ReleaseToPool = 2,
    }

    /// <summary>
    /// 스프라이트 배열을 순서대로 넘기는 경량 프레임 애니메이터.
    /// 풀링되는 총알/이펙트가 많아 Animator 대신 사용한다.
    /// </summary>
    public sealed class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField, Min(0.01f)] private float framesPerSecond = 12f;
        [SerializeField] private bool loop = true;
        [Tooltip("끝 프레임에서 역방향으로 되돌아가며 반복")]
        [SerializeField] private bool pingPong;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool randomStartFrame;
        [Tooltip("반복하지 않는 애니메이션이 끝났을 때의 동작")]
        [SerializeField] private AnimationEndAction endAction = AnimationEndAction.HoldLastFrame;

        private float frameTimer;
        private int frameIndex;
        private int direction = 1;
        private bool playing;

        public bool IsPlaying => playing;
        public SpriteRenderer Target => target;
        public Sprite[] Frames => frames;
        public float FramesPerSecond { get => framesPerSecond; set => framesPerSecond = Mathf.Max(0.01f, value); }

        /// <summary>반복하지 않는 애니메이션의 전체 재생 시간(초).</summary>
        public float Duration => frames != null && frames.Length > 0 ? frames.Length / framesPerSecond : 0f;

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (playOnEnable) Play();
        }

        /// <summary>현재 프레임 세트를 처음부터 재생한다.</summary>
        public void Play()
        {
            if (target == null || frames == null || frames.Length == 0)
            {
                playing = false;
                return;
            }

            frameTimer = 0f;
            direction = 1;
            frameIndex = randomStartFrame ? UnityEngine.Random.Range(0, frames.Length) : 0;
            target.enabled = true;
            target.sprite = frames[frameIndex];
            playing = true;
        }

        /// <summary>프레임 세트를 교체하고 재생한다.</summary>
        public void Play(Sprite[] newFrames, float fps, bool looping, bool pingPongLoop = false)
        {
            frames = newFrames ?? Array.Empty<Sprite>();
            framesPerSecond = Mathf.Max(0.01f, fps);
            loop = looping;
            pingPong = pingPongLoop;
            Play();
        }

        public void Stop()
        {
            playing = false;
        }

        private void Update()
        {
            if (!playing) return;

            frameTimer += Time.deltaTime * framesPerSecond;
            while (frameTimer >= 1f && playing)
            {
                frameTimer -= 1f;
                Advance();
            }
        }

        private void Advance()
        {
            int next = frameIndex + direction;

            if (next >= frames.Length || next < 0)
            {
                if (!loop)
                {
                    Finish();
                    return;
                }

                if (pingPong && frames.Length > 1)
                {
                    direction = -direction;
                    next = frameIndex + direction;
                }
                else
                {
                    next = 0;
                }
            }

            frameIndex = next;
            target.sprite = frames[frameIndex];
        }

        private void Finish()
        {
            playing = false;

            switch (endAction)
            {
                case AnimationEndAction.Hide:
                    target.enabled = false;
                    break;
                case AnimationEndAction.ReleaseToPool:
                    PoolManager.Despawn(gameObject);
                    break;
            }
        }
    }
}
