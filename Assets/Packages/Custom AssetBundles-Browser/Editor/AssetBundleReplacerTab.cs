using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AssetBundleBrowser.Custom
{
    [Serializable]
    public class AssetBundleReplacerTab
    {
        [SerializeField] private DictionaryData data;
        private long _key;
        private long _value;
        private bool _logging;
        private Rect _position;
        private Vector2 _scrollPosition;
        AssetBundleCabReplacerTab _tab;
        public AssetBundleReplacerTab(AssetBundleCabReplacerTab tab) 
        {
            _tab = tab;
        }

        internal void OnEnable(Rect pos)
        {
            OnEnable(pos, null);
        }

        internal void OnEnable(Rect pos, AssetBundleCabReplacerTab cabTab)
        {
            if (cabTab != null) _tab = cabTab;
            data = GetDataFromFile();
            _position = pos;
        }

        internal void OnGUI(Rect pos)
        {
            _position = new Rect(pos.position, pos.size);

            OnGUIEditor();
        }

        private void OnGUIEditor()
        {
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) {alignment = TextAnchor.MiddleCenter, fontSize = 16};
            _logging = GUI.Toggle(new Rect(17, 5, 15, 15), _logging, new GUIContent("", "Enable logging"));
            GUILayout.BeginArea(_position, titleStyle);

            EditorGUILayout.BeginHorizontal();
            DrawGUIField("SDK PathID", ref _key, titleStyle);
            GUILayout.Label("Made by SamSWAT", new GUIStyle(titleStyle){margin = new RectOffset(0,0,15,0)});
            DrawGUIField("EFT PathID", ref _value, titleStyle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5f);
            if (GUILayout.Button("ADD ENTRY"))
            {
                if (data.sdk.Contains(_key))
                {
                    Debug.LogError("This SDK PathID is already defined");
                    return;
                }

                data.Add(_key, _value);
            }

            if (GUILayout.Button("CLEAR EVERYTHING"))
            {
                data.Clear();
            }

            if (GUILayout.Button("SAVE DATA TO FILE"))
            {
                WriteDataToFile();
            }

            if (GUILayout.Button("GET DATA FROM FILE"))
            {
                data = GetDataFromFile();
            }

            if (GUILayout.Button("SORT AND SAVE"))
            {
                data.SortAndSaveData();
            }

            //==\\ VISUAL DICTIONARY REPRESENTATION //==\\
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            DrawKeys();
            DrawButtons();
            DrawValues();
            DrawDescriptions();
            GUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawGUIField(string label, ref long field, GUIStyle titleStyle)
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label(label, titleStyle);
            GUILayout.Space(5f);
            field = EditorGUILayout.LongField(field);
            EditorGUILayout.EndVertical();
        }

        private void DrawKeys()
        {
            GUILayout.BeginVertical(GUILayout.Width(_position.width * 0.25f));
            for (var i = 0; i < data.sdk.Count; i++)
            {
                GUILayout.Space(1f);
                EditorGUI.BeginChangeCheck();
                var key = EditorGUILayout.LongField(data.sdk[i]);
                if (!EditorGUI.EndChangeCheck()) continue;

                if (data.sdk.Contains(key))
                {
                    Debug.LogError($"{key} is already defined in SDK PathIDs");
                    break;
                }

                data.sdk[i] = key;
            }

            GUILayout.EndVertical();
        }

        private void DrawValues()
        {
            GUILayout.BeginVertical(GUILayout.Width(_position.width * 0.25f));
            for (var i = 0; i < data.eft.Count; i++)
            {
                GUILayout.Space(1f);
                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.LongField(data.eft[i]);
                if (!EditorGUI.EndChangeCheck()) continue;
                data.eft[i] = value;
            }

            GUILayout.EndVertical();
        }

        private void DrawButtons()
        {
            GUILayout.BeginVertical(GUILayout.MaxWidth(25f));
            for (var i = 0; i < data.sdk.Count; i++)
            {
                if (!GUILayout.Button(new GUIContent("∧", "Move item up"))) continue;
                var newIndex = i - 1;
                if (newIndex < 0) return;
                data.Move(i, newIndex);
            }
            GUILayout.EndVertical();
            
            GUILayout.BeginVertical(GUILayout.MaxWidth(25f));
            for (var i = 0; i < data.sdk.Count; i++)
            {
                if (!GUILayout.Button(new GUIContent("x", "Remove item"))) continue;
                data.RemoveAt(i);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.MaxWidth(25f));
            for (var i = 0; i < data.sdk.Count; i++)
            {
                if (!GUILayout.Button(new GUIContent("∨", "Move item down"))) continue;
                var newIndex = i + 1;
                if (newIndex >= data.sdk.Count) return;
                data.Move(i, newIndex);
            }
            GUILayout.EndVertical();
        }

        private void DrawDescriptions()
        {
            GUILayout.BeginVertical();
            for (var i = 0; i < data.sdk.Count; i++)
            {
                GUILayout.Space(1f);
                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.TextField(data.descriptionList[i]);
                if (!EditorGUI.EndChangeCheck()) continue;
                data.descriptionList[i] = value;
            }

            GUILayout.EndVertical();
        }

        private void WriteDataToFile()
        {
            var path = $"{Directory.GetCurrentDirectory()}/Assets/Packages/Custom AssetBundles-Browser/path_data.json";
            using var streamWriter = new StreamWriter(path);
            string json = JsonUtility.ToJson(data, true);
            streamWriter.Write(json);
        }

        private DictionaryData GetDataFromFile()
        {
            var path = $"{Directory.GetCurrentDirectory()}/Assets/Packages/Custom AssetBundles-Browser/path_data.json";
            try
            {
	            string json = File.ReadAllText(path);
	            DictionaryData dictionaryData = JsonConvert.DeserializeObject<DictionaryData>(json);

	            if (dictionaryData == null || !dictionaryData.SuitableForDict)
	            {
		            throw new Exception("Json is faulty");
	            }

	            dictionaryData.OnAfterDeserialize();
	            if (AssetBundleBrowserMain.VerboseLogs)
	                Debug.Log($"[PathID dict] loaded {dictionaryData.Lookup.Count} mappings from path_data.json (first key: {(dictionaryData.sdk.Count > 0 ? dictionaryData.sdk[0].ToString() : "none")})");
	            return dictionaryData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Some error occured while reading data at path: {path}, temporary empty data will be used. Exception: {ex}");
                return new DictionaryData();
            }
        }

        public void ReplacePathIDs(AssetsManager assetsManager, string bundleName, string outputDirectory,
	        BuildAssetBundleOptions options)
        {
            System.Diagnostics.Stopwatch total = System.Diagnostics.Stopwatch.StartNew();
            System.Diagnostics.Stopwatch step = new System.Diagnostics.Stopwatch();
            List<string> stepLog = new List<string>();

            void Mark(string name)
            {
                stepLog.Add($"{name}={step.ElapsedMilliseconds}ms");
                step.Restart();
            }

            string path = $"{Directory.GetCurrentDirectory()}/{outputDirectory}/{bundleName}";
            string bundleLabel = Path.GetFileName(path);
            string packedTempPath = path + ".packed";

            try
            {
                long inputSize = File.Exists(path) ? new FileInfo(path).Length : 0;

                step.Start();
                BundleFileInstance bundle = assetsManager.LoadBundleFile(path);
                Mark("load");

                AssetsFileInstance assetsFile = assetsManager.LoadAssetsFileFromBundle(bundle, 0, true);
                Mark("load-assets");

                IList<AssetFileInfo> assetList = assetsFile.file.AssetInfos;
                int pathIdChanges = ApplyPathIdReplacements(assetList, assetsManager, assetsFile,
                    out int walkedCount, out int skipByType, out int skipByScan);
                Mark($"walk({assetList.Count}assets,{pathIdChanges}changed,walk={walkedCount},skipT={skipByType},skipS={skipByScan})");

                if (pathIdChanges > 0)
                {
                    bundle.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(assetsFile.file);
                    Mark("set-dir-data");
                }

                AssetBundleCabReplacerTab cabTab = _tab ?? AssetBundleBrowserMain.instance?.m_CabReplacerTab;
                if (cabTab == null)
                {
                    Debug.LogWarning($"[{bundleLabel}] CAB replacer tab not available; CAB IDs will not be rewritten in this bundle.");
                }
                bool cabHasMappings = cabTab != null && cabTab.HasMappings;
                if (pathIdChanges == 0 && !cabHasMappings)
                {
                    assetsManager.UnloadAll();
                    Mark("unload");
                    total.Stop();
                    if (AssetBundleBrowserMain.VerboseLogs)
                        Debug.Log($"[{bundleLabel}] KEPT unity build (no work) | in={FormatSize(inputSize)} | total={total.ElapsedMilliseconds}ms | {string.Join(" ", stepLog)}");
                    return;
                }

                byte[] uncompressedBytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (AssetsFileWriter writer = new AssetsFileWriter(ms))
                    {
                        bundle.file.Write(writer);
                    }
                    uncompressedBytes = ms.ToArray();
                }
                Mark($"serialize({FormatSize(uncompressedBytes.Length)})");

                assetsManager.UnloadAll();
                Mark("unload");

                int cabChanges = cabHasMappings ? cabTab.ReplaceCabIdsInBuffer(uncompressedBytes) : 0;
                Mark($"cab-scan({cabChanges}changed)");

                int asmChanges = ReplaceAssemblyNamesInBuffer(uncompressedBytes);
                Mark($"asm-scan({asmChanges}changed)");

                if (pathIdChanges + cabChanges + asmChanges == 0)
                {
                    total.Stop();
                    if (AssetBundleBrowserMain.VerboseLogs)
                        Debug.Log($"[{bundleLabel}] KEPT unity build (no matches) | in={FormatSize(inputSize)} | total={total.ElapsedMilliseconds}ms | {string.Join(" ", stepLog)}");
                    return;
                }

                AssetBundleCompressionType compression = ResolveCompressionType(options);

                using (MemoryStream ms = new MemoryStream(uncompressedBytes))
                using (AssetsFileReader reader = new AssetsFileReader(ms))
                {
                    AssetBundleFile modifiedBundle = new AssetBundleFile();
                    modifiedBundle.Read(reader);
                    Mark("reread");

                    using (AssetsFileWriter writer = new AssetsFileWriter(packedTempPath))
                    {
                        if (compression == AssetBundleCompressionType.None)
                            modifiedBundle.Write(writer);
                        else
                            modifiedBundle.Pack(writer, compression);
                    }
                    Mark($"pack-{compression}");
                }

                long outputSize = new FileInfo(packedTempPath).Length;

                File.Delete(path);
                File.Move(packedTempPath, path);
                Mark("finalize");

                total.Stop();
                if (AssetBundleBrowserMain.VerboseLogs)
                    Debug.Log($"[{bundleLabel}] REWROTE | pathIDs={pathIdChanges} CABs={cabChanges} asmRefs={asmChanges} pack={compression} | in={FormatSize(inputSize)} out={FormatSize(outputSize)} | total={total.ElapsedMilliseconds}ms | {string.Join(" ", stepLog)}");
            }
            catch (Exception e)
            {
                try { assetsManager.UnloadAll(); } catch { }
                Debug.LogException(e);
            }
            finally
            {
                if (File.Exists(packedTempPath))
                {
                    try { File.Delete(packedTempPath); } catch { }
                }
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + "B";
            if (bytes < 1048576) return $"{bytes / 1024f:F1}KB";
            if (bytes < 1073741824) return $"{bytes / 1048576f:F1}MB";
            return $"{bytes / 1073741824f:F2}GB";
        }

        private int ApplyPathIdReplacements(IList<AssetFileInfo> assetList, AssetsManager am, AssetsFileInstance assetsFile,
            out int walkedCount, out int skipByType, out int skipByScan)
        {
            walkedCount = 0;
            skipByType = 0;
            skipByScan = 0;

            if (data == null || data.Lookup == null || data.Lookup.Count == 0) return 0;

            Dictionary<int, bool> typeHasPPtrCache = new Dictionary<int, bool>();
            int totalChanges = 0;

            foreach (AssetFileInfo assetInfo in assetList)
            {
                bool ownChanged = ReplacePathId(assetInfo);
                int typeKey = assetInfo.TypeIdOrIndex;

                if (!typeHasPPtrCache.TryGetValue(typeKey, out bool typeHasPPtrs))
                {
                    AssetTypeTemplateField template = am.GetTemplateBaseField(assetsFile, assetInfo);
                    typeHasPPtrs = TemplateHasPPtrField(template);
                    typeHasPPtrCache[typeKey] = typeHasPPtrs;
                }

                if (!typeHasPPtrs)
                {
                    if (ownChanged) totalChanges++;
                    skipByType++;
                    continue;
                }

                AssetTypeValueField baseField = am.GetBaseField(assetsFile, assetInfo);
                int childChanges = RecursiveReplaceChildPathIds(baseField);
                walkedCount++;

                if (ownChanged || childChanges > 0)
                {
                    byte[] newBytes = baseField.WriteToByteArray();
                    assetInfo.SetNewData(newBytes);
                    totalChanges += (ownChanged ? 1 : 0) + childChanges;
                }
            }
            return totalChanges;
        }

        private static bool TemplateHasPPtrField(AssetTypeTemplateField template)
        {
            if (template == null) return false;
            string typeName = template.Type;
            if (typeName != null && typeName.StartsWith("PPtr<")) return true;
            if (template.Children != null)
            {
                foreach (AssetTypeTemplateField child in template.Children)
                {
                    if (TemplateHasPPtrField(child)) return true;
                }
            }
            return false;
        }

        private bool ReplacePathId(AssetFileInfo assetInfo)
        {
            if (assetInfo.PathId == 0) return false;
            if (data.Lookup.TryGetValue(assetInfo.PathId, out long eftPathId))
            {
                assetInfo.PathId = eftPathId;
                return true;
            }
            return false;
        }

        private bool ReplacePathId(AssetTypeValueField field)
        {
            AssetTypeValue fieldValue = field.Get("m_PathID").Value;
            if (fieldValue == null) return false;

            long pathId = fieldValue.AsLong;
            if (pathId == 0) return false;

            if (data.Lookup.TryGetValue(pathId, out long eftPathId))
            {
                fieldValue.AsLong = eftPathId;
                if (_logging)
                    Debug.Log($"Found matching pathID: {pathId} -> {eftPathId} at {field.TypeName}{field.FieldName}");
                return true;
            }
            return false;
        }

        private int RecursiveReplaceChildPathIds(AssetTypeValueField field)
        {
            int count = 0;
            foreach (AssetTypeValueField child in field.Children)
            {
                if (child.TemplateField.HasValue && !child.TemplateField.IsArray)
                    continue;
                if (child.TemplateField.IsArray && child.TemplateField.Children[1].ValueType != AssetValueType.None)
                    continue;

                string typeName = child.TypeName;
                if (typeName.StartsWith("PPtr<") && child.Children.Count == 2)
                {
                    if (ReplacePathId(child)) count++;
                }
                else
                {
                    count += RecursiveReplaceChildPathIds(child);
                }
            }
            return count;
        }

        private static AssetBundleCompressionType ResolveCompressionType(BuildAssetBundleOptions options)
        {
            if ((options & BuildAssetBundleOptions.UncompressedAssetBundle) != 0)
                return AssetBundleCompressionType.None;
            return AssetBundleCompressionType.LZ4;
        }

        private static readonly (string From, string To)[] _assemblyNameReplacements = new[]
        {
            ("Tarkov.Assembly.dll", "Assembly-CSharp.dll"),
            ("Tarkov.Assembly-firstpass.dll", "Assembly-CSharp-firstpass.dll"),
        };

        private static int ReplaceAssemblyNamesInBuffer(byte[] bytes)
        {
            int totalReplacements = 0;
            foreach (var pair in _assemblyNameReplacements)
            {
                byte[] from = Encoding.UTF8.GetBytes(pair.From);
                byte[] to = Encoding.UTF8.GetBytes(pair.To);
                if (from.Length != to.Length)
                {
                    Debug.LogError($"Assembly name replacement length mismatch: '{pair.From}' ({from.Length}) -> '{pair.To}' ({to.Length}). Skipping this pair; byte-level replacement requires equal lengths.");
                    continue;
                }
                if (bytes.Length < from.Length) continue;

                int searchStart = 0;
                ReadOnlySpan<byte> fromSpan = from.AsSpan();
                while (searchStart <= bytes.Length - from.Length)
                {
                    int found = bytes.AsSpan(searchStart).IndexOf(fromSpan);
                    if (found < 0) break;

                    int matchAt = searchStart + found;
                    Array.Copy(to, 0, bytes, matchAt, to.Length);
                    totalReplacements++;
                    searchStart = matchAt + from.Length;
                }
            }
            return totalReplacements;
        }
    }

    [Serializable]
    internal class DictionaryData : ISerializationCallbackReceiver 
    {
        public DictionaryData()
        {
            sdk = new List<long>();
            eft = new List<long>();
            descriptionList = new List<string>();
            Lookup = new Dictionary<long, long>();
        }
        
        public List<long> sdk;
        public List<long> eft;
        public List<string> descriptionList;
        public Dictionary<long, long> Lookup;
        
        public bool SuitableForDict => sdk.Count == eft.Count;
        

        public void Add(long key, long value, string description = "")
        {
            sdk.Add(key);
            eft.Add(value);
            descriptionList.Add(description);
            Lookup.Add(key, value);
        }

        public void Clear()
        {
            sdk.Clear();
            eft.Clear();
            descriptionList.Clear();
            Lookup.Clear();
        }

        public void RemoveAt(int i)
        {
            var keyToDelete = sdk[i];
            sdk.RemoveAt(i);
            eft.RemoveAt(i);
            descriptionList.RemoveAt(i);
            Lookup.Remove(keyToDelete);
        }
        
        public void Move(int oldIndex, int newIndex)
        {
            var key = sdk[oldIndex];
            var value = eft[oldIndex];
            var desc = descriptionList[oldIndex];
            
            RemoveAt(oldIndex);

            //if (newIndex > oldIndex) newIndex--; 

            sdk.Insert(newIndex, key);
            eft.Insert(newIndex, value);
            descriptionList.Insert(newIndex, desc);
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            Lookup = new Dictionary<long, long>();
            for (int i = 0; i != Math.Min(sdk.Count, eft.Count); i++)
            {
                Lookup.Add(sdk[i], eft[i]);
            }
            FixMissingDescriptions();
        }

        private void FixMissingDescriptions()
        {
            if (sdk.Count == descriptionList.Count) return;
            
            foreach (var _ in sdk)
            {
                descriptionList.Add("");
            }
        }

        public void SortAndSaveData()
        {
            var sortedIndices = GetSortedIndices(descriptionList, true); // true for ascending (alphabetical order)

            SortListsByIndices(sortedIndices);
            WriteDataToFile();
        }

        private List<int> GetSortedIndices(List<string> list, bool ascending)
        {
            var sortedIndices = new List<int>();
            for (int i = 0; i < list.Count; i++)
            {
                sortedIndices.Add(i);
            }

            sortedIndices.Sort((a, b) => ascending ? string.Compare(list[a], list[b]) : string.Compare(list[b], list[a]));
            return sortedIndices;
        }

        private void SortListsByIndices(List<int> sortedIndices)
        {
            var sortedSdk = new List<long>();
            var sortedEft = new List<long>();
            var sortedDescriptions = new List<string>();

            foreach (var index in sortedIndices)
            {
                sortedSdk.Add(sdk[index]);
                sortedEft.Add(eft[index]);
                sortedDescriptions.Add(descriptionList[index]);
            }

            sdk = sortedSdk;
            eft = sortedEft;
            descriptionList = sortedDescriptions;

            Lookup.Clear();
            for (int i = 0; i < sdk.Count; i++)
            {
                Lookup[sdk[i]] = eft[i];
            }
        }

        private void WriteDataToFile()
        {
            var path = $"{Directory.GetCurrentDirectory()}/Assets/Packages/Custom AssetBundles-Browser/path_data.json";
            using (var streamWriter = new StreamWriter(path))
            {
                var json = JsonUtility.ToJson(this, true);
                streamWriter.Write(json);
            }
        }

    }
}
