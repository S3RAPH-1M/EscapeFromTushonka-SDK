using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AssetBundleBrowser.Custom
{
    internal class AssetBundleAddCategoryWindow : EditorWindow
    {
        private string _name = string.Empty;
        private string _buildLocation = string.Empty;
        private Action _onSaved;

        internal static void Show(Action onSaved)
        {
            AssetBundleAddCategoryWindow win = CreateInstance<AssetBundleAddCategoryWindow>();
            win.titleContent = new GUIContent("Add Category");
            win.minSize = new Vector2(460f, 160f);
            win.maxSize = new Vector2(700f, 200f);
            win._onSaved = onSaved;
            win.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("New Category", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            _name = EditorGUILayout.TextField("Name", _name);

            using (new EditorGUILayout.HorizontalScope())
            {
                _buildLocation = EditorGUILayout.TextField(
                    new GUIContent("Build Location (optional)", "Absolute folder path. Leave empty to build to <Output Path>/<Category Name>/."),
                    _buildLocation);
                if (GUILayout.Button("Browse", GUILayout.Width(70f)))
                {
                    string picked = EditorUtility.OpenFolderPanel(
                        "Category Build Location",
                        Directory.Exists(_buildLocation) ? _buildLocation : Directory.GetCurrentDirectory(),
                        string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _buildLocation = picked.Replace('/', Path.DirectorySeparatorChar);
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "Leave Build Location empty and bundles in this category will build to '<Output Path>/<Category Name>/'.",
                MessageType.Info);

            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_name)))
                {
                    if (GUILayout.Button("Create", GUILayout.Width(90f), GUILayout.Height(24f)))
                    {
                        if (CategoryStorage.AddCategory(_name, _buildLocation))
                        {
                            _onSaved?.Invoke();
                            Close();
                        }
                    }
                }

                if (GUILayout.Button("Cancel", GUILayout.Width(90f), GUILayout.Height(24f)))
                {
                    Close();
                }
            }
        }
    }
}
