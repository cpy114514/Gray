#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MiniMapBuilder
{
    private const string MiniMapPrefabPath = "Assets/Prefabs/MiniMap.prefab";
    private static readonly Vector2 UiReferenceResolution = new Vector2(1280f, 720f);

    [MenuItem("Tools/Gray/Build Minimap In Open Scene")]
    public static void BuildMinimapInOpenScene()
    {
        DeleteSceneObject("MiniMap");

        GameObject root = CreateMiniMapObjects();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Minimap created in the open scene.");
    }

    [MenuItem("Tools/Gray/Build Minimap In All Gameplay Scenes")]
    public static void BuildMinimapInAllGameplayScenes()
    {
        for (int buildIndex = SceneFlow.FirstGameplayBuildIndex; buildIndex < EditorBuildSettings.scenes.Length; buildIndex++)
        {
            EditorBuildSettingsScene buildScene = EditorBuildSettings.scenes[buildIndex];
            if (buildScene == null || !buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
            {
                continue;
            }

            EditorSceneManager.OpenScene(buildScene.path);
            BuildMinimapInOpenScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        Debug.Log("Minimap created in all enabled gameplay scenes.");
    }

    [MenuItem("Tools/Gray/Build Minimap Prefab")]
    public static void BuildMinimapPrefab()
    {
        EnsurePrefabFolder();

        GameObject root = CreateMiniMapObjects();
        PrefabUtility.SaveAsPrefabAsset(root, MiniMapPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Minimap prefab saved to {MiniMapPrefabPath}.");
    }

    private static GameObject CreateMiniMapObjects()
    {
        GameObject root = new GameObject("MiniMap");
        MiniMapController2D controller = root.AddComponent<MiniMapController2D>();

        Camera minimapCamera = CreateMiniMapCamera(root.transform);
        RawImage minimapImage = CreateMiniMapCanvas(root.transform);
        EnsureEventSystem();

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("minimapCamera").objectReferenceValue = minimapCamera;
        serialized.FindProperty("minimapImage").objectReferenceValue = minimapImage;
        serialized.FindProperty("orthographicSize").floatValue = 16f;
        serialized.FindProperty("textureWidth").intValue = 768;
        serialized.FindProperty("textureHeight").intValue = 432;
        serialized.FindProperty("defaultUiSize").vector2Value = new Vector2(340f, 191f);
        serialized.FindProperty("minUiSize").vector2Value = new Vector2(220f, 124f);
        serialized.FindProperty("maxUiSize").vector2Value = new Vector2(760f, 428f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static Camera CreateMiniMapCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("MiniMapCamera", typeof(Camera));
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 16f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.depth = -10f;
        camera.useOcclusionCulling = false;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        return camera;
    }

    private static RawImage CreateMiniMapCanvas(Transform parent)
    {
        GameObject canvasObject = new GameObject("MiniMapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = UiReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject frame = new GameObject("MiniMapFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(MiniMapMouseResize));
        frame.transform.SetParent(canvasObject.transform, false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0f, 1f);
        frameRect.anchorMax = new Vector2(0f, 1f);
        frameRect.pivot = new Vector2(0f, 1f);
        frameRect.anchoredPosition = new Vector2(16f, -16f);
        frameRect.sizeDelta = new Vector2(340f, 191f);

        Image frameImage = frame.GetComponent<Image>();
        frameImage.color = new Color(0f, 0f, 0f, 0.72f);
        frameImage.raycastTarget = true;

        MiniMapMouseResize resize = frame.GetComponent<MiniMapMouseResize>();
        SerializedObject resizeSerialized = new SerializedObject(resize);
        resizeSerialized.FindProperty("defaultSize").vector2Value = new Vector2(340f, 191f);
        resizeSerialized.FindProperty("minSize").vector2Value = new Vector2(220f, 124f);
        resizeSerialized.FindProperty("maxSize").vector2Value = new Vector2(760f, 428f);
        resizeSerialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject view = new GameObject("MiniMapView", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        view.transform.SetParent(frame.transform, false);
        RectTransform viewRect = view.GetComponent<RectTransform>();
        viewRect.anchorMin = Vector2.zero;
        viewRect.anchorMax = Vector2.one;
        viewRect.offsetMin = new Vector2(8f, 8f);
        viewRect.offsetMax = new Vector2(-8f, -8f);

        RawImage rawImage = view.GetComponent<RawImage>();
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;
        return rawImage;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void DeleteSceneObject(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
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
