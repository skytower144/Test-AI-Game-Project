using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    public enum SoundId
    {
        None = 0,

        // 타이틀
        TitleMusic = 10,
        KeyInput = 11,
        EraseInput = 12,

        // 인게임 배경음
        GameMusic = 20,
        RainThunder = 21,
        Blizzard = 22,

        // 플레이어
        Gun = 30,               // 첫 번째 총알타입 발사
        Gun2 = 31,              // 두 번째 총알타입 발사
        GunLayer = 32,          // Gun3: 발사에 공통으로 얹는 소리
        Chopper = 33,           // 생존 중 Loop
        PlayerHurt = 34,        // Hurt2: 피격
        Revive = 35,            // 부활 및 RETRY
        Ricochet = 36,          // 총알 충돌
        BulletHit = 37,         // 총알 충돌
        ImminentExplosion = 38, // 체력 0 도달
        Charge = 39,            // 체력 0 도달
        SmallExplosion = 40,    // Hurt: 소규모 폭발
        Mayday = 41,            // 헬리콥터 대폭발
        BombExplosion = 42,     // 헬리콥터 대폭발
        BombExplosion2 = 43,    // 헬리콥터 대폭발

        // 아이템
        Reload = 50,            // 라이플 탄창
        Blank = 51,             // 샷건 탄창
        Blank2 = 52,
        Blank3 = 53,
        IronHit = 54,           // IronHit2: 구급상자
        Health = 55,            // 구급상자

        // 몬스터
        BoxExplosion = 60,      // Wasp, Hornet 처치

        // 보스
        BossArrival = 70,
        BossSlowArrival = 71,
        BossEngine = 72,        // 생존 중 Loop
        BossBullet = 73,
        PortalIdle = 74,        // 패턴 진행 중 Loop(패턴 에셋의 Loop Sound로 지정)
        Explosion4 = 75,        // 대폭발 1회
        Explosion2 = 76,        // 대형 폭발 이펙트마다
        Explosion3 = 77,        // 대형 폭발 이펙트마다
    }

    /// <summary>동시 재생 한도를 넘겼을 때의 처리 방식.</summary>
    public enum SoundOverflowPolicy
    {
        /// <summary>새 재생 요청을 무시한다.</summary>
        SkipNew = 0,
        /// <summary>가장 오래된 재생을 끊고 새로 재생한다.</summary>
        StealOldest = 1,
    }

    [Serializable]
    public class SoundDefinition
    {
        public SoundId id = SoundId.None;
        public AudioClip clip;
        [Tooltip("볼륨. 효과음은 1을 넘기면 같은 소리를 여러 보이스로 겹쳐 재생해 그만큼 키운다(루프는 1까지). " +
                 "여러 소리가 겹쳐 출력 한계를 넘으면 소리가 눌리거나 찌그러질 수 있다")]
        [Range(0f, 3f)] public float volume = 1f;
        [Tooltip("재생할 때마다 이 범위에서 피치를 무작위로 선택")]
        public Vector2 pitchRange = Vector2.one;

        [Header("중복재생 정책")]
        [Tooltip("이 소리를 동시에 재생할 수 있는 최대 개수")]
        [Min(1)] public int maxVoices = 2;
        [Tooltip("최대 개수를 넘겼을 때의 처리")]
        public SoundOverflowPolicy overflow = SoundOverflowPolicy.StealOldest;
        [Tooltip("같은 소리를 다시 재생하기까지의 최소 간격(초)")]
        [Min(0f)] public float minInterval;

        [Header("타이밍")]
        [Tooltip("효과음 재생 시작 위치(초). 클립 앞쪽 무음을 건너뛴다")]
        [Min(0f)] public float startOffset;

        [Header("루프 전용")]
        [Tooltip("루프 시작 지점(초). 클립 앞쪽 무음을 제외한다")]
        [Min(0f)] public float loopStartTrim;
        [Tooltip("클립 끝에서 제외할 길이(초). 클립 뒤쪽 무음을 제외한다")]
        [Min(0f)] public float loopEndTrim;
        [Tooltip("루프 이음매의 교차 페이드 길이(초). 0이면 클립 전체를 그대로 반복")]
        [Min(0f)] public float loopCrossfade;
    }

    /// <summary>모든 사운드의 클립/볼륨/중복재생 정책을 모아 둔 에셋.</summary>
    [CreateAssetMenu(menuName = "Chopper/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [SerializeField] private List<SoundDefinition> sounds = new List<SoundDefinition>();

        private Dictionary<SoundId, SoundDefinition> lookup;

        public IReadOnlyList<SoundDefinition> Sounds => sounds;

        private void OnEnable()
        {
            lookup = null;
        }

        private void OnValidate()
        {
            lookup = null;
        }

        public bool TryGet(SoundId id, out SoundDefinition definition)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<SoundId, SoundDefinition>(sounds.Count);
                for (int i = 0; i < sounds.Count; i++)
                {
                    SoundDefinition sound = sounds[i];
                    if (sound != null && sound.id != SoundId.None) lookup[sound.id] = sound;
                }
            }
            return lookup.TryGetValue(id, out definition);
        }
    }
}
