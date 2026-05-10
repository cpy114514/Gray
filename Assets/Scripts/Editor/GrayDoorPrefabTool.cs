#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GrayDoorPrefabTool
{
    private const string GrayDoorPrefabPath = "Assets/Prefabs/GrayDoor.prefab";

    [MenuItem("Tools/Gray/GrayDoor/Rebuild Prefab")]
    public static void RebuildPrefab()
    {
        EnsurePrefabFolder();

        GameObject prefabRoot = CreateGrayDoorObject("GrayDoor");
        GrayDoor grayDoor = prefabRoot.GetComponent<GrayDoor>();
        grayDoor.RebuildEffects();

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, GrayDoorPrefabPath);
        Object.DestroyImmediate(prefabRoot);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"GrayDoor prefab rebuilt at {GrayDoorPrefabPath}.");
    }

    [MenuItem("Tools/Gray/GrayDoor/Replace In Open Scene")]
    public static void ReplaceInOpenScene()
    {
        GameObject prefab = EnsureGrayDoorPrefab();
        int replacedCount = ReplaceGrayDoorsInScene(prefab, SceneManager.GetActiveScene());
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"Replaced or refreshed {replacedCount} GrayDoor object(s) in the open scene.");
    }

    [MenuItem("Tools/Gray/GrayDoor/Rebuild Prefab And Replace In All Scenes")]
    public static void RebuildPrefabAndReplaceInAllScenes()
    {
        RebuildPrefab();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrayDoorPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Could not load GrayDoor prefab at {GrayDoorPrefabPath}.");
            return;
        }

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        int totalReplaced = 0;

        foreach (string sceneGuid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath);
            totalReplaced += ReplaceGrayDoorsInScene(prefab, scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"GrayDoor prefab rebuilt. Replaced or refreshed {totalReplaced} GrayDoor object(s) in all scenes under Assets/Scenes.");
    }

    private static GameObject EnsureGrayDoorPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrayDoorPrefabPath);
        if (prefab == null)
        {
            RebuildPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrayDoorPrefabPath);
        }

        return prefab;
    }

    private static int ReplaceGrayDoorsInScene(GameObject prefab, Scene scene)
    {
        if (prefab == null || !scene.IsValid())
        {
            return 0;
        }

        List<GrayDoor> doors = new List<GrayDoor>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            doors.AddRange(root.GetComponentsInChildren<GrayDoor>(true));
        }

        int changedCount = 0;
        foreach (GrayDoor oldDoor in doors)
        {
            if (oldDoor == null)
            {
                continue;
            }

            GameObject oldObject = oldDoor.gameObject;
            string prefabPath = GetPrefabAssetPath(oldObject);
            if (prefabPath == GrayDoorPrefabPath)
            {
                oldDoor.RebuildEffects();
                EditorUtility.SetDirty(oldDoor);
                changedCount++;
                continue;
            }

            Transform oldTransform = oldObject.transform;
            Transform parent = oldTransform.parent;
            int siblingIndex = oldTransform.GetSiblingIndex();

            GameObject newObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(newObject, "Replace GrayDoor With Prefab");
            newObject.name = oldObject.name;

            Transform newTransform = newObject.transform;
            newTransform.SetParent(parent, false);
            newTransform.SetSiblingIndex(siblingIndex);
            newTransform.localPosition = oldTransform.localPosition;
            newTransform.localRotation = oldTransform.localRotation;
            newTransform.localScale = oldTransform.localScale;

            CopyDoorSettings(oldDoor, newObject.GetComponent<GrayDoor>());
            CopyRendererSettings(oldObject.GetComponent<SpriteRenderer>(), newObject.GetComponent<SpriteRenderer>());
            CopyColliderSettings(oldObject.GetComponent<Collider2D>(), newObject.GetComponent<Collider2D>());

            Object.DestroyImmediate(oldObject);
            changedCount++;
        }

        return changedCount;
    }

    private static GameObject CreateGrayDoorObject(string objectName)
    {
        GameObject door = new GameObject(objectName);
        door.transform.localScale = new Vector3(0.5f, 2f, 1f);

        SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
        renderer.sprite = FindSquareSprite();
        renderer.color = Color.gray;
        renderer.sortingOrder = 2;

        BoxCollider2D collider = door.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        GrayDoor grayDoor = door.AddComponent<GrayDoor>();
        grayDoor.RebuildEffects();

        return door;
    }

    private static string GetPrefabAssetPath(GameObject instance)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        return source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
    }

    private static void CopyDoorSettings(GrayDoor source, GrayDoor target)
    {
        if (source == null || target == null)
        {
            return;
        }

        SerializedObject sourceSerialized = new SerializedObject(source);
        SerializedObject targetSerialized = new SerializedObject(target);

        CopyProperty(sourceSerialized, targetSerialized, "oneTimeUse");
        CopyProperty(sourceSerialized, targetSerialized, "message");
        CopyProperty(sourceSerialized, targetSerialized, "messageTime");
        CopyProperty(sourceSerialized, targetSerialized, "tutorialTextUI");
        CopyProperty(sourceSerialized, targetSerialized, "autoCreateParticles");
        CopyProperty(sourceSerialized, targetSerialized, "particleColor");
        CopyProperty(sourceSerialized, targetSerialized, "burstParticleCount");

        targetSerialized.ApplyModifiedPropertiesWithoutUndo();
        target.RebuildEffects();
        EditorUtility.SetDirty(target);
    }

    private static void CopyProperty(SerializedObject source, SerializedObject target, string propertyName)
    {
        SerializedProperty sourceProperty = source.FindProperty(propertyName);
        SerializedProperty targetProperty = target.FindProperty(propertyName);
        if (sourceProperty != null && targetProperty != null)
        {
            targetProperty.serializedObject.CopyFromSerializedProperty(sourceProperty);
        }
    }

    private static void CopyRendererSettings(SpriteRenderer source, SpriteRenderer target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.sprite = source.sprite;
        target.color = source.color;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
    }

    private static void CopyColliderSettings(Collider2D source, Collider2D target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.isTrigger = true;

        if (source is BoxCollider2D sourceBox && target is BoxCollider2D targetBox)
        {
            targetBox.offset = sourceBox.offset;
            targetBox.size = sourceBox.size;
            targetBox.edgeRadius = sourceBox.edgeRadius;
        }
    }

    private static Sprite FindSquareSprite()
    {
        string[] guids = AssetDatabase.FindAssets("square t:Sprite", new[] { "Assets/Art" });
        if (guids.Length > 0)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
    }
}
#endif
