using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TilemapSetupTool
{
    private const string NoFrictionMaterialPath = "Assets/Physics/NoFriction.physicsMaterial2D";
    private const string BlackTilePath = "Assets/Tiles/Prototype/BlackBlock.asset";
    private const string LevelGridPrefabPath = "Assets/Prefabs/LevelGrid.prefab";

    [MenuItem("Tools/Gray/Create Color Tilemaps In Current Scene")]
    public static void CreateColorTilemapsInCurrentScene()
    {
        Grid grid = FindOrCreateGrid();
        PhysicsMaterial2D surfaceMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionMaterialPath);
        TileBase blackTile = AssetDatabase.LoadAssetAtPath<TileBase>(BlackTilePath);

        Tilemap whiteTilemap = CreateOrUpdateColorTilemap(grid.transform, "WhiteTilemap", PlatformColorType.White, surfaceMaterial);
        Tilemap blackTilemap = CreateOrUpdateBlackTilemap(grid.transform, whiteTilemap, blackTile, surfaceMaterial);
        CreateOrUpdateSpikeTilemap(grid.transform, blackTilemap);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Level Grid is ready. Paint blocks on WhiteTilemap and spikes on SpikeTilemap under the same Grid.");
    }

    [MenuItem("Tools/Gray/Save Current Grid As LevelGrid Prefab")]
    public static void SaveCurrentGridAsPrefab()
    {
        Grid grid = Object.FindObjectOfType<Grid>();
        if (grid == null)
        {
            CreateColorTilemapsInCurrentScene();
            grid = Object.FindObjectOfType<Grid>();
        }

        if (grid == null)
        {
            Debug.LogError("Could not find or create a Grid.");
            return;
        }

        string folder = System.IO.Path.GetDirectoryName(LevelGridPrefabPath);
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        PrefabUtility.SaveAsPrefabAssetAndConnect(grid.gameObject, LevelGridPrefabPath, InteractionMode.UserAction);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Saved whole Grid prefab: {LevelGridPrefabPath}");
    }

    private static Grid FindOrCreateGrid()
    {
        Grid grid = Object.FindObjectOfType<Grid>();
        if (grid != null)
        {
            return grid;
        }

        GameObject gridObject = new GameObject("Grid");
        Undo.RegisterCreatedObjectUndo(gridObject, "Create Grid");
        Grid createdGrid = gridObject.AddComponent<Grid>();
        createdGrid.cellSize = new Vector3(0.5f, 0.5f, 1f);
        return createdGrid;
    }

    private static Tilemap CreateOrUpdateColorTilemap(Transform parent, string name, PlatformColorType platformColor, PhysicsMaterial2D surfaceMaterial)
    {
        Transform existing = parent.Find(name);
        GameObject tilemapObject = existing != null ? existing.gameObject : new GameObject(name);

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(tilemapObject, $"Create {name}");
            tilemapObject.transform.SetParent(parent);
            tilemapObject.transform.localPosition = Vector3.zero;
        }

        Tilemap tilemap = GetOrAdd<Tilemap>(tilemapObject);
        TilemapRenderer renderer = GetOrAdd<TilemapRenderer>(tilemapObject);
        TilemapCollider2D tilemapCollider = GetOrAdd<TilemapCollider2D>(tilemapObject);
        Rigidbody2D body = GetOrAdd<Rigidbody2D>(tilemapObject);
        CompositeCollider2D compositeCollider = GetOrAdd<CompositeCollider2D>(tilemapObject);
        ColorPlatform colorPlatform = GetOrAdd<ColorPlatform>(tilemapObject);

        body.bodyType = RigidbodyType2D.Static;
        tilemapCollider.usedByComposite = true;
        tilemapCollider.sharedMaterial = surfaceMaterial;
        compositeCollider.sharedMaterial = surfaceMaterial;
        renderer.sortingOrder = 0;

        colorPlatform.SetPlatformColor(platformColor);
        colorPlatform.SetSurfaceMaterial(surfaceMaterial);

        EditorUtility.SetDirty(tilemap);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(tilemapCollider);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(compositeCollider);
        EditorUtility.SetDirty(colorPlatform);

        return tilemap;
    }

    private static Tilemap CreateOrUpdateBlackTilemap(
        Transform parent,
        Tilemap whiteTilemap,
        TileBase blackTile,
        PhysicsMaterial2D surfaceMaterial)
    {
        Tilemap blackTilemap = CreateOrUpdateColorTilemap(parent, "BlackTilemap", PlatformColorType.Black, surfaceMaterial);
        AutoBlackTilemap autoBlackTilemap = GetOrAdd<AutoBlackTilemap>(blackTilemap.gameObject);
        autoBlackTilemap.Configure(whiteTilemap, blackTile);
        EditorUtility.SetDirty(autoBlackTilemap);
        return blackTilemap;
    }

    private static Tilemap CreateOrUpdateSpikeTilemap(Transform parent, Tilemap blackTilemap)
    {
        Transform existing = parent.Find("SpikeTilemap");
        GameObject tilemapObject = existing != null ? existing.gameObject : new GameObject("SpikeTilemap");

        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(tilemapObject, "Create SpikeTilemap");
            tilemapObject.transform.SetParent(parent);
            tilemapObject.transform.localPosition = Vector3.zero;
        }

        Tilemap tilemap = GetOrAdd<Tilemap>(tilemapObject);
        TilemapRenderer renderer = GetOrAdd<TilemapRenderer>(tilemapObject);
        TilemapCollider2D tilemapCollider = GetOrAdd<TilemapCollider2D>(tilemapObject);
        Rigidbody2D body = GetOrAdd<Rigidbody2D>(tilemapObject);
        CompositeCollider2D compositeCollider = GetOrAdd<CompositeCollider2D>(tilemapObject);
        SpikeTilemap spikeTilemap = GetOrAdd<SpikeTilemap>(tilemapObject);

        body.bodyType = RigidbodyType2D.Static;
        tilemapCollider.isTrigger = true;
        tilemapCollider.usedByComposite = true;
        compositeCollider.isTrigger = true;
        renderer.sortingOrder = 10;
        spikeTilemap.SetWhiteTilemap(parent.Find("WhiteTilemap") != null
            ? parent.Find("WhiteTilemap").GetComponent<Tilemap>()
            : null);
        spikeTilemap.SetBlackTilemap(blackTilemap);

        EditorUtility.SetDirty(tilemap);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(tilemapCollider);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(compositeCollider);
        EditorUtility.SetDirty(spikeTilemap);

        return tilemap;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return Undo.AddComponent<T>(gameObject);
    }
}
