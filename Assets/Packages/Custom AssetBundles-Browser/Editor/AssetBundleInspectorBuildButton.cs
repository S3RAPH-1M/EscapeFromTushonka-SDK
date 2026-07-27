using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace AssetBundleBrowser
{
    [InitializeOnLoad]
    internal static class AssetBundleInspectorBuildButton
    {
        private const string k_FooterName = "AssetBundleBuildFooter_TarkovSDK";
        private const string k_IncludeDepsPrefKey = "AssetBundleBrowser.InspectorBuildButton.IncludeDeps";
        private const double k_CheckIntervalSeconds = 0.5;

        private static readonly Type s_InspectorWindowType;
        private static readonly HashSet<int> s_InjectedWindowIds = new HashSet<int>();
        private static double s_NextCheckTime;

        static AssetBundleInspectorBuildButton()
        {
            s_InspectorWindowType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (s_InspectorWindowType == null) return;
            if (EditorApplication.timeSinceStartup < s_NextCheckTime) return;
            s_NextCheckTime = EditorApplication.timeSinceStartup + k_CheckIntervalSeconds;

            s_InjectedWindowIds.RemoveWhere(id => EditorUtility.InstanceIDToObject(id) == null);

            Object[] windows = Resources.FindObjectsOfTypeAll(s_InspectorWindowType);
            foreach (Object obj in windows)
            {
                EditorWindow window = obj as EditorWindow;
                if (window == null) continue;

                int id = window.GetInstanceID();
                if (s_InjectedWindowIds.Contains(id)) continue;

                VisualElement root = window.rootVisualElement;
                if (root == null) continue;

                if (root.Q(k_FooterName) != null)
                {
                    s_InjectedWindowIds.Add(id);
                    continue;
                }

                IMGUIContainer footer = new IMGUIContainer(DrawFooter);
                footer.name = k_FooterName;
                footer.style.marginTop = 4;
                footer.style.marginBottom = 8;
                footer.style.marginLeft = 8;
                footer.style.marginRight = 8;
                footer.style.flexGrow = 0;
                root.Add(footer);
                s_InjectedWindowIds.Add(id);
            }
        }

        private static void DrawFooter()
        {
            Object target = Selection.activeObject;
            if (target == null) return;
            if (!AssetDatabase.Contains(target)) return;

            string assetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(assetPath)) return;

            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset != target) return;

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null) return;

            string bundleName = importer.assetBundleName;
            if (string.IsNullOrEmpty(bundleName)) return;

            string variant = importer.assetBundleVariant;
            string fullBundleName = string.IsNullOrEmpty(variant)
                ? bundleName
                : $"{bundleName}.{variant}";

            GUIContent buttonContent = new GUIContent(
                $"Build AssetBundle: {fullBundleName}",
                "Build only this AssetBundle using the AssetBundle Browser Build tab's current settings.");

            GUIContent toggleContent = new GUIContent(
                "Build with Dependencies",
                "Also build every AssetBundle this one depends on (recursive).");

            bool includeDeps = EditorPrefs.GetBool(k_IncludeDepsPrefKey, false);

            EditorGUILayout.BeginHorizontal();
            bool clicked = GUILayout.Button(buttonContent);
            bool newIncludeDeps = GUILayout.Toggle(includeDeps, toggleContent, GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();

            if (newIncludeDeps != includeDeps)
            {
                EditorPrefs.SetBool(k_IncludeDepsPrefKey, newIncludeDeps);
                includeDeps = newIncludeDeps;
            }

            if (clicked)
            {
                AssetBundleBrowserMain browser = AssetBundleBrowserMain.instance;
                if (browser == null || browser.m_BuildTab == null)
                {
                    Debug.LogError("AssetBundle Browser is not available; cannot build.");
                    return;
                }
                browser.m_BuildTab.BuildSingleBundle(fullBundleName, includeDeps);
            }
        }
    }
}
