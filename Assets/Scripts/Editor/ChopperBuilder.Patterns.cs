using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chopper.EditorTools
{
    public static partial class ChopperBuilder
    {
        /// <summary>보스 패턴 에셋 폴더. 패턴 에디터(Boss Pattern Editor)도 이 폴더를 기본 저장 위치로 쓴다.</summary>
        public const string BossPatternDir = DataDir + "BossPatterns/";

        /// <summary>
        /// 보스 프리팹에 넣을 행동 패턴 목록.
        /// 패턴 에셋과 그 순서는 패턴 에디터에서 만들고 고치는 데이터이므로 빌더가 덮어쓰지 않는다:
        /// 보스 프리팹에 이미 패턴 목록이 있으면 그대로 돌려주고, 비어 있을 때만 기본 5개 패턴을 쓴다.
        /// </summary>
        private static BossPattern[] ResolveBossPatterns()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + PfBoss);
            if (existing != null && existing.TryGetComponent(out Boss boss))
            {
                var kept = new List<BossPattern>();
                foreach (BossPattern pattern in boss.Patterns)
                {
                    if (pattern != null) kept.Add(pattern);
                }
                if (kept.Count > 0) return kept.ToArray();
            }

            return BuildDefaultBossPatterns();
        }

        /// <summary>
        /// 기본 5개 패턴 에셋. 같은 이름의 에셋이 이미 있으면 그대로 쓰고(덮어쓰지 않음), 없는 것만 새로 만든다.
        /// </summary>
        public static BossPattern[] BuildDefaultBossPatterns()
        {
            var bullet = Prefab<Bullet>(PfEnemyBullet);

            return new[]
            {
                // 1) 상하로 크게 이동하면서 플레이어 방향으로 5발 부채꼴 발사
                EnsurePattern("BossPattern_1_Fan", pattern =>
                {
                    const float period = 2.6f;        // 상하 왕복 1회에 걸리는 시간(초)
                    const float amplitude = 1.7f;     // 상하 이동 폭(유닛)
                    const float fireInterval = 0.75f; // 발사 간격(초)

                    pattern.duration = period * 2f;   // 2회 왕복
                    pattern.boosters = BoosterLevel.Turbo;
                    pattern.bodyPath = new PatternPath
                    {
                        shape = PathShape.Line,
                        start = new Vector2(0f, -amplitude),
                        end = new Vector2(0f, amplitude),
                        travelTime = period * 0.5f,
                        timeOffset = period * 0.25f,  // 한가운데(기준 위치)에서 위쪽으로 출발
                        repeat = PathRepeat.PingPong,
                        ease = PathEase.Smooth,
                    };

                    PatternEmitter emitter = NewEmitter("Aimed Fan", new Color(1f, 0.6f, 0.2f), bullet, pattern.duration);
                    emitter.fireRate = 1f / fireInterval;
                    emitter.firstShotDelay = fireInterval * 0.5f;
                    emitter.aim = PatternAim.AtPlayer;
                    emitter.angle = 0f;
                    emitter.bulletsPerShot = 5;
                    emitter.layout = ShotLayout.Fan;
                    emitter.spreadStep = 3.4f;        // 화면 절반쯤에서 이웃한 총알이 살짝 겹치는 간격
                    pattern.emitters.Add(emitter);
                }),

                // 2) 위아래로 빠르게 흩뿌리는 연발, 4회 왕복
                EnsurePattern("BossPattern_2_Sweep", pattern =>
                {
                    const float sweepPeriod = 0.4413f;  // 위아래 왕복 1회에 걸리는 시간(초)
                    const float fireInterval = 0.0147f; // 연사 간격(초)

                    pattern.duration = sweepPeriod * 4f;

                    PatternEmitter emitter = NewEmitter("Sweep", new Color(1f, 0.4f, 0.4f), bullet, pattern.duration);
                    emitter.fireRate = 1f / fireInterval;
                    emitter.firstShotDelay = fireInterval;
                    emitter.angle = 180f;
                    emitter.sweepAngle = -55f;          // 위쪽부터 꺾는다
                    emitter.sweepPeriod = sweepPeriod;
                    pattern.emitters.Add(emitter);
                }),

                // 3) 12시 방향부터 시계방향으로 3바퀴 회전하며 연발
                EnsurePattern("BossPattern_3_Spiral", pattern =>
                {
                    const float revolutionTime = 1.11f; // 1회전에 걸리는 시간(초)
                    const float fireInterval = 0.029f;  // 연사 간격(초)

                    pattern.duration = revolutionTime * 3f;

                    PatternEmitter emitter = NewEmitter("Spiral", new Color(0.4f, 0.8f, 1f), bullet, pattern.duration);
                    emitter.fireRate = 1f / fireInterval;
                    emitter.angle = 90f;
                    emitter.rotationSpeed = -360f / revolutionTime;
                    pattern.emitters.Add(emitter);
                }),

                // 4) 좌우로 이동하면서 전방위 동시 단발 발사 3회
                EnsurePattern("BossPattern_4_Ring", pattern =>
                {
                    const int volleys = 3;
                    const int bulletsPerVolley = 24;
                    const float volleyInterval = 0.8f;  // 단발 발사 간격(초)
                    const float leadTime = 0.4f;        // 첫 발사 전 대기(초)
                    const float tailTime = 0.6f;        // 마지막 발사 후 대기(초)
                    const float moveAmplitude = 1.2f;   // 좌우 이동 폭(유닛)
                    const float movePeriod = 1.6f;      // 좌우 왕복 1회에 걸리는 시간(초)

                    float lastVolley = leadTime + (volleys - 1) * volleyInterval;
                    pattern.duration = lastVolley + tailTime;
                    pattern.boosters = BoosterLevel.Turbo;
                    pattern.bodyPath = new PatternPath
                    {
                        shape = PathShape.Line,
                        start = Vector2.zero,
                        end = new Vector2(-moveAmplitude, 0f),  // 기준 위치에서 왼쪽으로 나갔다가 돌아온다
                        travelTime = movePeriod * 0.5f,
                        repeat = PathRepeat.PingPong,
                        ease = PathEase.Smooth,
                    };

                    PatternEmitter emitter = NewEmitter("Ring Volley", new Color(0.6f, 1f, 0.4f), bullet, pattern.duration);
                    emitter.activeRanges.Clear();
                    emitter.activeRanges.Add(new PatternTimeRange(leadTime, lastVolley + volleyInterval * 0.25f));
                    emitter.fireRate = 1f / volleyInterval;
                    emitter.angle = 0f;
                    // 발사마다 총알 사이 간격의 절반만큼 어긋나게 한다
                    emitter.rotationSpeed = 360f / bulletsPerVolley * 0.5f / volleyInterval;
                    emitter.bulletsPerShot = bulletsPerVolley;
                    emitter.layout = ShotLayout.Ring;
                    pattern.emitters.Add(emitter);
                }),

                // 5) 45도 간격 8방향 연발, 발사 방향 전체가 시계 반대 방향으로 회전
                EnsurePattern("BossPattern_5_Rotary", pattern =>
                {
                    const float fireInterval = 0.0517f; // 연사 간격(초)

                    pattern.duration = 8f;
                    pattern.loopSound = SoundId.PortalIdle;

                    PatternEmitter emitter = NewEmitter("Rotary", new Color(0.85f, 0.55f, 1f), bullet, pattern.duration);
                    emitter.fireRate = 1f / fireInterval;
                    emitter.angle = 0f;
                    emitter.rotationSpeed = 47.52f;
                    emitter.bulletsPerShot = 8;
                    emitter.layout = ShotLayout.Ring;
                    pattern.emitters.Add(emitter);
                }),
            };
        }

        /// <summary>보스의 눈에 고정되어 패턴 전체 시간 동안 켜져 있는 발사 지점.</summary>
        private static PatternEmitter NewEmitter(string name, Color color, Bullet bullet, float duration)
        {
            var emitter = new PatternEmitter
            {
                name = name,
                color = color,
                bulletPrefab = bullet,
                bulletSpeed = EnemyBulletSpeed,
            };
            emitter.activeRanges.Add(new PatternTimeRange(0f, duration));
            return emitter;
        }

        private static BossPattern EnsurePattern(string fileName, Action<BossPattern> configure)
        {
            string path = BossPatternDir + fileName + ".asset";

            var pattern = AssetDatabase.LoadAssetAtPath<BossPattern>(path);
            if (pattern != null) return pattern; // 이미 있는 패턴은 덮어쓰지 않는다

            EnsureFolder(BossPatternDir);
            pattern = ScriptableObject.CreateInstance<BossPattern>();
            configure(pattern);
            pattern.Sanitize();
            AssetDatabase.CreateAsset(pattern, path);
            return pattern;
        }
    }
}
