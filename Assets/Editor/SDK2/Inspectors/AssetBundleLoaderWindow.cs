using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public class AssetBundleLoaderWindow : EditorWindow
{
    private const string k_FolderPrefKey = "TarkovSDK.BundleLoader.FolderPath";
    private const string k_PathDataFileRelativePath = "Packages/Custom AssetBundles-Browser/path_data.json";
    private const string k_ShadersCategory = "shaders";

    private static readonly (string From, string To)[] s_AssemblyNameSwaps =
    {
        ("Assembly-CSharp-firstpass", "Tarkov.Assembly-firstpass"),
        ("Assembly-CSharp",           "Tarkov.Assembly"),
    };

    private string _bundleFolderPath;

    [MenuItem("EFT-SDK/Inspectors/AssetBundle Loader")]
    public static void ShowWindow()
    {
        AssetBundleLoaderWindow win = GetWindow<AssetBundleLoaderWindow>("AssetBundle Loader");
        win.minSize = new Vector2(460f, 140f);
        win.Show();
    }

    private void OnEnable()
    {
        string defaultFolder = Path.Combine(Application.dataPath, "Tools/Unity Bundle Loader/Loaded Bundles")
            .Replace('/', Path.DirectorySeparatorChar);
        _bundleFolderPath = EditorPrefs.GetString(k_FolderPrefKey, defaultFolder);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
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

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!Directory.Exists(_bundleFolderPath)))
        {
            if (GUILayout.Button("Load All Bundles", GUILayout.Height(32f)))
            {
                LoadAllBundles();
            }
        }

        if (GUILayout.Button("Load Single Bundle...", GUILayout.Height(22f)))
        {
            string startFolder = Directory.Exists(_bundleFolderPath) ? _bundleFolderPath : Application.dataPath;
            string picked = EditorUtility.OpenFilePanel("Pick a bundle", startFolder, "bundle");
            if (!string.IsNullOrEmpty(picked))
            {
                LoadSingleBundle(picked.Replace('/', Path.DirectorySeparatorChar));
            }
        }
    }

    private void LoadAllBundles()
    {
        if (!Directory.Exists(_bundleFolderPath))
        {
            Debug.LogError($"[BundleLoader] Bundle folder not found: {_bundleFolderPath}");
            return;
        }

        string[] paths = Directory.GetFiles(_bundleFolderPath, "*.bundle");
        if (paths.Length == 0) return;

        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    "Loading bundles",
                    $"{Path.GetFileName(paths[i])}  ({i + 1}/{paths.Length})",
                    (float)i / paths.Length))
                {
                    break;
                }

                LoadSingleBundle(paths[i]);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private bool LoadSingleBundle(string path)
    {
        string label = Path.GetFileName(path);
        AssetBundle unityBundle = null;

        try
        {
            byte[] unpackedBytes = UnpackAndSwapAsm(path, out int asmChanges);
            if (unpackedBytes == null || unpackedBytes.Length == 0)
            {
                Debug.LogError($"[BundleLoader] [{label}] failed to prepare bytes");
                return false;
            }

            Dictionary<string, long> matToShaderPathId = ExtractMaterialShaderPathIds(path);
            Dictionary<long, string> eftToShaderName = LoadEftPathIdToShaderNameMap();
            Dictionary<string, string> matToShaderName = BuildMaterialToShaderName(matToShaderPathId, eftToShaderName);

            unityBundle = AssetBundle.LoadFromMemory(unpackedBytes);
            if (unityBundle == null)
            {
                Debug.LogError($"[BundleLoader] [{label}] AssetBundle.LoadFromMemory returned null");
                return false;
            }

            int assigned = 0, missingShader = 0;
            UnityEngine.Object[] assets = unityBundle.LoadAllAssets();
            foreach (UnityEngine.Object asset in assets)
            {
                GameObject go = asset as GameObject;
                if (go == null) continue;

                GameObject instance = Instantiate(go);
                instance.name = go.name;
                Undo.RegisterCreatedObjectUndo(instance, $"Load bundle {label}");
                AssignShaders(instance, matToShaderName, ref assigned, ref missingShader);
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
        finally
        {
            if (unityBundle != null) unityBundle.Unload(false);
        }
    }

    private static byte[] UnpackAndSwapAsm(string path, out int asmChanges)
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

    private static Dictionary<string, long> ExtractMaterialShaderPathIds(string path)
    {
        Dictionary<string, long> map = new Dictionary<string, long>();
        try
        {
            AssetsManager am = new AssetsManager();
            try
            {
                BundleFileInstance bundleInstance = am.LoadBundleFile(path);
                AssetsFileInstance assetsFile = am.LoadAssetsFileFromBundle(bundleInstance, 0);

                foreach (AssetFileInfo info in assetsFile.file.AssetInfos)
                {
                    AssetTypeValueField baseField;
                    try
                    {
                        baseField = am.GetBaseField(assetsFile, info);
                    }
                    catch
                    {
                        continue;
                    }

                    if (baseField == null) continue;
                    if (!string.Equals(baseField.TypeName, "Material", StringComparison.Ordinal)) continue;

                    string matName = baseField.Get("m_Name").AsString;
                    if (string.IsNullOrEmpty(matName)) continue;

                    AssetTypeValueField shaderField = baseField.Get("m_Shader");
                    if (shaderField == null) continue;

                    AssetTypeValueField pathIdField = shaderField.Get("m_PathID");
                    if (pathIdField == null) continue;

                    long shaderPathId = pathIdField.AsLong;
                    if (shaderPathId == 0L) continue;

                    map[matName] = shaderPathId;
                }
            }
            finally
            {
                am.UnloadAll();
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        return map;
    }

    private static Dictionary<long, string> LoadEftPathIdToShaderNameMap()
    {
        string fullPath = Path.Combine(Application.dataPath, k_PathDataFileRelativePath)
            .Replace('/', Path.DirectorySeparatorChar);
        if (!File.Exists(fullPath)) return new Dictionary<long, string>();

        try
        {
            string json = File.ReadAllText(fullPath);
            PathIdMapJson data = JsonConvert.DeserializeObject<PathIdMapJson>(json);
            if (data == null || data.eft == null || data.descriptionList == null)
                return new Dictionary<long, string>();

            Dictionary<long, string> map = new Dictionary<long, string>();
            int count = Math.Min(data.eft.Count, data.descriptionList.Count);
            for (int i = 0; i < count; i++)
            {
                string desc = data.descriptionList[i];
                if (string.IsNullOrEmpty(desc)) continue;

                int pipeIdx = desc.IndexOf('|');
                if (pipeIdx < 0) continue;

                string category = desc.Substring(0, pipeIdx).Trim();
                if (!string.Equals(category, k_ShadersCategory, StringComparison.OrdinalIgnoreCase)) continue;

                string shaderName = desc.Substring(pipeIdx + 1).Trim();
                if (string.IsNullOrEmpty(shaderName)) continue;

                long eft = data.eft[i];
                if (eft == 0L) continue;

                if (!map.ContainsKey(eft)) map[eft] = shaderName;
            }
            return map;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return new Dictionary<long, string>();
        }
    }

    private static Dictionary<string, string> BuildMaterialToShaderName(
        Dictionary<string, long> matToShaderPathId,
        Dictionary<long, string> eftPathIdToShaderName)
    {
        Dictionary<string, string> result = new Dictionary<string, string>();
        foreach (var kv in matToShaderPathId)
        {
            if (eftPathIdToShaderName.TryGetValue(kv.Value, out string shaderName))
            {
                result[kv.Key] = shaderName;
            }
        }
        return result;
    }

    private static void AssignShaders(GameObject instance, Dictionary<string, string> matToShaderName, ref int assigned, ref int missingShader)
    {
        if (matToShaderName == null || matToShaderName.Count == 0) return;

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                if (!matToShaderName.TryGetValue(m.name, out string shaderName)) continue;

                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    m.shader = shader;
                    assigned++;
                }
                else
                {
                    missingShader++;
                }
            }
        }
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

    [Serializable]
    private class PathIdMapJson
    {
        public List<long> sdk;
        public List<long> eft;
        public List<string> descriptionList;
    }
}
