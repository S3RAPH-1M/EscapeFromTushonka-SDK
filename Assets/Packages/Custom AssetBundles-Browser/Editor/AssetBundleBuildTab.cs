using AssetBundleBrowser.AssetBundleDataSource;
using AssetBundleBrowser.AssetBundleModel;
using AssetBundleBrowser.Custom;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEngine;

namespace AssetBundleBrowser
{
    [Serializable]
    internal class AssetBundleBuildTab
    {
        const string k_BuildPrefPrefix = "ABBBuild:";

        private string m_streamingPath = "Assets/StreamingAssets";

        [SerializeField]
        private bool m_AdvancedSettings;

        [SerializeField]
        private Vector2 m_ScrollPosition;


        private class ToggleData
        {
            internal ToggleData(bool s, 
                string title, 
                string tooltip,
                List<string> onToggles,
                BuildAssetBundleOptions opt = BuildAssetBundleOptions.None)
            {
                if (onToggles.Contains(title))
                    state = true;
                else
                    state = s;
                content = new GUIContent(title, tooltip);
                option = opt;
            }
            //internal string prefsKey
            //{ get { return k_BuildPrefPrefix + content.text; } }
            internal bool state;
            internal GUIContent content;
            internal BuildAssetBundleOptions option;
        }

        private AssetBundleInspectTab m_InspectTab;

        [SerializeField]
        private BuildTabData m_UserData;

        List<ToggleData> m_ToggleData;
        ToggleData m_ForceRebuild;
        ToggleData m_CopyToStreaming;
        GUIContent m_TargetContent;
        GUIContent m_CompressionContent;
        internal enum CompressOptions
        {
            Uncompressed = 0,
            StandardCompression,
            ChunkBasedCompression,
        }
        GUIContent[] m_CompressionOptions =
        {
            new GUIContent("No Compression"),
            new GUIContent("Standard Compression (LZMA)"),
            new GUIContent("Chunk Based Compression (LZ4)")
        };
        int[] m_CompressionValues = { 0, 1, 2 };


        internal AssetBundleBuildTab()
        {
            m_AdvancedSettings = false;
            m_UserData = new BuildTabData
            {
	            m_OnToggles = new List<string>(),
	            m_UseDefaultPath = true
            };
        }

        internal void OnDisable()
        {
            string dataPath = Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AssetBundleBrowserBuild.dat";

            var bf = new BinaryFormatter();
            FileStream file = File.Create(dataPath);

            bf.Serialize(file, m_UserData);
            file.Close();

        }
        internal void OnEnable(EditorWindow parent)
        {
            m_InspectTab = (parent as AssetBundleBrowserMain)?.m_InspectTab;

            //LoadData...
            string dataPath = Path.GetFullPath(".");
            dataPath = dataPath.Replace("\\", "/");
            dataPath += "/Library/AssetBundleBrowserBuild.dat";

            if (File.Exists(dataPath))
            {
                var bf = new BinaryFormatter();
                FileStream file = File.Open(dataPath, FileMode.Open);
                if (bf.Deserialize(file) is BuildTabData data)
                    m_UserData = data;
                file.Close();
            }
            
            m_ToggleData = new List<ToggleData>
            {
	            new ToggleData(
		            false,
		            "Exclude Type Information",
		            "Do not include type information within the asset bundle (don't write type tree).",
		            m_UserData.m_OnToggles,
		            BuildAssetBundleOptions.DisableWriteTypeTree),
	            new ToggleData(
		            false,
		            "Force Rebuild",
		            "Force rebuild the asset bundles",
		            m_UserData.m_OnToggles,
		            BuildAssetBundleOptions.ForceRebuildAssetBundle),
	            new ToggleData(
		            false,
		            "Ignore Type Tree Changes",
		            "Ignore the type tree changes when doing the incremental build check.",
		            m_UserData.m_OnToggles,
		            BuildAssetBundleOptions.IgnoreTypeTreeChanges),
	            new ToggleData(
		            false,
		            "Append Hash",
		            "Append the hash to the assetBundle name.",
		            m_UserData.m_OnToggles,
		            BuildAssetBundleOptions.AppendHashToAssetBundleName),
	            new ToggleData(
		            false,
		            "Strict Mode",
		            "Do not allow the build to succeed if any errors are reporting during it.",
		            m_UserData.m_OnToggles,
		            BuildAssetBundleOptions.StrictMode),
	            new ToggleData(
		            false,
		            "Dry Run Build",
		            "Do a dry run build.",
		            m_UserData.m_OnToggles,
		            BuildAssetBundleOptions.DryRunBuild)
            };


            m_ForceRebuild = new ToggleData(
                false,
                "Clear Folders",
                "Will wipe out all contents of build directory as well as StreamingAssets/AssetBundles if you are choosing to copy build there.",
                m_UserData.m_OnToggles);
            m_CopyToStreaming = new ToggleData(
                false,
                "Copy to StreamingAssets",
                "After build completes, will copy all build content to " + m_streamingPath + " for use in stand-alone player.",
                m_UserData.m_OnToggles);

            m_TargetContent = new GUIContent("Build Target", "Choose target platform to build for.");
            m_CompressionContent = new GUIContent("Compression", "Choose no compress, standard (LZMA), or chunk based (LZ4)");

            if(m_UserData.m_UseDefaultPath)
            {
                ResetPathToDefault();
            }
        }

