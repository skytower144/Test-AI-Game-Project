using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    /// <summary>발사 지점 위치의 기준.</summary>
    public enum PatternSpace
    {
        /// <summary>보스의 눈(총알이 나오는 곳) 기준. 보스가 움직이면 함께 움직인다.</summary>
        BossEye = 0,
        /// <summary>화면 중심 기준. 보스의 위치와 무관하다.</summary>
        Screen = 1,
    }

    public enum PathShape
    {
        /// <summary>움직이지 않는다.</summary>
        Fixed = 0,
        /// <summary>시작 위치에서 도착 위치까지 직선으로 이동한다.</summary>
        Line = 1,
        /// <summary>시작 위치에서 도착 위치까지 포물선으로 이동한다.</summary>
        Parabola = 2,
    }

    public enum PathRepeat
    {
        /// <summary>도착한 뒤 멈춘다.</summary>
        Once = 0,
        /// <summary>도착하면 시작 위치로 돌아가 다시 이동한다.</summary>
        Restart = 1,
        /// <summary>시작 위치와 도착 위치 사이를 왕복한다.</summary>
        PingPong = 2,
    }

    public enum PathEase
    {
        /// <summary>일정한 속도.</summary>
        Linear = 0,
        /// <summary>천천히 출발해 천천히 멈춘다.</summary>
        Smooth = 1,
        /// <summary>점점 빨라진다.</summary>
        Accelerate = 2,
        /// <summary>점점 느려진다.</summary>
        Decelerate = 3,
    }

    public enum PatternAim
    {
        /// <summary>정해진 각도로 발사한다.</summary>
        FixedAngle = 0,
        /// <summary>발사하는 순간의 플레이어 방향으로 발사한다.</summary>
        AtPlayer = 1,
    }

    public enum ShotLayout
    {
        /// <summary>발사 방향을 중심으로 일정한 각도 간격으로 펼친다.</summary>
        Fan = 0,
        /// <summary>360도를 같은 간격으로 나눠 전방위로 발사한다.</summary>
        Ring = 1,
    }

    /// <summary>
    /// 시작 위치에서 도착 위치까지의 이동 경로(직선/포물선)와 그 위를 움직이는 방식.
    /// 발사 지점의 이동과 보스 본체의 이동에 함께 쓴다.
    /// </summary>
    [Serializable]
    public sealed class PatternPath
    {
        public const float MinTravelTime = 0.01f;

        [Tooltip("이동 방식. Fixed = 움직이지 않음, Line = 직선 이동, Parabola = 포물선 이동")]
        public PathShape shape = PathShape.Fixed;
        [Tooltip("시작 위치(유닛). 움직이지 않을 때는 이 위치에 머문다")]
        public Vector2 start;
        [Tooltip("도착 위치(유닛)")]
        public Vector2 end;
        [Tooltip("포물선의 기울기: 직선 경로의 한가운데에서 옆으로 휘어 나가는 거리(유닛). " +
                 "0이면 직선과 같고, 클수록 가파르게 휜다. 부호를 바꾸면 반대쪽으로 휜다")]
        public float bend = 1f;
        [Tooltip("이동을 시작하는 시각(초, 패턴 시작 기준). 그 전에는 시작 위치에 머문다")]
        [Min(0f)] public float startTime;
        [Tooltip("시작 위치에서 도착 위치까지 가는 데 걸리는 시간(초)")]
        [Min(MinTravelTime)] public float travelTime = 1f;
        [Tooltip("Once = 도착한 뒤 멈춤, Restart = 시작 위치로 돌아가 다시 이동, PingPong = 왕복")]
        public PathRepeat repeat = PathRepeat.Once;
        [Tooltip("Linear = 일정한 속도, Smooth = 천천히 출발해 천천히 멈춤, Accelerate = 점점 빠르게, Decelerate = 점점 느리게")]
        public PathEase ease = PathEase.Linear;
        [Tooltip("경로를 이만큼(초) 이미 지나온 상태에서 출발한다. 왕복 경로를 한가운데에서 출발시킬 때 쓴다")]
        [Min(0f)] public float timeOffset;

        public bool IsMoving => shape != PathShape.Fixed;

        /// <summary>패턴 시각 time(초)에서의 위치.</summary>
        public Vector2 Evaluate(float time)
        {
            return shape == PathShape.Fixed ? start : PointAt(ProgressAt(time));
        }

        /// <summary>패턴 시각 time(초)에서 경로를 얼마나 지나왔는지(0 = 시작 위치, 1 = 도착 위치).</summary>
        public float ProgressAt(float time)
        {
            if (shape == PathShape.Fixed) return 0f;

            float legs = (Mathf.Max(0f, time - startTime) + Mathf.Max(0f, timeOffset)) / Mathf.Max(MinTravelTime, travelTime);
            float progress;
            switch (repeat)
            {
                case PathRepeat.Restart: progress = legs - Mathf.Floor(legs); break;
                case PathRepeat.PingPong: progress = Mathf.PingPong(legs, 1f); break;
                default: progress = Mathf.Clamp01(legs); break;
            }

            switch (ease)
            {
                case PathEase.Smooth: return 0.5f - 0.5f * Mathf.Cos(progress * Mathf.PI);
                case PathEase.Accelerate: return 1f - Mathf.Cos(progress * Mathf.PI * 0.5f);
                case PathEase.Decelerate: return Mathf.Sin(progress * Mathf.PI * 0.5f);
                default: return progress;
            }
        }

        /// <summary>경로 위의 한 점(progress: 0 = 시작 위치, 1 = 도착 위치).</summary>
        public Vector2 PointAt(float progress)
        {
            Vector2 point = Vector2.LerpUnclamped(start, end, progress);
            if (shape == PathShape.Parabola && bend != 0f)
                point += BendDirection * (4f * bend * progress * (1f - progress));
            return point;
        }

        /// <summary>포물선이 휘어 나가는 방향(진행 방향의 왼쪽). 시작과 도착이 같은 위치면 위쪽.</summary>
        public Vector2 BendDirection
        {
            get
            {
                Vector2 chord = end - start;
                return chord.sqrMagnitude > 1e-8f ? new Vector2(-chord.y, chord.x).normalized : Vector2.up;
            }
        }

        public void Sanitize()
        {
            startTime = Mathf.Max(0f, startTime);
            travelTime = Mathf.Max(MinTravelTime, travelTime);
            timeOffset = Mathf.Max(0f, timeOffset);
        }
    }

    /// <summary>타임라인에서 발사 지점이 켜져 있는 구간(초). 끝 시각은 구간에 포함되지 않는다.</summary>
    [Serializable]
    public struct PatternTimeRange
    {
        public float start;
        public float end;

        public PatternTimeRange(float start, float end)
        {
            this.start = start;
            this.end = end;
        }

        public bool Contains(float time)
        {
            return time >= start && time < end;
        }
    }

    /// <summary>패턴을 이루는 발사 지점 하나.</summary>
    [Serializable]
    public sealed class PatternEmitter
    {
        public const float MinFireRate = 0.01f;
        public const float MaxFireRate = 240f;
        public const int MaxBulletsPerShot = 128;

        public string name = "Emitter";
        [Tooltip("패턴 에디터에서 이 발사 지점을 구분하는 색")]
        public Color color = new Color(1f, 0.6f, 0.2f, 1f);

        [Header("Position / Path")]
        [Tooltip("위치의 기준. BossEye = 보스의 눈(보스가 움직이면 함께 움직임), Screen = 화면 중심")]
        public PatternSpace space = PatternSpace.BossEye;
        [Tooltip("발사 지점의 위치(시작 위치)와 이동 경로")]
        public PatternPath path = new PatternPath();

        [Header("Fire")]
        [Tooltip("발사 속도(초당 발사 횟수)")]
        [Range(MinFireRate, MaxFireRate)] public float fireRate = 4f;
        [Tooltip("활성 구간이 시작된 뒤 첫 발까지의 지연(초)")]
        [Min(0f)] public float firstShotDelay;
        [Tooltip("타임라인에서 이 발사 지점이 켜져 있는 구간(초). 구간 안에서만 발사한다")]
        public List<PatternTimeRange> activeRanges = new List<PatternTimeRange>();

        [Header("Direction")]
        [Tooltip("FixedAngle = 정해진 각도로 발사, AtPlayer = 발사하는 순간의 플레이어 방향으로 발사")]
        public PatternAim aim = PatternAim.FixedAngle;
        [Tooltip("발사 각도(도). 0 = 오른쪽, 90 = 위, 180 = 왼쪽, 270 = 아래. AtPlayer일 때는 조준 방향에 더하는 각도")]
        public float angle = 180f;
        [Tooltip("발사 방향이 도는 속도(도/초). 양수 = 시계 반대 방향, 음수 = 시계 방향. " +
                 "활성 구간이 시작될 때마다 처음 각도에서 다시 돈다")]
        public float rotationSpeed;
        [Tooltip("발사 방향을 양옆으로 흔드는 폭(도). 0이면 흔들지 않는다. 양수 = 시계 반대 방향부터, 음수 = 시계 방향부터 흔든다")]
        public float sweepAngle;
        [Tooltip("발사 방향을 한 번 흔들고 제자리로 돌아오는 데 걸리는 시간(초)")]
        [Min(0.01f)] public float sweepPeriod = 1f;

        [Header("Bullets Per Shot")]
        [Tooltip("한 번 발사할 때 나가는 총알 수")]
        [Range(1, MaxBulletsPerShot)] public int bulletsPerShot = 1;
        [Tooltip("Fan = 발사 방향을 중심으로 부채꼴, Ring = 360도 전방위")]
        public ShotLayout layout = ShotLayout.Fan;
        [Tooltip("부채꼴에서 이웃한 총알 사이의 각도(도)")]
        public float spreadStep = 10f;

        [Header("Bullet")]
        [Tooltip("총알 타입(Bullet 프리팹). 비워 두면 보스의 기본 총알을 쓴다")]
        public Bullet bulletPrefab;
        [Tooltip("총알 속도(유닛/초)")]
        [Min(0.1f)] public float bulletSpeed = 7.2f;

        /// <summary>발사 간격(초).</summary>
        public float FireInterval => 1f / Mathf.Clamp(fireRate, MinFireRate, MaxFireRate);

        public int BulletCount => Mathf.Clamp(bulletsPerShot, 1, MaxBulletsPerShot);

        /// <summary>활성 구간이 시작된 뒤 localTime초가 지났을 때의 발사 각도(도). 조준 모드에서는 조준 방향에 더할 각도.</summary>
        public float AngleAt(float localTime)
        {
            float result = angle + rotationSpeed * localTime;
            if (sweepAngle != 0f)
                result += sweepAngle * Mathf.Sin(localTime / Mathf.Max(0.01f, sweepPeriod) * Mathf.PI * 2f);
            return result;
        }

        /// <summary>한 번의 발사에서 index번째 총알의 각도(도).</summary>
        public float BulletAngle(float centerAngle, int index)
        {
            int count = BulletCount;
            return layout == ShotLayout.Ring
                ? centerAngle + index * (360f / count)
                : centerAngle + (index - (count - 1) * 0.5f) * spreadStep;
        }

        /// <summary>time(초)을 포함하는 활성 구간의 번호. 없으면 -1.</summary>
        public int FindRange(float time)
        {
            if (activeRanges == null) return -1;
            for (int i = 0; i < activeRanges.Count; i++)
            {
                if (activeRanges[i].Contains(time)) return i;
            }
            return -1;
        }

        public void Sanitize()
        {
            if (path == null) path = new PatternPath();
            path.Sanitize();

            fireRate = Mathf.Clamp(fireRate, MinFireRate, MaxFireRate);
            firstShotDelay = Mathf.Max(0f, firstShotDelay);
            sweepPeriod = Mathf.Max(0.01f, sweepPeriod);
            bulletsPerShot = Mathf.Clamp(bulletsPerShot, 1, MaxBulletsPerShot);
            bulletSpeed = Mathf.Max(0.1f, bulletSpeed);

            if (activeRanges == null) activeRanges = new List<PatternTimeRange>();
            for (int i = 0; i < activeRanges.Count; i++)
            {
                PatternTimeRange range = activeRanges[i];
                range.start = Mathf.Max(0f, range.start);
                range.end = Mathf.Max(range.start, range.end);
                activeRanges[i] = range;
            }
        }
    }

    /// <summary>발사 1회. 한 번의 발사에서 총알이 여러 발 나갈 수 있다(PatternEmitter.BulletAngle).</summary>
    public struct PatternShot
    {
        /// <summary>패턴 시작 기준 발사 시각(초).</summary>
        public float time;
        public int emitterIndex;
        /// <summary>발사 순간 발사 지점의 위치(발사 지점의 기준 좌표계).</summary>
        public Vector2 position;
        /// <summary>발사 각도(도). 조준 모드에서는 조준 방향에 더할 각도.</summary>
        public float angle;
    }

    /// <summary>
    /// 보스의 행동 패턴 1개. 발사 지점들의 위치/이동 경로/발사 속도/활성 구간/총알 타입과 패턴의 총 소요 시간을 담는다.
    /// 패턴은 시간에 대한 함수로 정의되어, 게임(Boss)과 패턴 에디터의 미리보기가 같은 계산(CollectShots)을 쓴다.
    /// 패턴 에셋은 Tools > Chopper > Boss Pattern Editor에서 만들고 고친다.
    /// </summary>
    [CreateAssetMenu(fileName = "BossPattern", menuName = "Chopper/Boss Pattern", order = 0)]
    public sealed class BossPattern : ScriptableObject
    {
        public const float MinDuration = 0.1f;

        /// <summary>활성 구간/패턴이 끝나는 시각에 걸친 발사를 걸러내는 여유(초).</summary>
        public const float EndEpsilon = 1e-4f;

        /// <summary>CollectShots 한 번에 돌려주는 발사 횟수의 한도(잘못된 값으로 에디터/게임이 멈추지 않도록).</summary>
        public const int MaxShotsPerQuery = 100000;

        [Tooltip("패턴의 총 소요 시간(초). 이 시간이 지나면 발사를 멈추고 다음 패턴까지 대기한다")]
        [Min(MinDuration)] public float duration = 4f;
        [Tooltip("패턴 진행 중 보스 부스터의 출력")]
        public BoosterLevel boosters = BoosterLevel.Normal;
        [Tooltip("패턴 진행 중 반복 재생할 소리. None = 없음")]
        public SoundId loopSound = SoundId.None;
        [Tooltip("패턴 진행 중 보스 본체의 이동. 위치는 전투 기준 위치로부터의 거리(유닛)")]
        public PatternPath bodyPath = new PatternPath();
        [Tooltip("발사 지점 목록")]
        public List<PatternEmitter> emitters = new List<PatternEmitter>();

        /// <summary>패턴 시각 time(초)에서 보스 본체가 전투 기준 위치로부터 떨어져 있어야 할 거리.</summary>
        public Vector2 BodyOffsetAt(float time)
        {
            return bodyPath != null ? bodyPath.Evaluate(time) : Vector2.zero;
        }

        /// <summary>
        /// fromTime 이상 toTime 미만 사이에 일어나는 모든 발사를 results에 추가한다.
        /// 상태를 갖지 않는 계산이라 임의의 시각으로 건너뛰거나 되돌려도 같은 결과가 나온다.
        /// emitterFilter가 0 이상이면 그 번호의 발사 지점만 계산한다.
        /// </summary>
        public void CollectShots(float fromTime, float toTime, List<PatternShot> results, int emitterFilter = -1)
        {
            if (emitters == null || results == null) return;

            double windowEnd = Math.Min((double)toTime, (double)duration - EndEpsilon);
            if (windowEnd <= fromTime) return;

            for (int e = 0; e < emitters.Count; e++)
            {
                if (emitterFilter >= 0 && e != emitterFilter) continue;

                PatternEmitter emitter = emitters[e];
                if (emitter == null || emitter.activeRanges == null || emitter.path == null) continue;

                double interval = 1.0 / Mathf.Clamp(emitter.fireRate, PatternEmitter.MinFireRate, PatternEmitter.MaxFireRate);

                for (int r = 0; r < emitter.activeRanges.Count; r++)
                {
                    PatternTimeRange range = emitter.activeRanges[r];
                    double first = (double)range.start + Mathf.Max(0f, emitter.firstShotDelay);
                    double limit = Math.Min(windowEnd, (double)range.end - EndEpsilon);
                    if (first >= limit || limit <= fromTime) continue;

                    // 구간 안의 k번째 발사 시각은 항상 first + k * interval로 계산한다(프레임을 어떻게 나눠도 같은 발사가 나온다).
                    long k = (long)Math.Floor((fromTime - first) / interval) - 1;
                    if (k < 0) k = 0;

                    for (; ; k++)
                    {
                        double shotTime = first + k * interval;
                        if (shotTime >= limit) break;
                        if (shotTime < fromTime) continue;

                        float time = (float)shotTime;
                        results.Add(new PatternShot
                        {
                            time = time,
                            emitterIndex = e,
                            position = emitter.path.Evaluate(time),
                            angle = emitter.AngleAt((float)(shotTime - range.start)),
                        });

                        if (results.Count >= MaxShotsPerQuery) return;
                    }
                }
            }
        }

        /// <summary>범위를 벗어난 값을 바로잡는다.</summary>
        public void Sanitize()
        {
            duration = Mathf.Max(MinDuration, duration);

            if (bodyPath == null) bodyPath = new PatternPath();
            bodyPath.Sanitize();

            if (emitters == null) emitters = new List<PatternEmitter>();
            for (int i = 0; i < emitters.Count; i++)
            {
                if (emitters[i] == null) emitters[i] = new PatternEmitter();
                emitters[i].Sanitize();
            }
        }

        private void OnValidate()
        {
            Sanitize();
        }
    }
}
