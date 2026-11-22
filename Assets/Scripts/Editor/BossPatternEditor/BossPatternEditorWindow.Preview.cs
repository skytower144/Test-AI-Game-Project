using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chopper.EditorTools
{
    // 미리보기: 패턴을 처음부터 끝까지 미리 계산해 두고(Bake), 재생 위치의 시각에 살아 있는 총알을 게임 화면 위에 그린다.
    // 발사 시각/위치/각도는 게임과 같은 BossPattern.CollectShots, 본체 이동은 게임과 같은 Boss.StepBody로 계산한다.
    public sealed partial class BossPatternEditorWindow
    {
        // 게임 화면(월드 단위). Game 씬의 메인 카메라: 직교 크기 2.7, 화면비 16:9
        private const float ScreenHalfHeight = 2.7f;
        private const float ScreenHalfWidth = ScreenHalfHeight * 16f / 9f;

        /// <summary>게임 화면 바깥으로 더 보여 주는 여백(유닛).</summary>
        private const float ViewMargin = 0.8f;

        /// <summary>본체 이동을 계산하는 간격(초).</summary>
        private const float BodyStep = 1f / 120f;

        /// <summary>패턴이 끝난 뒤 총알이 화면을 벗어날 때까지 더 보여 주는 시간의 한도(초).</summary>
        private const float MaxTail = 6f;

        private const int MaxBakedBullets = 60000;
        private const int MaxDrawnBullets = 8000;
        private const float HandlePickRadius = 10f;

        private static readonly Color PreviewBackground = new Color(0.115f, 0.115f, 0.125f);
        private static readonly Color ScreenFill = new Color(0.055f, 0.075f, 0.125f);
        private static readonly Color ScreenBorder = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color GridColor = new Color(1f, 1f, 1f, 0.055f);
        private static readonly Color AxisColor = new Color(1f, 1f, 1f, 0.13f);
        private static readonly Color DespawnBorder = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color BodyPathColor = new Color(0.65f, 0.85f, 1f);
        private static readonly Color PlayerColor = new Color(0.36f, 1f, 0.49f);
        private static readonly Color HitCircleColor = new Color(1f, 0.25f, 0.25f, 0.9f);

        // ───────────── 프리팹에서 읽은 미리보기 정보 ─────────────

        private sealed class SpriteVisual
        {
            public Texture texture;
            public Rect uv;
            public Vector2 size;    // 표시 크기(유닛)
            public Vector2 pivot;   // 0~1(왼쪽 아래 기준)
            public Vector2 offset;  // 루트 기준 피벗 위치(유닛)
            public float rotation;  // 도(시계 반대 방향)
            public Color color = Color.white;
        }

        private sealed class BulletVisual
        {
            public SpriteVisual sprite;
            public float despawnMargin = 0.3f;
            public float hitRadius = 0.056f;
            public bool rotate;
        }

        private SpriteVisual bossVisual;
        private Vector2 bossEyeOffset;
        private Vector2 bossAnchor;
        private float bossSmoothTime;
        private float bossMinY;
        private float bossMaxY;
        private Bullet bossDefaultBullet;

        private SpriteVisual playerVisual;
        private Vector2 playerHitOffset;
        private float playerHitRadius;

        private readonly Dictionary<Bullet, BulletVisual> bulletVisuals = new Dictionary<Bullet, BulletVisual>();
        private readonly BulletVisual fallbackBullet = new BulletVisual();
        private bool infoDirty = true;

        private void RefreshSceneInfo()
        {
            bulletVisuals.Clear();

            // 보스 프리팹을 찾지 못했을 때의 기본값(Boss 프리팹의 초기 설정과 같은 값)
            bossVisual = null;
            bossEyeOffset = new Vector2(-0.51f, 0.01f);
            bossAnchor = ViewportToWorld(new Vector2(0.8f, 0.5f));
            bossSmoothTime = 0.3f;
            bossMinY = -ScreenHalfHeight + 0.85f;
            bossMaxY = ScreenHalfHeight - 0.85f;
            bossDefaultBullet = null;

            Boss boss = TargetBoss;
            if (boss != null)
            {
                Transform root = boss.transform;
                bossVisual = CreateVisual(boss.ShipRenderer, root);
                if (boss.Eye != null) bossEyeOffset = boss.Eye.position - root.position;
                bossAnchor = ViewportToWorld(boss.AnchorViewport);
                bossSmoothTime = Mathf.Max(0.01f, boss.MoveSmoothTime);
                bossMinY = -ScreenHalfHeight + boss.VerticalPadding;
                bossMaxY = ScreenHalfHeight - boss.VerticalPadding;
                bossDefaultBullet = boss.DefaultBulletPrefab;
            }

            playerVisual = null;
            playerHitOffset = Vector2.zero;
            playerHitRadius = 0.165f;

            if (playerPrefab != null)
            {
                Transform root = playerPrefab.transform;

                SpriteRenderer best = null;
                float bestArea = 0f;
                foreach (SpriteRenderer renderer in playerPrefab.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!renderer.enabled || renderer.sprite == null) continue;
                    float area = renderer.sprite.rect.width * renderer.sprite.rect.height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = renderer;
                    }
                }
                playerVisual = CreateVisual(best, root);

                var circle = playerPrefab.GetComponent<CircleCollider2D>();
                if (circle != null)
                {
                    Vector3 scale = root.lossyScale;
                    playerHitOffset = (Vector2)root.TransformPoint(circle.offset) - (Vector2)root.position;
                    playerHitRadius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                }
            }
        }

        private static Vector2 ViewportToWorld(Vector2 viewport)
        {
            return new Vector2(Mathf.LerpUnclamped(-ScreenHalfWidth, ScreenHalfWidth, viewport.x),
                Mathf.LerpUnclamped(-ScreenHalfHeight, ScreenHalfHeight, viewport.y));
        }

        private static SpriteVisual CreateVisual(SpriteRenderer renderer, Transform root)
        {
            if (renderer == null || renderer.sprite == null || renderer.sprite.texture == null) return null;

            Sprite sprite = renderer.sprite;
            Texture2D texture = sprite.texture;
            Rect rect = sprite.rect;
            Vector3 scale = renderer.transform.lossyScale;

            var uv = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
            if (renderer.flipX)
            {
                uv.x += uv.width;
                uv.width = -uv.width;
            }
            if (renderer.flipY)
            {
                uv.y += uv.height;
                uv.height = -uv.height;
            }

            return new SpriteVisual
            {
                texture = texture,
                uv = uv,
                size = new Vector2(rect.width / sprite.pixelsPerUnit * Mathf.Abs(scale.x), rect.height / sprite.pixelsPerUnit * Mathf.Abs(scale.y)),
                pivot = new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height),
                offset = root != null ? (Vector2)(renderer.transform.position - root.position) : Vector2.zero,
                rotation = renderer.transform.eulerAngles.z,
                color = renderer.color,
            };
        }

        private BulletVisual GetBulletVisual(Bullet prefab)
        {
            if (prefab == null) prefab = bossDefaultBullet;
            if (prefab == null) return fallbackBullet;

            if (!bulletVisuals.TryGetValue(prefab, out BulletVisual visual))
            {
                visual = new BulletVisual
                {
                    sprite = CreateVisual(prefab.GetComponentInChildren<SpriteRenderer>(true), prefab.transform),
                    despawnMargin = prefab.DespawnMargin,
                    hitRadius = prefab.HitRadius,
                    rotate = prefab.RotatesToDirection,
                };
                bulletVisuals.Add(prefab, visual);
            }
            return visual;
        }

        // ───────────── 미리 계산(Bake) ─────────────

        private struct BakedBullet
        {
            public float spawn;      // 발사 시각
            public float death;      // 화면 밖으로 나가 사라지는 시각
            public Vector2 origin;
            public Vector2 velocity;
            public float angle;
            public int emitter;
            public BulletVisual visual;
        }

        private readonly List<BakedBullet> baked = new List<BakedBullet>(4096);
        private readonly List<PatternShot> shotScratch = new List<PatternShot>(1024);
        private readonly List<float> spawnTimes = new List<float>(4096);
        private readonly List<float> deathTimes = new List<float>(4096);

        private Vector2[] bodySamples = Array.Empty<Vector2>();
        private int[] emitterShots = Array.Empty<int>();
        private int[] emitterBullets = Array.Empty<int>();
        private int[] emitterLost = Array.Empty<int>();

        private int bakedVersion = -1;
        private Vector2 bakedPlayer;
        private float tail;
        private int totalShots;
        private int peakBullets;
        private int visibleBullets;
        private bool bakeTruncated;

        private void RebuildPreviewIfNeeded()
        {
            if (!infoDirty && bakedVersion == dataVersion && bakedPlayer == playerPosition) return;

            if (infoDirty)
            {
                RefreshSceneInfo();
                infoDirty = false;
            }
            Bake();
            bakedVersion = dataVersion;
            bakedPlayer = playerPosition;
        }

        private void Bake()
        {
            BossPattern pattern = working;
            int emitterCount = pattern.emitters.Count;
            if (emitterShots.Length != emitterCount)
            {
                emitterShots = new int[emitterCount];
                emitterBullets = new int[emitterCount];
                emitterLost = new int[emitterCount];
            }

            SimulateBody(pattern, pattern.duration + MaxTail);

            baked.Clear();
            bakeTruncated = false;
            totalShots = 0;
            float lastDeath = 0f;

            for (int e = 0; e < emitterCount; e++)
            {
                emitterShots[e] = 0;
                emitterBullets[e] = 0;
                emitterLost[e] = 0;
                if (bakeTruncated) continue;

                PatternEmitter emitter = pattern.emitters[e];
                shotScratch.Clear();
                pattern.CollectShots(0f, pattern.duration + 1f, shotScratch, e);

                BulletVisual visual = GetBulletVisual(emitter.bulletPrefab);
                float margin = visual.despawnMargin;
                Rect bounds = Rect.MinMaxRect(-ScreenHalfWidth - margin, -ScreenHalfHeight - margin,
                    ScreenHalfWidth + margin, ScreenHalfHeight + margin);

                int count = emitter.BulletCount;
                float speed = Mathf.Max(0.1f, emitter.bulletSpeed);
                emitterShots[e] = shotScratch.Count;
                totalShots += shotScratch.Count;

                for (int s = 0; s < shotScratch.Count && !bakeTruncated; s++)
                {
                    PatternShot shot = shotScratch[s];
                    Vector2 origin = SpaceOrigin(emitter, shot.time) + shot.position;

                    float center = shot.angle;
                    if (emitter.aim == PatternAim.AtPlayer) center += Boss.AimAngle(origin, true, playerPosition);

                    for (int b = 0; b < count; b++)
                    {
                        float angle = emitter.BulletAngle(center, b);
                        float radians = angle * Mathf.Deg2Rad;
                        var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                        // 게임에서 총알은 화면 밖으로 despawnMargin 이상 벗어나면 사라진다(밖에서 발사되면 바로 사라진다)
                        float life = ExitDistance(origin, direction, bounds) / speed;
                        if (life <= 0f) emitterLost[e]++;

                        float death = shot.time + life;
                        if (death > lastDeath) lastDeath = death;

                        baked.Add(new BakedBullet
                        {
                            spawn = shot.time,
                            death = death,
                            origin = origin,
                            velocity = direction * speed,
                            angle = angle,
                            emitter = e,
                            visual = visual,
                        });
                        emitterBullets[e]++;

                        if (baked.Count >= MaxBakedBullets)
                        {
                            bakeTruncated = true;
                            break;
                        }
                    }
                }
            }

            // 패턴이 끝난 뒤 마지막 총알이 화면을 벗어날 때까지를 0.1초 단위로 올려 "여운" 구간으로 보여 준다
            tail = Mathf.Clamp(Mathf.Ceil((lastDeath - pattern.duration) * 10f - 0.001f) / 10f, 0f, MaxTail);
            if (time > PreviewSpan) time = PreviewSpan;

            // 화면에 동시에 떠 있는 총알의 최대 수
            spawnTimes.Clear();
            deathTimes.Clear();
            for (int i = 0; i < baked.Count; i++)
            {
                if (baked[i].death <= baked[i].spawn) continue;
                spawnTimes.Add(baked[i].spawn);
                deathTimes.Add(baked[i].death);
            }
            spawnTimes.Sort();
            deathTimes.Sort();

            peakBullets = 0;
            int alive = 0, d = 0;
            for (int s = 0; s < spawnTimes.Count; s++)
            {
                while (d < deathTimes.Count && deathTimes[d] <= spawnTimes[s])
                {
                    d++;
                    alive--;
                }
                alive++;
                if (alive > peakBullets) peakBullets = alive;
            }
        }

        /// <summary>패턴 진행 중 보스 본체의 위치를 게임과 같은 방식(목표 위치로 부드럽게 따라감)으로 계산해 둔다.</summary>
        private void SimulateBody(BossPattern pattern, float span)
        {
            int count = Mathf.CeilToInt(span / BodyStep) + 2;
            if (bodySamples.Length != count) bodySamples = new Vector2[count];

            Vector2 position = bossAnchor;
            Vector2 velocity = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                bodySamples[i] = position;
                float t = i * BodyStep;
                Vector2 offset = t < pattern.duration ? pattern.BodyOffsetAt(t) : Vector2.zero;
                position = Boss.StepBody(position, bossAnchor + offset, ref velocity, bossSmoothTime, bossMinY, bossMaxY, BodyStep);
            }
        }

        private Vector2 BodyAt(float t)
        {
            if (bodySamples.Length == 0) return bossAnchor;

            float f = Mathf.Clamp(t / BodyStep, 0f, bodySamples.Length - 1);
            int i = Mathf.Min((int)f, bodySamples.Length - 2);
            if (i < 0) return bodySamples[0];
            return Vector2.LerpUnclamped(bodySamples[i], bodySamples[i + 1], f - i);
        }

        /// <summary>발사 지점의 위치가 기준으로 삼는 원점(보스의 눈 또는 화면 중심)의 월드 좌표.</summary>
        private Vector2 SpaceOrigin(PatternEmitter emitter, float t)
        {
            return emitter.space == PatternSpace.Screen ? Vector2.zero : BodyAt(t) + bossEyeOffset;
        }

        /// <summary>발사 지점/본체의 위치를 평가할 시각. 패턴이 끝난 뒤에는 끝난 순간에 머문다.</summary>
        private float PatternTime => Mathf.Min(time, PatternDuration);

        private static float ExitDistance(Vector2 origin, Vector2 direction, Rect bounds)
        {
            if (!bounds.Contains(origin)) return 0f;

            float tx = float.PositiveInfinity, ty = float.PositiveInfinity;
            if (direction.x > 1e-6f) tx = (bounds.xMax - origin.x) / direction.x;
            else if (direction.x < -1e-6f) tx = (bounds.xMin - origin.x) / direction.x;
            if (direction.y > 1e-6f) ty = (bounds.yMax - origin.y) / direction.y;
            else if (direction.y < -1e-6f) ty = (bounds.yMin - origin.y) / direction.y;

            float distance = Mathf.Min(tx, ty);
            return float.IsInfinity(distance) ? 0f : distance;
        }

        // ───────────── 좌표 변환 ─────────────

        private Rect previewRect;
        private Vector2 previewCenter;
        private float pixelsPerUnit = 1f;

        private Vector2 WorldToGui(Vector2 world)
        {
            return previewCenter + new Vector2(world.x, -world.y) * pixelsPerUnit;
        }

        private Vector2 GuiToWorld(Vector2 gui)
        {
            Vector2 d = (gui - previewCenter) / pixelsPerUnit;
            return new Vector2(d.x, -d.y);
        }

        private Rect WorldRectToGui(float xMin, float yMin, float xMax, float yMax)
        {
            Vector2 a = WorldToGui(new Vector2(xMin, yMax));
            Vector2 b = WorldToGui(new Vector2(xMax, yMin));
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        // ───────────── 그리기 ─────────────

        private void DrawPreview(Rect area)
        {
            previewRect = area;
            float worldWidth = (ScreenHalfWidth + ViewMargin) * 2f;
            float worldHeight = (ScreenHalfHeight + ViewMargin) * 2f;
            pixelsPerUnit = Mathf.Max(1f, Mathf.Min(area.width / worldWidth, area.height / worldHeight));
            previewCenter = new Vector2(area.width * 0.5f, area.height * 0.5f);

            GUI.BeginClip(area);
            var local = new Rect(0f, 0f, area.width, area.height);
            if (Event.current.type == EventType.Repaint) PaintPreview(local);
            HandlePreviewInput(local);
            GUI.EndClip();
        }

        private void PaintPreview(Rect local)
        {
            EditorGUI.DrawRect(local, PreviewBackground);

            // 게임 화면
            Rect screen = WorldRectToGui(-ScreenHalfWidth, -ScreenHalfHeight, ScreenHalfWidth, ScreenHalfHeight);
            EditorGUI.DrawRect(screen, ScreenFill);

            if (showGrid)
            {
                for (int x = -Mathf.FloorToInt(ScreenHalfWidth); x <= Mathf.FloorToInt(ScreenHalfWidth); x++)
                {
                    float gx = Mathf.Round(WorldToGui(new Vector2(x, 0f)).x);
                    EditorGUI.DrawRect(new Rect(gx, screen.y, 1f, screen.height), x == 0 ? AxisColor : GridColor);
                }
                for (int y = -Mathf.FloorToInt(ScreenHalfHeight); y <= Mathf.FloorToInt(ScreenHalfHeight); y++)
                {
                    float gy = Mathf.Round(WorldToGui(new Vector2(0f, y)).y);
                    EditorGUI.DrawRect(new Rect(screen.x, gy, screen.width, 1f), y == 0 ? AxisColor : GridColor);
                }
            }

            // 총알이 사라지는 경계(점선)와 게임 화면 테두리
            float margin = GetBulletVisual(null).despawnMargin;
            Rect despawn = WorldRectToGui(-ScreenHalfWidth - margin, -ScreenHalfHeight - margin,
                ScreenHalfWidth + margin, ScreenHalfHeight + margin);
            DrawDottedRect(despawn, DespawnBorder);
            DrawOutline(screen, ScreenBorder);
            GUI.Label(new Rect(screen.x + 4f, screen.yMax - 18f, 300f, 16f), "게임 화면 (16:9)", hintStyle);

            // 보스
            Vector2 body = BodyAt(time);
            if (bossVisual != null)
            {
                DrawVisual(bossVisual, body, 0.92f);
            }
            else
            {
                Rect placeholder = WorldRectToGui(body.x - 1.1f, body.y - 0.6f, body.x + 1.1f, body.y + 0.6f);
                EditorGUI.DrawRect(placeholder, new Color(0.5f, 0.5f, 0.6f, 0.5f));
            }

            // 가상 플레이어
            if (playerVisual != null) DrawVisual(playerVisual, playerPosition - playerHitOffset, 0.95f);
            Vector2 playerGui = WorldToGui(playerPosition);
            Handles.color = PlayerColor;
            Handles.DrawWireDisc(playerGui, Vector3.forward, Mathf.Max(3f, playerHitRadius * pixelsPerUnit));
            GUI.Label(new Rect(playerGui.x - 60f, playerGui.y + 0.32f * pixelsPerUnit + 4f, 120f, 16f), "가상 플레이어", centeredHintStyle);

            // 경로(선택된 것은 핸들까지)
            if (selectedEmitter == BodySelection && working.bodyPath.IsMoving)
                DrawPath(working.bodyPath, bossAnchor, BodyPathColor, true);

            for (int e = 0; e < working.emitters.Count; e++)
            {
                PatternEmitter emitter = working.emitters[e];
                if (!emitter.path.IsMoving) continue;
                DrawPath(emitter.path, SpaceOrigin(emitter, PatternTime), emitter.color, e == selectedEmitter);
            }

            // 총알
            visibleBullets = 0;
            for (int i = 0; i < baked.Count; i++)
            {
                BakedBullet bullet = baked[i];
                if (time < bullet.spawn || time >= bullet.death) continue;

                Vector2 position = bullet.origin + bullet.velocity * (time - bullet.spawn);
                DrawBullet(bullet, position);
                if (++visibleBullets >= MaxDrawnBullets) break;
            }

            // 발사 지점
            for (int e = 0; e < working.emitters.Count; e++)
                DrawEmitter(e);

            // 정보
            string info = time.ToString("0.000") + "초  ·  프레임 " + CurrentFrame + "  ·  화면의 총알 " + visibleBullets + "발";
            if (time > PatternDuration + 1e-4f) info += "  ·  패턴 종료 후";
            GUI.Label(new Rect(8f, 6f, local.width - 16f, 18f), info, overlayStyle);

            string stats = "발사 " + totalShots + "회  ·  총알 " + baked.Count + "발  ·  최대 동시 " + peakBullets + "발";
            if (bakeTruncated) stats += "  (너무 많아 일부만 표시)";
            GUI.Label(new Rect(8f, 6f, local.width - 16f, 18f), stats, overlayRightStyle);

            GUI.Label(new Rect(8f, local.height - 22f, local.width - 16f, 18f),
                "드래그: 발사 지점 · 도착 위치(○) · 포물선 기울기(◇) · 가상 플레이어      Shift: 시작 위치만      Ctrl: 0.1 단위", hintStyle);
        }

        private void DrawBullet(BakedBullet bullet, Vector2 position)
        {
            BulletVisual visual = bullet.visual;
            Vector2 gui = WorldToGui(position);

            if (visual.sprite != null)
            {
                DrawSprite(visual.sprite, position, visual.rotate ? bullet.angle : 0f, visual.sprite.color);
            }
            else
            {
                Color color = bullet.emitter < working.emitters.Count ? working.emitters[bullet.emitter].color : Color.white;
                color.a = 1f;
                Handles.color = color;
                Handles.DrawSolidDisc(gui, Vector3.forward, Mathf.Max(2f, 0.07f * pixelsPerUnit));
            }

            if (showHitCircles)
            {
                Handles.color = HitCircleColor;
                Handles.DrawWireDisc(gui, Vector3.forward, Mathf.Max(1.5f, visual.hitRadius * pixelsPerUnit));
            }
        }

        private void DrawEmitter(int index)
        {
            PatternEmitter emitter = working.emitters[index];
            bool selected = index == selectedEmitter;
            float t = PatternTime;

            Vector2 world = SpaceOrigin(emitter, t) + emitter.path.Evaluate(t);
            Vector2 gui = WorldToGui(world);

            int range = time < PatternDuration ? emitter.FindRange(time) : -1;
            bool active = range >= 0;

            Color color = emitter.color;
            color.a = 1f;
            Color dim = Color.Lerp(color, new Color(0.45f, 0.45f, 0.45f), 0.65f);

            // 켜져 있는 동안에는 지금 발사하는 방향을 짧은 선으로 보여 준다
            if (active)
            {
                float center = emitter.AngleAt(time - emitter.activeRanges[range].start);
                if (emitter.aim == PatternAim.AtPlayer) center += Boss.AimAngle(world, true, playerPosition);

                int count = emitter.BulletCount;
                int step = Mathf.Max(1, Mathf.CeilToInt(count / 36f));
                float length = 0.42f * pixelsPerUnit;
                Handles.color = new Color(color.r, color.g, color.b, selected ? 0.95f : 0.6f);
                for (int b = 0; b < count; b += step)
                {
                    float radians = emitter.BulletAngle(center, b) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
                    Handles.DrawAAPolyLine(selected ? 2f : 1.5f, gui + direction * 6f, gui + direction * (6f + length));
                }
            }

            Handles.color = active ? color : dim;
            Handles.DrawSolidDisc(gui, Vector3.forward, selected ? 5f : 4f);
            Handles.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            Handles.DrawWireDisc(gui, Vector3.forward, selected ? 8f : 6.5f);

            if (selected && !string.IsNullOrEmpty(emitter.name))
            {
                // 보스나 총알 위에서도 읽히도록 어두운 바탕을 깐다
                var content = new GUIContent(emitter.name);
                Vector2 size = overlayStyle.CalcSize(content);
                var labelRect = new Rect(gui.x + 12f, gui.y - 22f, size.x, 16f);
                EditorGUI.DrawRect(new Rect(labelRect.x - 3f, labelRect.y, labelRect.width + 6f, labelRect.height), new Color(0f, 0f, 0f, 0.6f));
                GUI.Label(labelRect, content, overlayStyle);
            }
        }

        /// <summary>이동 경로와(선택된 경우) 편집 핸들: □ 시작 위치, ○ 도착 위치, ◇ 포물선 기울기.</summary>
        private void DrawPath(PatternPath path, Vector2 origin, Color color, bool selected)
        {
            int segments = path.shape == PathShape.Parabola ? 28 : 1;
            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
                points[i] = WorldToGui(origin + path.PointAt(i / (float)segments));

            Handles.color = new Color(color.r, color.g, color.b, selected ? 0.95f : 0.4f);
            Handles.DrawAAPolyLine(selected ? 2.5f : 1.5f, points);

            if (!selected) return;

            Vector2 start = WorldToGui(origin + path.start);
            Vector2 end = WorldToGui(origin + path.end);

            var startRect = new Rect(start.x - 4.5f, start.y - 4.5f, 9f, 9f);
            EditorGUI.DrawRect(startRect, new Color(0.1f, 0.1f, 0.1f, 0.9f));
            DrawOutline(startRect, Color.white);

            Handles.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            Handles.DrawSolidDisc(end, Vector3.forward, 5f);
            Handles.color = Color.white;
            Handles.DrawWireDisc(end, Vector3.forward, 5f);

            if (path.shape == PathShape.Parabola)
            {
                Vector2 apex = WorldToGui(origin + path.PointAt(0.5f));
                var diamond = new Vector3[]
                {
                    apex + new Vector2(0f, -6f), apex + new Vector2(6f, 0f), apex + new Vector2(0f, 6f), apex + new Vector2(-6f, 0f),
                };
                Handles.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
                Handles.DrawAAConvexPolygon(diamond);
                Handles.color = Color.white;
                Handles.DrawAAPolyLine(1.5f, diamond[0], diamond[1], diamond[2], diamond[3], diamond[0]);
            }
        }

        private void DrawVisual(SpriteVisual visual, Vector2 rootWorld, float alpha)
        {
            Color color = visual.color;
            color.a *= alpha;
            DrawSprite(visual, rootWorld + visual.offset, visual.rotation, color);
        }

        /// <summary>스프라이트의 피벗이 pivotWorld에 오도록 그린다.</summary>
        private void DrawSprite(SpriteVisual visual, Vector2 pivotWorld, float rotation, Color color)
        {
            Vector2 size = visual.size * pixelsPerUnit;
            Vector2 gui = WorldToGui(pivotWorld);
            var rect = new Rect(gui.x - visual.pivot.x * size.x, gui.y - (1f - visual.pivot.y) * size.y, size.x, size.y);

            Matrix4x4 savedMatrix = GUI.matrix;
            Color savedColor = GUI.color;

            if (Mathf.Abs(rotation) > 0.01f) GUIUtility.RotateAroundPivot(-rotation, gui);
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(rect, visual.texture, visual.uv, true);

            GUI.color = savedColor;
            GUI.matrix = savedMatrix;
        }

        private static void DrawOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static void DrawDottedRect(Rect rect, Color color)
        {
            Handles.color = color;
            var a = new Vector3(rect.x, rect.y);
            var b = new Vector3(rect.xMax, rect.y);
            var c = new Vector3(rect.xMax, rect.yMax);
            var d = new Vector3(rect.x, rect.yMax);
            Handles.DrawDottedLine(a, b, 3f);
            Handles.DrawDottedLine(b, c, 3f);
            Handles.DrawDottedLine(c, d, 3f);
            Handles.DrawDottedLine(d, a, 3f);
        }

        // ───────────── 마우스 편집 ─────────────

        private enum PreviewDrag { None, Player, PathMove, PathEnd, PathBend }

        private static readonly int PreviewControlHash = "BossPatternPreview".GetHashCode();

        private PreviewDrag previewDrag;
        private int previewDragControl;         // 드래그를 시작한 컨트롤 ID
        private int previewDragTarget;          // 발사 지점 번호. BodySelection = 보스 본체 이동
        private Vector2 previewDragStartWorld;
        private Vector2 previewDragStart;
        private Vector2 previewDragEnd;
        private Vector2 previewDragPlayer;

        private void HandlePreviewInput(Rect local)
        {
            int id = GUIUtility.GetControlID(PreviewControlHash, FocusType.Passive, local);
            Event evt = Event.current;

            // 드래그 중에는 시작할 때의 컨트롤 ID로 이벤트를 받는다(도중에 ID가 달라져도 끊기지 않고, 영역 밖으로 나가도 이어진다)
            bool dragging = previewDrag != PreviewDrag.None && GUIUtility.hotControl == previewDragControl;
            if (!dragging) previewDrag = PreviewDrag.None;

            switch (evt.GetTypeForControl(dragging ? previewDragControl : id))
            {
                case EventType.MouseDown:
                    if (evt.button != 0 || !local.Contains(evt.mousePosition)) break;

                    GUI.FocusControl(null);
                    if (BeginPreviewDrag(evt.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        previewDragControl = id;
                    }
                    evt.Use();
                    Repaint();
                    break;

                case EventType.MouseDrag:
                    if (!dragging) break;
                    ApplyPreviewDrag(evt.mousePosition, evt.shift, evt.control || evt.command);
                    evt.Use();
                    break;

                case EventType.MouseUp:
                    if (!dragging) break;
                    GUIUtility.hotControl = 0;
                    previewDrag = PreviewDrag.None;
                    evt.Use();
                    Repaint();
                    break;
            }
        }

        private PatternPath PathOf(int target)
        {
            return target >= 0 ? working.emitters[target].path : working.bodyPath;
        }

        private Vector2 PathOrigin(int target)
        {
            return target >= 0 ? SpaceOrigin(working.emitters[target], PatternTime) : bossAnchor;
        }

        private bool IsNear(Vector2 mouse, Vector2 world)
        {
            return (WorldToGui(world) - mouse).sqrMagnitude <= HandlePickRadius * HandlePickRadius;
        }

        private bool BeginPreviewDrag(Vector2 mouse)
        {
            previewDrag = PreviewDrag.None;
            previewDragStartWorld = GuiToWorld(mouse);

            // 1) 선택된 경로의 핸들(포물선 기울기 → 도착 위치 → 시작 위치)
            if (selectedEmitter >= 0 || selectedEmitter == BodySelection)
            {
                PatternPath path = PathOf(selectedEmitter);
                Vector2 origin = PathOrigin(selectedEmitter);
                if (path.IsMoving)
                {
                    if (path.shape == PathShape.Parabola && IsNear(mouse, origin + path.PointAt(0.5f)))
                        return StartPathDrag(PreviewDrag.PathBend, selectedEmitter);
                    if (IsNear(mouse, origin + path.end)) return StartPathDrag(PreviewDrag.PathEnd, selectedEmitter);
                    if (IsNear(mouse, origin + path.start)) return StartPathDrag(PreviewDrag.PathMove, selectedEmitter);
                }
            }

            // 2) 발사 지점(선택된 것 먼저)
            float t = PatternTime;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int e = 0; e < working.emitters.Count; e++)
                {
                    if ((pass == 0) != (e == selectedEmitter)) continue;

                    PatternEmitter emitter = working.emitters[e];
                    if (!IsNear(mouse, SpaceOrigin(emitter, t) + emitter.path.Evaluate(t))) continue;

                    SelectEmitter(e);
                    return StartPathDrag(PreviewDrag.PathMove, e);
                }
            }

            // 3) 가상 플레이어
            float playerRadius = Mathf.Max(14f, playerHitRadius * pixelsPerUnit + 8f);
            if ((WorldToGui(playerPosition) - mouse).sqrMagnitude <= playerRadius * playerRadius)
            {
                previewDrag = PreviewDrag.Player;
                previewDragPlayer = playerPosition;
                return true;
            }

            // 4) 보스 본체를 클릭하면 "보스 본체 이동"을 선택한다
            Vector2 body = BodyAt(time);
            if (WorldRectToGui(body.x - 1.1f, body.y - 0.6f, body.x + 1.1f, body.y + 0.6f).Contains(mouse))
                SelectEmitter(BodySelection);

            return false;
        }

        private bool StartPathDrag(PreviewDrag kind, int target)
        {
            PatternPath path = PathOf(target);
            previewDrag = kind;
            previewDragTarget = target;
            previewDragStart = path.start;
            previewDragEnd = path.end;
            return true;
        }

        private void ApplyPreviewDrag(Vector2 mouse, bool startOnly, bool coarse)
        {
            float grid = coarse ? 0.1f : 0.01f;
            Vector2 world = GuiToWorld(mouse);
            Vector2 delta = world - previewDragStartWorld;

            if (previewDrag == PreviewDrag.Player)
            {
                Vector2 moved = Snap(previewDragPlayer + delta, grid);
                moved.x = Mathf.Clamp(moved.x, -ScreenHalfWidth, ScreenHalfWidth);
                moved.y = Mathf.Clamp(moved.y, -ScreenHalfHeight, ScreenHalfHeight);
                playerPosition = moved;
                Repaint();
                return;
            }

            if (previewDrag == PreviewDrag.None) return;
            if (previewDragTarget >= working.emitters.Count) return;

            PatternPath path = PathOf(previewDragTarget);
            BeginDirectEdit("패턴 경로 편집");
            switch (previewDrag)
            {
                case PreviewDrag.PathMove:
                {
                    // 발사 지점을 옮기면 경로 전체가 함께 움직인다(Shift = 시작 위치만)
                    Vector2 start = Snap(previewDragStart + delta, grid);
                    path.start = start;
                    if (!startOnly) path.end = Snap(previewDragEnd + (start - previewDragStart), 0.001f);
                    break;
                }
                case PreviewDrag.PathEnd:
                    path.end = Snap(previewDragEnd + delta, grid);
                    break;
                case PreviewDrag.PathBend:
                {
                    Vector2 local = world - PathOrigin(previewDragTarget);
                    Vector2 middle = (path.start + path.end) * 0.5f;
                    path.bend = Mathf.Round(Vector2.Dot(local - middle, path.BendDirection) / grid) * grid;
                    break;
                }
            }
            EndDirectEdit();
        }

        private static Vector2 Snap(Vector2 value, float grid)
        {
            return new Vector2(Mathf.Round(value.x / grid) * grid, Mathf.Round(value.y / grid) * grid);
        }
    }
}
