using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using UnityEditor;
using UnityEngine;

namespace TarkovSdk.Editor
{
    public class TarkovAssemblyExtractorWindow : EditorWindow
    {
        private const string OldPrefix = "Assembly-CSharp";
        private const string NewPrefix = "Tarkov.Assembly";
        private const string DefaultOutputRelPath = "Assets/Plugins/Tarkov.Assemblies";
        private const string EftDataPathKey = "TarkovSdk.Extractor.EftDataPath";
        private const string OutputPathKey = "TarkovSdk.Extractor.OutputPath";
        private const string UnpackZipFileName = "unpack_after_setup.zip";

        private static readonly string[] RequiredManagedDlls = new string[]
        {
            "Accessibility.dll",
            "AmplifyMotion.dll",
            "AnimationSystem.Recording.dll",
            "AnimationSystem.Types.dll",
            "Assembly-CSharp-firstpass.dll",
            "Assembly-CSharp.dll",
            "bsg.componentace.compression.libs.zlib.dll",
            "bsg.console.core.dll",
            "bsg.microsoft.extensions.objectpool.dll",
            "bsg.system.buffers.dll",
            "BSG.Unity.Wires.dll",
            "Cinemachine.dll",
            "Coffee.SoftMaskForUGUI.dll",
            "com.nvidia.reflex.Runtime.dll",
            "Comfort.dll",
            "Comfort.Unity.dll",
            "CommonExtensions.dll",
            "DissonanceVoip.dll",
            "DOTween.dll",
            "DOTween.Modules.dll",
            "FbxBuildTestAssets.dll",
            "FilesChecker.dll",
            "ItemComponent.Types.dll",
            "ItemTemplate.Types.dll",
            "JBooth.MicroSplat.Core.dll",
            "kcp.dll",
            "LibraryLoaderUtility.dll",
            "Meta.XR.Audio.dll",
            "Microsoft.CSharp.dll",
            "Mono.Data.Sqlite.dll",
            "Mono.Posix.dll",
            "Mono.Security.dll",
            "Mono.WebBrowser.dll",
            "Newtonsoft.Json.dll",
            "Newtonsoft.Json.UnityConverters.dll",
            "NLog.dll",
            "Novell.Directory.Ldap.dll",
            "Sirenix.OdinInspector.Attributes.dll",
            "Sirenix.Serialization.Config.dll",
            "Sirenix.Serialization.dll",
            "Sirenix.Utilities.dll",
            "System.Runtime.CompilerServices.Unsafe.dll",
            "uLipSync.Runtime.dll",
            "Unity.Collections.dll",
            "Unity.Collections.LowLevel.ILSupport.dll",
            "Unity.Formats.Fbx.Runtime.dll",
            "Unity.MemoryProfiler.dll",
            "Unity.PlayableGraphVisualizer.dll",
            "Unity.Postprocessing.Runtime.dll",
            "Unity.ProBuilder.Csg.dll",
            "Unity.ProBuilder.dll",
            "Unity.ProBuilder.KdTree.dll",
            "Unity.ProBuilder.Poly2Tri.dll",
            "Unity.ProBuilder.Stl.dll",
            "Unity.Profiling.Core.dll",
            "Unity.Recorder.Base.dll",
            "Unity.Recorder.dll",
            "Unity.ScriptableBuildPipeline.dll",
            "UnityEngine.AccessibilityModule.dll",
            "UnityEngine.AIModule.dll",
            "UnityEngine.AndroidJNIModule.dll",
            "UnityEngine.AnimationModule.dll",
            "UnityEngine.ARModule.dll",
            "UnityEngine.AssetBundleModule.dll",
            "UnityEngine.AudioModule.dll",
            "UnityEngine.ClothModule.dll",
            "UnityEngine.ClusterInputModule.dll",
            "UnityEngine.ClusterRendererModule.dll",
            "UnityEngine.ContentLoadModule.dll",
            "UnityEngine.CoreModule.dll",
            "UnityEngine.CrashReportingModule.dll",
            "UnityEngine.DirectorModule.dll",
            "UnityEngine.DSPGraphModule.dll",
            "UnityEngine.GameCenterModule.dll",
            "UnityEngine.GIModule.dll",
            "UnityEngine.GridModule.dll",
            "UnityEngine.HotReloadModule.dll",
            "UnityEngine.ImageConversionModule.dll",
            "UnityEngine.IMGUIModule.dll",
            "UnityEngine.InputLegacyModule.dll",
            "UnityEngine.InputModule.dll",
            "UnityEngine.JSONSerializeModule.dll",
            "UnityEngine.LocalizationModule.dll",
            "UnityEngine.NVIDIAModule.dll",
            "UnityEngine.ParticleSystemModule.dll",
            "UnityEngine.PerformanceReportingModule.dll",
            "UnityEngine.Physics2DModule.dll",
            "UnityEngine.PhysicsModule.dll",
            "websocket-sharp.dll",
            "where-allocations.dll",
        };

