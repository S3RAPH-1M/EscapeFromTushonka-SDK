using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AssetsTools.NET;
using UnityEditor;
using UnityEngine;

public class AssetBundleLoaderWindow : EditorWindow
{
    private const string k_FolderPrefKey = "TarkovSDK.BundleLoader.FolderPath";
    private const string k_SmapPrefKey = "TarkovSDK.BundleLoader.UseSmapDecal";
    private const string k_SmapShader = "p0/Reflective/Bumped Specular SMap";
    private const string k_SmapDecalShader = "p0/Reflective/Bumped Specular SMap_Decal";

    private static readonly (string From, string To)[] s_AssemblyNameSwaps =
    {
        ("Assembly-CSharp-firstpass", "Tarkov.Assembly-firstpass"),
        ("Assembly-CSharp",           "Tarkov.Assembly"),
    };

    private string _bundleFolderPath;
    private bool _useSmapDecal;
    private Vector2 _logScroll;
    private readonly List<string> _log = new List<string>();

    [MenuItem("EFT-SDK/Inspectors/AssetBundle Loader")]
    public static void ShowWindow()
    {
        AssetBundleLoaderWindow win = GetWindow<AssetBundleLoaderWindow>("AssetBundle Loader");
        win.minSize = new Vector2(560f, 420f);
        win.Show();
    }

    private void OnEnable()
    {
        string defaultFolder = Path.Combine(Application.dataPath, "Tools/Unity Bundle Loader/Loaded Bundles")
            .Replace('/', Path.DirectorySeparatorChar);
        _bundleFolderPath = EditorPrefs.GetString(k_FolderPrefKey, defaultFolder);
        _useSmapDecal = EditorPrefs.GetBool(k_SmapPrefKey, false);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("AssetBundle Loader", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Bundles are hot-modified before loading: MonoScript m_AssemblyName is byte-swapped from " +
            "'Assembly-CSharp' to 'Tarkov.Assembly' so game scripts resolve to the SDK's Tarkov.Assemblies plugins. " +
            "Original bundle files on disk are untouched.",
            MessageType.Info);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Bundle folder", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            string newFolder = EditorGUILayout.TextField(_bundleFolderPath);
            if (newFolder != _bundleFolderPath)
            {
                _bundleFolderPath = newFolder;
                EditorPrefs.SetString(k_FolderPrefKey, _bundleFolderPath);
            }
            if (GUILayout.Button("Browse", GUILayout.Width(80f)))
            {
                string start = Directory.Exists(_bundleFolderPath) ? _bundleFolderPath : Application.dataPath;
                string picked = EditorUtility.OpenFolderPanel("Bundle folder", start, string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    _bundleFolderPath = picked.Replace('/', Path.DirectorySeparatorChar);
                    EditorPrefs.SetString(k_FolderPrefKey, _bundleFolderPath);
                    GUI.FocusControl(null);
                }
            }
        }

