using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AssetBundleBrowser.Custom
{
    [Serializable]
    internal class AssetBundleCategoriesPanel
    {
        internal const float DefaultWidth = 180f;

        [SerializeField] private string _selectedCategory = CategoryStorage.AllCategoryName;
        [SerializeField] private bool _showDependencies;
        private Vector2 _scroll;

        internal string SelectedCategory
        {
            get
            {
                if (string.IsNullOrEmpty(_selectedCategory)) _selectedCategory = CategoryStorage.AllCategoryName;
                return _selectedCategory;
            }
        }

        internal bool ShowDependencies => _showDependencies;

        internal bool IsAllSelected =>
            string.Equals(SelectedCategory, CategoryStorage.AllCategoryName, StringComparison.OrdinalIgnoreCase);

        internal event Action SelectionChanged;

        internal void SelectAll()
        {
            if (_selectedCategory != CategoryStorage.AllCategoryName)
            {
                _selectedCategory = CategoryStorage.AllCategoryName;
                SelectionChanged?.Invoke();
            }
        }

        internal void OnGUI(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            using (new GUILayout.AreaScope(rect))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);
                EditorGUILayout.Space(2f);

                DrawCategoryButton(CategoryStorage.AllCategoryName, null);

                EditorGUILayout.Space(4f);
                using (new EditorGUI.DisabledScope(IsAllSelected))
                {
                    bool newShow = EditorGUILayout.ToggleLeft(
                        new GUIContent("Show Dependencies",
                            "Also include bundles that the categorized bundles recursively depend on."),
                        _showDependencies);
                    if (newShow != _showDependencies)
                    {
                        _showDependencies = newShow;
                        SelectionChanged?.Invoke();
                    }
                }
                EditorGUILayout.Space(2f);

                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                List<CategoryData> categories = CategoryStorage.Data.Categories;
                for (int i = 0; i < categories.Count; i++)
                {
                    CategoryData cat = categories[i];
                    DrawCategoryButton(cat.Name, cat);
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(4f);
                if (GUILayout.Button(new GUIContent("+ Add Category", "Create a new category."), GUILayout.Height(24f)))
                {
                    AssetBundleAddCategoryWindow.Show(() =>
                    {
                        SelectionChanged?.Invoke();
                    });
                }

                EditorGUILayout.Space(2f);
                DrawBuildSelectedCategoryButton();
                EditorGUILayout.Space(4f);
            }
        }

        private void DrawBuildSelectedCategoryButton()
        {
            CategoryData selectedData = CategoryStorage.FindByName(SelectedCategory);
            int selectedBundleCount = selectedData?.BundleNames?.Count ?? 0;
            bool disabled = IsAllSelected || selectedData == null || selectedBundleCount == 0;

            using (new EditorGUI.DisabledScope(disabled))
            {
                string label = IsAllSelected
                    ? "Build Selected Category"
                    : $"Build '{SelectedCategory}' ({selectedBundleCount})";
                string tooltip = IsAllSelected
                    ? "Select a category first."
                    : selectedBundleCount == 0
                        ? $"Category '{SelectedCategory}' has no bundles assigned."
                        : $"Build every bundle in '{SelectedCategory}' plus their recursive dependencies. Uses the category's Build Location if set, otherwise <Output Path>/{SelectedCategory}/.";

                if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(24f)))
                {
                    AssetBundleBrowserMain browser = AssetBundleBrowserMain.instance;
                    if (browser == null || browser.m_BuildTab == null)
                    {
                        Debug.LogError("AssetBundle Browser Build tab is not available.");
                        return;
                    }
                    browser.m_BuildTab.BuildCategory(SelectedCategory);
                }
            }
        }

        private void DrawCategoryButton(string name, CategoryData data)
        {
            bool isSelected = string.Equals(_selectedCategory, name, StringComparison.OrdinalIgnoreCase);
            GUIStyle style = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
            };

            using (new EditorGUILayout.HorizontalScope())
            {
                Color prevBg = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = new Color(0.35f, 0.55f, 0.85f, 1f);

                GUIContent content = data == null
                    ? new GUIContent(name, "Show all bundles (default).")
                    : new GUIContent(name, BuildTooltip(data));

                if (GUILayout.Button(content, style, GUILayout.Height(22f)))
                {
                    if (_selectedCategory != name)
                    {
                        _selectedCategory = name;
                        SelectionChanged?.Invoke();
                    }
                }

                GUI.backgroundColor = prevBg;

                if (data != null)
                {
                    if (GUILayout.Button(new GUIContent("×", $"Delete category '{data.Name}'."), GUILayout.Width(22f), GUILayout.Height(22f)))
                    {
                        if (EditorUtility.DisplayDialog(
                            "Delete Category",
                            $"Delete category '{data.Name}'?\n\nBundles previously assigned to it will become uncategorized. Bundles themselves are not deleted.",
                            "Delete",
                            "Cancel"))
                        {
                            CategoryStorage.RemoveCategory(data.Name);
                            if (string.Equals(_selectedCategory, data.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                _selectedCategory = CategoryStorage.AllCategoryName;
                            }
                            SelectionChanged?.Invoke();
                        }
                    }
                }
            }
        }

        private static string BuildTooltip(CategoryData data)
        {
            string loc = string.IsNullOrEmpty(data.BuildLocation)
                ? "<Output Path>/" + data.Name + "/"
                : data.BuildLocation;
            int count = data.BundleNames?.Count ?? 0;
            return $"{data.Name}\nBuild location: {loc}\nBundles: {count}";
        }
    }
}
