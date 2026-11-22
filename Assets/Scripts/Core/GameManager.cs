using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    /// <summary>
    /// 인게임 진행 관리: 점수, 목숨, 게임오버 → 결과 화면 흐름, RETRY/QUIT.
    /// 플레이어 사망/부활, 몬스터 처치 등은 모두 이벤트 버스로 전달받는다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        public enum GameState { Playing, GameOverPending, GameOver, Results }

        [Header("Rules")]
        [Tooltip("시작 목숨 수(최대 목숨)")]
        [SerializeField, Min(1)] private int maxLives = 3;

        [Header("Game Over Flow")]
        [Tooltip("헬리콥터 대폭발이 끝난 뒤 GAME OVER 표시까지의 시간(초)")]
        [SerializeField, Min(0f)] private float gameOverDelay = 1f;
        [Tooltip("GAME OVER 표시 후 결과 화면까지의 시간(초)")]
        [SerializeField, Min(0f)] private float resultsDelay = 2f;

        [Header("Performance")]
        [Tooltip("VSync가 꺼져 있을 때 적용할 목표 프레임률(0 = 모니터 주사율)")]
        [SerializeField, Min(0)] private int targetFrameRate;

        private static GameManager s_Instance;

        private int score;
        private int lives;
        private GameState state = GameState.Playing;
        private bool awaitingRevive;
        private bool unranked;

        public static int Score => s_Instance != null ? s_Instance.score : 0;
        public static int Lives => s_Instance != null ? s_Instance.lives : 0;
        public static GameState State => s_Instance != null ? s_Instance.state : GameState.Playing;

        /// <summary>플레이어 조작(이동/발사)이 허용되는 상태인지.</summary>
        public static bool ControlsEnabled => s_Instance == null || s_Instance.state == GameState.Playing;

        /// <summary>이번 판의 점수를 리더보드에 기록하지 않는지(패턴 테스트를 한 판).</summary>
        public static bool IsRunUnranked => s_Instance != null && s_Instance.unranked;

        /// <summary>
        /// 이번 판의 점수를 리더보드에 기록하지 않게 한다. 보스를 마음대로 불러내는 패턴 테스트처럼
        /// 정상적인 진행이 아닌 기능을 쓴 판에 쓴다. RETRY로 새 판을 시작하면 해제된다.
        /// </summary>
        public static void MarkRunUnranked()
        {
            if (s_Instance != null) s_Instance.unranked = true;
        }

        /// <summary>사망 연출이 끝났고 목숨이 남아 R키로 부활할 수 있는 상태인지.</summary>
        public static bool CanRevive => s_Instance != null && s_Instance.state == GameState.Playing &&
                                        s_Instance.awaitingRevive && s_Instance.lives > 0;

        private void Awake()
        {
            s_Instance = this;
            lives = maxLives;
            score = 0;
            state = GameState.Playing;

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false; // 인게임 도중 커서 숨김

            GameSession.ApplyFrameRateLimit(targetFrameRate);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Subscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Subscribe<PlayerDeathSequenceFinishedEvent>(OnPlayerDeathSequenceFinished);
            EventBus.Subscribe<PlayerRevivedEvent>(OnPlayerRevived);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Unsubscribe<PlayerDeathSequenceFinishedEvent>(OnPlayerDeathSequenceFinished);
            EventBus.Unsubscribe<PlayerRevivedEvent>(OnPlayerRevived);
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            s_Instance = null;
        }

        private void Start()
        {
            AnnounceStart(false);
        }

        private void AnnounceStart(bool isRetry)
        {
            EventBus.Publish(new ScoreChangedEvent { score = score, delta = 0 });
            EventBus.Publish(new LivesChangedEvent { lives = lives, maxLives = maxLives });
            EventBus.Publish(new GameStartedEvent { isRetry = isRetry });
        }

        // 모든 Update(트랜스폼 이동)가 끝난 뒤 물리 월드를 한 번 동기화한다.
        // 이후 LateUpdate에서 수행되는 총알 스윕/플레이어 접촉 질의가 현재 프레임 위치를 기준으로 판정된다.
        private void LateUpdate()
        {
            Physics2D.SyncTransforms();
        }

        // ───────────── 이벤트 처리 ─────────────

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            AddScore(evt.score);
        }

        private void OnBossDefeated(BossDefeatedEvent evt)
        {
            AddScore(evt.score);
        }

        private void AddScore(int amount)
        {
            if (amount == 0) return;
            score = Mathf.Max(0, score + amount);
            EventBus.Publish(new ScoreChangedEvent { score = score, delta = amount });
        }

        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            // 체력이 0이면 목숨 1개 차감
            awaitingRevive = false;
            lives = Mathf.Max(0, lives - 1);
            EventBus.Publish(new LivesChangedEvent { lives = lives, maxLives = maxLives });
        }

        private void OnPlayerDeathSequenceFinished(PlayerDeathSequenceFinishedEvent evt)
        {
            if (state != GameState.Playing) return;

            if (lives > 0)
            {
                awaitingRevive = true; // R키 입력 대기
            }
            else
            {
                state = GameState.GameOverPending;
                StartCoroutine(GameOverRoutine());
            }
        }

        private void OnPlayerRevived(PlayerRevivedEvent evt)
        {
            awaitingRevive = false;
        }

        private IEnumerator GameOverRoutine()
        {
            yield return new WaitForSeconds(gameOverDelay);

            state = GameState.GameOver;
            EventBus.Publish(new GameOverEvent());

            yield return new WaitForSeconds(resultsDelay);

            // 기록하지 않는 판(패턴 테스트)은 기존 순위만 보여 준다
            int rank = -1;
            List<ScoreEntry> entries = unranked
                ? Leaderboard.Load()
                : Leaderboard.Submit(GameSession.PlayerName, score, out rank);
            state = GameState.Results;
            EventBus.Publish(new ResultsShownEvent { entries = entries, playerRank = rank, score = score });
        }

        // ───────────── 결과 화면 동작 ─────────────

        /// <summary>타이틀을 거치지 않고, 씬을 다시 로드하지도 않고 인게임을 처음 상태로 되돌린다.</summary>
        public static void Retry()
        {
            if (s_Instance != null) s_Instance.Restart();
        }

        private void Restart()
        {
            StopAllCoroutines();

            score = 0;
            lives = maxLives;
            state = GameState.Playing;
            awaitingRevive = false;
            unranked = false;

            Time.timeScale = 1f;
            Cursor.visible = false;

            // 총알, 몬스터, 보스, 아이템, 이펙트, 잔해 등 풀링된 모든 오브젝트를 반납한 뒤
            // 플레이어/스포너/UI 등 씬에 상주하는 시스템이 각자 초기 상태로 돌아가도록 알린다.
            PoolManager.DespawnAll();
            EventBus.Publish(new GameResetEvent());

            AnnounceStart(true);
        }

        /// <summary>게임 종료.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