        EditorGUILayout.Space(4f);
        bool newSmap = EditorGUILayout.ToggleLeft(
            new GUIContent("Use SMap Decal shader variant",
                "When on, assigns 'p0/Reflective/Bumped Specular SMap_Decal' instead of the base SMap shader."),
            _useSmapDecal);
        if (newSmap != _useSmapDecal)
        {
            _useSmapDecal = newSmap;
            EditorPrefs.SetBool(k_SmapPrefKey, _useSmapDecal);
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!Directory.Exists(_bundleFolderPath)))
        {
            if (GUILayout.Button(new GUIContent("Load All Bundles",
                "Read every .bundle file in the folder, hot-modify assembly refs, instantiate GameObjects in the active scene."),
                GUILayout.Height(32f)))
            {
                LoadAllBundles();
            }
        }

        if (GUILayout.Button(new GUIContent("Load Single Bundle...",
            "Pick a specific .bundle file and load it."), GUILayout.Height(22f)))
        {
            string startFolder = Directory.Exists(_bundleFolderPath) ? _bundleFolderPath : Application.dataPath;
            string picked = EditorUtility.OpenFilePanel("Pick a bundle", startFolder, "bundle");
            if (!string.IsNullOrEmpty(picked))
            {
                _log.Clear();
                LoadSingleBundle(picked.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (_log.Count > 0 && GUILayout.Button("Clear", GUILayout.Width(60f)))
            {
                _log.Clear();
            }
        }
        using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(_logScroll, GUILayout.ExpandHeight(true)))
        {
            _logScroll = scroll.scrollPosition;
            foreach (string line in _log)
            {
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    private void LoadAllBundles()
    {
        _log.Clear();

        if (!Directory.Exists(_bundleFolderPath))
        {
            Log($"Bundle folder not found: {_bundleFolderPath}");
            return;
        }

        string[] paths = Directory.GetFiles(_bundleFolderPath, "*.bundle");
        if (paths.Length == 0)
        {
            Log($"No .bundle files found in {_bundleFolderPath}");
            return;
        }

        Log($"Loading {paths.Length} bundle(s)...");
        int loaded = 0, failed = 0;

        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    "Loading bundles",
                    $"{Path.GetFileName(paths[i])}  ({i + 1}/{paths.Length})",
                    (float)i / paths.Length))
                {
                    Log("Cancelled by user.");
                    break;
                }

                if (LoadSingleBundle(paths[i])) loaded++;
                else failed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Log($"Done. Loaded: {loaded}, failed: {failed}");
    }

    private bool LoadSingleBundle(string path)
    {
        string label = Path.GetFileName(path);
        AssetBundle unityBundle = null;

        try
        {
            byte[] modifiedBytes = HotModifyBundle(path, out int asmChanges);
            if (modifiedBytes == null || modifiedBytes.Length == 0)
            {
                Log($"[{label}] failed to prepare modified bytes");
                return false;
            }

            unityBundle = AssetBundle.LoadFromMemory(modifiedBytes);
            if (unityBundle == null)
            {
                Log($"[{label}] AssetBundle.LoadFromMemory returned null");
                return false;
            }

            int instantiated = 0;
            UnityEngine.Object[] assets = unityBundle.LoadAllAssets();
            foreach (UnityEngine.Object asset in assets)
            {
                GameObject go = asset as GameObject;
                if (go == null) continue;

                GameObject instance = Instantiate(go);
                instance.name = go.name;
                ReassignShaders(instance);
                Undo.RegisterCreatedObjectUndo(instance, $"Load bundle {label}");
                instantiated++;
            }

            Log($"[{label}] loaded (asmRefs swapped: {asmChanges}, GameObjects instantiated: {instantiated})");
            return true;
        }
        catch (Exception ex)
        {
            Log($"[{label}] EXCEPTION: {ex.Message}");
            Debug.LogException(ex);
            return false;
        }
        finally
        {
            if (unityBundle != null) unityBundle.Unload(false);
        }
    }

    private static byte[] HotModifyBundle(string path, out int asmChanges)
    {
        asmChanges = 0;

        byte[] unpackedBytes;
        using (FileStream fileStream = File.OpenRead(path))
        using (AssetsFileReader reader = new AssetsFileReader(fileStream))
        {
            AssetBundleFile bundle = new AssetBundleFile();
            bundle.Read(reader);

            using (MemoryStream outMs = new MemoryStream())
            {
                using (AssetsFileWriter writer = new AssetsFileWriter(outMs))
                {
                    bundle.Unpack(writer);
                }
                unpackedBytes = outMs.ToArray();
            }
        }

        foreach (var pair in s_AssemblyNameSwaps)
        {
            asmChanges += ReplacePattern(unpackedBytes, pair.From, pair.To);
        }
        return unpackedBytes;
    }

    private static int ReplacePattern(byte[] bytes, string fromStr, string toStr)
    {
        byte[] from = Encoding.UTF8.GetBytes(fromStr);
        byte[] to = Encoding.UTF8.GetBytes(toStr);
        if (from.Length != to.Length) return 0;
        if (bytes.Length < from.Length) return 0;

        int matches = 0;
        int searchStart = 0;
        ReadOnlySpan<byte> fromSpan = from.AsSpan();
        while (searchStart <= bytes.Length - from.Length)
        {
            int found = bytes.AsSpan(searchStart).IndexOf(fromSpan);
            if (found < 0) break;

            int matchAt = searchStart + found;
            Array.Copy(to, 0, bytes, matchAt, to.Length);
            matches++;
            searchStart = matchAt + from.Length;
        }
        return matches;
    }

    private void ReassignShaders(GameObject instance)
    {
        string shaderName = _useSmapDecal ? k_SmapDecalShader : k_SmapShader;
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Log($"Shader not found: '{shaderName}'. Materials left with their original shaders.");
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (Renderer r in renderers)
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null) mats[i].shader = shader;
            }
        }
    }

    private void Log(string line)
    {
        _log.Add(line);
        Debug.Log($"[BundleLoader] {line}");
        _logScroll = new Vector2(0f, float.MaxValue);
        Repaint();
    }
}
