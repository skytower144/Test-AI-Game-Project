using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace Chopper.EditorTools
{
    /// <summary>
    /// CHOPPER 게임의 임포트 설정, 레이어, 프리팹, 사운드 라이브러리, 씬을 프로젝트 리소스로부터 생성하는 빌더.
    /// 메뉴: Tools > Chopper > Rebuild Game
    /// </summary>
    public static partial class ChopperBuilder
    {
        // ───────────── 입력 리소스 경로 ─────────────

        private const string Imported = "Assets/ImportedAssets/";
        private const string Lunar = Imported + "Lunar Lander Upload/";
        private const string Ship6Dir = Imported + "BattleShipAsset/PNG_Parts&Spriter_Animation/Ship6/";
        private const string SkyDomeDir = Imported + "YoruStudios/Sky Domes - High Performance Skybox Alternative/";

        private const string HelicopterTex = Imported + "Helicopter/helicopter.png";
        private const string CabinTex = Imported + "Helicopter/broken_parts/cabine.png";
        private const string TailTex = Imported + "Helicopter/broken_parts/helicopter_back.png";
        private const string DoorTex = Imported + "Helicopter/broken_parts/door_16x16.png";

        private const string Fx01Tex = Lunar + "Effects/Fx_01.png";
        private const string Fx02Tex = Lunar + "Effects/Fx_02.png";
        private const string Fx03Tex = Lunar + "Effects/Fx_03.png";
        private const string Fx05Tex = Lunar + "Effects/Fx_05.png";
        private const string SmallExplosionTex = Lunar + "Effects/Explosion.png";
        private const string BulletHitTex = Imported + "Effects/BulletHit.png";
        private const string BigExplosionTex = Imported + "Effects/Explosion SpriteSheet.png";
        private const string BossBlastTex = Imported + "Effects/explosion-4.png";
        private const string PlayerHitTex = Imported + "Effects/hit - yellow.png";

        private const string MoonLayerFormat = Lunar + "Moon Background/Surface_Layer{0}.png";
        private const string DebrisTex = Lunar + "Space Background/debris.png";
        private const string CrewCounterTex = Lunar + "UI/Crew_Counter.png";
        private const string CrewIconTex = Lunar + "UI/Crew_Icon.png";
        private const string FuelBarTex = Lunar + "UI/Fuel_Bar.png";
        private const string RadarFrameTex = Lunar + "UI/Radar_Frame.png";

        private const string CloseTex = Imported + "Misc/Close.png";
        private const string RestartTex = Imported + "Misc/Restart.png";
        private const string RifleAmmoTex = Imported + "Misc/ammo-rifle 32px.png";
        private const string ShotgunAmmoTex = Imported + "Misc/ammo-shotgun 32px.png";
        private const string MedkitTex = Imported + "Misc/health-green 32px.png";
        private const string MousePointerTex = Imported + "Misc/mouse-pointer.png";
        private const string WhiteTex = Imported + "Misc/white.png";

        private const string HornetTex = Imported + "RobotBugs/Hornet.png";
        private const string WaspTex = Imported + "RobotBugs/Wasp.png";

        private const string Ship6Tex = Ship6Dir + "Ship6.png";
        private const string ExhaustFormat = Ship6Dir + "Exhaust/Turbo_flight/Exhaust{0}/exhaust{1}.png";

        /// <summary>보스 처치 시 튀어 오르는 잔해로 쓰는 Ship6 파츠 이미지.</summary>
        private static readonly string[] Ship6DebrisParts =
        {
            "ship6_deail1.png", "ship6_detail2.png", "ship6_detail3.png", "ship6_detail4.png", "ship6_detail5.png",
            "ship6_wing_detail1.png", "ship6_wing_detail2.png", "ship6_body2.png", "ship6_body3.png",
        };

        private const string DomePrefabPath = SkyDomeDir + "Prefabs_SkyDomes/DomePrefab_Toon.prefab";
        private const string SkyTexturePath = SkyDomeDir + "SkiesTextures_SkyDomes/Toon/SkyToon_17.png";
        private const string FogMaterialPath = Imported + "FogAsset/FogMAT.mat";

        private const string WhiteMaterialPath = "Assets/Materials/whiteMAT.mat";
        private const string RedMaterialPath = "Assets/Materials/redMAT.mat";
        private const string ReviveMaterialPath = "Assets/Materials/reviveMAT.mat";

        private const string BaskicFontPath = "Assets/Fonts/Baskic8-Bold SDF.asset";
        private const string SpacerunnerFontPath = "Assets/Fonts/Spacerunner SDF.asset";
        private const string SoundDir = "Assets/Sound/";

        // ───────────── 출력 경로 ─────────────

        private const string PrefabDir = "Assets/Prefabs/";
        private const string DataDir = "Assets/Data/";
        private const string SoundLibraryPath = DataDir + "SoundLibrary.asset";
        private const string TitleScenePath = "Assets/Scenes/Title.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        // ───────────── 레이어 ─────────────

        private const string LayerPlayer = "Player";
        private const string LayerEnemy = "Enemy";
        private const string LayerItem = "Item";

        private const string SortBackground = "Background";
        private const string SortForeground = "Foreground";
        private const string SortGameplay = "Gameplay";
        private const string SortEffects = "Effects";
        private const string SortFog = "Fog";
        private const string SortWorldUI = "WorldUI";

        private static readonly string[] SortingLayerOrder =
        {
            SortBackground, SortForeground, SortGameplay, SortEffects, SortFog, SortWorldUI,
        };

        // ───────────── 공통 값 ─────────────

        /// <summary>Crew_Icon.png과 어울리는 형광 초록색. 모든 UI 텍스트에 사용.</summary>
        private static readonly Color UiGreen = new Color32(92, 255, 124, 255);

        private static Material s_SpriteMaterial;

        // ───────────── 메뉴 ─────────────

        [MenuItem("Tools/Chopper/Rebuild Game (Prefabs + Scenes)")]
        private static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Chopper", "플레이 모드를 종료한 뒤 실행하세요.", "OK");
                return;
            }

            bool ok = EditorUtility.DisplayDialog("Chopper",
                "Assets/Prefabs, Assets/Data, Title/Game 씬을 프로젝트 리소스로부터 다시 생성합니다.\n" +
                "해당 프리팹과 씬에서 직접 수정한 값은 덮어써집니다. 계속할까요?", "Rebuild", "Cancel");
            if (ok) BuildAll();
        }

        [MenuItem("Tools/Chopper/Clear Leaderboard")]
        private static void ClearLeaderboard()
        {
            Leaderboard.Clear();
            Debug.Log("[Chopper] Leaderboard cleared.");
        }

        /// <summary>전체 빌드.</summary>
        public static void BuildAll()
        {
            ConfigureImportSettings();
            ConfigureLayers();
            BuildPrefabsAndData();
            BuildScenes();
            Debug.Log("[Chopper] Build complete.");
        }

        public static void BuildPrefabsAndData()
        {
            s_SpriteMaterial = null;
            EnsureFolder(PrefabDir);
            EnsureFolder(DataDir);
            BuildPrefabs();
            BuildSoundLibrary();
            AssetDatabase.SaveAssets();
        }

        public static void BuildScenes()
        {
            s_SpriteMaterial = null;
            BuildTitleScene();
            BuildGameScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(TitleScenePath);
        }

        // ───────────── 임포트 설정 ─────────────

        public static void ConfigureImportSettings()
        {
            // helicopter.png: 96x32 프레임 8장으로 슬라이스
            SliceGrid(HelicopterTex, 96, 32);

            // 벌레 스프라이트 하단의 바닥 그림자(탑뷰용)를 프레임에서 제외
            CropSpriteCells(HornetTex, 24, 24, 6);
            CropSpriteCells(WaspTex, 16, 16, 4);

            // 달 표면 레이어(1~3 사용): Tiled 드로우 모드용 Full Rect 메시
            for (int i = 1; i <= 3; i++)
                SetSpriteMeshType(string.Format(MoonLayerFormat, i), SpriteMeshType.FullRect);

            // Ship6 파츠 이미지는 일반 텍스처로 임포트되어 있으므로 픽셀 아트 스프라이트로 바꾼다(보스 잔해용)
            foreach (string part in Ship6DebrisParts)
                ImportAsPixelSprite(Ship6Dir + "Parts/" + part);

            // 고해상도(4096²) 피격 이펙트는 화면에 작게 표시되므로 부드러운 필터 + 밉맵을 쓰고,
            // 메모리를 아끼기 위해 임포트 해상도를 2048로 제한한다.
            ConfigureTexture(PlayerHitTex, importer =>
            {
                bool changed = false;
                if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; changed = true; }
                if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; changed = true; }
                if (importer.maxTextureSize > 2048) { importer.maxTextureSize = 2048; changed = true; }
                return changed;
            });

            AssetDatabase.Refresh();
        }

        private static void ConfigureTexture(string path, Func<TextureImporter, bool> configure)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Texture not found: " + path);
            if (configure(importer)) importer.SaveAndReimport();
        }

        /// <summary>픽셀 아트 이미지를 단일 스프라이트(Point 필터, 무압축)로 임포트한다. 이미 설정되어 있으면 건너뛴다.</summary>
        private static void ImportAsPixelSprite(string path)
        {
            ConfigureTexture(path, importer =>
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; changed = true; }
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    changed = true;
                }
                return changed;
            });
        }

        private static void SetSpriteMeshType(string path, SpriteMeshType meshType)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Texture not found: " + path);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType == meshType) return;

            settings.spriteMeshType = meshType;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        /// <summary>텍스처를 동일 크기 셀로 슬라이스한다(왼쪽 위부터 행 우선). 이미 슬라이스되어 있으면 건너뛴다.</summary>
        private static void SliceGrid(string path, int cellWidth, int cellHeight)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Texture not found: " + path);

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            int columns = width / cellWidth;
            int rows = height / cellHeight;
            int expected = columns * rows;

            bool alreadySliced = importer.textureType == TextureImporterType.Sprite &&
                                 importer.spriteImportMode == SpriteImportMode.Multiple &&
                                 AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == expected;
            if (alreadySliced) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            string baseName = Path.GetFileNameWithoutExtension(path);
            var rects = new List<SpriteRect>(expected);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    rects.Add(new SpriteRect
                    {
                        name = baseName + "_" + rects.Count,
                        rect = new Rect(column * cellWidth, height - (row + 1) * cellHeight, cellWidth, cellHeight),
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        spriteID = GUID.Generate(),
                    });
                }
            }

            provider.SetSpriteRects(rects.ToArray());
            var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameProvider.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            provider.Apply();
            importer.SaveAndReimport();
        }

        /// <summary>기존 스프라이트 셀의 아래쪽 cutBottom 픽셀을 잘라낸다(스프라이트 이름/ID 유지).</summary>
        private static void CropSpriteCells(string path, int cellWidth, int cellHeight, int cutBottom)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Texture not found: " + path);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            SpriteRect[] rects = provider.GetSpriteRects();
            bool changed = false;
            for (int i = 0; i < rects.Length; i++)
            {
                Rect r = rects[i].rect;
                float cellX = Mathf.Floor(r.x / cellWidth) * cellWidth;
                float cellY = Mathf.Floor(r.y / cellHeight) * cellHeight;
                var wanted = new Rect(cellX, cellY + cutBottom, cellWidth, cellHeight - cutBottom);
                if (r != wanted)
                {
                    rects[i].rect = wanted;
                    rects[i].alignment = SpriteAlignment.Center;
                    rects[i].pivot = new Vector2(0.5f, 0.5f);
                    changed = true;
                }
            }

            if (!changed) return;
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        // ───────────── 레이어 / 정렬 레이어 ─────────────

        public static void ConfigureLayers()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) throw new Exception("TagManager.asset not found");

            var tagManager = new SerializedObject(assets[0]);

            SerializedProperty layers = tagManager.FindProperty("layers");
            foreach (string layer in new[] { LayerPlayer, LayerEnemy, LayerItem })
                EnsureLayer(layers, layer);

            SerializedProperty sortingLayers = tagManager.FindProperty("m_SortingLayers");
            foreach (string sortingLayer in SortingLayerOrder)
                EnsureSortingLayer(sortingLayers, sortingLayer);

            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureLayer(SerializedProperty layers, string layerName)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;
            }

            for (int i = 6; i < layers.arraySize; i++) // 0~5는 빌트인/예약
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    return;
                }
            }
            throw new Exception("No free layer slot for '" + layerName + "'");
        }

        private static void EnsureSortingLayer(SerializedProperty sortingLayers, string layerName)
        {
            var usedIds = new HashSet<long>();
            SerializedProperty existing = null;
            for (int i = 0; i < sortingLayers.arraySize; i++)
            {
                SerializedProperty element = sortingLayers.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("name").stringValue == layerName)
                    existing = element;
                else
                    usedIds.Add(element.FindPropertyRelative("uniqueID").longValue);
            }

            // 이미 유효한 ID로 존재하면 그대로 둔다
            if (existing != null && existing.FindPropertyRelative("uniqueID").longValue != 0) return;

            // uniqueID는 부호 없는 정수이므로 양수 해시만 사용한다(0은 Default 전용)
            long id = Animator.StringToHash("Chopper/" + layerName) & 0x7FFFFFFF;
            while (id == 0 || usedIds.Contains(id)) id++;

            if (existing == null)
            {
                int index = sortingLayers.arraySize;
                sortingLayers.InsertArrayElementAtIndex(index);
                existing = sortingLayers.GetArrayElementAtIndex(index);
                existing.FindPropertyRelative("name").stringValue = layerName;
                existing.FindPropertyRelative("locked").boolValue = false;
            }
            existing.FindPropertyRelative("uniqueID").longValue = id;
        }

        private static int Layer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) throw new Exception("Layer '" + layerName + "' does not exist. Run ConfigureLayers first.");
            return layer;
        }

        private static LayerMask Mask(params string[] layerNames)
        {
            int mask = 0;
            foreach (string layerName in layerNames) mask |= 1 << Layer(layerName);
            return mask;
        }

        // ───────────── 에셋 로드 ─────────────

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new FileNotFoundException("Missing " + typeof(T).Name + ": " + path);
            return asset;
        }

        /// <summary>멀티 스프라이트 텍스처의 스프라이트를 이름 끝 번호 순서로 가져온다.</summary>
        private static Sprite[] LoadSprites(string path)
        {
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => TrailingNumber(s.name)).ToArray();
            if (sprites.Length == 0) throw new FileNotFoundException("No sprites in: " + path);
            return sprites;
        }

        private static int TrailingNumber(string name)
        {
            int underscore = name.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int n) ? n : 0;
        }

        private static Sprite[] Pick(Sprite[] source, params int[] indices)
        {
            return indices.Select(i => source[i]).ToArray();
        }

        private static AudioClip Clip(string fileName)
        {
            return Load<AudioClip>(SoundDir + fileName);
        }

        /// <summary>2D 렌더러에서 조명 없이 그대로 보이는 스프라이트 머티리얼.</summary>
        private static Material SpriteMaterial
        {
            get
            {
                if (s_SpriteMaterial == null)
                {
                    s_SpriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
                    if (s_SpriteMaterial == null)
                        s_SpriteMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                }
                return s_SpriteMaterial;
            }
        }

        // ───────────── 오브젝트 생성 헬퍼 ─────────────

        /// <summary>private [SerializeField] 필드에 값을 넣는다. 필드가 없으면 예외(오타 방지).</summary>
        private static void Set(object target, string fieldName, object value)
        {
            Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                type = type.BaseType;
            }
            if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);

            field.SetValue(target, value);
            if (target is UnityEngine.Object unityObject) EditorUtility.SetDirty(unityObject);
        }

        private static GameObject NewObject(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static SpriteRenderer AddSprite(GameObject go, Sprite sprite, string sortingLayer, int sortingOrder)
        {
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = SpriteMaterial;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static SpriteAnimator AddAnimator(GameObject go, SpriteRenderer target, Sprite[] frames, float fps,
            bool loop, bool pingPong = false, bool playOnEnable = true, bool randomStart = false,
            AnimationEndAction endAction = AnimationEndAction.HoldLastFrame)
        {
            var animator = go.AddComponent<SpriteAnimator>();
            Set(animator, "target", target);
            Set(animator, "frames", frames);
            Set(animator, "framesPerSecond", fps);
            Set(animator, "loop", loop);
            Set(animator, "pingPong", pingPong);
            Set(animator, "playOnEnable", playOnEnable);
            Set(animator, "randomStartFrame", randomStart);
            Set(animator, "endAction", endAction);
            return animator;
        }

        private static Rigidbody2D AddKinematicBody(GameObject go)
        {
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.gravityScale = 0f;
            return body;
        }

        private static CircleCollider2D AddTriggerCircle(GameObject go, float radius, Vector2 offset = default)
        {
            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = radius;
            collider.offset = offset;
            return collider;
        }

        private static GameObject SavePrefab(GameObject go, string relativePath)
        {
            string path = PrefabDir + relativePath;
            EnsureFolder(Path.GetDirectoryName(path));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
            UnityEngine.Object.DestroyImmediate(go);
            if (!success || prefab == null) throw new Exception("Failed to save prefab: " + path);
            return prefab;
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ───────────── UI 헬퍼 ─────────────

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static RectTransform Rect(Component component)
        {
            return (RectTransform)component.transform;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = NewUI(name, parent);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, string text,
            float fontSize, Color color, TextAlignmentOptions alignment, bool italic)
        {
            GameObject go = NewUI(name, parent);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = italic ? FontStyles.Italic : FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static Canvas NewCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // 월드 카메라와 마찬가지로 세로 기준으로 스케일
            scaler.referencePixelsPerUnit = 100f;
            return canvas;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
        }
    }
}