        internal void OnGUI()
        {
            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
            bool newState = false;
            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
            {
	            alignment = TextAnchor.UpperCenter
            };
            GUILayout.Label(new GUIContent("Example build setup"), centeredStyle);
            //basic options
            EditorGUILayout.Space();
            GUILayout.BeginVertical();

            // build target
            using (new EditorGUI.DisabledScope (!Model.DataSource.CanSpecifyBuildTarget)) {
                ValidBuildTarget tgt = (ValidBuildTarget)EditorGUILayout.EnumPopup(m_TargetContent, m_UserData.m_BuildTarget);
                if (tgt != m_UserData.m_BuildTarget)
                {
                    m_UserData.m_BuildTarget = tgt;
                    if(m_UserData.m_UseDefaultPath)
                    {
                        m_UserData.m_OutputPath = "AssetBundles/";
                        m_UserData.m_OutputPath += m_UserData.m_BuildTarget.ToString();
                        //EditorUserBuildSettings.SetPlatformSettings(EditorUserBuildSettings.activeBuildTarget.ToString(), "AssetBundleOutputPath", m_OutputPath);
                    }
                }
            }


            ////output path
            using (new EditorGUI.DisabledScope (!Model.DataSource.CanSpecifyBuildOutputDirectory)) {
                EditorGUILayout.Space();
                GUILayout.BeginHorizontal();
                string newPath = EditorGUILayout.TextField("Output Path", m_UserData.m_OutputPath);
                if (!string.IsNullOrEmpty(newPath) && newPath != m_UserData.m_OutputPath)
                {
                    m_UserData.m_UseDefaultPath = false;
                    m_UserData.m_OutputPath = newPath;
                    //EditorUserBuildSettings.SetPlatformSettings(EditorUserBuildSettings.activeBuildTarget.ToString(), "AssetBundleOutputPath", m_OutputPath);
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Browse", GUILayout.MaxWidth(75f)))
                    BrowseForFolder();
                if (GUILayout.Button("Reset", GUILayout.MaxWidth(75f)))
                    ResetPathToDefault();
                //if (string.IsNullOrEmpty(m_OutputPath))
                //    m_OutputPath = EditorUserBuildSettings.GetPlatformSettings(EditorUserBuildSettings.activeBuildTarget.ToString(), "AssetBundleOutputPath");
                GUILayout.EndHorizontal();
                EditorGUILayout.Space();

                newState = GUILayout.Toggle(
                    m_ForceRebuild.state,
                    m_ForceRebuild.content);
                if (newState != m_ForceRebuild.state)
                {
                    if (newState)
                        m_UserData.m_OnToggles.Add(m_ForceRebuild.content.text);
                    else
                        m_UserData.m_OnToggles.Remove(m_ForceRebuild.content.text);
                    m_ForceRebuild.state = newState;
                }
                newState = GUILayout.Toggle(
                    m_CopyToStreaming.state,
                    m_CopyToStreaming.content);
                if (newState != m_CopyToStreaming.state)
                {
                    if (newState)
                        m_UserData.m_OnToggles.Add(m_CopyToStreaming.content.text);
                    else
                        m_UserData.m_OnToggles.Remove(m_CopyToStreaming.content.text);
                    m_CopyToStreaming.state = newState;
                }
            }

            // advanced options
            using (new EditorGUI.DisabledScope (!Model.DataSource.CanSpecifyBuildOptions)) {
                EditorGUILayout.Space();
                m_AdvancedSettings = EditorGUILayout.Foldout(m_AdvancedSettings, "Advanced Settings");
                if(m_AdvancedSettings)
                {
                    int indent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 1;
                    var cmp = (CompressOptions)EditorGUILayout.IntPopup(
                        m_CompressionContent, 
                        (int)m_UserData.m_Compression,
                        m_CompressionOptions,
                        m_CompressionValues);

                    m_UserData.m_Compression = cmp;

                    bool verboseLogs = AssetBundleBrowserMain.VerboseLogs;
                    bool newVerbose = EditorGUILayout.ToggleLeft(
                        new GUIContent("Verbose Logs", "Print per-bundle step timing logs and dictionary-load logs to the Console during builds."),
                        verboseLogs);
                    if (newVerbose != verboseLogs)
                    {
                        AssetBundleBrowserMain.VerboseLogs = newVerbose;
                    }

                    foreach (ToggleData tog in m_ToggleData)
                    {
                        newState = EditorGUILayout.ToggleLeft(
                            tog.content,
                            tog.state);
                        if (newState != tog.state)
                        {
                            if (newState)
                                m_UserData.m_OnToggles.Add(tog.content.text);
                            else
                                m_UserData.m_OnToggles.Remove(tog.content.text);
                            tog.state = newState;
                        }
                    }
                    EditorGUILayout.Space();
                    EditorGUI.indentLevel = indent;
                }
            }

            // build.
            EditorGUILayout.Space();
            if (GUILayout.Button("Build") )
            {
                EditorApplication.delayCall += ExecuteBuild;
            }
            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void ExecuteBuild()
        {
            if (Model.DataSource.CanSpecifyBuildOutputDirectory) {
                if (string.IsNullOrEmpty(m_UserData.m_OutputPath))
                    BrowseForFolder();

                if (string.IsNullOrEmpty(m_UserData.m_OutputPath)) //in case they hit "cancel" on the open browser
                {
                    Debug.LogError("AssetBundle Build: No valid output path for build.");
                    return;
                }

                if (m_ForceRebuild.state)
                {
                    string message = "Do you want to delete all files in the directory " + m_UserData.m_OutputPath;
                    if (m_CopyToStreaming.state)
                        message += " and " + m_streamingPath;
                    message += "?";
                    if (EditorUtility.DisplayDialog("File delete confirmation", message, "Yes", "No"))
                    {
                        try
                        {
                            if (Directory.Exists(m_UserData.m_OutputPath))
                                Directory.Delete(m_UserData.m_OutputPath, true);

                            if (m_CopyToStreaming.state && Directory.Exists(m_streamingPath))
	                            Directory.Delete(m_streamingPath, true);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
                if (!Directory.Exists(m_UserData.m_OutputPath))
                    Directory.CreateDirectory(m_UserData.m_OutputPath);
            }

            var opt = BuildAssetBundleOptions.None;

            if (Model.DataSource.CanSpecifyBuildOptions) {
                if (m_UserData.m_Compression == CompressOptions.Uncompressed)
                    opt |= BuildAssetBundleOptions.UncompressedAssetBundle;
                else
                    opt |= BuildAssetBundleOptions.ChunkBasedCompression;
                foreach (ToggleData tog in m_ToggleData)
                {
                    if (tog.state)
                        opt |= tog.option;
                }
            }

            var assetsManager = new AssetsManager();
            var buildInfo = new ABBuildInfo
            {
                outputDirectory = m_UserData.m_OutputPath,
                options = opt,
                buildTarget = (BuildTarget)m_UserData.m_BuildTarget,
                onBuild = (assetBundleName) =>
                {
	                AssetBundleBrowserMain.instance.m_ReplacerTab.ReplacePathIDs(assetsManager, assetBundleName,
		                m_UserData.m_OutputPath, opt);
	                MoveBundleToCategoryLocation(assetBundleName);
	                if (m_InspectTab == null) return;
	                m_InspectTab.AddBundleFolder(m_UserData.m_OutputPath);
	                m_InspectTab.RefreshBundles();
                }
            };

            Model.DataSource.BuildAssetBundles (buildInfo);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if(m_CopyToStreaming.state)
                DirectoryCopy(m_UserData.m_OutputPath, m_streamingPath);
        }

        internal void BuildSingleBundle(string assetBundleName)
        {
            BuildSingleBundle(assetBundleName, includeDependencies: false);
        }

        internal void BuildSingleBundle(string assetBundleName, bool includeDependencies)
        {
            if (string.IsNullOrEmpty(assetBundleName))
            {
                Debug.LogError("BuildSingleBundle: bundle name is empty.");
                return;
            }
            BuildBundleSet(new[] { assetBundleName }, includeDependencies, $"'{assetBundleName}'");
        }

        internal void BuildCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName))
            {
                Debug.LogError("BuildCategory: category name is empty.");
                return;
            }
            if (string.Equals(categoryName, CategoryStorage.AllCategoryName, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("BuildCategory: cannot build the 'All' pseudo-category. Use the Build tab's Build button for a full build.");
                return;
            }

            CategoryData category = CategoryStorage.FindByName(categoryName);
            if (category == null)
            {
                Debug.LogError($"BuildCategory: category '{categoryName}' not found.");
                return;
            }
            if (category.BundleNames == null || category.BundleNames.Count == 0)
            {
                Debug.LogWarning($"BuildCategory: category '{categoryName}' has no bundles assigned.");
                return;
            }

            BuildBundleSet(category.BundleNames, includeDependencies: true, $"category '{categoryName}' ({category.BundleNames.Count} bundle{(category.BundleNames.Count == 1 ? string.Empty : "s")})");
        }

        private void BuildBundleSet(IEnumerable<string> seedBundleNames, bool includeDependencies, string contextLabel)
        {
            List<string> bundleNames = new List<string>();
            foreach (string name in seedBundleNames)
            {
                if (!string.IsNullOrEmpty(name) && !bundleNames.Contains(name))
                    bundleNames.Add(name);
            }
            int seedCount = bundleNames.Count;

            if (includeDependencies)
            {
                List<string> seeds = new List<string>(bundleNames);
                foreach (string b in seeds)
                {
                    string[] deps = AssetDatabase.GetAssetBundleDependencies(b, recursive: true);
                    if (deps == null) continue;
                    foreach (string d in deps)
                    {
                        if (!string.IsNullOrEmpty(d) && !bundleNames.Contains(d))
                            bundleNames.Add(d);
                    }
                }
            }

            List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
            foreach (string name in bundleNames)
            {
                string[] paths = AssetDatabase.GetAssetPathsFromAssetBundle(name);
                if (paths == null || paths.Length == 0)
                {
                    Debug.LogWarning($"BuildBundleSet: bundle '{name}' has no assets assigned; skipping.");
                    continue;
                }
                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = name,
                    assetNames = paths,
                });
            }

            if (builds.Count == 0)
            {
                Debug.LogError($"BuildBundleSet: no buildable bundles resolved for {contextLabel}.");
                return;
            }

            if (Model.DataSource.CanSpecifyBuildOutputDirectory)
            {
                if (string.IsNullOrEmpty(m_UserData.m_OutputPath))
                {
                    Debug.LogError("BuildBundleSet: Output path is not set. Open the AssetBundle Browser Build tab and set an Output Path first.");
                    return;
                }
                if (!Directory.Exists(m_UserData.m_OutputPath))
                {
                    Directory.CreateDirectory(m_UserData.m_OutputPath);
                }
            }

            var opt = BuildAssetBundleOptions.None;
            if (Model.DataSource.CanSpecifyBuildOptions)
            {
                if (m_UserData.m_Compression == CompressOptions.Uncompressed)
                    opt |= BuildAssetBundleOptions.UncompressedAssetBundle;
                else
                    opt |= BuildAssetBundleOptions.ChunkBasedCompression;
                foreach (ToggleData tog in m_ToggleData)
                {
                    if (tog.state)
                        opt |= tog.option;
                }
            }

            var assetsManager = new AssetsManager();
            BuildTarget target = (BuildTarget)m_UserData.m_BuildTarget;

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                m_UserData.m_OutputPath, builds.ToArray(), opt, target);

            if (manifest == null)
            {
                Debug.LogError($"BuildBundleSet: Unity build failed for {contextLabel}.");
                return;
            }

            foreach (string builtBundleName in manifest.GetAllAssetBundles())
            {
                AssetBundleBrowserMain.instance.m_ReplacerTab.ReplacePathIDs(
                    assetsManager, builtBundleName, m_UserData.m_OutputPath, opt);
                MoveBundleToCategoryLocation(builtBundleName);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            int depCount = builds.Count - seedCount;
            string depsSuffix = includeDependencies && depCount > 0
                ? $" (+ {depCount} dependencies)"
                : string.Empty;
            Debug.Log($"[AssetBundle Build] Built {contextLabel}{depsSuffix} -> {m_UserData.m_OutputPath}");
        }

        private string ResolveCategoryOutputDirectory(string bundleName)
        {
            CategoryData category = CategoryStorage.FindByBundle(bundleName);
            if (category == null) return null;

            if (!string.IsNullOrWhiteSpace(category.BuildLocation))
            {
                try
                {
                    return Path.GetFullPath(category.BuildLocation);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Category '{category.Name}' has invalid BuildLocation '{category.BuildLocation}': {ex.Message}. Falling back to '<OutputPath>/{category.Name}/'.");
                }
            }

            return Path.Combine(m_UserData.m_OutputPath, category.Name);
        }

        private void MoveBundleToCategoryLocation(string bundleName)
        {
            string targetDir = ResolveCategoryOutputDirectory(bundleName);
            if (string.IsNullOrEmpty(targetDir)) return;

            string sourcePath = Path.Combine(m_UserData.m_OutputPath, bundleName);
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"MoveBundleToCategoryLocation: bundle '{bundleName}' not found at expected location '{sourcePath}'.");
                return;
            }

