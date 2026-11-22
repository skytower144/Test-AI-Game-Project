using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Chopper.EditorTools
{
    // 오른쪽 설정 패널: 패턴 전체 설정, 발사 지점 목록, 선택한 발사 지점의 설정, 미리보기 설정, 보스의 패턴 목록.
    public sealed partial class BossPatternEditorWindow
    {
        // 열거형 표시 이름(선언 순서와 같아야 한다)
        private static readonly string[] BoosterNames = { "약하게", "보통", "최대" };
        private static readonly string[] SpaceNames = { "보스의 눈", "화면 중심" };
        private static readonly string[] ShapeNames = { "고정(움직이지 않음)", "직선", "포물선" };
        private static readonly string[] RepeatNames = { "한 번만", "처음부터 반복", "왕복" };
        private static readonly string[] EaseNames = { "일정한 속도", "부드럽게(가감속)", "점점 빠르게", "점점 느리게" };
        private static readonly string[] AimNames = { "고정 각도", "플레이어 조준" };
        private static readonly string[] LayoutNames = { "부채꼴", "전방위(360도)" };

        private const string HelpText =
            "재생\n" +
            "  Space = 재생/정지,  ← → = 한 프레임 이동(Shift = 10프레임),  Home / End = 처음 / 패턴 끝\n\n" +
            "타임라인\n" +
            "  · 눈금자나 트랙의 빈 곳을 드래그하면 그 프레임으로 이동합니다.\n" +
            "  · 색 막대 = 발사 지점이 켜져 있는 구간. 가운데를 끌면 이동, 양 끝을 끌면 길이 조절.\n" +
            "  · 트랙의 빈 곳을 더블클릭하면 구간 추가, 구간을 우클릭하면 삭제/나누기, Delete = 선택한 구간 삭제.\n" +
            "  · 눈금자의 노란 '끝' 표시를 끌면 패턴의 총 소요 시간이 바뀝니다.\n" +
            "  · 어두운 부분은 패턴이 끝난 뒤 총알이 화면을 벗어날 때까지의 시간입니다.\n\n" +
            "미리보기\n" +
            "  · 발사 지점(●)을 끌면 위치가 바뀝니다. 이동 경로가 있으면 □ 시작, ○ 도착, ◇ 포물선 기울기를 끌 수 있습니다.\n" +
            "  · 헬리콥터는 가상 플레이어입니다. '플레이어 조준' 패턴이 이 위치를 겨냥합니다.\n" +
            "  · 점선 밖에서 발사된 총알은 게임에서 바로 사라집니다.\n\n" +
            "저장\n" +
            "  편집 내용은 '저장' 또는 '에셋으로 추출'을 눌러야 패턴 에셋에 반영됩니다.\n\n" +
            "게임에서 테스트\n" +
            "  플레이 모드에서 '게임에서 테스트'를 누르면 보스가 기다림 없이 바로 나타나 이 패턴만 반복합니다.\n" +
            "  값을 고치면 저장하지 않아도 게임에 바로 반영됩니다. 테스트하는 동안 플레이어는 무적입니다(끌 수 있음).\n" +
            "  테스트를 한 판의 점수는 리더보드에 기록되지 않습니다.";

        private static GUIStyle overlayStyle;
        private static GUIStyle overlayRightStyle;
        private static GUIStyle hintStyle;
        private static GUIStyle centeredHintStyle;
        private static GUIStyle trackLabelStyle;
        private static GUIStyle rulerLabelStyle;
        private static GUIStyle flagStyle;
        private static GUIStyle sectionStyle;
        private static GUIStyle groupStyle;
        private static GUIStyle centeredLabelStyle;

        private static readonly Dictionary<string[], GUIContent[]> s_PopupContents = new Dictionary<string[], GUIContent[]>();

        private readonly List<Bullet> bulletOptions = new List<Bullet>();
        private bool bulletOptionsDirty = true;

        private ReorderableList emitterList;
        private SerializedObject bossSerialized;
        private ReorderableList bossList;
        private BossPattern pendingOpen;

        // ───────────── 스타일 / 공통 ─────────────

        private static void InitStyles()
        {
            if (overlayStyle != null) return;

            overlayStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
            SetTextColor(overlayStyle, new Color(0.93f, 0.93f, 0.93f));
            overlayRightStyle = new GUIStyle(overlayStyle) { alignment = TextAnchor.UpperRight };

            hintStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip };
            SetTextColor(hintStyle, new Color(1f, 1f, 1f, 0.5f));
            centeredHintStyle = new GUIStyle(hintStyle) { alignment = TextAnchor.UpperCenter };

            trackLabelStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            SetTextColor(trackLabelStyle, new Color(0.9f, 0.9f, 0.9f));

            rulerLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperLeft };
            SetTextColor(rulerLabelStyle, new Color(1f, 1f, 1f, 0.62f));

            flagStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9, padding = new RectOffset(0, 0, 0, 0) };
            SetTextColor(flagStyle, Color.black);

            sectionStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
            groupStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, alignment = TextAnchor.LowerLeft };
            centeredLabelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
        }

        private static void SetTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
        }

        private static void Section(string title)
        {
            GUILayout.Space(8f);
            Rect rect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.075f) : new Color(0f, 0f, 0f, 0.1f));
            GUI.Label(new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height), title, sectionStyle);
            GUILayout.Space(2f);
        }

        private static void Group(string title)
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField(title, groupStyle);
        }

        /// <summary>한글 라벨 + 필드의 [Tooltip]으로 그리는 PropertyField.</summary>
        private static void Field(SerializedProperty property, string label)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
        }

        // 아래 두 가지는 데이터 클래스의 [Header]/[Range] 장식을 그리지 않는 숫자 필드다
        // (인스펙터용 영문 머리글이 이 패널의 한글 머리글과 겹쳐 보이지 않게 한다).
        private static void NumberField(SerializedProperty property, string label)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            var content = new GUIContent(label, property.tooltip);
            EditorGUI.BeginProperty(rect, content, property);
            EditorGUI.BeginChangeCheck();
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                int value = EditorGUI.IntField(rect, content, property.intValue);
                if (EditorGUI.EndChangeCheck()) property.intValue = value;
            }
            else
            {
                float value = EditorGUI.FloatField(rect, content, property.floatValue);
                if (EditorGUI.EndChangeCheck()) property.floatValue = value;
            }
            EditorGUI.EndProperty();
        }

        private static void EnumField(SerializedProperty property, string label, string[] names)
        {
            if (!s_PopupContents.TryGetValue(names, out GUIContent[] options))
            {
                options = new GUIContent[names.Length];
                for (int i = 0; i < names.Length; i++) options[i] = new GUIContent(names[i]);
                s_PopupContents.Add(names, options);
            }

            Rect rect = EditorGUILayout.GetControlRect();
            var content = new GUIContent(label, property.tooltip);
            EditorGUI.BeginProperty(rect, content, property);
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.Popup(rect, content, property.enumValueIndex, options);
            if (EditorGUI.EndChangeCheck()) property.enumValueIndex = value;
            EditorGUI.EndProperty();
        }

        // ───────────── 패널 ─────────────

        private void DrawSettings(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), SeparatorColor);

            GUILayout.BeginArea(new Rect(rect.x + 1f, rect.y, rect.width - 1f, rect.height));
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);

            float savedLabelWidth = EditorGUIUtility.labelWidth;
            bool savedWideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.labelWidth = 134f;
            EditorGUIUtility.wideMode = true;
            try
            {
                if (showHelp)
                {
                    Section("도움말");
                    EditorGUILayout.HelpBox(HelpText, MessageType.None);
                }

                DrawPatternSection();
                DrawEmitterListSection();
                DrawSelectedEmitterSection();
                DrawGameTestSection();
                DrawPreviewSection();
                DrawBossSection();
                GUILayout.Space(10f);
            }
            finally
            {
                EditorGUIUtility.labelWidth = savedLabelWidth;
                EditorGUIUtility.wideMode = savedWideMode;
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPatternSection()
        {
            Section("패턴");
            Field(serializedPattern.FindProperty("duration"), "총 소요 시간(초)");
            EnumField(serializedPattern.FindProperty("boosters"), "부스터 출력", BoosterNames);
            Field(serializedPattern.FindProperty("loopSound"), "반복 재생 소리");

            GUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("보스 본체 이동", groupStyle);
                if (working.bodyPath.IsMoving &&
                    GUILayout.Button(new GUIContent("미리보기에서 편집", "미리보기에 본체 이동 경로와 핸들을 표시한다"), EditorStyles.miniButton, GUILayout.Width(112f)))
                    SelectEmitter(BodySelection);
            }
            PathFields(serializedPattern.FindProperty("bodyPath"), true);

            if (bakeTruncated)
            {
                EditorGUILayout.HelpBox("총알이 " + MaxBakedBullets + "발을 넘어 미리보기에는 일부만 표시합니다. 발사 속도나 총알 수를 줄이세요.", MessageType.Warning);
            }
            else if (peakBullets > 500)
            {
                EditorGUILayout.HelpBox("화면에 동시에 뜨는 총알이 최대 " + peakBullets + "발입니다. 총알이 많으면 게임의 프레임률이 떨어질 수 있습니다.", MessageType.Warning);
            }
        }

        /// <summary>이동 경로 설정. 발사 지점은 "위치"가 곧 경로의 시작 위치다.</summary>
        private void PathFields(SerializedProperty path, bool isBody)
        {
            SerializedProperty shape = path.FindPropertyRelative("shape");
            SerializedProperty start = path.FindPropertyRelative("start");
            SerializedProperty end = path.FindPropertyRelative("end");

            if (!isBody) Field(start, "위치");

            int before = shape.enumValueIndex;
            EnumField(shape, isBody ? "이동 방식" : "이동 경로", ShapeNames);
            int after = shape.enumValueIndex;

            // 고정에서 이동으로 바꿨는데 도착 위치가 시작 위치와 같으면, 경로가 눈에 보이도록 기본 도착 위치를 준다
            if (before == (int)PathShape.Fixed && after != (int)PathShape.Fixed && end.vector2Value == start.vector2Value)
                end.vector2Value = start.vector2Value + (isBody ? new Vector2(0f, 1f) : new Vector2(-2f, 0f));

            if (after == (int)PathShape.Fixed)
            {
                if (isBody) Field(start, "기준 위치와의 거리");
                return;
            }

            if (isBody) Field(start, "시작 위치");
            Field(end, "도착 위치");
            if (after == (int)PathShape.Parabola) Field(path.FindPropertyRelative("bend"), "포물선 기울기");
            Field(path.FindPropertyRelative("startTime"), "이동 시작 시각(초)");
            Field(path.FindPropertyRelative("travelTime"), "편도 이동 시간(초)");
            EnumField(path.FindPropertyRelative("repeat"), "반복", RepeatNames);
            EnumField(path.FindPropertyRelative("ease"), "속도 변화", EaseNames);
            Field(path.FindPropertyRelative("timeOffset"), "미리 진행(초)");
        }

        // ───────────── 발사 지점 목록 ─────────────

        private void DrawEmitterListSection()
        {
            Section("발사 지점 목록");

            if (emitterList == null || emitterList.serializedProperty == null ||
                emitterList.serializedProperty.serializedObject != serializedPattern)
                emitterList = CreateEmitterList();

            emitterList.index = selectedEmitter;
            emitterList.DoLayoutList();

            using (new EditorGUI.DisabledScope(selectedEmitter < 0))
            {
                if (GUILayout.Button(new GUIContent("선택한 발사 지점 복제", "선택한 발사 지점과 같은 설정의 발사 지점을 하나 더 만든다"), EditorStyles.miniButton))
                {
                    DuplicateEmitter(selectedEmitter);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private ReorderableList CreateEmitterList()
        {
            var list = new ReorderableList(serializedPattern, serializedPattern.FindProperty("emitters"), true, true, true, true);
            list.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "색 · 이름 (드래그해 순서 변경)");
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                if (index >= list.serializedProperty.arraySize) return;
                SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
                SerializedProperty color = element.FindPropertyRelative("color");

                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;
                var colorRect = new Rect(rect.x, rect.y, 34f, rect.height);
                var nameRect = new Rect(rect.x + 40f, rect.y, rect.width - 40f - 80f, rect.height);
                var infoRect = new Rect(rect.xMax - 76f, rect.y, 76f, rect.height);

                EditorGUI.BeginChangeCheck();
                Color picked = EditorGUI.ColorField(colorRect, GUIContent.none, color.colorValue, false, false, false);
                if (EditorGUI.EndChangeCheck())
                {
                    picked.a = 1f;
                    color.colorValue = picked;
                }

                EditorGUI.PropertyField(nameRect, element.FindPropertyRelative("name"), GUIContent.none);
                GUI.Label(infoRect, element.FindPropertyRelative("fireRate").floatValue.ToString("0.##") + "발/초", EditorStyles.miniLabel);
            };
            list.onSelectCallback = l => SelectEmitter(l.index);
            list.onAddCallback = l => AddEmitter();
            list.onRemoveCallback = l => RemoveEmitter(l.index);
            list.onReorderCallbackWithDetails = (l, oldIndex, newIndex) =>
            {
                selectedEmitter = newIndex;
                selectedRange = -1;
            };
            return list;
        }

        private void AddEmitter()
        {
            BeginDirectEdit("발사 지점 추가");
            working.emitters.Add(NewEmitter(working));
            EndDirectEdit();
            SelectEmitter(working.emitters.Count - 1);
            GUIUtility.ExitGUI();
        }

        private void RemoveEmitter(int index)
        {
            if (index < 0 || index >= working.emitters.Count) return;

            BeginDirectEdit("발사 지점 삭제");
            working.emitters.RemoveAt(index);
            EndDirectEdit();
            selectedRange = -1;
            selectedEmitter = Mathf.Min(index, working.emitters.Count - 1);
            GUIUtility.ExitGUI();
        }

        private void DuplicateEmitter(int index)
        {
            if (index < 0 || index >= working.emitters.Count) return;

            PatternEmitter original = working.emitters[index];
            PatternEmitter copy = JsonUtility.FromJson<PatternEmitter>(JsonUtility.ToJson(original));
            copy.bulletPrefab = original.bulletPrefab;
            copy.name = original.name + " Copy";
            copy.color = EmitterPalette[working.emitters.Count % EmitterPalette.Length];

            BeginDirectEdit("발사 지점 복제");
            working.emitters.Insert(index + 1, copy);
            EndDirectEdit();
            SelectEmitter(index + 1);
        }

        // ───────────── 선택한 발사 지점 ─────────────

        private void DrawSelectedEmitterSection()
        {
            SerializedProperty emitters = serializedPattern.FindProperty("emitters");
            if (selectedEmitter < 0 || selectedEmitter >= emitters.arraySize || selectedEmitter >= working.emitters.Count)
            {
                Section("발사 지점 설정");
                EditorGUILayout.HelpBox(working.emitters.Count == 0
                    ? "발사 지점이 없습니다. 위 목록의 + 버튼으로 추가하세요."
                    : "목록, 타임라인, 미리보기 중 한 곳에서 발사 지점을 선택하세요.", MessageType.Info);
                return;
            }

            SerializedProperty emitter = emitters.GetArrayElementAtIndex(selectedEmitter);
            PatternEmitter data = working.emitters[selectedEmitter];
            Section("발사 지점 설정: " + data.name);

            // 위치 / 이동 경로
            Group("위치 · 이동 경로");
            EnumField(emitter.FindPropertyRelative("space"), "위치 기준", SpaceNames);
            PathFields(emitter.FindPropertyRelative("path"), false);

            // 발사 속도
            Group("발사 속도");
            SerializedProperty rate = emitter.FindPropertyRelative("fireRate");
            NumberField(rate, "발사 속도(발/초)");

            EditorGUI.BeginChangeCheck();
            float interval = EditorGUILayout.FloatField(
                new GUIContent("발사 간격(초)", "발사 속도를 간격으로 입력할 때 쓴다(발사 간격 = 1 / 발사 속도)"),
                1f / Mathf.Max(PatternEmitter.MinFireRate, rate.floatValue));
            if (EditorGUI.EndChangeCheck() && interval > 0f)
                rate.floatValue = Mathf.Clamp(1f / interval, PatternEmitter.MinFireRate, PatternEmitter.MaxFireRate);

            Field(emitter.FindPropertyRelative("firstShotDelay"), "첫 발 지연(초)");

            // 발사 방향
            Group("발사 방향");
            SerializedProperty aim = emitter.FindPropertyRelative("aim");
            SerializedProperty angle = emitter.FindPropertyRelative("angle");
            int aimBefore = aim.enumValueIndex;
            EnumField(aim, "조준 방식", AimNames);
            if (aim.enumValueIndex != aimBefore)
            {
                // 고정 각도의 기본값(왼쪽 = 180도)과 조준의 기본값(0 = 정확히 조준)을 서로 바꿔 준다
                if (aim.enumValueIndex == (int)PatternAim.AtPlayer && Mathf.Approximately(angle.floatValue, 180f)) angle.floatValue = 0f;
                else if (aim.enumValueIndex == (int)PatternAim.FixedAngle && Mathf.Approximately(angle.floatValue, 0f)) angle.floatValue = 180f;
            }

            bool aiming = aim.enumValueIndex == (int)PatternAim.AtPlayer;
            Field(angle, aiming ? "조준에서 틀기(도)" : "발사 각도(도)");
            Field(emitter.FindPropertyRelative("rotationSpeed"), "회전 속도(도/초)");

            SerializedProperty sweepAngle = emitter.FindPropertyRelative("sweepAngle");
            Field(sweepAngle, "흔들기 폭(도)");
            using (new EditorGUI.DisabledScope(Mathf.Approximately(sweepAngle.floatValue, 0f)))
                Field(emitter.FindPropertyRelative("sweepPeriod"), "흔들기 주기(초)");

            // 한 번에 쏘는 총알
            Group("한 번에 쏘는 총알");
            SerializedProperty count = emitter.FindPropertyRelative("bulletsPerShot");
            SerializedProperty layout = emitter.FindPropertyRelative("layout");
            NumberField(count, "총알 수");
            using (new EditorGUI.DisabledScope(count.intValue <= 1))
            {
                EnumField(layout, "배치", LayoutNames);
                if (layout.enumValueIndex == (int)ShotLayout.Fan)
                    Field(emitter.FindPropertyRelative("spreadStep"), "총알 사이 각도(도)");
                else
                    EditorGUILayout.LabelField(new GUIContent("총알 사이 각도(도)", "전방위 배치에서는 360도를 총알 수로 나눈 값"),
                        new GUIContent((360f / Mathf.Max(1, count.intValue)).ToString("0.##")));
            }

            // 총알
            Group("총알");
            BulletTypeField(emitter.FindPropertyRelative("bulletPrefab"));
            Field(emitter.FindPropertyRelative("bulletSpeed"), "총알 속도(유닛/초)");

            // 활성 구간
            Group("활성 구간 (타임라인에서 켜져 있는 시간)");
            DrawRangeRows(emitter.FindPropertyRelative("activeRanges"), data);

            // 요약 / 경고
            if (selectedEmitter < emitterShots.Length)
            {
                EditorGUILayout.HelpBox("이 발사 지점: 발사 " + emitterShots[selectedEmitter] + "회  ·  총알 " + emitterBullets[selectedEmitter] + "발",
                    MessageType.None);

                if (emitterLost[selectedEmitter] > 0)
                {
                    EditorGUILayout.HelpBox("총알 " + emitterLost[selectedEmitter] + "발이 화면 밖(미리보기의 점선 바깥)에서 발사되어 게임에서 바로 사라집니다.",
                        MessageType.Warning);
                }
            }

            if (data.activeRanges.Count == 0)
            {
                EditorGUILayout.HelpBox("활성 구간이 없어서 발사하지 않습니다. '구간 추가'를 누르거나 타임라인의 빈 곳을 더블클릭하세요.", MessageType.Warning);
            }
            else
            {
                for (int i = 0; i < data.activeRanges.Count; i++)
                {
                    if (data.activeRanges[i].end <= working.duration + 1e-4f) continue;
                    EditorGUILayout.HelpBox("패턴의 총 소요 시간을 넘는 구간은 패턴이 끝나는 시각까지만 발사합니다.", MessageType.Info);
                    break;
                }
            }
        }

        private void DrawRangeRows(SerializedProperty ranges, PatternEmitter data)
        {
            for (int i = 0; i < ranges.arraySize; i++)
            {
                SerializedProperty range = ranges.GetArrayElementAtIndex(i);
                Rect row = EditorGUILayout.GetControlRect();
                if (selectedRange == i)
                    EditorGUI.DrawRect(new Rect(row.x - 2f, row.y - 1f, row.width + 4f, row.height + 2f), new Color(0.3f, 0.55f, 1f, 0.22f));

                const float labelWidth = 52f, tildeWidth = 18f, unitWidth = 20f, buttonWidth = 24f;
                float fieldWidth = Mathf.Max(30f, (row.width - labelWidth - tildeWidth - unitWidth - buttonWidth - 6f) * 0.5f);
                float x = row.x;

                GUI.Label(new Rect(x, row.y, labelWidth, row.height), "구간 " + (i + 1));
                x += labelWidth;
                EditorGUI.PropertyField(new Rect(x, row.y, fieldWidth, row.height), range.FindPropertyRelative("start"), GUIContent.none);
                x += fieldWidth;
                GUI.Label(new Rect(x, row.y, tildeWidth, row.height), "~", centeredLabelStyle);
                x += tildeWidth;
                EditorGUI.PropertyField(new Rect(x, row.y, fieldWidth, row.height), range.FindPropertyRelative("end"), GUIContent.none);
                x += fieldWidth + 3f;
                GUI.Label(new Rect(x, row.y, unitWidth, row.height), "초");

                if (GUI.Button(new Rect(row.xMax - buttonWidth, row.y, buttonWidth, row.height), new GUIContent("-", "이 구간 삭제"), EditorStyles.miniButton))
                {
                    DeleteRange(selectedEmitter, i);
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("구간 추가", "마지막 구간 뒤(없으면 재생 위치)에 활성 구간을 추가한다"), EditorStyles.miniButton))
                {
                    int before = data.activeRanges.Count;
                    float at = before > 0 ? data.activeRanges[before - 1].end : 0f;
                    if (at >= working.duration - 0.5f / frameRate) at = SnapTime(PatternTime);
                    AddRangeAt(selectedEmitter, at);
                    if (data.activeRanges.Count == before)
                        ShowNotification(new GUIContent("그 시각에는 이미 활성 구간이 있습니다.\n타임라인의 빈 곳을 더블클릭해 추가하세요."));
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("패턴 전체로", "활성 구간을 패턴의 처음부터 끝까지 하나로 만든다"), EditorStyles.miniButton))
                {
                    BeginDirectEdit("활성 구간 편집");
                    data.activeRanges.Clear();
                    data.activeRanges.Add(new PatternTimeRange(0f, working.duration));
                    EndDirectEdit();
                    selectedRange = 0;
                    GUIUtility.ExitGUI();
                }
            }
        }

        /// <summary>
        /// 총알 타입 선택. 프로젝트의 적 총알 프리팹(Bullet 컴포넌트, Team = Enemy)을 모두 찾아 목록으로 보여 주므로,
        /// 새 총알 프리팹을 만들면 코드를 고치지 않아도 여기에 나타난다.
        /// </summary>
        private void BulletTypeField(SerializedProperty property)
        {
            if (bulletOptionsDirty) RefreshBulletOptions();

            var current = property.objectReferenceValue as Bullet;
            var names = new List<GUIContent>(bulletOptions.Count + 2);
            var values = new List<Bullet>(bulletOptions.Count + 2);

            names.Add(new GUIContent(bossDefaultBullet != null ? "보스 기본 총알 (" + bossDefaultBullet.name + ")" : "보스 기본 총알"));
            values.Add(null);
            for (int i = 0; i < bulletOptions.Count; i++)
            {
                if (bulletOptions[i] == null) continue;
                names.Add(new GUIContent(bulletOptions[i].name));
                values.Add(bulletOptions[i]);
            }

            int index = 0;
            if (current != null)
            {
                index = values.IndexOf(current);
                if (index < 0)
                {
                    names.Add(new GUIContent(current.name));
                    values.Add(current);
                    index = values.Count - 1;
                }
            }

            Rect rect = EditorGUILayout.GetControlRect();
            var popupRect = new Rect(rect.x, rect.y, rect.width - 44f, rect.height);
            var findRect = new Rect(rect.xMax - 40f, rect.y, 40f, rect.height);
            var label = new GUIContent("총알 타입",
                "발사할 총알 프리팹. 프로젝트에 적 총알 프리팹(Bullet 컴포넌트, Team = Enemy)을 추가하면 이 목록에 자동으로 나타난다");

            EditorGUI.BeginProperty(popupRect, label, property);
            EditorGUI.BeginChangeCheck();
            int chosen = EditorGUI.Popup(popupRect, label, index, names.ToArray());
            if (EditorGUI.EndChangeCheck()) property.objectReferenceValue = values[chosen];
            EditorGUI.EndProperty();

            Bullet shown = values[chosen] != null ? values[chosen] : bossDefaultBullet;
            using (new EditorGUI.DisabledScope(shown == null))
            {
                if (GUI.Button(findRect, new GUIContent("찾기", "Project 창에서 이 총알 프리팹을 보여 준다"), EditorStyles.miniButton))
                    EditorGUIUtility.PingObject(shown);
            }
        }

        private void RefreshBulletOptions()
        {
            bulletOptionsDirty = false;
            bulletOptions.Clear();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.TryGetComponent(out Bullet bullet) && bullet.Team == BulletTeam.Enemy)
                    bulletOptions.Add(bullet);
            }
            bulletOptions.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        // ───────────── 게임에서 테스트 ─────────────

        private void DrawGameTestSection()
        {
            Section("게임에서 테스트");

            bool inPlayMode = EditorApplication.isPlaying;
            bool testing = IsTestingInGame;

            EditorGUI.BeginChangeCheck();
            float delay = EditorGUILayout.FloatField(
                new GUIContent("반복 간격(초)", "패턴이 끝난 뒤 다시 시작하기까지의 대기 시간"), testRepeatDelay);
            bool invincible = EditorGUILayout.Toggle(
                new GUIContent("플레이어 무적", "테스트하는 동안 플레이어가 피해를 받지 않는다(총알과 몬스터가 맞지 않고 그대로 지나간다)"),
                testPlayerInvincible);
            if (EditorGUI.EndChangeCheck())
            {
                testRepeatDelay = Mathf.Max(0f, delay);
                testPlayerInvincible = invincible;
                if (testing) ApplyGameTest();
            }

            using (new EditorGUI.DisabledScope(!inPlayMode))
            {
                var content = new GUIContent(testing ? "■ 테스트 중지" : "▶ 게임에서 이 패턴만 반복 실행", GameTestTooltip);
                if (GUILayout.Button(content, GUILayout.Height(26f))) SetGameTest(!testing);
            }

            string message;
            if (!inPlayMode) message = "플레이 모드에서 쓸 수 있습니다. 게임을 실행한 뒤 누르세요.";
            else if (testing) message = "보스가 이 패턴만 반복하고 있습니다. 값을 고치면 저장하지 않아도 게임에 바로 반영됩니다." +
                                        (testPlayerInvincible ? " 플레이어는 무적입니다." : string.Empty);
            else message = "누르면 보스가 기다림 없이 바로 나타나 이 패턴만 반복합니다.";
            EditorGUILayout.HelpBox(message + "\n테스트를 한 판의 점수는 리더보드에 기록되지 않습니다.", MessageType.None);
        }

        // ───────────── 미리보기 설정 ─────────────

        private void DrawPreviewSection()
        {
            Section("미리보기 설정");

            EditorGUI.BeginChangeCheck();
            Vector2 player = EditorGUILayout.Vector2Field(
                new GUIContent("가상 플레이어 위치", "'플레이어 조준' 패턴이 겨냥하는 위치. 미리보기에서 헬리콥터를 끌어도 된다"), playerPosition);
            if (EditorGUI.EndChangeCheck())
            {
                player.x = Mathf.Clamp(player.x, -ScreenHalfWidth, ScreenHalfWidth);
                player.y = Mathf.Clamp(player.y, -ScreenHalfHeight, ScreenHalfHeight);
                playerPosition = player;
            }

            showGrid = EditorGUILayout.Toggle(new GUIContent("격자 표시", "1유닛 간격의 격자를 그린다"), showGrid);
            showHitCircles = EditorGUILayout.Toggle(new GUIContent("총알 판정 크기 표시", "총알마다 충돌 판정 원을 함께 그린다"), showHitCircles);

            EditorGUI.BeginChangeCheck();
            bossPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("보스 프리팹", "미리보기에 그릴 보스이자, 아래 패턴 목록을 편집할 대상"), bossPrefab, typeof(GameObject), false);
            playerPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("플레이어 프리팹", "가상 플레이어의 모습과 피격 판정 크기를 읽어 올 프리팹"), playerPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                infoDirty = true;
                bossSerialized = null;
                bossList = null;
            }
        }

        // ───────────── 보스의 패턴 목록 ─────────────

        private void DrawBossSection()
        {
            Section("보스의 행동 패턴 목록");

            Boss boss = TargetBoss;
            if (boss == null)
            {
                EditorGUILayout.HelpBox("Boss 컴포넌트가 있는 프리팹을 찾지 못했습니다. 위의 '보스 프리팹'을 지정하세요.", MessageType.Warning);
                return;
            }

            if (bossSerialized == null || bossSerialized.targetObject != boss)
            {
                bossSerialized = new SerializedObject(boss);
                bossList = null;
            }
            bossSerialized.Update();

            if (bossList == null) bossList = CreateBossList();
            bossList.DoLayoutList();

            if (bossSerialized.ApplyModifiedProperties())
            {
                SaveBossPrefab(boss);
                infoDirty = true;
            }

            bool listed = false;
            if (source != null)
            {
                foreach (BossPattern pattern in boss.Patterns)
                {
                    if (pattern == source) listed = true;
                }
            }

            using (new EditorGUI.DisabledScope(listed))
            {
                if (GUILayout.Button(new GUIContent(listed ? "현재 패턴은 이미 목록에 있습니다" : "현재 패턴을 목록 끝에 추가",
                        "지금 편집 중인 패턴 에셋을 보스의 패턴 목록에 추가한다")))
                {
                    AddToBoss();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.HelpBox("게임에서 보스는 이 목록의 패턴을 위에서부터 순서대로 실행하고, 끝까지 가면 처음으로 돌아갑니다.\n" +
                                    "(패턴 사이의 대기 시간은 보스 프리팹의 Pattern Wait Range)", MessageType.None);
        }

        private ReorderableList CreateBossList()
        {
            var list = new ReorderableList(bossSerialized, bossSerialized.FindProperty("patterns"), true, true, true, true);
            list.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "실행 순서 (드래그해 순서 변경)");
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                if (index >= list.serializedProperty.arraySize) return;
                SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);

                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;
                var numberRect = new Rect(rect.x, rect.y, 20f, rect.height);
                var fieldRect = new Rect(rect.x + 22f, rect.y, rect.width - 22f - 46f, rect.height);
                var openRect = new Rect(rect.xMax - 42f, rect.y, 42f, rect.height);

                GUI.Label(numberRect, (index + 1).ToString());
                EditorGUI.PropertyField(fieldRect, element, GUIContent.none);

                var pattern = element.objectReferenceValue as BossPattern;
                using (new EditorGUI.DisabledScope(pattern == null || pattern == source))
                {
                    if (GUI.Button(openRect, new GUIContent("열기", "이 패턴을 에디터로 불러온다"), EditorStyles.miniButton))
                        pendingOpen = pattern; // 창을 그리는 도중에는 패턴을 바꾸지 않고, 다음 Update에서 연다
                }
            };
            list.onAddCallback = l =>
            {
                SerializedProperty property = l.serializedProperty;
                property.arraySize++;
                property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = null;
            };
            return list;
        }
    }
}
