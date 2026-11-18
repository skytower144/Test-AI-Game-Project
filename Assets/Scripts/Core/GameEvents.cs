using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    public enum EnemyType { Wasp = 0, Hornet = 1, Boss = 2 }
    public enum ItemType { RifleAmmo = 0, ShotgunAmmo = 1, Medkit = 2 }
    public enum BulletType { Primary = 0, Rifle = 1 }

    // ───────────── 게임 흐름 ─────────────

    /// <summary>인게임 시작(씬 로드 직후 또는 RETRY 직후). RETRY로 시작했는지 여부 포함.</summary>
    public struct GameStartedEvent : IGameEvent { public bool isRetry; }

    /// <summary>
    /// RETRY: 씬을 다시 로드하지 않고 인게임을 처음 상태로 되돌리는 순간.
    /// 풀링된 오브젝트는 이미 모두 반납된 상태이며, 각 시스템은 이 이벤트를 받아 자신의 상태를 초기화한다.
    /// 직후에 GameStartedEvent(isRetry = true)가 발행된다.
    /// </summary>
    public struct GameResetEvent : IGameEvent { }

    public struct ScoreChangedEvent : IGameEvent { public int score; public int delta; }

    public struct LivesChangedEvent : IGameEvent { public int lives; public int maxLives; }

    /// <summary>목숨이 0인 상태로 사망 연출이 끝나고 GAME OVER가 표시되는 순간.</summary>
    public struct GameOverEvent : IGameEvent { }

    /// <summary>결과 화면이 표시되는 순간.</summary>
    public struct ResultsShownEvent : IGameEvent
    {
        public List<ScoreEntry> entries; // 1~5위
        public int playerRank;           // 0부터 시작, 순위권 밖이면 -1
        public int score;
    }

    /// <summary>화면 전체 플래시 요청.</summary>
    public struct ScreenFlashEvent : IGameEvent { public Color color; public float holdTime; public float fadeTime; }

    // ───────────── 플레이어 ─────────────

    public struct PlayerHealthChangedEvent : IGameEvent { public int health; public int maxHealth; }

    /// <summary>총알 1발 발사. interval은 현재 발사 간격(초).</summary>
    public struct PlayerFiredEvent : IGameEvent { public BulletType bulletType; public float interval; }

    /// <summary>피격(체력 감소) 순간.</summary>
    public struct PlayerDamagedEvent : IGameEvent { public int health; public Vector2 position; }

    /// <summary>체력이 0에 도달한 순간(사망 연출 시작).</summary>
    public struct PlayerDiedEvent : IGameEvent { public Vector2 position; }

    /// <summary>헬리콥터 대폭발 순간.</summary>
    public struct PlayerExplodedEvent : IGameEvent { public Vector2 position; }

    /// <summary>헬리콥터 대폭발 애니메이션이 완전히 끝난 순간.</summary>
    public struct PlayerDeathSequenceFinishedEvent : IGameEvent { }

    public struct PlayerRevivedEvent : IGameEvent { public Vector2 position; }

    /// <summary>플레이어 총알이 적에게 충돌한 순간.</summary>
    public struct BulletImpactEvent : IGameEvent { public Vector2 position; }

    /// <summary>소규모 폭발 1회(헬리콥터/보스 사망 연출).</summary>
    public struct SmallExplosionEvent : IGameEvent { public Vector2 position; }

    // ───────────── 아이템 / 몬스터 ─────────────

    public struct ItemPickedEvent : IGameEvent { public ItemType type; public Vector2 position; }

    public struct EnemyKilledEvent : IGameEvent
    {
        public EnemyType type;
        public Vector2 position;
        public int score;
    }

    // ───────────── 보스 ─────────────

    /// <summary>보스 등장 순간(화면을 가로지르는 비행 시작).</summary>
    public struct BossSpawnedEvent : IGameEvent { }

    /// <summary>화면 오른쪽 끝에서 서서히 모습을 드러내기 시작하는 순간.</summary>
    public struct BossRevealStartedEvent : IGameEvent { }

    public struct BossFiredEvent : IGameEvent { }

    /// <summary>행동 패턴 시작. patternIndex는 0~4.</summary>
    public struct BossPatternStartedEvent : IGameEvent { public int patternIndex; }

    public struct BossPatternEndedEvent : IGameEvent { public int patternIndex; }

    /// <summary>보스 체력이 0이 된 순간(처치).</summary>
    public struct BossDefeatedEvent : IGameEvent { public Vector2 position; public int score; }

    /// <summary>보스 대폭발 순간.</summary>
    public struct BossBigExplosionEvent : IGameEvent { public Vector2 position; }

    /// <summary>보스 대형 폭발 이펙트 1개 생성.</summary>
    public struct BossBlastEvent : IGameEvent { public Vector2 position; public int index; }

    /// <summary>보스 오브젝트가 완전히 제거된 순간.</summary>
    public struct BossDestroyedEvent : IGameEvent { }
}