            string sourceFull = Path.GetFullPath(sourcePath);
            string targetPath = Path.Combine(targetDir, bundleName);
            string targetFull = Path.GetFullPath(targetPath);

            if (string.Equals(sourceFull, targetFull, StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                string targetParent = Path.GetDirectoryName(targetFull);
                if (!string.IsNullOrEmpty(targetParent))
                {
                    Directory.CreateDirectory(targetParent);
                }
                if (File.Exists(targetFull)) File.Delete(targetFull);
                File.Move(sourceFull, targetFull);

                string sourceManifest = sourceFull + ".manifest";
                string targetManifest = targetFull + ".manifest";
                if (File.Exists(sourceManifest))
                {
                    if (File.Exists(targetManifest)) File.Delete(targetManifest);
                    File.Move(sourceManifest, targetManifest);
                }

                if (AssetBundleBrowserMain.VerboseLogs)
                    Debug.Log($"[Category Routing] Moved '{bundleName}' -> {targetDir}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to move bundle '{bundleName}' to category location '{targetDir}': {ex.Message}");
            }
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName)
        {
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            foreach (string folderPath in Directory.GetDirectories(sourceDirName, "*", SearchOption.AllDirectories))
            {
                if (!Directory.Exists(folderPath.Replace(sourceDirName, destDirName)))
                    Directory.CreateDirectory(folderPath.Replace(sourceDirName, destDirName));
            }

            foreach (string filePath in Directory.GetFiles(sourceDirName, "*.*", SearchOption.AllDirectories))
            {
                string fileDirName = Path.GetDirectoryName(filePath)!.Replace("\\", "/");
                string fileName = Path.GetFileName(filePath);
                string newFilePath = Path.Combine(fileDirName.Replace(sourceDirName, destDirName), fileName);

                File.Copy(filePath, newFilePath, true);
            }
        }

