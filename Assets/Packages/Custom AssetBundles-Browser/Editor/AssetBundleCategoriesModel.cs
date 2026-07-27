using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace AssetBundleBrowser.Custom
{
    [Serializable]
    internal class CategoryData
    {
        public string Name;
        public string BuildLocation;
        public List<string> BundleNames;

        public CategoryData()
        {
            BundleNames = new List<string>();
        }

        public CategoryData(string name, string buildLocation)
        {
            Name = name;
            BuildLocation = buildLocation;
            BundleNames = new List<string>();
        }
    }

    [Serializable]
    internal class CategoriesData
    {
        public List<CategoryData> Categories;

        public CategoriesData()
        {
            Categories = new List<CategoryData>();
        }
    }

    internal static class CategoryStorage
    {
        private const string k_RelativePath = "Assets/Packages/Custom AssetBundles-Browser/category_data.json";
        internal const string AllCategoryName = "All";

        private static CategoriesData s_Data;
        internal static event Action Changed;

        internal static CategoriesData Data
        {
            get
            {
                if (s_Data == null) Load();
                return s_Data;
            }
        }

        internal static string FilePath => $"{Directory.GetCurrentDirectory()}/{k_RelativePath}";

        internal static void Load()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path))
                {
                    s_Data = new CategoriesData();
                    return;
                }

                string json = File.ReadAllText(path);
                CategoriesData loaded = JsonConvert.DeserializeObject<CategoriesData>(json);
                s_Data = loaded ?? new CategoriesData();
                if (s_Data.Categories == null) s_Data.Categories = new List<CategoryData>();
                foreach (CategoryData c in s_Data.Categories)
                {
                    if (c.BundleNames == null) c.BundleNames = new List<string>();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load category_data.json: {ex.Message}");
                s_Data = new CategoriesData();
            }
        }

        internal static void Save()
        {
            try
            {
                string path = FilePath;
                string json = JsonConvert.SerializeObject(Data, Formatting.Indented);
                File.WriteAllText(path, json);
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save category_data.json: {ex.Message}");
            }
        }

        internal static bool CategoryExists(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Data.Categories.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        internal static CategoryData FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return Data.Categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        internal static CategoryData FindByBundle(string bundleName)
        {
            if (string.IsNullOrEmpty(bundleName)) return null;
            return Data.Categories.FirstOrDefault(c => c.BundleNames != null && c.BundleNames.Contains(bundleName));
        }

        internal static bool AddCategory(string name, string buildLocation)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.LogError("Category name cannot be empty.");
                return false;
            }
            name = name.Trim();
            if (string.Equals(name, AllCategoryName, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"Category name '{AllCategoryName}' is reserved.");
                return false;
            }
            if (CategoryExists(name))
            {
                Debug.LogError($"Category '{name}' already exists.");
                return false;
            }

            Data.Categories.Add(new CategoryData(name, string.IsNullOrWhiteSpace(buildLocation) ? null : buildLocation.Trim()));
            Save();
            return true;
        }

        internal static bool RemoveCategory(string name)
        {
            CategoryData cat = FindByName(name);
            if (cat == null) return false;
            Data.Categories.Remove(cat);
            Save();
            return true;
        }

        internal static bool RenameCategory(string oldName, string newName)
        {
            CategoryData cat = FindByName(oldName);
            if (cat == null) return false;
            if (string.IsNullOrWhiteSpace(newName)) return false;
            newName = newName.Trim();
            if (string.Equals(newName, AllCategoryName, StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase) && CategoryExists(newName)) return false;
            cat.Name = newName;
            Save();
            return true;
        }

        internal static void SetBuildLocation(string categoryName, string buildLocation)
        {
            CategoryData cat = FindByName(categoryName);
            if (cat == null) return;
            cat.BuildLocation = string.IsNullOrWhiteSpace(buildLocation) ? null : buildLocation.Trim();
            Save();
        }

        internal static void AssignBundle(string bundleName, string categoryName)
        {
            if (string.IsNullOrEmpty(bundleName)) return;

            foreach (CategoryData c in Data.Categories)
            {
                if (c.BundleNames != null) c.BundleNames.Remove(bundleName);
            }

            if (!string.IsNullOrEmpty(categoryName) &&
                !string.Equals(categoryName, AllCategoryName, StringComparison.OrdinalIgnoreCase))
            {
                CategoryData target = FindByName(categoryName);
                if (target != null && !target.BundleNames.Contains(bundleName))
                {
                    target.BundleNames.Add(bundleName);
                }
            }

            Save();
        }
    }
}
