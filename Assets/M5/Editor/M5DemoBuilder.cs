using System;
using System.IO;
using Lessons.M5.Y6;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Lessons.M5.EditorTools
{
    // Собирает готовую игру модуля 5 (вертикальный Tower Defence): префабы и сцену.
    // Пересборка: Unity -batchmode -executeMethod Lessons.M5.EditorTools.M5DemoBuilder.BuildFromCommandLine. Результат совпадает с тем, что ученик собирает руками за 6 уроков.
    public static class M5DemoBuilder
    {
        private const string Root = "Assets/M5";
        private const string Sprites = Root + "/Sprites";
        private const string Audio = Root + "/Audio";
        private const string Prefabs = Root + "/Demo/Prefabs";
        public const string ScenePath = Root + "/Demo/Scenes/TowerDefence.unity";

        // Без пункта меню: сцену открывают из окна Project или запуском редактора с -executeMethod.
        public static void OpenDemoScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // Точка входа для batchmode: -executeMethod Lessons.M5.EditorTools.M5DemoBuilder.BuildFromCommandLine
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                string preview = Environment.GetEnvironmentVariable("M5_PREVIEW_PATH");
                if (!string.IsNullOrEmpty(preview))
                {
                    RenderPreview(preview);
                }

                Debug.Log("M5_BUILD_OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("M5_BUILD_FAILED: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void Build()
        {
            ConfigureSprites();
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();

            GameObject deathEffect = BuildDeathEffectPrefab();
            GameObject bolt = BuildBoltPrefab();

            GameObject slime = BuildEnemy<EnemyUnit>("Slime", 0.33f, 3, 1.2f, 1, 2, deathEffect);
            GameObject slimeFast = BuildEnemy<EnemyUnit>("SlimeFast", 0.19f, 1, 2.8f, 1, 1, deathEffect);
            GameObject slimeBig = BuildEnemy<EnemyUnit>("SlimeBig", 0.55f, 8, 0.7f, 3, 6, deathEffect);
            GameObject slimeArmored = BuildEnemy<ArmoredEnemy>("SlimeArmored", 0.38f, 4, 1f, 2, 4, deathEffect);
            GameObject slimeSplitter = BuildEnemy<SplitterEnemy>("SlimeSplitter", 0.36f, 5, 0.9f, 2, 3, deathEffect);

            // Делящийся слизень распадается на быстрых
            GameObject splitterRoot = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(slimeSplitter));
            splitterRoot.GetComponent<SplitterEnemy>().smallPrefab = slimeFast.GetComponent<EnemyUnit>();
            PrefabUtility.SaveAsPrefabAsset(splitterRoot, AssetDatabase.GetAssetPath(slimeSplitter));
            PrefabUtility.UnloadPrefabContents(splitterRoot);

            BuildScene(bolt, new[] { slime, slimeFast, slimeBig, slimeArmored, slimeSplitter });
            AssetDatabase.SaveAssets();
        }

        // ---------- Спрайты ----------

        private static void ConfigureSprites()
        {
            SetSprite("Field", 200);
            SetSprite("Wall", 200);
            SetSprite("Crossbow", 256);
            SetSprite("Bolt", 256);
            SetSprite("Slime", 256);
            SetSprite("SlimeFast", 256);
            SetSprite("SlimeBig", 256);
            SetSprite("SlimeArmored", 256);
            SetSprite("SlimeSplitter", 256);
        }

        private static void SetSprite(string name, float pixelsPerUnit)
        {
            string path = Sprites + "/" + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("M5: не найден спрайт " + path);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static Sprite LoadSprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "/" + name + ".png");
            if (sprite == null)
            {
                throw new FileNotFoundException("M5: спрайт не импортирован — " + name);
            }

            return sprite;
        }

        // ---------- Префабы ----------

        private static GameObject NewSpriteObject(string name, string spriteName, int order)
        {
            GameObject go = new(name);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(spriteName);
            renderer.sortingOrder = order;
            return go;
        }

        private static void AddKinematicBody(GameObject go)
        {
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        private static GameObject SavePrefab(GameObject go)
        {
            string path = Prefabs + "/" + go.name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void AddSound(GameObject go, string clipName, float volume)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "/" + clipName + ".wav");
            if (clip == null)
            {
                Debug.LogWarning("M5: звук не найден — " + clipName);
                return;
            }

            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = true;
            source.volume = volume;
            source.spatialBlend = 0f;
        }

        private static GameObject BuildDeathEffectPrefab()
        {
            GameObject go = new("DeathEffect");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 2.5f;
            main.startSize = 0.25f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 1f, 0.5f), new Color(0.3f, 0.8f, 0.4f));
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 20;
            Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            if (material == null)
            {
                material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            }

            renderer.sharedMaterial = material;
            AddSound(go, "Splat", 0.5f);
            return SavePrefab(go);
        }

        private static GameObject BuildBoltPrefab()
        {
            GameObject go = NewSpriteObject("Bolt", "Bolt", 8);
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.1f, 0.42f);
            AddKinematicBody(go);
            go.AddComponent<BoltProjectile>();
            AddSound(go, "Shot", 0.3f);
            return SavePrefab(go);
        }

        private static GameObject BuildEnemy<T>(string name, float radius, int health, float speed, int wallDamage,
            int reward, GameObject deathEffect) where T : EnemyUnit
        {
            GameObject go = NewSpriteObject(name, name, 4);
            CircleCollider2D circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = radius;
            AddKinematicBody(go);

            T enemy = go.AddComponent<T>();
            enemy.maxHealth = health;
            enemy.moveSpeed = speed;
            enemy.wallDamage = wallDamage;
            enemy.reward = reward;
            enemy.deathEffectPrefab = deathEffect;
            return SavePrefab(go);
        }

        // ---------- Сцена ----------

        private static void BuildScene(GameObject bolt, GameObject[] enemies)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(60, 120, 56, 255);
            cameraObject.AddComponent<AudioListener>();

            NewSpriteObject("Field", "Field", -20);

            // Стена: её защищаем
            GameObject wallObject = NewSpriteObject("DefensePoint", "Wall", 5);
            wallObject.transform.position = new Vector3(0f, -4.2f, 0f);
            wallObject.AddComponent<BoxCollider2D>();
            DefensePoint wall = wallObject.AddComponent<DefensePoint>();

            // Арбалет на стене
            GameObject crossbowObject = NewSpriteObject("Crossbow", "Crossbow", 10);
            crossbowObject.transform.position = new Vector3(0f, -3.6f, 0f);
            GameObject shootPoint = new("ShootPoint");
            shootPoint.transform.SetParent(crossbowObject.transform, false);
            shootPoint.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            crossbowObject.AddComponent<CrossbowAim>();
            CrossbowShooter crossbow = crossbowObject.AddComponent<CrossbowShooter>();
            crossbow.boltPrefab = bolt;
            crossbow.shootPoint = shootPoint.transform;

            // Спавнер над верхним краем экрана
            GameObject spawnerObject = new("Spawner");
            spawnerObject.transform.position = new Vector3(0f, 6f, 0f);
            EnemySpawner spawner = spawnerObject.AddComponent<EnemySpawner>();
            spawner.enemyPrefabs = new EnemyUnit[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                spawner.enemyPrefabs[i] = enemies[i].GetComponent<EnemyUnit>();
            }

            // У каждого объекта одна задача: правила партии, монеты, магазин
            DefenceController controller = new GameObject("DefenceController").AddComponent<DefenceController>();
            Wallet wallet = new GameObject("Wallet").AddComponent<Wallet>();
            UpgradeShop shop = new GameObject("Shop").AddComponent<UpgradeShop>();

            // UI
            GameObject canvasObject = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            // Всё, что написано на экране, показывает один скрипт
            DefenceUI gameUI = canvasObject.AddComponent<DefenceUI>();
            Transform canvas = canvasObject.transform;

            TMP_Text coinsText = NewText(canvas, "CoinsText", "Монеты: 0", 52, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(480f, 80f));
            TMP_Text wallText = NewText(canvas, "WallText", "Стена: 10", 52, TextAlignmentOptions.TopRight,
                new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(480f, 80f));
            TMP_Text waveText = NewText(canvas, "WaveText", "Волна 1", 52, TextAlignmentOptions.Top,
                new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(480f, 80f));

            TMP_Text damageText = NewButton(canvas, "DamageButton", "Урон\n10", new Vector2(-270f, 30f),
                new Vector2(480f, 140f), 40, shop.BuyDamage);
            TMP_Text speedText = NewButton(canvas, "SpeedButton", "Скорость\n10", new Vector2(270f, 30f),
                new Vector2(480f, 140f), 40, shop.BuySpeed);

            GameObject panel = new("GameOverPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            NewText(panel.transform, "GameOverText", "Замок пал", 96, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1000f, 200f));

            // «Заново» — монеты и улучшения остаются. «Сбросить прогресс» — стирает сохранение.
            NewButton(panel.transform, "RestartButton", "Заново", new Vector2(0f, -60f), new Vector2(520f, 140f), 56,
                controller.Restart, 0.5f);
            TMP_Text resetLabel = NewButton(panel.transform, "ResetButton", "Сбросить прогресс",
                new Vector2(0f, -240f), new Vector2(520f, 100f), 38, wallet.ResetSave, 0.5f);
            // У кнопки три действия по порядку: каждый владелец стирает своё сохранение, потом перезапуск
            Button resetButton = resetLabel.transform.parent.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(resetButton.onClick, shop.ResetSave);
            UnityEventTools.AddPersistentListener(resetButton.onClick, controller.Restart);
            resetLabel.transform.parent.GetComponent<Image>().color = new Color32(200, 90, 80, 255);
            resetLabel.color = Color.white;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Ссылки, которые ученик заполняет в инспекторе
            wall.gameUI = gameUI;
            wall.gameController = controller;
            spawner.gameUI = gameUI;
            controller.gameUI = gameUI;
            wallet.gameUI = gameUI;
            shop.wallet = wallet;
            shop.crossbow = crossbow;
            shop.gameUI = gameUI;
            gameUI.wallText = wallText;
            gameUI.coinsText = coinsText;
            gameUI.waveText = waveText;
            gameUI.damagePriceText = damageText;
            gameUI.speedPriceText = speedText;
            gameUI.gameOverPanel = panel;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildList();
        }

        private static TMP_Text NewButton(Transform parent, string name, string text, Vector2 position,
            Vector2 size, float fontSize, UnityAction onClick, float anchorY = 0f)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, anchorY);
            rect.anchorMax = new Vector2(0.5f, anchorY);
            rect.pivot = new Vector2(0.5f, anchorY);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color32(250, 200, 70, 255);
            TMP_Text label = NewText(go.transform, "Text", text, fontSize, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), Vector2.zero, size);
            label.color = new Color32(40, 30, 20, 255);
            UnityEventTools.AddPersistentListener(go.GetComponent<Button>().onClick, onClick);
            return label;
        }

        private static TMP_Text NewText(Transform parent, string name, string text, float size,
            TextAlignmentOptions alignment, Vector2 anchor, Vector2 position, Vector2 sizeDelta)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = sizeDelta;
            return label;
        }

        // Restart() перезагружает сцену, а загрузить можно только сцену из списка билда.
        public static void EnsureSceneInBuildList()
        {
            if (File.Exists(ScenePath))
            {
                AddSceneToBuildList();
            }
        }

        private static void AddSceneToBuildList()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene existing in scenes)
            {
                if (existing.path == ScenePath)
                {
                    return;
                }
            }

            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }

        // ---------- Превью (только для проверки сборки) ----------

        private static void RenderPreview(string path)
        {
            Camera camera = Camera.main;
            GameObject[] extras =
            {
                Place("Slime", new Vector3(-1.4f, 1.2f, 0f)),
                Place("SlimeFast", new Vector3(0.4f, -0.6f, 0f)),
                Place("SlimeBig", new Vector3(1.3f, 3.2f, 0f)),
                Place("SlimeArmored", new Vector3(-0.3f, 2.6f, 0f)),
                Place("SlimeSplitter", new Vector3(1.5f, 0.6f, 0f)),
                Place("Bolt", new Vector3(0f, -2.4f, 0f)),
                Place("Bolt", new Vector3(0f, -1.2f, 0f))
            };

            RenderTexture target = new(563, 1000, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            Texture2D shot = new(563, 1000, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, 563, 1000), 0, 0);
            shot.Apply();
            File.WriteAllBytes(path, shot.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(target);
            foreach (GameObject extra in extras)
            {
                Object.DestroyImmediate(extra);
            }
        }

        private static GameObject Place(string prefabName, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + prefabName + ".prefab");
            return Object.Instantiate(prefab, position, Quaternion.identity);
        }
    }

    // Игра вертикальная: при запуске демо-сцены переключаем окно Game на портрет 9:16.
    [InitializeOnLoad]
    internal static class M5PortraitGameView
    {
        static M5PortraitGameView()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            // Кнопка «Заново» перезагружает сцену — для этого сцена должна быть в списке билда.
            EditorApplication.delayCall += M5DemoBuilder.EnsureSceneInBuildList;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().path != M5DemoBuilder.ScenePath)
            {
                return;
            }

            try
            {
                Type window = typeof(Editor).Assembly.GetType("UnityEditor.PlayModeWindow");
                window?.GetMethod("SetCustomRenderingResolution")
                    ?.Invoke(null, new object[] { 1080u, 1920u, "M5 Portrait 9:16" });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("M5: не удалось переключить окно Game на 9:16 — " + ex.Message);
            }
        }
    }
}
