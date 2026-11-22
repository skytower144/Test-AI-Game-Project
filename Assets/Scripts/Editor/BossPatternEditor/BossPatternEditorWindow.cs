using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chopper.EditorTools
{
    /// <summary>
    /// 보스 행동 패턴 에디터. 메뉴: Tools > Chopper > Boss Pattern Editor
    ///  - 미리보기: 재생 버튼을 누르면 패턴이 게임 화면에서 어떻게 보이는지 그대로 재생한다.
    ///  - 타임라인: 눈금자를 드래그하거나 프레임 번호를 입력해 원하는 프레임으로 이동하고, 발사 지점의 활성 구간을 편집한다.
    ///  - 추출: 만든 패턴을 ScriptableObject(BossPattern) 에셋으로 저장하고 보스의 패턴 목록에 추가한다.
    ///  - 게임에서 테스트: 플레이 모드에서 보스가 지금 편집 중인 패턴만 반복 실행하게 한다(BossPatternTest).
    /// 편집은 작업용 사본에서 이뤄지며, 저장(추출)할 때 에셋에 반영된다.
    /// </summary>
    public sealed partial class BossPatternEditorWindow : EditorWindow
    {
        private const string WindowTitle = "Boss Pattern Editor";
        private const float SettingsWidth = 352f;

        /// <summary>발사 지점 대신 "보스 본체 이동"이 선택된 상태.</summary>
        private const int BodySelection = -1;

        private const string GameTestTooltip =
            "플레이 모드에서 누르면 보스가 기다림 없이 바로 나타나, 지금 편집 중인 패턴만 반복 실행한다. " +
            "테스트하는 동안 플레이어는 무적이다(오른쪽 패널에서 끌 수 있다). " +
            "다시 누르면 원래의 패턴 목록으로 돌아간다";

        private static readonly float[] SpeedOptions = { 0.1f, 0.25f, 0.5f, 1f, 2f };
        private static readonly string[] SpeedLabels = { "0.1x", "0.25x", "0.5x", "1x", "2x" };
        private static readonly int[] FrameRateOptions = { 30, 60, 120, 144 };
        private static readonly string[] FrameRateLabels = { "30", "60", "120", "144" };

        private static readonly Color[] EmitterPalette =
        {
            new Color(1f, 0.6f, 0.2f), new Color(0.4f, 0.8f, 1f), new Color(0.6f, 1f, 0.4f),
            new Color(1f, 0.4f, 0.4f), new Color(0.85f, 0.55f, 1f), new Color(1f, 0.9f, 0.3f),
        };

        // ───────────── 상태(스크립트 재컴파일/플레이 모드 전환 후에도 유지) ─────────────

        [SerializeField] private BossPattern working;      // 편집 중인 작업용 사본
        [SerializeField] private BossPattern source;       // 불러온(저장할) 에셋. 아직 추출하지 않은 새 패턴이면 null
        [SerializeField] private string savedJson = string.Empty;   // 마지막으로 저장한(불러온) 시점의 작업용 사본
        [SerializeField] private string sourceJson = string.Empty;  // 그 시점의 원본 에셋
        [SerializeField] private float time;
        [SerializeField] private bool loopPlayback = true;
        [SerializeField] private int speedIndex = 3;
        [SerializeField] private int frameRate = 60;
        [SerializeField] private int selectedEmitter;
        [SerializeField] private int selectedRange = -1;
        [SerializeField] private Vector2 playerPosition = new Vector2(-3.3f, 0f);
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private bool showHelp;
        [SerializeField] private bool showGrid = true;
        [SerializeField] private bool showHitCircles;
        [SerializeField] private float testRepeatDelay = BossPatternTest.DefaultRepeatDelay;
        [SerializeField] private bool testPlayerInvincible = true;
        [SerializeField] private Vector2 settingsScroll;
        [SerializeField] private Vector2 timelineScroll;

        private SerializedObject serializedPattern;
        private bool playing;
        private double lastTick;
        private int dataVersion;
        private int dirtyVersion = -1;
        private bool dirty;
        private bool prefabSearchDone;

        // ───────────── 열기 ─────────────

        [MenuItem("Tools/Chopper/Boss Pattern Editor", priority = 0)]
        private static void OpenFromMenu()
        {
            OpenWindow();
        }

        public static BossPatternEditorWindow OpenWindow()
        {
            var window = GetWindow<BossPatternEditorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(980f, 640f);
            window.Show();
            return window;
        }

        /// <summary>패턴 에셋을 에디터에서 연다.</summary>
        public static void OpenPattern(BossPattern asset)
        {
            BossPatternEditorWindow window = OpenWindow();
            if (asset != null && asset != window.source) window.RequestOpen(asset);
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            saveChangesMessage = "저장하지 않은 패턴 변경 사항이 있습니다. 에셋에 저장할까요?";
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            infoDirty = true;
            bulletOptionsDirty = true;
            prefabSearchDone = false;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            playing = false;
        }

        private void OnDestroy()
        {
            if (IsTestingInGame) BossPatternTest.End();
            DestroyWorking();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            // 플레이를 끝낼 때 게임에서 하던 테스트도 함께 끝낸다
            if (change == PlayModeStateChange.ExitingPlayMode) BossPatternTest.End();
            Repaint();
        }

        private void OnFocus()
        {
            // 다른 창(인스펙터 등)에서 프리팹/패턴 에셋을 고쳤을 수 있으므로 미리보기 정보를 다시 읽는다
            infoDirty = true;
            ReloadIfSourceChanged();
        }

        private void OnProjectChanged()
        {
            infoDirty = true;
            bulletOptionsDirty = true;
            prefabSearchDone = false;
            Repaint();
        }

        private void OnUndoRedo()
        {
            if (working == null) return;
            if (serializedPattern != null && serializedPattern.targetObject != null) serializedPattern.Update();
            MarkChanged();
        }

        // ───────────── 작업용 사본 / 에셋 ─────────────

        private void EnsureState()
        {
            ResolvePrefabs();

            if (working == null)
            {
                BossPattern asset = source != null ? source : FindInitialPattern();
                if (asset != null) LoadFrom(asset);
                else CreateNew();
            }

            if (serializedPattern == null || serializedPattern.targetObject != working)
                serializedPattern = new SerializedObject(working);
        }

        private void ResolvePrefabs()
        {
            if (prefabSearchDone) return;
            prefabSearchDone = true;

            if (bossPrefab != null && playerPrefab != null) return;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;

                if (bossPrefab == null && prefab.GetComponent<Boss>() != null) { bossPrefab = prefab; infoDirty = true; }
                if (playerPrefab == null && prefab.GetComponent<PlayerHealth>() != null) { playerPrefab = prefab; infoDirty = true; }
                if (bossPrefab != null && playerPrefab != null) break;
            }
        }

        private Boss TargetBoss => bossPrefab != null ? bossPrefab.GetComponent<Boss>() : null;

        /// <summary>창을 처음 열었을 때 보여 줄 패턴: 보스의 첫 번째 패턴, 없으면 프로젝트의 아무 패턴 에셋.</summary>
        private BossPattern FindInitialPattern()
        {
            Boss boss = TargetBoss;
            if (boss != null)
            {
                foreach (BossPattern pattern in boss.Patterns)
                {
                    if (pattern != null) return pattern;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:BossPattern"))
            {
                var pattern = AssetDatabase.LoadAssetAtPath<BossPattern>(AssetDatabase.GUIDToAssetPath(guid));
                if (pattern != null) return pattern;
            }
            return null;
        }

        private void CreateNew()
        {
            var pattern = CreateInstance<BossPattern>();
            pattern.name = "New Boss Pattern";
            pattern.duration = 4f;
            pattern.emitters.Add(NewEmitter(pattern));
            ReplaceWorking(pattern, null);
        }

        private void LoadFrom(BossPattern asset)
        {
            BossPattern copy = Instantiate(asset);
            copy.name = asset.name;
            ReplaceWorking(copy, asset);
        }

        private void ReplaceWorking(BossPattern pattern, BossPattern asset)
        {
            bool wasTesting = IsTestingInGame;
            DestroyWorking();

            pattern.hideFlags = HideFlags.DontSave;
            pattern.Sanitize();
            working = pattern;
            source = asset;
            serializedPattern = new SerializedObject(working);
            RememberSaved();

            selectedEmitter = working.emitters.Count > 0 ? 0 : BodySelection;
            selectedRange = -1;
            time = 0f;
            playing = false;
            emitterList = null;
            timelineDrag = TimelineDrag.None;
            previewDrag = PreviewDrag.None;
            MarkChanged();

            // 게임에서 테스트하던 중이면 새로 연 패턴으로 테스트를 이어 간다
            if (wasTesting) ApplyGameTest();
        }

        private void DestroyWorking()
        {
            if (working == null || EditorUtility.IsPersistent(working)) return;
            Undo.ClearUndo(working);
            DestroyImmediate(working);
            working = null;
            serializedPattern = null;
        }

        /// <summary>지금 상태를 "저장된 상태"로 기억한다(저장 여부 판단과 외부 변경 감지에 쓴다).</summary>
        private void RememberSaved()
        {
            savedJson = JsonUtility.ToJson(working);
            sourceJson = source != null ? JsonUtility.ToJson(source) : string.Empty;
        }

        /// <summary>발사 지점 1개의 기본값(보스의 눈에서 왼쪽으로 1발씩, 패턴 전체 시간 동안 발사).</summary>
        private static PatternEmitter NewEmitter(BossPattern pattern)
        {
            int index = pattern.emitters.Count;
            var emitter = new PatternEmitter
            {
                name = "Emitter " + (index + 1),
                color = EmitterPalette[index % EmitterPalette.Length],
            };
            emitter.activeRanges.Add(new PatternTimeRange(0f, pattern.duration));
            return emitter;
        }

        /// <summary>저장하지 않은 변경이 있으면 물어본다. 계속 진행해도 되면 true.</summary>
        private bool ConfirmDiscard()
        {
            if (!IsDirty) return true;

            int choice = EditorUtility.DisplayDialogComplex(WindowTitle,
                "'" + working.name + "' 패턴에 저장하지 않은 변경 사항이 있습니다.", "저장", "취소", "저장 안 함");
            if (choice == 0) return Save();
            return choice == 2;
        }

        private void RequestNew()
        {
            if (ConfirmDiscard()) CreateNew();
        }

        private void RequestOpen(BossPattern asset)
        {
            if (asset == null) return;
            if (ConfirmDiscard()) LoadFrom(asset);
            Repaint();
        }

        /// <summary>인스펙터 등 에디터 밖에서 원본 에셋이 바뀌었고, 에디터에 저장하지 않은 변경이 없으면 다시 불러온다.</summary>
        private void ReloadIfSourceChanged()
        {
            if (working == null || source == null || IsDirty) return;
            if (JsonUtility.ToJson(source) == sourceJson) return;

            int emitter = selectedEmitter;
            float keptTime = time;
            LoadFrom(source);
            selectedEmitter = Mathf.Clamp(emitter, BodySelection, working.emitters.Count - 1);
            time = keptTime;
        }

        /// <summary>현재 패턴을 원본 에셋에 저장한다. 아직 에셋이 없으면 저장 위치를 물어 추출한다.</summary>
        private bool Save()
        {
            if (source == null) return SaveAs();

            serializedPattern.ApplyModifiedProperties();
            CopyPatternData(working, source);
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssetIfDirty(source);

            RememberSaved();
            MarkChanged();
            return true;
        }

        /// <summary>현재 패턴을 새 ScriptableObject 에셋으로 추출한다.</summary>
        private bool SaveAs()
        {
            EnsureFolder(ChopperBuilder.BossPatternDir);

            string defaultName = source != null ? source.name + "_Copy" : "BossPattern_New";
            string path = EditorUtility.SaveFilePanelInProject("패턴을 에셋으로 추출", defaultName, "asset",
                "패턴 에셋(ScriptableObject)을 저장할 위치를 고르세요.", ChopperBuilder.BossPatternDir.TrimEnd('/'));
            return !string.IsNullOrEmpty(path) && ExportTo(path);
        }

        /// <summary>현재 패턴을 path의 에셋으로 내보낸다. 같은 경로에 패턴 에셋이 있으면 내용을 덮어쓴다(에셋을 가리키던 참조는 유지된다).</summary>
        internal bool ExportTo(string path)
        {
            serializedPattern.ApplyModifiedProperties();

            var asset = AssetDatabase.LoadAssetAtPath<BossPattern>(path);
            if (asset == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    EditorUtility.DisplayDialog(WindowTitle, "같은 이름의 다른 종류 에셋이 이미 있습니다:\n" + path, "확인");
                    return false;
                }

                EnsureFolder(Path.GetDirectoryName(path));
                asset = CreateInstance<BossPattern>();
                AssetDatabase.CreateAsset(asset, path);
            }

            CopyPatternData(working, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);

            source = asset;
            working.name = asset.name;
            RememberSaved();
            MarkChanged();
            EditorGUIUtility.PingObject(asset);
            return true;
        }

        /// <summary>패턴의 내용(직렬화된 필드)만 복사한다. 에셋 이름이나 숨김 플래그는 건드리지 않는다.</summary>
        private static void CopyPatternData(BossPattern from, BossPattern to)
        {
            var fromObject = new SerializedObject(from);
            var toObject = new SerializedObject(to);

            SerializedProperty property = fromObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script") continue;
                toObject.CopyFromSerializedProperty(property);
            }
            toObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        public override void SaveChanges()
        {
            if (Save()) base.SaveChanges();
        }

        // ───────────── 변경 추적 ─────────────

        /// <summary>패턴 데이터가 바뀌었음을 알린다(미리보기 재계산, 저장 여부 재확인).</summary>
        private void MarkChanged()
        {
            dataVersion++;
            Repaint();
        }

        private bool IsDirty
        {
            get
            {
                if (dirtyVersion != dataVersion)
                {
                    dirtyVersion = dataVersion;
                    dirty = working != null && JsonUtility.ToJson(working) != savedJson;
                }
                return dirty;
            }
        }

        /// <summary>SerializedObject를 거치지 않고 패턴 데이터를 직접 고칠 때 앞에서 호출한다(Undo 기록).</summary>
        private void BeginDirectEdit(string undoName)
        {
            if (serializedPattern.ApplyModifiedProperties()) dataVersion++;
            Undo.RecordObject(working, undoName);
        }

        private void EndDirectEdit()
        {
            working.Sanitize();
            serializedPattern.Update();
            MarkChanged();
        }

        private void SelectEmitter(int index)
        {
            index = Mathf.Clamp(index, BodySelection, working.emitters.Count - 1);
            if (index != selectedEmitter) selectedRange = -1;
            selectedEmitter = index;
            Repaint();
        }

        private void ClampSelection()
        {
            int count = working.emitters.Count;
            if (selectedEmitter >= count) selectedEmitter = count - 1;
            if (selectedEmitter < BodySelection) selectedEmitter = BodySelection;

            if (selectedEmitter < 0 || selectedRange >= working.emitters[selectedEmitter].activeRanges.Count)
                selectedRange = -1;
        }

        // ───────────── 재생 ─────────────

        /// <summary>패턴이 끝나는 시각. 미리보기는 그 뒤 총알이 화면을 벗어날 때까지(PreviewSpan) 더 재생한다.</summary>
        private float PatternDuration => working != null ? working.duration : BossPattern.MinDuration;

        private float PreviewSpan => PatternDuration + tail;

        private int CurrentFrame => Mathf.RoundToInt(time * frameRate);

        private float SnapTime(float value)
        {
            return Mathf.Round(value * frameRate) / frameRate;
        }

        private void SetTime(float value, bool snapToFrame)
        {
            if (snapToFrame) value = SnapTime(value);
            time = Mathf.Clamp(value, 0f, PreviewSpan);
            Repaint();
        }

        private void StepFrames(int frames)
        {
            playing = false;
            SetTime((CurrentFrame + frames) / (float)frameRate, false);
        }

        private void TogglePlay()
        {
            if (!playing && time >= PreviewSpan - 1e-4f) time = 0f;
            playing = !playing;
            lastTick = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void Update()
        {
            if (pendingOpen != null)
            {
                BossPattern asset = pendingOpen;
                pendingOpen = null;
                RequestOpen(asset);
            }

            double now = EditorApplication.timeSinceStartup;
            if (playing && working != null)
            {
                // 에디터가 잠깐 멈췄다 돌아와도 시간이 크게 건너뛰지 않게 한 번에 0.1초까지만 진행한다
                float delta = Mathf.Min((float)(now - lastTick), 0.1f) * SpeedOptions[Mathf.Clamp(speedIndex, 0, SpeedOptions.Length - 1)];
                float span = PreviewSpan;
                float next = time + delta;
                if (next >= span)
                {
                    if (loopPlayback && span > 0f)
                    {
                        next %= span;
                    }
                    else
                    {
                        next = span;
                        playing = false;
                    }
                }
                time = next;
                Repaint();
            }
            lastTick = now;
        }

        // ───────────── GUI ─────────────

        private void OnGUI()
        {
            EnsureState();
            InitStyles();
            serializedPattern.Update();
            ClampSelection();

            HandleShortcuts();
            DrawToolbar();

            float top = ToolbarHeight;
            float bodyHeight = Mathf.Max(0f, position.height - top);
            float timelineHeight = TimelineHeight(bodyHeight);
            float previewWidth = Mathf.Max(100f, position.width - SettingsWidth);

            var previewArea = new Rect(0f, top, previewWidth, bodyHeight - timelineHeight);
            var settingsArea = new Rect(previewWidth, top, position.width - previewWidth, bodyHeight - timelineHeight);
            var timelineArea = new Rect(0f, top + bodyHeight - timelineHeight, position.width, timelineHeight);

            // 미리보기와 타임라인을 설정 패널보다 먼저 처리한다: 설정 패널은 내용에 따라 컨트롤 수가 달라지므로,
            // 그 뒤에 두면 드래그 도중 컨트롤 ID가 밀릴 수 있다.
            RebuildPreviewIfNeeded();
            DrawPreview(previewArea);
            DrawTimeline(timelineArea);
            DrawSettings(settingsArea);

            if (serializedPattern.ApplyModifiedProperties()) MarkChanged();
            hasUnsavedChanges = IsDirty;
        }

        private static float ToolbarHeight
        {
            get
            {
                float height = EditorStyles.toolbar.fixedHeight;
                return height > 0f ? height : 21f;
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("새 패턴", "빈 패턴을 새로 만든다"), EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    RequestNew();
                    GUIUtility.ExitGUI();
                }

                var openContent = new GUIContent("열기", "프로젝트에 있는 패턴 에셋을 불러온다");
                Rect openRect = GUILayoutUtility.GetRect(openContent, EditorStyles.toolbarDropDown, GUILayout.Width(56f));
                if (EditorGUI.DropdownButton(openRect, openContent, FocusType.Passive, EditorStyles.toolbarDropDown))
                    ShowOpenMenu(openRect);

                if (source != null)
                {
                    using (new EditorGUI.DisabledScope(!IsDirty))
                    {
                        if (GUILayout.Button(new GUIContent("저장", "변경 내용을 원본 패턴 에셋에 저장한다 (Ctrl+S)"),
                                EditorStyles.toolbarButton, GUILayout.Width(46f)))
                        {
                            Save();
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (GUILayout.Button(new GUIContent("다른 이름으로 추출", "현재 패턴을 새 ScriptableObject 에셋으로 저장한다"),
                            EditorStyles.toolbarButton, GUILayout.Width(120f)))
                    {
                        SaveAs();
                        GUIUtility.ExitGUI();
                    }
                }
                else
                {
                    if (GUILayout.Button(new GUIContent("에셋으로 추출", "현재 패턴을 ScriptableObject 에셋으로 저장한다 (Ctrl+S)"),
                            EditorStyles.toolbarButton, GUILayout.Width(96f)))
                    {
                        SaveAs();
                        GUIUtility.ExitGUI();
                    }
                }

                GUILayout.Space(10f);

                string label = source != null ? AssetDatabase.GetAssetPath(source) : "아직 에셋으로 추출하지 않은 새 패턴";
                if (IsDirty) label += "   (저장 안 됨)";
                if (GUILayout.Button(new GUIContent(label, "클릭하면 Project 창에서 이 에셋을 보여 준다"), EditorStyles.toolbarButton) && source != null)
                    EditorGUIUtility.PingObject(source);

                GUILayout.FlexibleSpace();

                DrawGameTestButton();

                if (GUILayout.Button(new GUIContent("보스 패턴 목록에 추가", "이 패턴을 보스의 행동 패턴 목록 맨 끝에 추가한다"),
                        EditorStyles.toolbarButton, GUILayout.Width(140f)))
                {
                    AddToBoss();
                    GUIUtility.ExitGUI();
                }

                showHelp = GUILayout.Toggle(showHelp, new GUIContent("도움말", "조작 방법을 오른쪽 패널 맨 위에 보여 준다"),
                    EditorStyles.toolbarButton, GUILayout.Width(54f));
            }
        }

        private void ShowOpenMenu(Rect buttonRect)
        {
            var menu = new GenericMenu();
            var seen = new HashSet<BossPattern>();

            // 보스의 패턴 목록에 있는 것부터 실행 순서대로
            Boss boss = TargetBoss;
            if (boss != null)
            {
                int order = 1;
                foreach (BossPattern pattern in boss.Patterns)
                {
                    if (pattern == null || !seen.Add(pattern)) continue;
                    BossPattern captured = pattern;
                    menu.AddItem(new GUIContent("보스 " + order + "번  " + pattern.name), pattern == source, () => RequestOpen(captured));
                    order++;
                }
            }

            // 그 밖의 패턴 에셋(보스 목록에 아직 넣지 않은 것)
            bool needSeparator = seen.Count > 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BossPattern"))
            {
                var pattern = AssetDatabase.LoadAssetAtPath<BossPattern>(AssetDatabase.GUIDToAssetPath(guid));
                if (pattern == null || !seen.Add(pattern)) continue;

                if (needSeparator)
                {
                    menu.AddSeparator(string.Empty);
                    needSeparator = false;
                }
                BossPattern captured = pattern;
                menu.AddItem(new GUIContent(pattern.name), pattern == source, () => RequestOpen(captured));
            }

            if (seen.Count == 0) menu.AddDisabledItem(new GUIContent("프로젝트에 패턴 에셋이 없습니다"));
            menu.DropDown(buttonRect);
        }

        private void HandleShortcuts()
        {
            Event evt = Event.current;
            if (evt.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;

            switch (evt.keyCode)
            {
                case KeyCode.Space:
                    TogglePlay();
                    evt.Use();
                    break;
                case KeyCode.LeftArrow:
                    StepFrames(evt.shift ? -10 : -1);
                    evt.Use();
                    break;
                case KeyCode.RightArrow:
                    StepFrames(evt.shift ? 10 : 1);
                    evt.Use();
                    break;
                case KeyCode.Home:
                    playing = false;
                    SetTime(0f, false);
                    evt.Use();
                    break;
                case KeyCode.End:
                    playing = false;
                    SetTime(PatternDuration, false);
                    evt.Use();
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    if (selectedEmitter >= 0 && selectedRange >= 0)
                    {
                        DeleteRange(selectedEmitter, selectedRange);
                        evt.Use();
                    }
                    break;
                case KeyCode.S:
                    if (evt.control || evt.command)
                    {
                        evt.Use();
                        Save();
                        GUIUtility.ExitGUI();
                    }
                    break;
            }
        }

        // ───────────── 게임에서 테스트 ─────────────

        /// <summary>플레이 중인 게임에서 보스가 지금 편집 중인 패턴(작업용 사본)을 반복 실행하고 있는지.</summary>
        private bool IsTestingInGame =>
            EditorApplication.isPlaying && working != null && ReferenceEquals(BossPatternTest.Pattern, working);

        /// <summary>
        /// 게임에서 테스트를 켜거나 끈다. 보스는 작업용 사본을 그대로 실행하므로,
        /// 테스트 중에 값을 고치면 저장하지 않아도 게임에 바로 반영된다.
        /// </summary>
        private void SetGameTest(bool on)
        {
            if (!EditorApplication.isPlaying || working == null) return;

            if (on) ApplyGameTest();
            else BossPatternTest.End();
            Repaint();
        }

        /// <summary>지금 편집 중인 패턴과 테스트 설정(반복 간격, 플레이어 무적)으로 테스트를 시작하거나 갱신한다.</summary>
        private void ApplyGameTest()
        {
            BossPatternTest.Begin(working, testRepeatDelay, testPlayerInvincible);
        }

        /// <summary>도구 모음의 "게임에서 테스트" 버튼. 플레이 모드에서만 누를 수 있다.</summary>
        private void DrawGameTestButton()
        {
            bool testing = IsTestingInGame;
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                Color savedColor = GUI.backgroundColor;
                if (testing) GUI.backgroundColor = new Color(1f, 0.6f, 0.5f);

                var content = new GUIContent(testing ? "■ 테스트 중지" : "게임에서 테스트", GameTestTooltip);
                bool wanted = GUILayout.Toggle(testing, content, EditorStyles.toolbarButton, GUILayout.Width(104f));

                GUI.backgroundColor = savedColor;
                if (wanted != testing) SetGameTest(wanted);
            }
        }

        // ───────────── 보스의 패턴 목록 ─────────────

        private void AddToBoss()
        {
            if (source == null && !SaveAs()) return;

            Boss boss = TargetBoss;
            if (boss == null)
            {
                EditorUtility.DisplayDialog(WindowTitle, "Boss 컴포넌트가 있는 프리팹을 찾지 못했습니다.\n오른쪽 패널의 '보스 프리팹'을 지정하세요.", "확인");
                return;
            }

            var bossObject = new SerializedObject(boss);
            SerializedProperty list = bossObject.FindProperty("patterns");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == source)
                {
                    EditorUtility.DisplayDialog(WindowTitle, "'" + source.name + "' 패턴은 이미 보스의 패턴 목록 " + (i + 1) + "번에 있습니다.", "확인");
                    return;
                }
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = source;
            bossObject.ApplyModifiedProperties();
            SaveBossPrefab(boss);
            bossSerialized = null;
            Repaint();
        }

        private static void SaveBossPrefab(Boss boss)
        {
            EditorUtility.SetDirty(boss);
            GameObject root = boss.transform.root.gameObject;
            if (PrefabUtility.IsPartOfPrefabAsset(root)) PrefabUtility.SavePrefabAsset(root);
        }
    }
}