        private string _eftDataPath = string.Empty;
        private string _outputPath = DefaultOutputRelPath;
        private Vector2 _logScroll;
        private readonly List<string> _log = new List<string>();
        private bool _lastRunHadFailures;

        [MenuItem("EFT-SDK/Kit Creation/Tools/Tarkov Assembly Extractor")]
        public static void ShowWindow()
        {
            TarkovAssemblyExtractorWindow win = GetWindow<TarkovAssemblyExtractorWindow>("Tarkov Assembly Extractor");
            win.minSize = new Vector2(620f, 460f);
            win.Show();
        }

        private void OnEnable()
        {
            _eftDataPath = EditorPrefs.GetString(EftDataPathKey, string.Empty);
            _outputPath = EditorPrefs.GetString(OutputPathKey, DefaultOutputRelPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("EscapeFromTarkov_Data folder", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                string newVal = EditorGUILayout.TextField(_eftDataPath);
                if (newVal != _eftDataPath)
                {
                    _eftDataPath = newVal;
                    EditorPrefs.SetString(EftDataPathKey, _eftDataPath);
                }
                if (GUILayout.Button("Browse", GUILayout.Width(90f)))
                {
                    string start = Directory.Exists(_eftDataPath) ? _eftDataPath : string.Empty;
                    string picked = EditorUtility.OpenFolderPanel("Select EscapeFromTarkov_Data folder", start, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _eftDataPath = picked.Replace('/', Path.DirectorySeparatorChar);
                        EditorPrefs.SetString(EftDataPathKey, _eftDataPath);
                        GUI.FocusControl(null);
                    }
                }
            }

            string managedHint = string.IsNullOrEmpty(_eftDataPath)
                ? "(no folder selected)"
                : (Directory.Exists(Path.Combine(_eftDataPath, "Managed"))
                    ? "✓ Managed subfolder found"
                    : "✗ no Managed subfolder here");
            EditorGUILayout.LabelField(managedHint, EditorStyles.miniLabel);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Output folder (project-relative or absolute)", EditorStyles.boldLabel);
            string newOut = EditorGUILayout.TextField(_outputPath);
            if (newOut != _outputPath)
            {
                _outputPath = newOut;
                EditorPrefs.SetString(OutputPathKey, _outputPath);
            }
            EditorGUILayout.LabelField("→ " + ResolveOutputAbsolute(_outputPath), EditorStyles.miniLabel);

            EditorGUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(!CanExtract()))
            {
                if (GUILayout.Button("Extract & Rewrite (" + RequiredManagedDlls.Length + " DLLs)", GUILayout.Height(32f)))
                {
                    Run();
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
                    _lastRunHadFailures = false;
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

        private bool CanExtract()
        {
            return !string.IsNullOrEmpty(_eftDataPath)
                && Directory.Exists(_eftDataPath)
                && Directory.Exists(Path.Combine(_eftDataPath, "Managed"))
                && !string.IsNullOrEmpty(_outputPath);
        }

        private void Run()
        {
            _log.Clear();
            _lastRunHadFailures = false;

            string managedDir = Path.Combine(_eftDataPath, "Managed");
            string absOutput = ResolveOutputAbsolute(_outputPath);

            Log("Source: " + managedDir);
            Log("Output: " + absOutput);
            Log(string.Empty);

            try
            {
                Directory.CreateDirectory(absOutput);
            }
            catch (Exception ex)
            {
                Log("Cannot create output folder: " + ex.Message);
                _lastRunHadFailures = true;
                return;
            }

            int rewritten = 0, copied = 0, missing = 0, failed = 0;
            int unpackNew = 0, unpackOverwritten = 0, unpackFailed = 0;
            bool unpackAttempted = false;
            List<string> missingFiles = new List<string>();
            List<string> failedFiles = new List<string>();

            bool inAssets = absOutput.Replace('\\', '/').StartsWith(
                Application.dataPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

            try
            {
                if (inAssets)
                {
                    AssetDatabase.StartAssetEditing();
                }

                for (int i = 0; i < RequiredManagedDlls.Length; i++)
                {
                    string src = RequiredManagedDlls[i];
                    string srcPath = Path.Combine(managedDir, src);
                    float pct = (float)i / RequiredManagedDlls.Length;

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Extracting Tarkov assemblies",
                        src + "  (" + (i + 1) + "/" + RequiredManagedDlls.Length + ")",
                        pct))
                    {
                        Log("Cancelled by user.");
                        return;
                    }

                    if (!File.Exists(srcPath))
                    {
                        Log("MISSING: " + src);
                        missingFiles.Add(src);
                        missing++;
                        continue;
                    }

                    try
                    {
                        ProcessResult r = ProcessOne(srcPath, absOutput, out string outFileName, out int refChanges);
                        switch (r)
                        {
                            case ProcessResult.Rewritten:
                                Log("REWRITE: " + src + "  → " + outFileName + "  (refs updated: " + refChanges + ")");
                                rewritten++;
                                break;
                            case ProcessResult.CopiedAsIs:
                                Log("COPY:    " + src);
                                copied++;
                                break;
                            case ProcessResult.CopiedNonManaged:
                                Log("COPY (native): " + src);
                                copied++;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("FAILED:  " + src + "  -  " + ex.Message);
                        failedFiles.Add(src);
                        failed++;
                    }
                }

                EditorUtility.DisplayProgressBar(
                    "Extracting Tarkov assemblies",
                    "Unpacking " + UnpackZipFileName,
                    1f);
                unpackAttempted = TryUnpackAfterSetupZip(out unpackNew, out unpackOverwritten, out unpackFailed);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (inAssets)
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                }
            }

            Log(string.Empty);
            Log("Done. Rewritten: " + rewritten
                + ", copied: " + copied
                + ", missing: " + missing
                + ", failed: " + failed);
            if (unpackAttempted)
            {
                Log("Unpack: " + unpackNew + " new, " + unpackOverwritten + " overwritten"
                    + (unpackFailed > 0 ? ", " + unpackFailed + " failed" : "") + ".");
            }

            _lastRunHadFailures = failed > 0 || missing > 0 || unpackFailed > 0;

            if (missingFiles.Count > 0)
            {
                Log("Missing from Managed: " + string.Join(", ", missingFiles));
            }
            if (failedFiles.Count > 0)
            {
                Log("Failed: " + string.Join(", ", failedFiles));
            }
        }

        private enum ProcessResult { Rewritten, CopiedAsIs, CopiedNonManaged }

        private static ProcessResult ProcessOne(string srcPath, string outDir, out string outFileName, out int refChanges)
        {
            byte[] bytes = File.ReadAllBytes(srcPath);
            string srcName = Path.GetFileName(srcPath);
            outFileName = srcName;
            refChanges = 0;

            ModuleDefMD module;
            try
            {
                module = ModuleDefMD.Load(bytes);
            }
            catch (BadImageFormatException)
            {
                File.WriteAllBytes(Path.Combine(outDir, srcName), bytes);
                return ProcessResult.CopiedNonManaged;
            }

            try
            {
                bool changed = false;

                AssemblyDef assembly = module.Assembly;
                if (assembly != null)
                {
                    string name = assembly.Name;
                    if (name.Contains(OldPrefix))
                    {
                        string newName = name.Replace(OldPrefix, NewPrefix);
                        assembly.Name = newName;
                        module.Name = newName + ".dll";
                        outFileName = newName + ".dll";
                        changed = true;
                    }
                }

                foreach (AssemblyRef aref in module.GetAssemblyRefs())
                {
                    string refName = aref.Name;
                    if (refName.Contains(OldPrefix))
                    {
                        aref.Name = refName.Replace(OldPrefix, NewPrefix);
                        refChanges++;
                        changed = true;
                    }
                }

                string outPath = Path.Combine(outDir, outFileName);
                if (changed)
                {
                    ModuleWriterOptions opts = new ModuleWriterOptions(module);
                    opts.MetadataOptions.Flags = MetadataFlags.PreserveAll | MetadataFlags.KeepOldMaxStack;
                    module.Write(outPath, opts);
                    return ProcessResult.Rewritten;
                }

                File.WriteAllBytes(outPath, bytes);
                return ProcessResult.CopiedAsIs;
            }
            finally
            {
                module.Dispose();
            }
        }

        private bool TryUnpackAfterSetupZip(out int newFiles, out int overwritten, out int failed)
        {
            newFiles = 0;
            overwritten = 0;
            failed = 0;

            string zipPath = ResolveScriptSiblingPath(UnpackZipFileName);
            if (!File.Exists(zipPath))
            {
                Log(string.Empty);
                Log("No " + UnpackZipFileName + " next to script; skipping unpack.");
                Log("Looked at: " + zipPath);
                return false;
            }

            Log(string.Empty);
            Log("Unpacking " + zipPath + " into Assets/...");

            string destRoot = Application.dataPath;
            string normalizedRoot = Path.GetFullPath(destRoot)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                        string outPath = Path.GetFullPath(Path.Combine(destRoot, rel));

                        if (!outPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            Log("UNPACK SKIP (outside Assets): " + entry.FullName);
                            failed++;
                            continue;
                        }

                        try
                        {
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(outPath);
                                continue;
                            }

                            string parentDir = Path.GetDirectoryName(outPath);
                            if (!string.IsNullOrEmpty(parentDir))
                            {
                                Directory.CreateDirectory(parentDir);
                            }

                            bool existed = File.Exists(outPath);
                            entry.ExtractToFile(outPath, overwrite: true);
                            if (existed)
                            {
                                overwritten++;
                                Log("UNPACK OVERWRITE: " + entry.FullName);
                            }
                            else
                            {
                                newFiles++;
                                Log("UNPACK NEW:       " + entry.FullName);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("UNPACK FAILED: " + entry.FullName + "  -  " + ex.Message);
                            failed++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Unpack failed to open " + UnpackZipFileName + ": " + ex.Message);
                failed++;
            }

            return true;
        }

        private string ResolveScriptSiblingPath(string fileName)
        {
            MonoScript ms = MonoScript.FromScriptableObject(this);
            string scriptAssetPath = ms != null ? AssetDatabase.GetAssetPath(ms) : null;

            if (string.IsNullOrEmpty(scriptAssetPath))
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, "Editor", "SDK2", fileName));
            }

            string scriptDirRelative = Path.GetDirectoryName(scriptAssetPath);
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, scriptDirRelative, fileName));
        }

        private static string ResolveOutputAbsolute(string maybeRel)
        {
            if (string.IsNullOrEmpty(maybeRel))
            {
                return string.Empty;
            }
            if (Path.IsPathRooted(maybeRel))
            {
                return Path.GetFullPath(maybeRel);
            }
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, maybeRel));
        }

        private void Log(string line)
        {
            _log.Add(line);
            if (!string.IsNullOrEmpty(line))
            {
                Debug.Log("[TarkovAssemblyExtractor] " + line);
            }
            _logScroll = new Vector2(0f, float.MaxValue);
            Repaint();
        }
    }
}
