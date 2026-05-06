using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class SimplePrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/1.unity";
    private const string ArtFolder = "Assets/Art/Prototype";
    private const string TileFolder = "Assets/Tiles/Prototype";
    private const string MaterialFolder = "Assets/Materials/Effects";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PhysicsFolder = "Assets/Physics";
    private const string RespawnPointPrefabPath = PrefabFolder + "/RespawnPoint.prefab";
    private const string NoFrictionMaterialPath = PhysicsFolder + "/NoFriction.physicsMaterial2D";
    private const string SquareSpritePath = ArtFolder + "/square.png";
    private const string WhiteTilePath = TileFolder + "/WhiteBlock.asset";
    private const string BlackTilePath = TileFolder + "/BlackBlock.asset";
    private const string ParticleMaterialPath = MaterialFolder + "/MovementParticle.mat";
    private const string TrailMaterialPath = MaterialFolder + "/MovementTrail.mat";
    private const string AutoBuildSessionKey = "Gray.SimplePrototypeSceneBuilder.AutoBuilt";

    [InitializeOnLoadMethod]
    private static void AutoBuildOpenEditorScene()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (SessionState.GetBool(AutoBuildSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(AutoBuildSessionKey, true);
        EditorApplication.delayCall += BuildSceneIfMissingPrototypeObjects;
    }

    [MenuItem("Tools/Gray/Build Simple Prototype Scene")]
    public static void BuildScene()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder(ArtFolder);
        EnsureFolder("Assets/Tiles");
        EnsureFolder(TileFolder);
        EnsureFolder("Assets/Materials");
        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
        EnsureFolder(PhysicsFolder);

        Sprite squareSprite = CreateSquareSprite();
        Tile whiteTile = CreateTile(WhiteTilePath, squareSprite, Color.white);
        Tile blackTile = CreateTile(BlackTilePath, squareSprite, Color.black);
        Material particleMaterial = CreateSpriteMaterial(ParticleMaterialPath);
        Material trailMaterial = CreateSpriteMaterial(TrailMaterialPath);
        PhysicsMaterial2D noFrictionMaterial = CreateNoFrictionMaterial();
        GameObject respawnPointPrefab = CreateRespawnPointPrefab(squareSprite);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        RespawnPoint respawnPoint = CreateRespawnPoint(respawnPointPrefab, new Vector2(-6f, -0.8f), PlayerColorState.White);
        CreatePlayer(squareSprite, respawnPoint, noFrictionMaterial, particleMaterial, trailMaterial);
        CreateColorTilemaps(whiteTile, blackTile, noFrictionMaterial);
        CreateGrayDoor("GrayDoor", new Vector2(0.25f, -0.65f), new Vector2(0.55f, 2.4f), squareSprite);
        CreateKillZone("KillZone", new Vector2(0f, -6f), new Vector2(24f, 1f));

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
    }

    public static void BuildSceneBatch()
    {
        BuildScene();
        EditorApplication.Exit(0);
    }

    [MenuItem("Tools/Gray/Fix Effect Materials In Current Scene")]
    public static void FixEffectMaterialsInCurrentScene()
    {
        EnsureFolder("Assets/Materials");
        EnsureFolder(MaterialFolder);

        Material particleMaterial = CreateSpriteMaterial(ParticleMaterialPath);
        Material trailMaterial = CreateSpriteMaterial(TrailMaterialPath);
        PlayerMovementEffects[] effects = Object.FindObjectsOfType<PlayerMovementEffects>(true);

        foreach (PlayerMovementEffects effect in effects)
        {
            effect.SetEffectMaterials(particleMaterial, trailMaterial);
            EditorUtility.SetDirty(effect);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"Assigned effect materials to {effects.Length} PlayerMovementEffects component(s).");
    }

    private static void BuildSceneIfMissingPrototypeObjects()
    {
        string sceneAbsolutePath = ToAbsoluteProjectPath(ScenePath);
        bool sceneMissing = !File.Exists(sceneAbsolutePath);
        bool prototypeMissing = sceneMissing || !File.ReadAllText(sceneAbsolutePath).Contains("WhiteTilemap");

        if (prototypeMissing)
        {
            BuildScene();
            Debug.Log("Built simple Gray prototype scene at Assets/Scenes/1.unity.");
        }
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, -0.5f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.backgroundColor = new Color(0.62f, 0.62f, 0.62f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        cameraObject.AddComponent<AudioListener>();
    }

    private static void CreatePlayer(Sprite sprite, RespawnPoint respawnPoint, PhysicsMaterial2D noFrictionMaterial, Material particleMaterial, Material trailMaterial)
    {
        GameObject player = new GameObject("Player");
        player.transform.position = respawnPoint.transform.position;

        SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 5;

        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 4f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.75f, 1.35f);
        collider.sharedMaterial = noFrictionMaterial;

        PlayerController2D controller = player.AddComponent<PlayerController2D>();
        controller.SetRespawnPoint(respawnPoint);

        PlayerMovementEffects effects = player.AddComponent<PlayerMovementEffects>();
        effects.SetEffectMaterials(particleMaterial, trailMaterial);
    }

    private static GameObject CreateRespawnPointPrefab(Sprite sprite)
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RespawnPointPrefabPath);
        if (existingPrefab != null)
        {
            return existingPrefab;
        }

        GameObject prefabSource = new GameObject("RespawnPoint");
        prefabSource.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        SpriteRenderer renderer = prefabSource.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 3;

        BoxCollider2D collider = prefabSource.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.isTrigger = true;

        RespawnPoint respawnPoint = prefabSource.AddComponent<RespawnPoint>();
        respawnPoint.SetRespawnColor(PlayerColorState.White);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabSource, RespawnPointPrefabPath);
        Object.DestroyImmediate(prefabSource);
        return prefab;
    }

    private static RespawnPoint CreateRespawnPoint(GameObject prefab, Vector2 position, PlayerColorState respawnColor)
    {
        GameObject respawnObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (respawnObject == null)
        {
            respawnObject = new GameObject("RespawnPoint");
        }

        respawnObject.name = "RespawnPoint";
        respawnObject.transform.position = position;

        RespawnPoint respawnPoint = respawnObject.GetComponent<RespawnPoint>();
        if (respawnPoint == null)
        {
            respawnPoint = respawnObject.AddComponent<RespawnPoint>();
        }

        respawnPoint.SetRespawnColor(respawnColor);
        return respawnPoint;
    }

    private static void CreateColorTilemaps(Tile whiteTile, Tile blackTile, PhysicsMaterial2D noFrictionMaterial)
    {
        GameObject gridObject = new GameObject("Grid");
        gridObject.AddComponent<Grid>();

        Tilemap whiteTilemap = CreateColorTilemap("WhiteTilemap", gridObject.transform, PlatformColorType.White, noFrictionMaterial);
        PaintRect(whiteTilemap, whiteTile, -8, -2, 8, 1);
        PaintRect(whiteTilemap, whiteTile, -3, 0, 3, 1);

        Tilemap blackTilemap = CreateColorTilemap("BlackTilemap", gridObject.transform, PlatformColorType.Black, noFrictionMaterial);
        PaintRect(blackTilemap, blackTile, 1, -2, 8, 1);
        PaintRect(blackTilemap, blackTile, 4, 1, 3, 1);
    }

    private static Tilemap CreateColorTilemap(string name, Transform parent, PlatformColorType color, PhysicsMaterial2D noFrictionMaterial)
    {
        GameObject tilemapObject = new GameObject(name);
        tilemapObject.transform.SetParent(parent);

        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = 0;

        TilemapCollider2D collider = tilemapObject.AddComponent<TilemapCollider2D>();
        collider.sharedMaterial = noFrictionMaterial;

        ColorPlatform colorPlatform = tilemapObject.AddComponent<ColorPlatform>();
        colorPlatform.SetPlatformColor(color);
        colorPlatform.SetSurfaceMaterial(noFrictionMaterial);

        return tilemap;
    }

    private static void PaintRect(Tilemap tilemap, Tile tile, int startX, int startY, int width, int height)
    {
        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    private static void CreateGrayDoor(string name, Vector2 position, Vector2 size, Sprite sprite)
    {
        GameObject door = new GameObject(name);
        door.transform.position = position;
        door.transform.localScale = size;

        SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.gray;
        renderer.sortingOrder = 2;

        BoxCollider2D collider = door.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.isTrigger = true;

        door.AddComponent<GrayDoor>();
    }

    private static void CreateKillZone(string name, Vector2 position, Vector2 size)
    {
        GameObject killZone = new GameObject(name);
        killZone.transform.position = position;

        BoxCollider2D collider = killZone.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = true;

        killZone.AddComponent<KillZone>();
    }

    private static Sprite CreateSquareSprite()
    {
        if (!File.Exists(ToAbsoluteProjectPath(SquareSpritePath)))
        {
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(ToAbsoluteProjectPath(SquareSpritePath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(SquareSpritePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(SquareSpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
    }

    private static Tile CreateTile(string path, Sprite sprite, Color color)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        tile.sprite = sprite;
        tile.color = color;
        tile.colliderType = Tile.ColliderType.Sprite;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static Material CreateSpriteMaterial(string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        }

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        material.name = Path.GetFileNameWithoutExtension(path);
        material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static PhysicsMaterial2D CreateNoFrictionMaterial()
    {
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionMaterialPath);
        if (material == null)
        {
            material = new PhysicsMaterial2D("NoFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
            AssetDatabase.CreateAsset(material, NoFrictionMaterialPath);
        }
        else
        {
            material.friction = 0f;
            material.bounciness = 0f;
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static string ToAbsoluteProjectPath(string assetPath)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
    }
}
