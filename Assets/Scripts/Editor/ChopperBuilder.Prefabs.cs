using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chopper.EditorTools
{
    public static partial class ChopperBuilder
    {
        // ───────────── 프리팹 상대 경로 ─────────────

        private const string PfPlayerBulletPrimary = "Bullets/PlayerBullet_Primary.prefab";
        private const string PfPlayerBulletRifle = "Bullets/PlayerBullet_Rifle.prefab";
        private const string PfEnemyBullet = "Bullets/EnemyBullet.prefab";

        private const string PfFxBulletHit = "Effects/FX_BulletHit.prefab";
        private const string PfFxSmallExplosion = "Effects/FX_SmallExplosion.prefab";
        private const string PfFxBigExplosion = "Effects/FX_BigExplosion.prefab";
        private const string PfFxPlayerHit = "Effects/FX_PlayerHit.prefab";
        private const string PfFxBossBlast = "Effects/FX_BossBlast.prefab";
        private const string PfFxShockwave = "Effects/FX_Shockwave.prefab";

        private const string PfWasp = "Enemies/Enemy_Wasp.prefab";
        private const string PfHornet = "Enemies/Enemy_Hornet.prefab";
        private const string PfBoss = "Enemies/Boss.prefab";

        private const string PfItemRifle = "Items/Item_RifleAmmo.prefab";
        private const string PfItemShotgun = "Items/Item_ShotgunAmmo.prefab";
        private const string PfItemMedkit = "Items/Item_Medkit.prefab";

        private const string PfPlayer = "Player/Player.prefab";
        private const string PfWreckCabin = "Player/Wreck_Cabin.prefab";
        private const string PfWreckTail = "Player/Wreck_Tail.prefab";
        private const string PfWreckDoor = "Player/Wreck_Door.prefab";

        private const string PfDebris = "Environment/Debris.prefab";

        /// <summary>헬리콥터(및 그 잔해)의 표시 배율. 1 = 화면 2px/아트 1px.</summary>
        private const float PlayerScale = 1.5f;

        private static GameObject Prefab(string relativePath)
        {
            return Load<GameObject>(PrefabDir + relativePath);
        }

        private static T Prefab<T>(string relativePath) where T : Component
        {
            var component = Prefab(relativePath).GetComponent<T>();
            if (component == null) throw new System.Exception(relativePath + " has no " + typeof(T).Name);
            return component;
        }

        // ───────────── 프리팹 생성 ─────────────

        private static void BuildPrefabs()
        {
            var whiteMat = Load<Material>(WhiteMaterialPath);
            var redMat = Load<Material>(RedMaterialPath);
            var reviveMat = Load<Material>(ReviveMaterialPath);

            Sprite[] fx01 = LoadSprites(Fx01Tex);
            Sprite[] fx02 = LoadSprites(Fx02Tex);
            Sprite[] fx03 = LoadSprites(Fx03Tex);
            Sprite[] fx05 = LoadSprites(Fx05Tex);

            // ── 총알 ──
            // 첫 번째 총알타입(Fx_02) / 두 번째 총알타입(Fx_01). 발사 간격은 PlayerWeapon에서 동일하게 적용.
            BuildPlayerBullet(PfPlayerBulletPrimary, fx02, 14f, 1, 0.07f);
            BuildPlayerBullet(PfPlayerBulletRifle, fx01, 19f, 1, 0.1f);
            BuildEnemyBullet(PfEnemyBullet, fx05[0]);

            // ── 이펙트 ──
            BuildEffect(PfFxBulletHit, LoadSprites(BulletHitTex), 24f, 2f, 5);
            BuildEffect(PfFxSmallExplosion, LoadSprites(SmallExplosionTex), 16f, 1f, 0);
            BuildEffect(PfFxBigExplosion, LoadSprites(BigExplosionTex), 14f, 4f, 20);
            BuildEffect(PfFxPlayerHit, LoadSprites(PlayerHitTex), 30f, 0.13f, 15);
            BuildEffect(PfFxBossBlast, LoadSprites(BossBlastTex), 12f, 2.5f, 25);
            BuildShockwave(PfFxShockwave);

            // ── 몬스터 / 보스 ──
            BuildWasp(PfWasp, whiteMat);
            BuildHornet(PfHornet, whiteMat);
            BuildBoss(PfBoss, whiteMat, fx03);

            // ── 아이템 ──
            BuildItem(PfItemRifle, Load<Sprite>(RifleAmmoTex), ItemType.RifleAmmo);
            BuildItem(PfItemShotgun, Load<Sprite>(ShotgunAmmoTex), ItemType.ShotgunAmmo);
            BuildItem(PfItemMedkit, Load<Sprite>(MedkitTex), ItemType.Medkit);

            // ── 플레이어 ──
            BuildWreck(PfWreckCabin, Load<Sprite>(CabinTex), null);
            BuildWreck(PfWreckTail, Load<Sprite>(TailTex), null);
            BuildWreck(PfWreckDoor, null, LoadSprites(DoorTex));
            BuildPlayer(PfPlayer, whiteMat, redMat, reviveMat, fx03);

            // ── 환경 ──
            BuildDebris(PfDebris);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildPlayerBullet(string path, Sprite[] frames, float speed, int damage, float hitRadius)
        {
            GameObject go = NewObject(Path.GetFileNameWithoutExtension(path));
            go.transform.localScale = new Vector3(2f, 2f, 1f);

            SpriteRenderer renderer = AddSprite(go, frames[0], SortGameplay, 25);
            AddAnimator(go, renderer, frames, 18f, loop: true, pingPong: true);

            var bullet = go.AddComponent<Bullet>();
            Set(bullet, "team", BulletTeam.Player);
            Set(bullet, "speed", speed);
            Set(bullet, "damage", damage);
            Set(bullet, "hitRadius", hitRadius);
            Set(bullet, "targetMask", Mask(LayerEnemy));
            Set(bullet, "rotateToDirection", true);
            Set(bullet, "despawnMargin", 0.4f);
            Set(bullet, "hitEffectScale", 1f);

            SavePrefab(go, path);
        }

        private static void BuildEnemyBullet(string path, Sprite sprite)
        {
            GameObject go = NewObject(Path.GetFileNameWithoutExtension(path));
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            // 탄막은 항상 가장 잘 보여야 하므로 이펙트보다 위에 그린다
            AddSprite(go, sprite, SortEffects, 50);

            var bullet = go.AddComponent<Bullet>();
            Set(bullet, "team", BulletTeam.Enemy);
            Set(bullet, "speed", 4.5f);
            Set(bullet, "damage", 1);
            Set(bullet, "hitRadius", 0.07f);
            Set(bullet, "targetMask", (LayerMask)0);
            Set(bullet, "rotateToDirection", false);
            Set(bullet, "despawnMargin", 0.3f);
            Set(bullet, "hitEffectScale", 0.8f);

            SavePrefab(go, path);
        }

        /// <summary>1회 재생 후 풀로 돌아가는 스프라이트 이펙트.</summary>
        private static void BuildEffect(string path, Sprite[] frames, float fps, float scale, int sortingOrder)
        {
            GameObject go = NewObject(Path.GetFileNameWithoutExtension(path));
            go.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = AddSprite(go, frames[0], SortEffects, sortingOrder);
            AddAnimator(go, renderer, frames, fps, loop: false, playOnEnable: true, endAction: AnimationEndAction.ReleaseToPool);

            SavePrefab(go, path);
        }

        private static void BuildShockwave(string path)
        {
            GameObject go = NewObject(Path.GetFileNameWithoutExtension(path));

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 0;
            line.numCornerVertices = 2;
            line.numCapVertices = 0;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = 1f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingLayerName = SortEffects;
            line.sortingOrder = 30;

            var shockwave = go.AddComponent<Shockwave>();
            Set(shockwave, "line", line);
            Set(shockwave, "segments", 96);
            Set(shockwave, "duration", 0.9f);
            Set(shockwave, "maxRadius", 9.5f);
            Set(shockwave, "startRadius", 0.2f);
            Set(shockwave, "startThickness", 0.6f);
            Set(shockwave, "startColor", new Color(1f, 0.1f, 0.06f, 1f));
            Set(shockwave, "endColor", new Color(1f, 0.93f, 0.2f, 1f));
            // 처음에 빠르게 퍼지고 끝으로 갈수록 느려지는 곡선
            Set(shockwave, "radiusCurve", new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 1.9f),
                new Keyframe(1f, 1f, 0.35f, 0f)));

            SavePrefab(go, path);
        }

        private static void BuildWasp(string path, Material whiteMat)
        {
            Sprite[] frames = LoadSprites(WaspTex);

            GameObject go = NewObject(Path.GetFileNameWithoutExtension(path));
            go.layer = Layer(LayerEnemy);
            go.transform.localScale = new Vector3(3f, 3f, 1f);

            AddKinematicBody(go);
            AddTriggerCircle(go, 0.06f, new Vector2(0.01f, 0f));

            SpriteRenderer renderer = AddSprite(go, frames[0], SortGameplay, 10);
            renderer.flipX = true; // 원본은 오른쪽을 향함 → 왼쪽으로 이동하므로 반전
            SpriteAnimator animator = AddAnimator(go, renderer, frames, 16f, loop: true, randomStart: true);

            var flasher = go.AddComponent<SpriteFlasher>();
            Set(flasher, "renderers", new[] { renderer });

            var wasp = go.AddComponent<WaspEnemy>();
            Set(wasp, "enemyType", EnemyType.Wasp);
            Set(wasp, "maxHealth", 1);          // 총알 1회 피격 시 처치
            Set(wasp, "scoreValue", 50);
            Set(wasp, "animator", animator);
            Set(wasp, "flasher", flasher);
            Set(wasp, "flashOnHit", false);     // 1회 피격으로 처치되므로 점멸 없음(체력을 올리면 켤 수 있다)
            Set(wasp, "hitFlashMaterial", whiteMat);
            Set(wasp, "deathEffectScale", 2.6f);

            SavePrefab(go, path);
        }

        private static void BuildHornet(string path, Material whiteMat)
        {
            Sprite[] frames = LoadSprites(HornetTex);

            GameObject go = NewObject(Path.GetFileNameWithoutExtension(path));
            go.layer = Layer(LayerEnemy);
            go.transform.localScale = new Vector3(3f, 3f, 1f);

            AddKinematicBody(go);
            AddTriggerCircle(go, 0.075f);

            Sprite[] idle = Pick(frames, 0, 1, 2, 3, 4, 5, 6, 7);
            // 날갯짓을 유지하면서 입/꼬리가 노랗게 깜박이는 공격 예고 프레임
            Sprite[] attack = Pick(frames, 8, 12, 9, 13);

            SpriteRenderer renderer = AddSprite(go, idle[0], SortGameplay, 11);
            renderer.flipX = true;
            SpriteAnimator animator = AddAnimator(go, renderer, idle, 16f, loop: true);

            var flasher = go.AddComponent<SpriteFlasher>();
            Set(flasher, "renderers", new[] { renderer });

            var hornet = go.AddComponent<HornetEnemy>();
            Set(hornet, "enemyType", EnemyType.Hornet);
            Set(hornet, "maxHealth", 4);        // 총알 4회 피격 시 처치
            Set(hornet, "scoreValue", 100);
            Set(hornet, "animator", animator);
            Set(hornet, "flasher", flasher);
            Set(hornet, "flashOnHit", true);    // 피격되면 흰색 점멸
            Set(hornet, "hitFlashMaterial", whiteMat);
            Set(hornet, "deathEffectScale", 3.2f);
            Set(hornet, "bulletPrefab", Prefab<Bullet>(PfEnemyBullet));
            Set(hornet, "idleFrames", idle);
            Set(hornet, "attackFrames", attack);

            SavePrefab(go, path);
        }

        private static void BuildBoss(string path, Material whiteMat, Sprite[] muzzleFrames)
        {
            const float shipScale = 2f;

            GameObject root = NewObject(Path.GetFileNameWithoutExtension(path));
            root.layer = Layer(LayerEnemy);
            AddKinematicBody(root);

            var hull = root.AddComponent<CapsuleCollider2D>();
            hull.isTrigger = true;
            hull.direction = CapsuleDirection2D.Horizontal;
            hull.size = new Vector2(2.2f, 1.25f);

            // 원본 아트(오른쪽을 향함) 그대로 배치하고, 방향 전환은 Visual의 X 스케일 반전으로 처리
            GameObject visual = NewObject("Visual", root.transform);
            visual.transform.localScale = new Vector3(shipScale, shipScale, 1f);

            GameObject ship = NewObject("Ship", visual.transform);
            SpriteRenderer shipRenderer = AddSprite(ship, Load<Sprite>(Ship6Tex), SortGameplay, 15);
            var flasher = ship.AddComponent<SpriteFlasher>();
            Set(flasher, "renderers", new[] { shipRenderer });

            // 부스터 2개: Spriter 원본 배치(선체 중심 기준 -79px, ±12px)
            Sprite[] turbo = LoadExhaust(1);
            Sprite[] normal = LoadExhaust(2);
            Sprite[] weak = LoadExhaust(3);
            BoosterFlame boosterTop = BuildBooster(visual.transform, "Booster Top", new Vector2(-0.79f, 0.135f), turbo, normal, weak);
            BoosterFlame boosterBottom = BuildBooster(visual.transform, "Booster Bottom", new Vector2(-0.79f, -0.105f), turbo, normal, weak);

            // 눈알(총알 발사 지점): Ship6.png의 (38.5, 63.5)px
            GameObject eye = NewObject("Eye", visual.transform);
            eye.transform.localPosition = new Vector3(-0.255f, 0.005f, 0f);

            SpriteAnimator muzzle = BuildMuzzleFlash(root.transform, muzzleFrames, 2.5f);

            var boss = root.AddComponent<Boss>();
            Set(boss, "maxHealth", 520);
            Set(boss, "scoreValue", 1000);
            Set(boss, "visualRoot", visual.transform);
            Set(boss, "shipRenderer", shipRenderer);
            Set(boss, "flasher", flasher);
            Set(boss, "hitFlashMaterial", whiteMat);
            Set(boss, "eye", eye.transform);
            Set(boss, "muzzleFlash", muzzle);
            Set(boss, "boosters", new[] { boosterTop, boosterBottom });
            Set(boss, "hitColliders", new Collider2D[] { hull });
            Set(boss, "bulletPrefab", Prefab<Bullet>(PfEnemyBullet));
            Set(boss, "anchorViewport", new Vector2(0.8f, 0.5f));

            SavePrefab(root, path);
        }

        private static Sprite[] LoadExhaust(int set)
        {
            var frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Load<Sprite>(string.Format(ExhaustFormat, set, i + 1));
            return frames;
        }

        private static BoosterFlame BuildBooster(Transform parent, string name, Vector2 localPosition,
            Sprite[] turbo, Sprite[] normal, Sprite[] weak)
        {
            GameObject go = NewObject(name, parent);
            go.transform.localPosition = localPosition;

            SpriteRenderer renderer = AddSprite(go, normal[0], SortGameplay, 14);
            SpriteAnimator animator = AddAnimator(go, renderer, normal, 14f, loop: true, pingPong: true, playOnEnable: false);

            var flame = go.AddComponent<BoosterFlame>();
            Set(flame, "animator", animator);
            Set(flame, "turboFrames", turbo);
            Set(flame, "normalFrames", normal);
            Set(flame, "weakFrames", weak);
            Set(flame, "framesPerSecond", 14f);
            Set(flame, "initialLevel", BoosterLevel.Normal);
            return flame;
        }

        /// <summary>발사 지점에 붙는 Muzzle Flash(Fx_03). 플레이어와 보스가 같은 구성을 사용한다.</summary>
        private static SpriteAnimator BuildMuzzleFlash(Transform parent, Sprite[] frames, float scale)
        {
            GameObject pivot = NewObject("MuzzleFlash", parent);

            GameObject spriteObject = NewObject("Sprite", pivot.transform);
            spriteObject.transform.localScale = new Vector3(scale, scale, 1f);
            // 불꽃의 밝은 중심(스프라이트 왼쪽 5px 지점)이 발사 지점에 오도록 오른쪽으로 민다
            spriteObject.transform.localPosition = new Vector3(0.05f * scale, 0f, 0f);

            SpriteRenderer renderer = AddSprite(spriteObject, frames[0], SortEffects, 10);
            renderer.enabled = false;

            return AddAnimator(pivot, renderer, frames, 24f, loop: false, playOnEnable: false, endAction: AnimationEndAction.Hide);
        }

        private static void BuildItem(string path, Sprite sprite, ItemType type)
        {
            GameObject go = NewObject(Path.GetFileNameWithoutExtension(path));
            go.layer = Layer(LayerItem);
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            AddKinematicBody(go);
            AddTriggerCircle(go, 0.13f);
            AddSprite(go, sprite, SortGameplay, 20);

            var item = go.AddComponent<ItemPickup>();
            Set(item, "itemType", type);

            SavePrefab(go, path);
        }

        private static void BuildWreck(string path, Sprite sprite, Sprite[] animationFrames)
        {
            GameObject go = NewObject(Path.GetFileNameWithoutExtension(path));
            go.transform.localScale = new Vector3(PlayerScale, PlayerScale, 1f);

            bool animated = animationFrames != null && animationFrames.Length > 0;
            SpriteRenderer renderer = AddSprite(go, animated ? animationFrames[0] : sprite, SortGameplay, 28);

            var wreck = go.AddComponent<WreckPiece>();
            Set(wreck, "gravity", 9f);

            if (animated)
            {
                // 잔해: door_16x16 스프라이트의 회전 애니메이션
                SpriteAnimator animator = AddAnimator(go, renderer, animationFrames, 16f, loop: true, randomStart: true);
                Set(wreck, "animator", animator);
            }

            SavePrefab(go, path);
        }

        private static void BuildPlayer(string path, Material whiteMat, Material redMat, Material reviveMat, Sprite[] muzzleFrames)
        {
            Sprite[] frames = LoadSprites(HelicopterTex);

            GameObject root = NewObject(Path.GetFileNameWithoutExtension(path));
            root.layer = Layer(LayerPlayer);
            root.transform.localScale = new Vector3(PlayerScale, PlayerScale, 1f);

            AddKinematicBody(root);
            // 피격 판정: 캐빈(동체) 중심의 CircleCollider2D (월드 반지름 = 0.11 × PlayerScale)
            CircleCollider2D hitCollider = AddTriggerCircle(root, 0.11f, new Vector2(0.12f, -0.02f));

            GameObject visual = NewObject("Visual", root.transform);
            SpriteRenderer body = AddSprite(visual, frames[0], SortGameplay, 30);
            AddAnimator(visual, body, frames, 30f, loop: true); // 빠른 Idle 애니메이션(로터 회전)
            var flasher = visual.AddComponent<SpriteFlasher>();
            Set(flasher, "renderers", new[] { body });

            // 헬리콥터의 문: 프레임 기준 (61, 21)px → 중심 기준 (+0.13, -0.05)
            GameObject firePoint = NewObject("FirePoint", root.transform);
            firePoint.transform.localPosition = new Vector3(0.14f, -0.05f, 0f);

            // 루트가 PlayerScale만큼 커지므로 월드 기준 2배가 되도록 보정
            SpriteAnimator muzzle = BuildMuzzleFlash(root.transform, muzzleFrames, 2f / PlayerScale);
            muzzle.transform.localPosition = firePoint.transform.localPosition;

            var health = root.AddComponent<PlayerHealth>();
            var controller = root.AddComponent<PlayerController>();
            var weapon = root.AddComponent<PlayerWeapon>();
            var death = root.AddComponent<PlayerDeathSequence>();

            Set(health, "maxHealth", 5);
            Set(health, "hitCollider", hitCollider);
            Set(health, "flasher", flasher);
            Set(health, "deathSequence", death);
            Set(health, "hitFlashMaterial", whiteMat);
            Set(health, "reviveFlashMaterial", reviveMat);
            Set(health, "contactMask", Mask(LayerEnemy, LayerItem));
            Set(health, "hitEffectScale", 1.3f);

            Set(controller, "health", health);
            Set(controller, "paddingMin", new Vector2(0.8f, 0.35f));
            Set(controller, "paddingMax", new Vector2(0.8f, 0.72f));

            Set(weapon, "primaryBulletPrefab", Prefab<Bullet>(PfPlayerBulletPrimary));
            Set(weapon, "rifleBulletPrefab", Prefab<Bullet>(PfPlayerBulletRifle));
            Set(weapon, "firePoint", firePoint.transform);
            Set(weapon, "muzzleFlash", muzzle);
            Set(weapon, "health", health);

            Set(death, "visual", visual.transform);
            Set(death, "bodyRenderer", body);
            Set(death, "flasher", flasher);
            Set(death, "deathFlashMaterial", redMat);
            Set(death, "cabinPrefab", Prefab<WreckPiece>(PfWreckCabin));
            Set(death, "tailPrefab", Prefab<WreckPiece>(PfWreckTail));
            Set(death, "debrisPrefab", Prefab<WreckPiece>(PfWreckDoor));
            Set(death, "cabinOffset", new Vector2(0.16f, 0f) * PlayerScale);
            Set(death, "tailOffset", new Vector2(-0.17f, 0.03f) * PlayerScale);
            Set(death, "shakeAmplitude", 0.1f);
            Set(death, "smallExplosionArea", new Vector2(0.6f, 0.28f));
            Set(death, "smallExplosionScale", new Vector2(1.2f, 2f));
            Set(death, "bigExplosionScale", 1.1f);

            SavePrefab(root, path);
        }

        private static void BuildDebris(string path)
        {
            Sprite[] frames = LoadSprites(DebrisTex);

            GameObject go = NewObject(Path.GetFileNameWithoutExtension(path));
            SpriteRenderer renderer = AddSprite(go, frames[0], SortBackground, 10);

            var debris = go.AddComponent<DriftingDebris>();
            Set(debris, "spriteRenderer", renderer);

            SavePrefab(go, path);
        }

        // ───────────── 사운드 라이브러리 ─────────────

        // 볼륨은 각 클립의 실측 음량(RMS)을 기준으로, 배경음 < 반복 효과음 < 중요한 단발 효과음 순으로 들리도록 맞춘 값이다.
        // MP3 클립은 앞쪽에 수십 ms의 무음(인코더 지연)이 있어 효과음은 startOffset으로 건너뛰고,
        // 루프는 앞뒤 무음을 잘라낸 구간을 교차 페이드로 이어 붙인다.
        private static void BuildSoundLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(SoundLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, SoundLibraryPath);
            }

            const SoundOverflowPolicy Steal = SoundOverflowPolicy.StealOldest;
            const SoundOverflowPolicy Skip = SoundOverflowPolicy.SkipNew;

            var sounds = new List<SoundDefinition>
            {
                // ── 타이틀 ──
                Loop(SoundId.TitleMusic, "TitleMusic.mp3", 0.9f, 0.06f, 0.18f, 0.05f),
                Sfx(SoundId.KeyInput, "KeyInput.mp3", 0.75f, 3, Steal, 0f, 0.04f, 0.045f),
                Sfx(SoundId.EraseInput, "EraseInput.wav", 1f, 2, Steal, 0f, 0.04f),

                // ── 인게임 배경음 ──
                Loop(SoundId.GameMusic, "GameMusic.wav", 0.2f),
                Loop(SoundId.RainThunder, "Rain-and-thunder-loop.mp3", 0.6f, 0.05f, 0.01f, 0.3f),
                Loop(SoundId.Blizzard, "WinterBlizzardOutside4.wav", 0.5f),

                // ── 플레이어 ──
                // 초당 약 11발 반복되므로 낮은 볼륨 + 동시 4개까지, 초과 시 가장 오래된 소리를 대체
                Sfx(SoundId.Gun, "Gun.wav", 0.16f, 4, Steal, 0.03f, 0.04f),
                Sfx(SoundId.Gun2, "Gun2.mp3", 0.2f, 4, Steal, 0.03f, 0.04f, 0.045f),
                // Gun3: 발사음을 방해하지 않을 정도의 낮은 볼륨으로 얹는다
                Sfx(SoundId.GunLayer, "Gun3.mp3", 0.2f, 3, Steal, 0.03f, 0f, 0.07f),
                Loop(SoundId.Chopper, "Chopper.mp3", 0.16f, 0.046f, 0.008f, 0.12f),
                Sfx(SoundId.PlayerHurt, "Hurt2.mp3", 0.9f, 2, Steal, 0.08f, 0f, 0.025f),
                Sfx(SoundId.Revive, "Revive.mp3", 0.42f, 1, Steal, 0f, 0f, 0.04f),
                Sfx(SoundId.Ricochet, "Ricochet.wav", 0.3f, 2, Skip, 0.07f, 0.1f),
                Sfx(SoundId.BulletHit, "BulletHit.wav", 0.35f, 3, Steal, 0.035f, 0.08f),
                Sfx(SoundId.ImminentExplosion, "ImminentExplosion.mp3", 0.57f, 1, Steal, 0f, 0f, 0.05f),
                Sfx(SoundId.Charge, "Charge.mp3", 0.9f, 1, Steal, 0f, 0f, 0.045f),
                Sfx(SoundId.SmallExplosion, "Hurt.wav", 0.7f, 4, Steal, 0.04f, 0.15f),
                Sfx(SoundId.Mayday, "Mayday.wav", 0.7f, 1, Steal, 0f, 0f),
                Sfx(SoundId.BombExplosion, "BombExplosion.wav", 0.85f, 1, Steal, 0f, 0f),
                Sfx(SoundId.BombExplosion2, "BombExplosion2.wav", 0.8f, 1, Steal, 0f, 0f),

                // ── 아이템 ──
                Sfx(SoundId.Reload, "Reload.mp3", 0.85f, 1, Steal, 0f, 0f, 0.075f),
                Sfx(SoundId.Blank, "Blank.wav", 0.7f, 2, Steal, 0.05f, 0f, 0.015f),
                Sfx(SoundId.Blank2, "Blank2.mp3", 0.45f, 2, Steal, 0.05f, 0f, 0.045f),
                Sfx(SoundId.Blank3, "Blank3.mp3", 0.3f, 2, Steal, 0.05f, 0f, 0.025f),
                Sfx(SoundId.IronHit, "IronHit2.wav", 0.95f, 2, Steal, 0.05f, 0f),
                Sfx(SoundId.Health, "Health.wav", 0.75f, 2, Steal, 0.05f, 0f),

                // ── 몬스터 ──
                Sfx(SoundId.BoxExplosion, "BoxExplosion.wav", 1f, 4, Steal, 0.04f, 0.1f),

                // ── 보스 ──
                Sfx(SoundId.BossArrival, "BossArrival.mp3", 0.38f, 1, Steal, 0f, 0f, 0.05f),
                Sfx(SoundId.BossSlowArrival, "BossSlowArrival.mp3", 0.28f, 1, Steal, 0f, 0f, 0.045f),
                Loop(SoundId.BossEngine, "SpaceShip_Engine_Small_Loop_00.mp3", 1f, 0.051f, 0.044f, 0.15f),
                // 패턴에 따라 초당 20회까지 요청되므로 최소 간격으로 솎아낸다
                Sfx(SoundId.BossBullet, "BossBullet.mp3", 0.15f, 4, Steal, 0.045f, 0.05f, 0.035f),
                Loop(SoundId.PortalIdle, "portal_idle.mp3", 0.2f, 0.1f, 0.009f, 0.25f),
                Sfx(SoundId.Explosion4, "Explosion4.wav", 0.9f, 1, Steal, 0f, 0f),
                // 대형 폭발 3개가 0.1초 간격으로 겹쳐 울리도록 동시 3개 허용
                Sfx(SoundId.Explosion2, "Explosion2.wav", 0.58f, 3, Steal, 0f, 0.06f),
                Sfx(SoundId.Explosion3, "Explosion3.wav", 0.95f, 3, Steal, 0f, 0.06f),
            };

            Set(library, "sounds", sounds);
            EditorUtility.SetDirty(library);
        }

        /// <param name="startTrim">루프 시작 지점(초): 앞쪽 무음 제외</param>
        /// <param name="endTrim">클립 끝에서 제외할 길이(초): 뒤쪽 무음 제외</param>
        /// <param name="crossfade">이음매 교차 페이드 길이(초). 0이면 클립 전체를 그대로 반복</param>
        private static SoundDefinition Loop(SoundId id, string fileName, float volume,
            float startTrim = 0f, float endTrim = 0f, float crossfade = 0f)
        {
            return new SoundDefinition
            {
                id = id,
                clip = Clip(fileName),
                volume = volume,
                pitchRange = Vector2.one,
                maxVoices = 1,
                overflow = SoundOverflowPolicy.SkipNew,
                minInterval = 0f,
                loopStartTrim = startTrim,
                loopEndTrim = endTrim,
                loopCrossfade = crossfade,
            };
        }

        /// <param name="pitchVariation">피치 무작위 변화 폭(±). 반복 재생되는 소리의 단조로움을 줄인다.</param>
        /// <param name="startOffset">재생 시작 위치(초): 클립 앞쪽 무음을 건너뛴다.</param>
        private static SoundDefinition Sfx(SoundId id, string fileName, float volume, int maxVoices,
            SoundOverflowPolicy overflow, float minInterval, float pitchVariation, float startOffset = 0f)
        {
            return new SoundDefinition
            {
                id = id,
                clip = Clip(fileName),
                volume = volume,
                pitchRange = new Vector2(1f - pitchVariation, 1f + pitchVariation),
                maxVoices = maxVoices,
                overflow = overflow,
                minInterval = minInterval,
                startOffset = startOffset,
            };
        }
    }
}
