using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Chopper.EditorTools
{
    public static partial class ChopperBuilder
    {
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // ═════════════ 타이틀 씬 ═════════════

        private static void BuildTitleScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var baskic = Load<TMP_FontAsset>(BaskicFontPath);
            var spacerunner = Load<TMP_FontAsset>(SpacerunnerFontPath);

            // 카메라: 배경은 검은색
            GameObject cameraObject = NewObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData();

            // 오디오
            var audio = NewObject("AudioManager").AddComponent<AudioManager>();
            Set(audio, "library", Load<SoundLibrary>(SoundLibraryPath));

            Canvas canvas = NewCanvas("Canvas", 0);
            Transform root = canvas.transform;

            Image background = NewImage("Background", root, null, Color.black);
            Stretch(Rect(background));

            // 화면 가장자리에 여백을 두고 배치한 Radar_Frame, 투명도가 부드럽게 증가/감소
            Image radar = NewImage("Radar Frame", root, Load<Sprite>(RadarFrameTex), Color.white);
            radar.type = Image.Type.Sliced;
            radar.fillCenter = true;
            radar.pixelsPerUnitMultiplier = 0.125f; // 테두리 1px → 8px
            Stretch(Rect(radar), 48f, 48f, 48f, 48f);
            var pulse = radar.gameObject.AddComponent<UIAlphaPulse>();
            Set(pulse, "target", radar);
            Set(pulse, "minAlpha", 0.18f);
            Set(pulse, "maxAlpha", 1f);
            Set(pulse, "period", 2.8f);

            // 중앙: 제목 + 입력창 (화면 폭의 약 1/3)
            const float boxWidth = 672f;   // Fuel_Bar 112px × 6
            const float boxHeight = 60f;   // Fuel_Bar 10px × 6
            const float titleCapHeight = boxHeight * 1.2f; // 제목은 입력창 높이의 약 1.2배
            const float gap = 44f;
            float groupHeight = titleCapHeight + gap + boxHeight;
            float titleY = groupHeight * 0.5f - titleCapHeight * 0.5f;
            float boxY = -groupHeight * 0.5f + boxHeight * 0.5f;

            // Spacerunner의 대문자 높이는 폰트 크기의 약 0.623배
            TextMeshProUGUI title = NewText("Title", root, spacerunner, "CHOPPER", titleCapHeight / 0.623f,
                UiGreen, TextAlignmentOptions.Capline, true);
            Place(Rect(title), Center, Center, new Vector2(0f, titleY), new Vector2(900f, titleCapHeight + 40f));

            Sprite[] fuelBar = LoadSprites(FuelBarTex);
            Image inputBox = NewImage("Input Box", root, fuelBar[0], Color.white); // Fuel_Bar의 첫 번째 스프라이트
            Place(Rect(inputBox), Center, Center, new Vector2(0f, boxY), new Vector2(boxWidth, boxHeight));

            Color faint = UiGreen;
            faint.a = 0.3f;
            TextMeshProUGUI placeholder = NewText("Placeholder", inputBox.transform, baskic, "INSERT NAME", 28f,
                faint, TextAlignmentOptions.Capline, true);
            Stretch(Rect(placeholder));

            TextMeshProUGUI nameText = NewText("Name", inputBox.transform, baskic, string.Empty, 28f,
                UiGreen, TextAlignmentOptions.Capline, true);
            Stretch(Rect(nameText));

            Image caret = NewImage("Caret", inputBox.transform, Load<Sprite>(WhiteTex), UiGreen);
            Place(Rect(caret), Center, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(12f, 24f));
            caret.gameObject.SetActive(false);

            // 입력창 왼쪽, 높이에 맞춘 Crew_Icon
            Image crewIcon = NewImage("Crew Icon", root, Load<Sprite>(CrewIconTex), Color.white);
            Place(Rect(crewIcon), Center, Center,
                new Vector2(-(boxWidth * 0.5f + 14f + boxHeight * 0.5f), boxY), new Vector2(boxHeight, boxHeight));

            Image fade = NewImage("Fade Overlay", root, null, new Color(0f, 0f, 0f, 0f));
            Stretch(Rect(fade));

            // 커서: mouse-pointer.png, 크기는 입력창 높이의 약 절반
            BuildCursor(root, 32f, true);

            var titleScreen = canvas.gameObject.AddComponent<TitleScreen>();
            Set(titleScreen, "nameText", nameText);
            Set(titleScreen, "placeholderText", placeholder);
            Set(titleScreen, "caret", Rect(caret));
            Set(titleScreen, "fadeOverlay", fade);
            Set(titleScreen, "placeholderAlpha", faint.a);

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        private static GameObject BuildCursor(Transform canvasRoot, float size, bool active)
        {
            GameObject cursorObject = NewUI("Cursor", canvasRoot);
            var image = cursorObject.AddComponent<RawImage>();
            image.texture = Load<Texture2D>(MousePointerTex);
            image.raycastTarget = false;

            // 화살표 끝(이미지 왼쪽 위)이 마우스 위치에 오도록 피벗 설정
            RectTransform rect = Rect(image);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(size, size);

            var cursor = cursorObject.AddComponent<CustomCursor>();
            Set(cursor, "cursor", rect);

            cursorObject.SetActive(active);
            return cursorObject;
        }

        // ═════════════ 인게임 씬 ═════════════

        private static void BuildGameScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            const float orthoSize = 2.7f;                 // 화면 높이 5.4유닛 (픽셀아트 540px)
            const float halfWidth = orthoSize * 16f / 9f; // 16:9 기준 4.8유닛

            // ── 하늘: DomePrefab_Toon + 전용 원근 카메라 ──
            GameObject sky = NewObject("Sky");

            var dome = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(DomePrefabPath), scene);
            dome.transform.SetParent(sky.transform, false);
            dome.transform.localScale = Vector3.one * 0.1f; // 돔 반지름 1000 → 100
            PrefabUtility.RecordPrefabInstancePropertyModifications(dome.transform);

            // 데모용 SelectSky 스크립트는 구 Input API와 누락된 텍스처를 참조하므로 비활성화한다.
            foreach (MonoBehaviour behaviour in dome.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "SelectSky")
                {
                    behaviour.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                }
            }

            MeshRenderer domeRenderer = dome.GetComponentInChildren<MeshRenderer>(true);
            int skyLayer = domeRenderer.gameObject.layer;

            var skyController = sky.AddComponent<SkyDomeController>();
            Set(skyController, "domeRenderer", domeRenderer);
            Set(skyController, "skyTexture", Load<Texture2D>(SkyTexturePath));
            Set(skyController, "tiling", new Vector2(2f, 1f));
            Set(skyController, "scrollSpeed", 0.5f);
            Set(skyController, "tint", Color.white);

            GameObject skyCameraObject = NewObject("Sky Camera", sky.transform);
            skyCameraObject.transform.localPosition = new Vector3(0f, 38f, 0f);
            var skyCamera = skyCameraObject.AddComponent<Camera>();
            skyCamera.orthographic = false;
            skyCamera.fieldOfView = 60f;
            skyCamera.nearClipPlane = 0.3f;
            skyCamera.farClipPlane = 300f;
            skyCamera.clearFlags = CameraClearFlags.SolidColor;
            skyCamera.backgroundColor = Color.black;
            skyCamera.cullingMask = 1 << skyLayer;
            skyCamera.depth = -2f;
            UniversalAdditionalCameraData skyData = skyCamera.GetUniversalAdditionalCameraData();
            skyData.renderType = CameraRenderType.Base;
            skyData.renderShadows = false;

            // ── 메인 카메라(2D 직교): 하늘 카메라 위에 겹쳐 그리는 오버레이 ──
            GameObject cameraObject = NewObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthoSize;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            camera.cullingMask = ~(1 << skyLayer);
            camera.depth = 0f;
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Overlay;
            cameraData.renderShadows = false;
            skyData.cameraStack.Add(camera);

            // ── 시스템 ──
            GameObject systems = NewObject("Systems");

            NewObject("GameManager", systems.transform).AddComponent<GameManager>();

            var pool = NewObject("PoolManager", systems.transform).AddComponent<PoolManager>();
            Set(pool, "prewarm", new[]
            {
                Prewarm(PfPlayerBulletPrimary, 24),
                Prewarm(PfPlayerBulletRifle, 24),
                Prewarm(PfEnemyBullet, 220),
                Prewarm(PfFxBulletHit, 40),
                Prewarm(PfFxSmallExplosion, 16),
                Prewarm(PfFxBigExplosion, 2),
                Prewarm(PfFxPlayerHit, 4),
                Prewarm(PfFxBossBlast, 3),
                Prewarm(PfFxShockwave, 2),
                Prewarm(PfWasp, 8),
                Prewarm(PfHornet, 4),
                Prewarm(PfBoss, 1),
                Prewarm(PfItemRifle, 4),
                Prewarm(PfItemShotgun, 3),
                Prewarm(PfItemMedkit, 2),
                Prewarm(PfDebris, 10),
                Prewarm(PfWreckCabin, 1),
                Prewarm(PfWreckTail, 1),
                Prewarm(PfWreckDoor, 6),
            });

            GameObject audioObject = NewObject("AudioManager", systems.transform);
            var audio = audioObject.AddComponent<AudioManager>();
            Set(audio, "library", Load<SoundLibrary>(SoundLibraryPath));
            audioObject.AddComponent<GameAudioDirector>();

            var vfx = NewObject("VfxManager", systems.transform).AddComponent<VfxManager>();
            Set(vfx, "entries", new[]
            {
                new VfxManager.Entry { id = VfxId.BulletHit, prefab = Prefab(PfFxBulletHit) },
                new VfxManager.Entry { id = VfxId.SmallExplosion, prefab = Prefab(PfFxSmallExplosion) },
                new VfxManager.Entry { id = VfxId.BigExplosion, prefab = Prefab(PfFxBigExplosion) },
                new VfxManager.Entry { id = VfxId.PlayerHit, prefab = Prefab(PfFxPlayerHit) },
                new VfxManager.Entry { id = VfxId.BossBlast, prefab = Prefab(PfFxBossBlast) },
                new VfxManager.Entry { id = VfxId.Shockwave, prefab = Prefab(PfFxShockwave) },
            });

            var spawner = NewObject("EnemySpawner", systems.transform).AddComponent<EnemySpawner>();
            Set(spawner, "waspPrefab", Prefab<Enemy>(PfWasp));
            Set(spawner, "hornetPrefab", Prefab<Enemy>(PfHornet));

            var bossDirector = NewObject("BossDirector", systems.transform).AddComponent<BossDirector>();
            Set(bossDirector, "bossPrefab", Prefab<Boss>(PfBoss));
            Set(bossDirector, "spawnDelay", 60f);

            var dropper = NewObject("ItemDropper", systems.transform).AddComponent<ItemDropper>();
            Set(dropper, "drops", new[]
            {
                new ItemDropper.DropEntry { type = ItemType.RifleAmmo, prefab = Prefab<ItemPickup>(PfItemRifle), chance = 0.4f },
                new ItemDropper.DropEntry { type = ItemType.ShotgunAmmo, prefab = Prefab<ItemPickup>(PfItemShotgun), chance = 0.2f },
                new ItemDropper.DropEntry { type = ItemType.Medkit, prefab = Prefab<ItemPickup>(PfItemMedkit), chance = 0.1f },
            });

            // ── 배경(화면 위쪽 절반): 번개 + 잔해 ──
            GameObject background = NewObject("Background");

            GameObject flashObject = NewObject("Lightning Flash", background.transform);
            SpriteRenderer flashRenderer = AddSprite(flashObject, Load<Sprite>(WhiteTex), SortBackground, 0);
            flashRenderer.color = new Color(1f, 1f, 1f, 0f);
            var lightning = flashObject.AddComponent<LightningController>();
            Set(lightning, "flashRenderer", flashRenderer);

            var debrisSpawner = NewObject("Debris Spawner", background.transform).AddComponent<DebrisSpawner>();
            Set(debrisSpawner, "debrisPrefab", Prefab<DriftingDebris>(PfDebris));
            Set(debrisSpawner, "sprites", LoadSprites(DebrisTex)); // debris.png의 모든 스프라이트

            // ── 전경(화면 아래쪽 절반): 달 표면 패럴랙스. 산은 후경, 분화구는 전경 ──
            GameObject foreground = NewObject("Foreground");
            const float layerScale = 1.5f;
            const float seam = 0.02f; // 띠 사이 틈이 보이지 않도록 살짝 겹침
            float h12 = 0.32f * layerScale; // Layer 1, 2: 32px
            float h34 = 0.64f * layerScale; // Layer 3, 4: 64px
            float y4 = -orthoSize - seam + h34 * 0.5f;
            float y3 = y4 + h34 * 0.5f - seam + h34 * 0.5f;
            float y2 = y3 + h34 * 0.5f - seam + h12 * 0.5f;
            float y1 = y2 + h12 * 0.5f - seam + h12 * 0.5f;
            BuildParallaxLayer(foreground.transform, "Surface Layer 1 (Mountains)", 1, y1, 0.35f, 0, layerScale);
            BuildParallaxLayer(foreground.transform, "Surface Layer 2 (Far Craters)", 2, y2, 0.9f, 1, layerScale);
            BuildParallaxLayer(foreground.transform, "Surface Layer 3 (Craters)", 3, y3, 1.8f, 2, layerScale);
            BuildParallaxLayer(foreground.transform, "Surface Layer 4 (Near Craters)", 4, y4, 3.2f, 3, layerScale);

            // ── 최전방 안개 ──
            BuildFog(halfWidth);

            // ── 플레이어 ──
            var player = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(PfPlayer), scene);
            player.transform.position = new Vector3(-halfWidth + halfWidth * 2f * 0.14f, 0f, 0f);

            BuildPlayerHealthBar(player.transform);

            // ── UI ──
            BuildHud();

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static PoolManager.PrewarmEntry Prewarm(string prefabPath, int count)
        {
            return new PoolManager.PrewarmEntry { prefab = Prefab(prefabPath), count = count };
        }

        private static void BuildParallaxLayer(Transform parent, string name, int layerIndex, float centerY,
            float speed, int sortingOrder, float scale)
        {
            var sprite = Load<Sprite>(string.Format(MoonLayerFormat, layerIndex));

            GameObject go = NewObject(name, parent);
            go.transform.position = new Vector3(0f, centerY, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = AddSprite(go, sprite, SortForeground, sortingOrder);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(sprite.bounds.size.x * 8f, sprite.bounds.size.y);

            var layer = go.AddComponent<ParallaxLayer>();
            Set(layer, "tiledRenderer", renderer);
            Set(layer, "scrollSpeed", speed);
        }

        private static void BuildFog(float halfWidth)
        {
            GameObject go = NewObject("Fog");
            go.transform.position = new Vector3(halfWidth + 3f, -0.9f, 0f);

            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = false;
            main.playOnAwake = true;
            main.startLifetime = 3.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.6f, 4.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // 게임 화면을 가리지 않을 만큼 옅게
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.84f, 0.95f, 0.035f), new Color(0.9f, 0.94f, 1f, 0.075f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 100;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 5f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.2f, 3.6f, 0.01f);

            // 오른쪽 → 왼쪽으로 빠르게 이동
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-8f, -5.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            // 생성/소멸 시 자연스럽게 나타나고 사라지도록
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Load<Material>(FogMaterialPath);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = SortFog;
            renderer.sortingOrder = 0;
            renderer.maxParticleSize = 3f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var fog = go.AddComponent<FogController>();
            Set(fog, "particles", particles);
            Set(fog, "spawnMargin", 3f);
            Set(fog, "viewportYRange", new Vector2(-0.05f, 0.62f));
            Set(fog, "prewarmTime", 5f);
        }

        private static void BuildPlayerHealthBar(Transform player)
        {
            Sprite[] counter = LoadSprites(CrewCounterTex); // [0] = 초록, [1] = 초록 없음

            GameObject bar = NewObject("Player Health Bar");
            const int cellCount = 5;
            const float cellScale = 1.5f;
            const float spacing = 0.13f;

            var cells = new SpriteRenderer[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                GameObject cell = NewObject("Cell " + (i + 1), bar.transform);
                cell.transform.localPosition = new Vector3((i - (cellCount - 1) * 0.5f) * spacing, 0f, 0f);
                cell.transform.localScale = new Vector3(cellScale, cellScale, 1f);
                cells[i] = AddSprite(cell, counter[0], SortWorldUI, 0);
            }

            var healthBar = bar.AddComponent<PlayerHealthBar>();
            Set(healthBar, "target", player);
            Set(healthBar, "offset", new Vector2(0.12f, 0.47f));
            Set(healthBar, "cells", cells);
            Set(healthBar, "filledSprite", counter[0]);
            Set(healthBar, "emptySprite", counter[1]);

            bar.transform.position = player.position + new Vector3(0.12f, 0.47f, 0f);
        }

        private static void BuildHud()
        {
            var baskic = Load<TMP_FontAsset>(BaskicFontPath);
            Sprite crewIcon = Load<Sprite>(CrewIconTex);

            Canvas canvas = NewCanvas("HUD Canvas", 10);
            Transform root = canvas.transform;

            var topLeft = new Vector2(0f, 1f);
            const float scoreFontSize = 46f;

            // 왼쪽 상단: SCORE
            TextMeshProUGUI score = NewText("Score", root, baskic, "SCORE: 0", scoreFontSize, UiGreen,
                TextAlignmentOptions.TopLeft, false);
            Place(Rect(score), topLeft, topLeft, new Vector2(48f, -36f), new Vector2(900f, 64f));

            // 점수 아래: 목숨 아이콘 3개
            GameObject lives = NewUI("Lives", root);
            Place((RectTransform)lives.transform, topLeft, topLeft, new Vector2(48f, -104f), new Vector2(240f, 56f));
            const int maxLives = 3;
            const float lifeSize = 56f;
            var lifeIcons = new GameObject[maxLives];
            for (int i = 0; i < maxLives; i++)
            {
                Image icon = NewImage("Life " + (i + 1), lives.transform, crewIcon, Color.white);
                Place(Rect(icon), topLeft, topLeft, new Vector2(i * (lifeSize + 10f), 0f), new Vector2(lifeSize, lifeSize));
                lifeIcons[i] = icon.gameObject;
            }

            // 화면 중앙 안내 문구
            TextMeshProUGUI revivePrompt = NewText("Revive Prompt", root, baskic, "PRESS R TO REVIVE", 34f, UiGreen,
                TextAlignmentOptions.Capline, false);
            Place(Rect(revivePrompt), Center, Center, new Vector2(0f, -150f), new Vector2(1200f, 60f));

            // GAME OVER: SCORE 텍스트의 약 1.5배 크기
            TextMeshProUGUI gameOver = NewText("Game Over", root, baskic, "GAME OVER", scoreFontSize * 1.5f, UiGreen,
                TextAlignmentOptions.Capline, false);
            Place(Rect(gameOver), Center, Center, Vector2.zero, new Vector2(1400f, 120f));

            // 결과 화면
            ResultsBuild results = BuildResultsPanel(root, baskic);

            // 화면 전체 플래시
            Image flash = NewImage("Screen Flash", root, null, new Color(1f, 1f, 1f, 0f));
            Stretch(Rect(flash));
            flash.enabled = false;

            // 진입 연출용 검은색 바 2개
            Image topBar = NewImage("Intro Bar Top", root, null, Color.black);
            Image bottomBar = NewImage("Intro Bar Bottom", root, null, Color.black);
            Rect(topBar).anchorMin = new Vector2(0f, 0.495f);
            Rect(topBar).anchorMax = new Vector2(1f, 1f);
            Rect(topBar).offsetMin = Vector2.zero;
            Rect(topBar).offsetMax = Vector2.zero;
            Rect(bottomBar).anchorMin = new Vector2(0f, 0f);
            Rect(bottomBar).anchorMax = new Vector2(1f, 0.505f);
            Rect(bottomBar).offsetMin = Vector2.zero;
            Rect(bottomBar).offsetMax = Vector2.zero;

            // 커서: 결과 화면에서만 활성화
            GameObject cursor = BuildCursor(root, 32f, false);

            // 컨트롤러
            var hud = canvas.gameObject.AddComponent<HudController>();
            Set(hud, "scoreText", score);
            Set(hud, "lifeIcons", lifeIcons);

            var bars = canvas.gameObject.AddComponent<IntroBars>();
            Set(bars, "topBar", Rect(topBar));
            Set(bars, "bottomBar", Rect(bottomBar));

            var screenFlash = canvas.gameObject.AddComponent<ScreenFlash>();
            Set(screenFlash, "overlay", flash);

            var banner = canvas.gameObject.AddComponent<GameOverBanner>();
            Set(banner, "gameOverRoot", gameOver.gameObject);
            Set(banner, "revivePromptRoot", revivePrompt.gameObject);
            Set(banner, "revivePromptText", revivePrompt);

            var panel = canvas.gameObject.AddComponent<ResultsPanel>();
            Set(panel, "panelRoot", results.root);
            Set(panel, "canvasGroup", results.group);
            Set(panel, "rows", results.rows);
            Set(panel, "retryButton", results.retry);
            Set(panel, "quitButton", results.quit);
            Set(panel, "cursorObject", cursor);
            Set(panel, "rowColor", UiGreen);
            Set(panel, "playerRowColor", Color.white);

            gameOver.gameObject.SetActive(false);
            revivePrompt.gameObject.SetActive(false);
            results.root.SetActive(false);
        }

        private struct ResultsBuild
        {
            public GameObject root;
            public CanvasGroup group;
            public ResultsPanel.Row[] rows;
            public RectTransform retry;
            public RectTransform quit;
        }

        private static ResultsBuild BuildResultsPanel(Transform canvasRoot, TMP_FontAsset font)
        {
            // 정사각형에 가까운 형태, 화면 높이의 약 80%
            const float panelHeight = 864f;
            const float panelWidth = 900f;
            const float sideMargin = 120f;

            // Radar_Frame 배경: 뒤의 게임 화면이 비치도록 반투명
            Image panel = NewImage("Results Panel", canvasRoot, Load<Sprite>(RadarFrameTex), new Color(1f, 1f, 1f, 0.93f));
            panel.type = Image.Type.Sliced;
            panel.fillCenter = true;
            panel.pixelsPerUnitMultiplier = 0.14f;
            Place(Rect(panel), Center, Center, Vector2.zero, new Vector2(panelWidth, panelHeight));
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Transform root = panel.transform;
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);

            // 상단 중앙: RESULTS
            TextMeshProUGUI title = NewText("Title", root, font, "RESULTS", 64f, UiGreen, TextAlignmentOptions.Capline, false);
            Place(Rect(title), top, Center, new Vector2(0f, -108f), new Vector2(panelWidth - 80f, 90f));

            // 1~5위: 왼쪽 유저네임(좌측 정렬), 오른쪽 점수(우측 정렬)
            const int rowCount = Leaderboard.MaxEntries;
            const float rowHeight = 80f;
            const float firstRowY = -236f;
            var rows = new ResultsPanel.Row[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                GameObject row = NewUI("Row " + (i + 1), root);
                Place((RectTransform)row.transform, top, Center, new Vector2(0f, firstRowY - i * rowHeight),
                    new Vector2(panelWidth - sideMargin * 2f, rowHeight));

                TextMeshProUGUI nameText = NewText("Name", row.transform, font, "PLAYER", 42f, UiGreen,
                    TextAlignmentOptions.CaplineLeft, false);
                Stretch(Rect(nameText));

                TextMeshProUGUI scoreText = NewText("Score", row.transform, font, "0", 42f, UiGreen,
                    TextAlignmentOptions.CaplineRight, false);
                Stretch(Rect(scoreText));

                rows[i] = new ResultsPanel.Row { root = row, nameText = nameText, scoreText = scoreText };
            }

            // 하단 중앙: RETRY / QUIT 아이콘과 이름
            RectTransform retry = BuildResultsButton(root, font, "Retry Button", Load<Sprite>(RestartTex), "RETRY", -150f);
            RectTransform quit = BuildResultsButton(root, font, "Quit Button", Load<Sprite>(CloseTex), "QUIT", 150f);
            retry.anchorMin = retry.anchorMax = bottom;
            quit.anchorMin = quit.anchorMax = bottom;
            retry.anchoredPosition = new Vector2(-150f, 168f);
            quit.anchoredPosition = new Vector2(150f, 168f);

            return new ResultsBuild { root = panel.gameObject, group = group, rows = rows, retry = retry, quit = quit };
        }

        private static RectTransform BuildResultsButton(Transform parent, TMP_FontAsset font, string name,
            Sprite iconSprite, string label, float x)
        {
            GameObject button = NewUI(name, parent);
            var rect = (RectTransform)button.transform;
            rect.pivot = Center;
            rect.sizeDelta = new Vector2(220f, 200f);
            rect.anchoredPosition = new Vector2(x, 0f);

            // 아이콘: 원본 비율을 유지하며 높이 110px
            const float iconHeight = 110f;
            float aspect = iconSprite.rect.width / iconSprite.rect.height;
            Image icon = NewImage("Icon", button.transform, iconSprite, Color.white);
            icon.preserveAspect = true;
            Place(Rect(icon), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(iconHeight * aspect, iconHeight));

            // 아이콘 아래 이름
            TextMeshProUGUI text = NewText("Label", button.transform, font, label, 34f, UiGreen, TextAlignmentOptions.Capline, false);
            Place(Rect(text), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(220f, 50f));

            return rect;
        }
    }
}