        private void BrowseForFolder()
        {
            m_UserData.m_UseDefaultPath = false;
            string newPath = EditorUtility.OpenFolderPanel("Bundle Folder", m_UserData.m_OutputPath, string.Empty);
            if (!string.IsNullOrEmpty(newPath))
            {
                string gamePath = Path.GetFullPath(".");
                gamePath = gamePath.Replace("\\", "/");
                if (newPath.StartsWith(gamePath) && newPath.Length > gamePath.Length)
                    newPath = newPath.Remove(0, gamePath.Length+1);
                m_UserData.m_OutputPath = newPath;
                //EditorUserBuildSettings.SetPlatformSettings(EditorUserBuildSettings.activeBuildTarget.ToString(), "AssetBundleOutputPath", m_OutputPath);
            }
        }
        private void ResetPathToDefault()
        {
            m_UserData.m_UseDefaultPath = true;
            m_UserData.m_OutputPath = "AssetBundles/";
            m_UserData.m_OutputPath += m_UserData.m_BuildTarget.ToString();
            //EditorUserBuildSettings.SetPlatformSettings(EditorUserBuildSettings.activeBuildTarget.ToString(), "AssetBundleOutputPath", m_OutputPath);
        }

        //Note: this is the provided BuildTarget enum with some entries removed as they are invalid in the dropdown
        internal enum ValidBuildTarget
        {
            //NoTarget = -2,        --doesn't make sense
            //iPhone = -1,          --deprecated
            //BB10 = -1,            --deprecated
            //MetroPlayer = -1,     --deprecated
            StandaloneOSXUniversal = 2,
            StandaloneOSXIntel = 4,
            StandaloneWindows = 5,
            WebPlayer = 6,
            WebPlayerStreamed = 7,
            iOS = 9,
            PS3 = 10,
            XBOX360 = 11,
            Android = 13,
            StandaloneLinux = 17,
            StandaloneWindows64 = 19,
            WebGL = 20,
            WSAPlayer = 21,
            StandaloneLinux64 = 24,
            StandaloneLinuxUniversal = 25,
            WP8Player = 26,
            StandaloneOSXIntel64 = 27,
            BlackBerry = 28,
            Tizen = 29,
            PSP2 = 30,
            PS4 = 31,
            PSM = 32,
            XboxOne = 33,
            SamsungTV = 34,
            N3DS = 35,
            WiiU = 36,
            tvOS = 37,
            Switch = 38
        }

        [Serializable]
        internal class BuildTabData
        {
            internal List<string> m_OnToggles;
            internal ValidBuildTarget m_BuildTarget = ValidBuildTarget.StandaloneWindows;
            internal CompressOptions m_Compression = CompressOptions.StandardCompression;
            internal string m_OutputPath = string.Empty;
            internal bool m_UseDefaultPath = true;
        }
    }

}