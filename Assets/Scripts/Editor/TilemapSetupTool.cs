using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TilemapSetupTool
{
    private const string NoFrictionMaterialPath = "Assets/Physics/NoFriction.physicsMaterial2D";

    [MenuItem("Tools/Gray/Create Color Tilemaps In Current Scene")]
    public static void CreateColorTilemapsInCurrentScene()
    {
        Grid grid = FindOrCreateGrid();
        PhysicsMaterial2D surfaceMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionMaterialPath);

        CreateOrUpdateTilemap(grid.transform, "WhiteTilemap", PlatformColorType.White, surfaceMaterial);
        CreateOrUpdateTilemap(grid.transform, "BlackTilemap", PlatformColorType.Black, surfaceMaterial);
        CreateOrUpdateTilemap(grid.transform, "GrayTilemap", PlatformColorType.Gray, surfaceMaterial);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Color tilemaps are ready. Use Window > 2D > Tile Palette and paint on WhiteTilemap, BlackTilemap, or GrayTilemap.");
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
        return gridObject.AddComponent<Grid>();
    }

    private static void CreateOrUpdateTilemap(Transform parent, string name, PlatformColorType platformColor, PhysicsMaterial2D surfaceMaterial)
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
