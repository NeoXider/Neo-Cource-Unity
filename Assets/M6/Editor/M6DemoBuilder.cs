using System;
using System.IO;
using Lessons.M6.Y4;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Lessons.M6.EditorTools
{
    // Собирает готовую игру модуля 6 («Звёздный патруль»): префабы и сцену.
    // Пересборка: Unity -batchmode -executeMethod Lessons.M6.EditorTools.M6DemoBuilder.BuildFromCommandLine. Результат совпадает с тем, что ученик собирает руками за 4 урока.
    public static class M6DemoBuilder
    {
        private const string Root = "Assets/M6";
        private const string Sprites = Root + "/Sprites";
        private const string Audio = Root + "/Audio";
        private const string Prefabs = Root + "/Demo/Prefabs";
        public const string ScenePath = Root + "/Demo/Scenes/Shooter.unity";

        // Без пункта меню: сцену открывают из окна Project или запуском редактора с -executeMethod.
        public static void OpenDemoScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // Точка входа для batchmode: -executeMethod Lessons.M6.EditorTools.M6DemoBuilder.BuildFromCommandLine
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                string preview = Environment.GetEnvironmentVariable("M6_PREVIEW_PATH");
                if (!string.IsNullOrEmpty(preview))
                {
                    RenderPreview(preview);
                }

                Debug.Log("M6_BUILD_OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("M6_BUILD_FAILED: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void Build()
        {
            ConfigureSprites();
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();

            GameObject explosion = BuildExplosionPrefab();
            GameObject bonus = BuildBonusPrefab();
            GameObject bullet = BuildBulletPrefab();
            GameObject enemyBullet = BuildEnemyBulletPrefab(explosion);
            GameObject asteroid = BuildAsteroidPrefab("Asteroid", "Asteroid", 0.4f, 2.5f, 2, 10, 0.15f, bonus,
                explosion);
            GameObject asteroidBig = BuildAsteroidPrefab("AsteroidBig", "AsteroidBig", 0.62f, 1.5f, 5, 30, 0.3f,
                bonus, explosion);
            GameObject enemyShip = BuildEnemyShipPrefab(enemyBullet, bonus, explosion);

BuildScene(bullet, new[] { asteroid, asteroid, asteroid, asteroidBig, enemyShip, enemyShip });
            AssetDatabase.SaveAssets();
        }

        // ---------- Спрайты ----------

        private static void ConfigureSprites()
        {
            SetSprite("PlayerShip", 256);
            SetSprite("EnemyShip", 256);
            SetSprite("Asteroid", 256);
            SetSprite("AsteroidBig", 256);
            SetSprite("Bullet", 256);
            SetSprite("EnemyBullet", 256);
            SetSprite("Bonus", 256);
            SetSprite("Starfield", 200);
            SetSprite("Pixel", 16);
        }

        private static void SetSprite(string name, float pixelsPerUnit)
        {
            string path = Sprites + "/" + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("M6: не найден спрайт " + path);
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
                throw new FileNotFoundException("M6: спрайт не импортирован — " + name);
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

        private static void AddCircleTrigger(GameObject go, float radius)
        {
            CircleCollider2D circle = go.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = radius;
        }

        private static GameObject SavePrefab(GameObject go)
        {
            string path = Prefabs + "/" + go.name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void Set(Object target, string field, object value)
        {
            SerializedObject so = new(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                throw new MissingFieldException(target.GetType().Name, field);
            }

            switch (value)
            {
                case float f:
                    prop.floatValue = f;
                    break;
                case int i:
                    prop.intValue = i;
                    break;
                case Vector3 v:
                    prop.vector3Value = v;
                    break;
                case Object o:
                    prop.objectReferenceValue = o;
                    break;
                case null:
                    prop.objectReferenceValue = null;
                    break;
                default:
                    throw new ArgumentException("M6: неподдерживаемый тип значения для " + field);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildExplosionPrefab()
        {
            GameObject go = new("Explosion");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 3f;
            main.startSize = 0.3f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.4f, 0.1f));
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

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

            AddSound(go, "Explosion", 0.5f);
            return SavePrefab(go);
        }

        private static void AddSound(GameObject go, string clipName, float volume)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "/" + clipName + ".wav");
            if (clip == null)
            {
                Debug.LogWarning("M6: звук не найден — " + clipName);
                return;
            }

            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = true;
            source.volume = volume;
            source.spatialBlend = 0f;
        }

        private static GameObject BuildBulletPrefab()
        {
            GameObject go = NewSpriteObject("Bullet", "Bullet", 5);
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.12f, 0.44f);
            AddKinematicBody(go);
            Bullet bullet = go.AddComponent<Bullet>();
            Set(bullet, "direction", Vector3.up);
            Set(bullet, "speed", 12f);
            AddSound(go, "Shoot", 0.3f);
            return SavePrefab(go);
        }

        // Вражеский снаряд — тоже Enemy: ранит корабль при касании, и его можно сбить.
        private static GameObject BuildEnemyBulletPrefab(GameObject explosion)
        {
            GameObject go = NewSpriteObject("EnemyBullet", "EnemyBullet", 5);
            AddCircleTrigger(go, 0.13f);
            AddKinematicBody(go);
            Enemy enemy = go.AddComponent<Enemy>();
            Set(enemy, "speed", 5f);
            Set(enemy, "maxHealth", 1);
            Set(enemy, "scoreValue", 0);
            Set(enemy, "bonusChance", 0f);
            Set(enemy, "explosionPrefab", explosion);
            return SavePrefab(go);
        }

        private static GameObject BuildBonusPrefab()
        {
            GameObject go = NewSpriteObject("Bonus", "Bonus", 6);
            AddCircleTrigger(go, 0.3f);
            AddKinematicBody(go);
            Bonus bonus = go.AddComponent<Bonus>();
            Set(bonus, "speed", 2f);
            return SavePrefab(go);
        }

        private static GameObject BuildAsteroidPrefab(string name, string sprite, float radius, float fallSpeed,
            int health, int score, float bonusChance, GameObject bonus, GameObject explosion)
        {
            GameObject go = NewSpriteObject(name, sprite, 3);
            AddCircleTrigger(go, radius);
            AddKinematicBody(go);
            Enemy asteroid = go.AddComponent<Enemy>();
            Set(asteroid, "speed", fallSpeed);
            Set(asteroid, "maxHealth", health);
            Set(asteroid, "scoreValue", score);
            Set(asteroid, "bonusChance", bonusChance);
            Set(asteroid, "bonusPrefab", bonus);
            Set(asteroid, "explosionPrefab", explosion);
            return SavePrefab(go);
        }

        private static GameObject BuildEnemyShipPrefab(GameObject enemyBullet, GameObject bonus, GameObject explosion)
        {
            GameObject go = NewSpriteObject("EnemyShip", "EnemyShip", 4);
            AddCircleTrigger(go, 0.38f);
            AddKinematicBody(go);

            GameObject shootPoint = new("ShootPoint");
            shootPoint.transform.SetParent(go.transform, false);
            shootPoint.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            EnemyShip ship = go.AddComponent<EnemyShip>();
            Set(ship, "speed", 1.2f);
            Set(ship, "maxHealth", 3);
            Set(ship, "scoreValue", 50);
            Set(ship, "bonusChance", 0.5f);
            Set(ship, "bonusPrefab", bonus);
            Set(ship, "explosionPrefab", explosion);
            // Тот же Shooter, что у игрока: другой префаб пули и другая пауза
            Shooter shooter = go.AddComponent<Shooter>();
            Set(shooter, "bulletPrefab", enemyBullet);
            Set(shooter, "shootPoint", shootPoint.transform);
            Set(shooter, "shootInterval", 1.5f);
            return SavePrefab(go);
        }

        // ---------- Сцена ----------

private static void BuildScene(GameObject bullet, GameObject[] enemies)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Камера: вертикальный экран 9:16, Size 5 -> поле 5.6 x 10 юнитов
            GameObject cameraObject = new("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(8, 10, 30, 255);
            cameraObject.AddComponent<AudioListener>();

            // Фон из двух картинок: нижняя и верхняя, вместе бесконечно едут вниз
            GameObject background = NewSpriteObject("Background", "Starfield", -20);
            GameObject backgroundTop = NewSpriteObject("BackgroundTop", "Starfield", -20);
            backgroundTop.transform.SetParent(background.transform, false);
            backgroundTop.transform.localPosition = new Vector3(0f, 10f, 0f);
            background.AddComponent<ScrollingBackground>();

            // Игрок
            GameObject player = NewSpriteObject("Player", "PlayerShip", 10);
            player.transform.position = new Vector3(0f, -3.5f, 0f);
            GameObject shootPoint = new("ShootPoint");
            shootPoint.transform.SetParent(player.transform, false);
            shootPoint.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            AddCircleTrigger(player, 0.3f);
            AddKinematicBody(player);
            // Три маленьких скрипта — три задачи: движение, оружие, жизни
            player.AddComponent<PlayerMovement>();
            PlayerShooter shooter = player.AddComponent<PlayerShooter>();
            PlayerHealth health = player.AddComponent<PlayerHealth>();

            // Спавнер над верхним краем экрана
            GameObject spawnerObject = new("Spawner");
            spawnerObject.transform.position = new Vector3(0f, 6f, 0f);
            Spawner spawner = spawnerObject.AddComponent<Spawner>();

            GameObject controllerObject = new("ShooterController");
            ShooterController controller = controllerObject.AddComponent<ShooterController>();

            // UI
            GameObject canvasObject = new("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ShooterUI gameUI = canvasObject.AddComponent<ShooterUI>();
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            TMP_Text scoreText = NewText(canvasObject.transform, "ScoreText", "Очки: 0", 56,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(520f, 90f));
            TMP_Text livesText = NewText(canvasObject.transform, "LivesText", "Жизни: 3", 56,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(520f, 90f));
            TMP_Text hint = NewText(canvasObject.transform, "HintText","Ведите корабль пальцем или зажатой мышью — он стреляет сам", 34,
                TextAlignmentOptions.Bottom, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1000f, 60f));
            hint.color = new Color(1f, 1f, 1f, 0.55f);

            GameObject panel = new("GameOverPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            NewText(panel.transform, "TitleText", "Игра окончена", 96, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1000f, 140f));
            TMP_Text resultText = NewText(panel.transform, "ResultText", "Счёт: 0\nРекорд: 0", 64,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1000f, 220f));

            GameObject buttonObject = new("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = new Vector2(0f, -220f);
            buttonRect.sizeDelta = new Vector2(520f, 140f);
            buttonObject.GetComponent<Image>().color = new Color32(60, 200, 255, 255);
            TMP_Text buttonText = NewText(buttonObject.transform, "Text", "Играть снова", 56,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 140f));
            buttonText.color = new Color32(8, 10, 30, 255);
            Button button = buttonObject.GetComponent<Button>();
UnityEventTools.AddPersistentListener(button.onClick, controller.Restart);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Ссылки, которые ученик заполняет в инспекторе
            Set(shooter, "bulletPrefab", bullet);
            Set(shooter, "shootPoint", shootPoint.transform);
            Set(health, "gameUI", gameUI);
            Set(health, "gameController", controller);
            SerializedObject spawnerSo = new(spawner);
            SerializedProperty array = spawnerSo.FindProperty("prefabs");
            array.arraySize = enemies.Length;
            for (int i = 0; i < enemies.Length; i++)
            {
array.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            }

            spawnerSo.ApplyModifiedPropertiesWithoutUndo();

            Set(controller, "gameUI", gameUI);
            Set(gameUI, "scoreText", scoreText);
            Set(gameUI, "livesText", livesText);
            Set(gameUI, "gameOverPanel", panel);
            Set(gameUI, "resultText", resultText);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildList();
        }

        //Restart() перезагружает сцену, а загрузить можно только сцену из списка билда.
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

        // ---------- Превью (только для проверки сборки) ----------

        private static void RenderPreview(string path)
        {
            Camera camera = Camera.main;
            GameObject[] extras =
            {
                Place("Asteroid", new Vector3(1.4f, 2.2f, 0f)),
                Place("AsteroidBig", new Vector3(-1.3f, 3.4f, 0f)),
                Place("EnemyShip", new Vector3(0.6f, 0.8f, 0f)),
                Place("EnemyBullet", new Vector3(0.6f, -0.4f, 0f)),
                Place("Bonus", new Vector3(-1.6f, -1.2f, 0f)),
                Place("Bullet", new Vector3(-0.25f, -2.2f, 0f)),
                Place("Bullet", new Vector3(0.25f, -2.2f, 0f)),
                Place("Bullet", new Vector3(0f, -1f, 0f))
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
    internal static class M6PortraitGameView
    {
        static M6PortraitGameView()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().path != M6DemoBuilder.ScenePath)
            {
                return;
            }

            try
            {
                // PlayModeWindow есть не во всех версиях Unity — зовём через рефлексию.
                Type window = typeof(Editor).Assembly.GetType("UnityEditor.PlayModeWindow");
                window?.GetMethod("SetCustomRenderingResolution")
                    ?.Invoke(null, new object[] { 1080u, 1920u, "M6 Portrait 9:16" });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("M6: не удалось переключить окно Game на 9:16 — " + ex.Message);
            }
        }
    }
}
