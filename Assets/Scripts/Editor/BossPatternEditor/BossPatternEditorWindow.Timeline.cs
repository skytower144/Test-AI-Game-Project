using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chopper.EditorTools
{
    // 재생 버튼과 타임라인: 눈금자(원하는 프레임으로 이동, 패턴 길이 조절)와 발사 지점별 활성 구간 트랙.
    public sealed partial class BossPatternEditorWindow
    {
        private const float TransportHeight = 28f;
        private const float RulerHeight = 24f;
        private const float TrackHeight = 24f;
        private const float TrackLabelWidth = 176f;
        private const float TrackPadding = 12f;
        private const float ScrollbarWidth = 14f;
        private const float EdgeGrab = 5f;

        private static readonly Color TimelineBackground = new Color(0.16f, 0.16f, 0.17f);
        private static readonly Color RulerBackground = new Color(0.2f, 0.2f, 0.21f);
        private static readonly Color RulerTick = new Color(1f, 1f, 1f, 0.4f);
        private static readonly Color TrackEven = new Color(0.185f, 0.185f, 0.195f);
        private static readonly Color TrackOdd = new Color(0.2f, 0.2f, 0.21f);
        private static readonly Color TrackSelected = new Color(0.24f, 0.3f, 0.4f);
        private static readonly Color TrackLine = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color SeparatorColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color TailShade = new Color(0f, 0f, 0f, 0.38f);
        private static readonly Color EndMarker = new Color(1f, 0.82f, 0.25f);
        private static readonly Color PlayheadColor = new Color(1f, 0.3f, 0.3f);
        private static readonly Color BodyClipColor = new Color(0.42f, 0.56f, 0.68f);

        private enum TimelineDrag { None, Scrub, Duration, MoveRange, ResizeStart, ResizeEnd }

        private static readonly int TimelineControlHash = "BossPatternTimeline".GetHashCode();

        private TimelineDrag timelineDrag;
        private int timelineDragControl;   // 드래그를 시작한 컨트롤 ID
        private int dragEmitter;
        private int dragRange;
        private float dragStartTime;
        private PatternTimeRange dragOriginal;

        /// <summary>타임라인이 보여 주는 전체 시간(패턴 길이 + 여운). 드래그 중에는 눈금이 흔들리지 않게 고정한다.</summary>
        private float timelineSpan = 1f;
        private float timeAxisX;
        private float timeAxisWidth = 100f;

        private float TimelineHeight(float available)
        {
            int rows = 1 + working.emitters.Count;
            float wanted = TransportHeight + RulerHeight + rows * TrackHeight + 8f;
            return Mathf.Clamp(wanted, 132f, Mathf.Max(132f, available * 0.42f));
        }

        private float TimeToX(float t)
        {
            return timeAxisX + t / timelineSpan * timeAxisWidth;
        }

        /// <summary>화면 x좌표에 해당하는 시각(범위를 벗어나도 그대로 돌려준다).</summary>
        private float XToTimeRaw(float x)
        {
            return (x - timeAxisX) / timeAxisWidth * timelineSpan;
        }

        private float XToTime(float x)
        {
            return Mathf.Clamp(XToTimeRaw(x), 0f, timelineSpan);
        }

        // ───────────── 전체 배치 ─────────────

        private void DrawTimeline(Rect rect)
        {
            if (timelineDrag == TimelineDrag.None) timelineSpan = Mathf.Max(BossPattern.MinDuration, PreviewSpan);

            EditorGUI.DrawRect(rect, TimelineBackground);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), SeparatorColor);

            var transport = new Rect(rect.x, rect.y + 1f, rect.width, TransportHeight);
            var ruler = new Rect(rect.x, transport.yMax, rect.width, RulerHeight);
            var tracks = new Rect(rect.x, ruler.yMax, rect.width, Mathf.Max(0f, rect.yMax - ruler.yMax));

            timeAxisX = rect.x + TrackLabelWidth + TrackPadding;
            timeAxisWidth = Mathf.Max(60f, rect.width - TrackLabelWidth - TrackPadding * 2f - ScrollbarWidth);

            int rows = 1 + working.emitters.Count;
            var content = new Rect(0f, 0f, Mathf.Max(0f, tracks.width - ScrollbarWidth), rows * TrackHeight);

            DrawTransport(transport);
            HandleTimelineInput(ruler, tracks, rows);

            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) PaintRuler(ruler);

            timelineScroll = GUI.BeginScrollView(tracks, timelineScroll, content, false, false);
            if (repaint) PaintTracks(content, rows, timeAxisX - tracks.x);
            GUI.EndScrollView();

            if (repaint) PaintPlayhead(ruler, tracks);
        }

        // ───────────── 재생 버튼 줄 ─────────────

        private void DrawTransport(Rect rect)
        {
            GUILayout.BeginArea(rect);
            GUILayout.Space(4f);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);

                if (GUILayout.Button(new GUIContent("|◀", "처음으로 (Home)"), EditorStyles.miniButtonLeft, GUILayout.Width(30f)))
                {
                    playing = false;
                    SetTime(0f, false);
                }
                if (GUILayout.Button(new GUIContent("◀", "이전 프레임 (←, Shift = 10프레임)"), EditorStyles.miniButtonMid, GUILayout.Width(26f)))
                    StepFrames(-1);
                if (GUILayout.Button(new GUIContent(playing ? "■ 정지" : "▶ 재생", "재생 / 정지 (Space)"), EditorStyles.miniButtonMid, GUILayout.Width(66f)))
                    TogglePlay();
                if (GUILayout.Button(new GUIContent("▶", "다음 프레임 (→, Shift = 10프레임)"), EditorStyles.miniButtonMid, GUILayout.Width(26f)))
                    StepFrames(1);
                if (GUILayout.Button(new GUIContent("▶|", "패턴이 끝나는 시각으로 (End)"), EditorStyles.miniButtonRight, GUILayout.Width(30f)))
                {
                    playing = false;
                    SetTime(PatternDuration, false);
                }

                GUILayout.Space(10f);
                loopPlayback = GUILayout.Toggle(loopPlayback, new GUIContent("반복", "끝까지 재생하면 처음부터 다시 재생한다"), GUILayout.Width(48f));

                GUILayout.Label(new GUIContent("속도", "미리보기 재생 속도"), GUILayout.Width(28f));
                speedIndex = EditorGUILayout.Popup(Mathf.Clamp(speedIndex, 0, SpeedLabels.Length - 1), SpeedLabels, GUILayout.Width(58f));

                GUILayout.Space(14f);
                GUILayout.Label(new GUIContent("시간(초)", "재생 위치(초). 값을 입력해 원하는 시각으로 이동한다"), GUILayout.Width(52f));
                EditorGUI.BeginChangeCheck();
                float newTime = EditorGUILayout.FloatField((float)System.Math.Round(time, 3), GUILayout.Width(58f));
                if (EditorGUI.EndChangeCheck())
                {
                    playing = false;
                    SetTime(newTime, false);
                }
                GUILayout.Label("/ " + PatternDuration.ToString("0.###"), GUILayout.Width(52f));

                GUILayout.Space(10f);
                GUILayout.Label(new GUIContent("프레임", "재생 위치(프레임). 값을 입력해 원하는 프레임으로 이동한다"), GUILayout.Width(40f));
                EditorGUI.BeginChangeCheck();
                int newFrame = EditorGUILayout.IntField(CurrentFrame, GUILayout.Width(52f));
                if (EditorGUI.EndChangeCheck())
                {
                    playing = false;
                    SetTime(newFrame / (float)frameRate, false);
                }
                GUILayout.Label("/ " + Mathf.RoundToInt(PatternDuration * frameRate), GUILayout.Width(46f));

                GUILayout.Space(10f);
                GUILayout.Label(new GUIContent("FPS", "타임라인의 프레임 단위(초당 프레임 수). 미리보기 전용이며 게임의 프레임률과는 무관하다"), GUILayout.Width(28f));
                int rateIndex = Mathf.Max(0, System.Array.IndexOf(FrameRateOptions, frameRate));
                rateIndex = EditorGUILayout.Popup(rateIndex, FrameRateLabels, GUILayout.Width(52f));
                frameRate = FrameRateOptions[rateIndex];

                GUILayout.FlexibleSpace();
            }
            GUILayout.EndArea();
        }

        // ───────────── 마우스 ─────────────

        private void HandleTimelineInput(Rect ruler, Rect tracks, int rows)
        {
            Event evt = Event.current;
            int id = GUIUtility.GetControlID(TimelineControlHash, FocusType.Passive);
            Vector2 mouse = evt.mousePosition;

            // 드래그 중에는 시작할 때의 컨트롤 ID로 이벤트를 받는다(도중에 ID가 달라져도 끊기지 않는다)
            bool dragging = timelineDrag != TimelineDrag.None && GUIUtility.hotControl == timelineDragControl;
            if (!dragging) timelineDrag = TimelineDrag.None;

            switch (evt.GetTypeForControl(dragging ? timelineDragControl : id))
            {
                case EventType.MouseDown:
                {
                    var trackArea = new Rect(tracks.x, tracks.y, tracks.width - ScrollbarWidth, tracks.height);
                    bool inRuler = ruler.Contains(mouse);
                    bool inTracks = trackArea.Contains(mouse);
                    if (!inRuler && !inTracks) break;

                    bool onTimeAxis = mouse.x >= timeAxisX - TrackPadding;

                    if (inRuler)
                    {
                        if (evt.button != 0 || !onTimeAxis) break;

                        GUI.FocusControl(null);
                        if (Mathf.Abs(mouse.x - TimeToX(PatternDuration)) <= 6f)
                        {
                            timelineDrag = TimelineDrag.Duration;
                        }
                        else
                        {
                            timelineDrag = TimelineDrag.Scrub;
                            playing = false;
                            SetTime(XToTime(mouse.x), true);
                        }
                        GUIUtility.hotControl = id;
                        timelineDragControl = id;
                        evt.Use();
                        break;
                    }

                    float contentY = mouse.y - tracks.y + timelineScroll.y;
                    int row = Mathf.FloorToInt(contentY / TrackHeight);
                    if (row < 0 || row >= rows) break;

                    int emitterIndex = row - 1; // 0번 줄 = 보스 본체 이동
                    GUI.FocusControl(null);

                    if (!onTimeAxis)
                    {
                        if (evt.button == 0)
                        {
                            SelectEmitter(emitterIndex);
                            evt.Use();
                        }
                        break;
                    }

                    int zone = 0;
                    int hitRange = emitterIndex >= 0 ? HitRange(emitterIndex, mouse.x, out zone) : -1;

                    if (evt.button == 1)
                    {
                        SelectEmitter(emitterIndex);
                        selectedRange = hitRange;
                        ShowTrackMenu(emitterIndex, hitRange, XToTime(mouse.x));
                        evt.Use();
                        break;
                    }
                    if (evt.button != 0) break;

                    SelectEmitter(emitterIndex);
                    if (hitRange >= 0)
                    {
                        selectedRange = hitRange;
                        dragEmitter = emitterIndex;
                        dragRange = hitRange;
                        dragOriginal = working.emitters[emitterIndex].activeRanges[hitRange];
                        dragStartTime = XToTimeRaw(mouse.x);
                        timelineDrag = zone < 0 ? TimelineDrag.ResizeStart : zone > 0 ? TimelineDrag.ResizeEnd : TimelineDrag.MoveRange;
                    }
                    else if (emitterIndex >= 0 && evt.clickCount == 2)
                    {
                        AddRangeAt(emitterIndex, SnapTime(XToTime(mouse.x)));
                        evt.Use();
                        break;
                    }
                    else
                    {
                        selectedRange = -1;
                        timelineDrag = TimelineDrag.Scrub;
                        playing = false;
                        SetTime(XToTime(mouse.x), true);
                    }

                    GUIUtility.hotControl = id;
                    timelineDragControl = id;
                    evt.Use();
                    break;
                }

                case EventType.MouseDrag:
                    if (!dragging) break;
                    ApplyTimelineDrag(mouse.x);
                    evt.Use();
                    break;

                case EventType.MouseUp:
                    if (!dragging) break;
                    GUIUtility.hotControl = 0;
                    timelineDrag = TimelineDrag.None;
                    evt.Use();
                    Repaint();
                    break;
            }
        }

        /// <summary>x좌표에 있는 활성 구간의 번호(없으면 -1). zone: -1 = 시작 쪽 가장자리, 1 = 끝 쪽 가장자리, 0 = 가운데.</summary>
        private int HitRange(int emitterIndex, float x, out int zone)
        {
            zone = 0;
            List<PatternTimeRange> ranges = working.emitters[emitterIndex].activeRanges;
            float duration = PatternDuration;

            for (int i = 0; i < ranges.Count; i++)
            {
                float start = Mathf.Clamp(ranges[i].start, 0f, duration);
                float end = Mathf.Clamp(ranges[i].end, 0f, duration);
                if (end <= start) continue;

                float x0 = TimeToX(start);
                float x1 = Mathf.Max(x0 + 2f, TimeToX(end));
                if (x < x0 - 2f || x > x1 + 2f) continue;

                float edge = Mathf.Min(EdgeGrab, (x1 - x0) / 3f);
                zone = x <= x0 + edge ? -1 : x >= x1 - edge ? 1 : 0;
                return i;
            }
            return -1;
        }

        private void ApplyTimelineDrag(float mouseX)
        {
            switch (timelineDrag)
            {
                case TimelineDrag.Scrub:
                    SetTime(XToTime(mouseX), true);
                    break;

                case TimelineDrag.Duration:
                {
                    float duration = Mathf.Max(BossPattern.MinDuration, SnapTime(XToTimeRaw(mouseX)));
                    if (Mathf.Approximately(duration, working.duration)) break;

                    BeginDirectEdit("패턴 길이 변경");
                    working.duration = duration;
                    EndDirectEdit();
                    break;
                }

                case TimelineDrag.MoveRange:
                case TimelineDrag.ResizeStart:
                case TimelineDrag.ResizeEnd:
                {
                    if (dragEmitter < 0 || dragEmitter >= working.emitters.Count) break;
                    List<PatternTimeRange> ranges = working.emitters[dragEmitter].activeRanges;
                    if (dragRange < 0 || dragRange >= ranges.Count) break;

                    // 같은 발사 지점의 이웃 구간과 겹치지 않게 한다(겹치면 그 구간에서 두 번 발사된다)
                    float min = dragRange > 0 ? ranges[dragRange - 1].end : 0f;
                    float max = dragRange < ranges.Count - 1 ? ranges[dragRange + 1].start : Mathf.Max(working.duration, dragOriginal.end);
                    float minLength = 1f / frameRate;
                    float delta = XToTimeRaw(mouseX) - dragStartTime;

                    PatternTimeRange range = dragOriginal;
                    if (timelineDrag == TimelineDrag.MoveRange)
                    {
                        float length = range.end - range.start;
                        range.start = Mathf.Clamp(SnapTime(range.start + delta), min, Mathf.Max(min, max - length));
                        range.end = range.start + length;
                    }
                    else if (timelineDrag == TimelineDrag.ResizeStart)
                    {
                        range.start = Mathf.Clamp(SnapTime(range.start + delta), min, Mathf.Max(min, range.end - minLength));
                    }
                    else
                    {
                        range.end = Mathf.Clamp(SnapTime(range.end + delta), Mathf.Min(max, range.start + minLength), max);
                    }

                    PatternTimeRange current = ranges[dragRange];
                    if (Mathf.Approximately(current.start, range.start) && Mathf.Approximately(current.end, range.end)) break;

                    BeginDirectEdit("활성 구간 편집");
                    ranges[dragRange] = range;
                    EndDirectEdit();
                    break;
                }
            }
        }

        private void ShowTrackMenu(int emitterIndex, int rangeIndex, float clickTime)
        {
            var menu = new GenericMenu();

            if (emitterIndex < 0)
            {
                menu.AddDisabledItem(new GUIContent("보스 본체 이동은 오른쪽 패널에서 편집합니다"));
            }
            else if (rangeIndex >= 0)
            {
                PatternTimeRange range = working.emitters[emitterIndex].activeRanges[rangeIndex];
                float split = SnapTime(time);

                menu.AddItem(new GUIContent("이 구간 삭제"), false, () => DeleteRange(emitterIndex, rangeIndex));
                if (split > range.start + 1e-3f && split < range.end - 1e-3f)
                    menu.AddItem(new GUIContent("재생 위치에서 둘로 나누기"), false, () => SplitRange(emitterIndex, rangeIndex, split));
                else
                    menu.AddDisabledItem(new GUIContent("재생 위치에서 둘로 나누기"));
            }
            else
            {
                float at = SnapTime(clickTime);
                menu.AddItem(new GUIContent("여기에 활성 구간 추가"), false, () => AddRangeAt(emitterIndex, at));
            }

            menu.ShowAsContext();
        }

        // ───────────── 활성 구간 편집 ─────────────

        /// <summary>at 시각부터 시작하는 활성 구간을 추가한다(다음 구간이나 패턴 끝을 넘지 않는 최대 1초).</summary>
        private void AddRangeAt(int emitterIndex, float at)
        {
            if (emitterIndex < 0 || emitterIndex >= working.emitters.Count) return;

            PatternEmitter emitter = working.emitters[emitterIndex];
            float duration = working.duration;
            at = Mathf.Clamp(at, 0f, duration);
            if (emitter.FindRange(at) >= 0) return;

            float limit = duration;
            for (int i = 0; i < emitter.activeRanges.Count; i++)
            {
                float start = emitter.activeRanges[i].start;
                if (start > at && start < limit) limit = start;
            }

            float end = Mathf.Min(limit, at + 1f);
            if (end - at < 0.5f / frameRate) return;

            BeginDirectEdit("활성 구간 추가");
            var added = new PatternTimeRange(at, end);
            emitter.activeRanges.Add(added);
            emitter.activeRanges.Sort((a, b) => a.start.CompareTo(b.start));
            EndDirectEdit();

            selectedEmitter = emitterIndex;
            selectedRange = emitter.activeRanges.FindIndex(r => Mathf.Approximately(r.start, added.start) && Mathf.Approximately(r.end, added.end));
        }

        private void DeleteRange(int emitterIndex, int rangeIndex)
        {
            if (emitterIndex < 0 || emitterIndex >= working.emitters.Count) return;
            List<PatternTimeRange> ranges = working.emitters[emitterIndex].activeRanges;
            if (rangeIndex < 0 || rangeIndex >= ranges.Count) return;

            BeginDirectEdit("활성 구간 삭제");
            ranges.RemoveAt(rangeIndex);
            EndDirectEdit();
            selectedRange = -1;
        }

        private void SplitRange(int emitterIndex, int rangeIndex, float at)
        {
            if (emitterIndex < 0 || emitterIndex >= working.emitters.Count) return;
            List<PatternTimeRange> ranges = working.emitters[emitterIndex].activeRanges;
            if (rangeIndex < 0 || rangeIndex >= ranges.Count) return;

            PatternTimeRange range = ranges[rangeIndex];
            if (at <= range.start || at >= range.end) return;

            BeginDirectEdit("활성 구간 나누기");
            ranges[rangeIndex] = new PatternTimeRange(range.start, at);
            ranges.Insert(rangeIndex + 1, new PatternTimeRange(at, range.end));
            EndDirectEdit();
        }

        // ───────────── 그리기 ─────────────

        private void PaintRuler(Rect ruler)
        {
            EditorGUI.DrawRect(ruler, RulerBackground);
            EditorGUI.DrawRect(new Rect(ruler.x, ruler.yMax - 1f, ruler.width, 1f), SeparatorColor);

            GUI.Label(new Rect(ruler.x + 8f, ruler.y + 3f, TrackLabelWidth - 8f, 18f),
                time.ToString("0.000") + "초  ·  프레임 " + CurrentFrame, trackLabelStyle);

            float major = MajorStep(timelineSpan, timeAxisWidth);
            int subdivisions = 5;
            float minor = major / subdivisions;
            if (minor / timelineSpan * timeAxisWidth < 5f)
            {
                subdivisions = 1;
                minor = major;
            }

            int ticks = Mathf.FloorToInt(timelineSpan / minor + 1e-3f);
            for (int i = 0; i <= ticks; i++)
            {
                float t = i * minor;
                float x = Mathf.Round(TimeToX(t));
                bool isMajor = i % subdivisions == 0;
                float height = isMajor ? 9f : 4f;
                EditorGUI.DrawRect(new Rect(x, ruler.yMax - 1f - height, 1f, height), RulerTick);
                if (isMajor) GUI.Label(new Rect(x + 3f, ruler.y + 1f, 60f, 14f), t.ToString("0.##"), rulerLabelStyle);
            }

            // 패턴이 끝나는 시각(드래그해 패턴 길이를 바꾼다)
            float endX = Mathf.Round(TimeToX(PatternDuration));
            EditorGUI.DrawRect(new Rect(endX, ruler.y, 2f, ruler.height), EndMarker);
            var flag = new Rect(endX + 2f, ruler.y + 1f, 20f, 13f);
            EditorGUI.DrawRect(flag, EndMarker);
            GUI.Label(flag, "끝", flagStyle);
            EditorGUIUtility.AddCursorRect(new Rect(endX - 6f, ruler.y, 12f, ruler.height), MouseCursor.ResizeHorizontal);
        }

        private static float MajorStep(float span, float width)
        {
            float[] candidates = { 0.02f, 0.05f, 0.1f, 0.2f, 0.5f, 1f, 2f, 5f, 10f, 20f, 30f, 60f };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] / span * width >= 64f) return candidates[i];
            }
            return candidates[candidates.Length - 1];
        }

        /// <param name="axisX">스크롤 영역 안에서의 시간 축 시작 x좌표</param>
        private void PaintTracks(Rect content, int rows, float axisX)
        {
            float duration = PatternDuration;
            float scale = timeAxisWidth / timelineSpan;

            for (int row = 0; row < rows; row++)
            {
                int emitterIndex = row - 1;
                var rowRect = new Rect(0f, row * TrackHeight, content.width, TrackHeight);
                bool selected = selectedEmitter == emitterIndex;

                EditorGUI.DrawRect(rowRect, selected ? TrackSelected : (row % 2 == 0 ? TrackEven : TrackOdd));
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), TrackLine);

                var labelRect = new Rect(8f, rowRect.y + 3f, TrackLabelWidth - 12f, 18f);
                if (emitterIndex < 0)
                {
                    GUI.Label(labelRect, "보스 본체 이동", trackLabelStyle);
                    PaintBodyTrack(rowRect, axisX, scale, duration);
                }
                else
                {
                    PatternEmitter emitter = working.emitters[emitterIndex];
                    Color swatch = emitter.color;
                    swatch.a = 1f;
                    EditorGUI.DrawRect(new Rect(labelRect.x, rowRect.y + 7f, 10f, 10f), swatch);
                    GUI.Label(new Rect(labelRect.x + 16f, labelRect.y, labelRect.width - 16f, labelRect.height),
                        string.IsNullOrEmpty(emitter.name) ? "(이름 없음)" : emitter.name, trackLabelStyle);
                    PaintEmitterTrack(emitterIndex, emitter, rowRect, axisX, scale, duration);
                }
            }

            EditorGUI.DrawRect(new Rect(TrackLabelWidth, 0f, 1f, content.height), TrackLine);

            // 패턴이 끝난 뒤(총알이 화면을 벗어날 때까지)의 구간은 어둡게
            float endX = axisX + duration * scale;
            float spanX = axisX + timeAxisWidth + TrackPadding;
            if (spanX > endX + 1f) EditorGUI.DrawRect(new Rect(endX, 0f, spanX - endX, content.height), TailShade);
            EditorGUI.DrawRect(new Rect(Mathf.Round(endX), 0f, 1f, content.height), new Color(EndMarker.r, EndMarker.g, EndMarker.b, 0.6f));
        }

        private void PaintBodyTrack(Rect rowRect, float axisX, float scale, float duration)
        {
            PatternPath path = working.bodyPath;
            if (!path.IsMoving) return;

            float start = Mathf.Clamp(path.startTime, 0f, duration);
            float end = MotionEnd(path, duration);
            if (end <= start) return;

            var clip = new Rect(axisX + start * scale, rowRect.y + 5f, Mathf.Max(2f, (end - start) * scale), TrackHeight - 11f);
            EditorGUI.DrawRect(clip, BodyClipColor);

            // 편도 이동이 끝나는(방향이 바뀌는) 시각
            float legPixels = path.travelTime * scale;
            if (path.repeat != PathRepeat.Once && legPixels >= 4f)
            {
                var tick = new Color(0f, 0f, 0f, 0.45f);
                for (int k = 1; k < 2000; k++)
                {
                    float t = path.startTime + k * path.travelTime - path.timeOffset;
                    if (t >= end) break;
                    if (t <= start) continue;
                    EditorGUI.DrawRect(new Rect(Mathf.Round(axisX + t * scale), clip.y + 1f, 1f, clip.height - 2f), tick);
                }
            }
        }

        /// <summary>경로 이동이 끝나는 시각(패턴 길이를 넘지 않는다).</summary>
        private static float MotionEnd(PatternPath path, float duration)
        {
            if (path.repeat != PathRepeat.Once) return duration;
            return Mathf.Clamp(path.startTime + Mathf.Max(0f, path.travelTime - path.timeOffset), 0f, duration);
        }

        private void PaintEmitterTrack(int emitterIndex, PatternEmitter emitter, Rect rowRect, float axisX, float scale, float duration)
        {
            Color fill = Color.Lerp(emitter.color, Color.black, 0.22f);
            fill.a = 1f;
            double interval = 1.0 / Mathf.Clamp(emitter.fireRate, PatternEmitter.MinFireRate, PatternEmitter.MaxFireRate);
            float shotPixels = (float)interval * scale;

            for (int i = 0; i < emitter.activeRanges.Count; i++)
            {
                PatternTimeRange range = emitter.activeRanges[i];
                float start = Mathf.Clamp(range.start, 0f, duration);
                float end = Mathf.Clamp(range.end, 0f, duration);
                if (end <= start) continue;

                var clip = new Rect(axisX + start * scale, rowRect.y + 4f, Mathf.Max(2f, (end - start) * scale), TrackHeight - 10f);
                EditorGUI.DrawRect(clip, fill);

                if (shotPixels >= 3f)
                {
                    // 발사 시각마다 눈금 1개
                    var tick = new Color(0f, 0f, 0f, 0.5f);
                    double first = (double)range.start + emitter.firstShotDelay;
                    double limit = (double)end - BossPattern.EndEpsilon;
                    for (int k = 0; k < 4000; k++)
                    {
                        double t = first + k * interval;
                        if (t >= limit) break;
                        if (t < start) continue;
                        EditorGUI.DrawRect(new Rect(Mathf.Round(axisX + (float)t * scale), clip.y + 2f, 1f, clip.height - 4f), tick);
                    }
                }
                else
                {
                    // 눈금을 그리기엔 너무 촘촘한 연사
                    EditorGUI.DrawRect(new Rect(clip.x + 1f, clip.y + clip.height * 0.5f - 1f, Mathf.Max(0f, clip.width - 2f), 2f), new Color(0f, 0f, 0f, 0.35f));
                }

                bool selected = selectedEmitter == emitterIndex && selectedRange == i;
                DrawOutline(clip, selected ? Color.white : new Color(1f, 1f, 1f, 0.28f));

                float edge = Mathf.Min(EdgeGrab, clip.width / 3f);
                EditorGUIUtility.AddCursorRect(new Rect(clip.x - 2f, clip.y, edge + 2f, clip.height), MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(new Rect(clip.xMax - edge, clip.y, edge + 2f, clip.height), MouseCursor.ResizeHorizontal);
            }

            // 발사 지점이 경로를 따라 움직이는 동안은 아래쪽에 얇은 선으로 표시
            if (emitter.path.IsMoving)
            {
                float start = Mathf.Clamp(emitter.path.startTime, 0f, duration);
                float end = MotionEnd(emitter.path, duration);
                if (end > start)
                {
                    Color line = emitter.color;
                    line.a = 0.75f;
                    EditorGUI.DrawRect(new Rect(axisX + start * scale, rowRect.yMax - 5f, (end - start) * scale, 2f), line);
                }
            }
        }

        private void PaintPlayhead(Rect ruler, Rect tracks)
        {
            float x = Mathf.Round(TimeToX(Mathf.Clamp(time, 0f, timelineSpan)));
            EditorGUI.DrawRect(new Rect(x, ruler.y, 1f, ruler.height + tracks.height), PlayheadColor);
            EditorGUI.DrawRect(new Rect(x - 4f, ruler.y, 9f, 7f), PlayheadColor);
        }
    }
}
