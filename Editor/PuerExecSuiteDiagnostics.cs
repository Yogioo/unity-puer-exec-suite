using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PuerExecSuite
{
    /// <summary>
    /// Answers "why does the JS engine refuse to start" from inside the Editor: does Unity
    /// treat the vendored native plugins as plugins at all, and did it actually load them.
    ///
    /// Runs from the menu, or automatically once on the next domain reload when the marker
    /// file Temp/PuerExecSuite/diagnose exists (handy for CI or for an agent that cannot
    /// click a menu item).
    /// </summary>
    public static class PuerExecSuiteDiagnostics
    {
        private const string MarkerRelative = "Temp/PuerExecSuite/diagnose";

        private static readonly string[] NativePluginTails =
        {
            "/Vendor/com.tencent.puerts.core/Plugins/x86_64/PuertsCore.dll",
            "/Vendor/com.tencent.puerts.core/Plugins/x86_64/WSPPAddon.dll",
            "/Vendor/com.tencent.puerts.v8/Plugins/x86_64/PapiV8.dll",
        };

        private static readonly string[] NativeModuleNames = { "PuertsCore", "WSPPAddon", "PapiV8" };

        [InitializeOnLoadMethod]
        private static void AutoRunIfMarked()
        {
            var root = PuerExecSuiteGate.ProjectRoot;
            if (string.IsNullOrEmpty(root))
            {
                return;
            }

            var marker = Path.Combine(root, MarkerRelative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(marker))
            {
                return;
            }

            try
            {
                File.Delete(marker);
            }
            catch
            {
            }

            EditorApplication.delayCall += Run;
        }

        [MenuItem(PuerExecSuiteGate.MenuPath + "诊断原生库（V8 起不来时用）", false, 40)]
        public static void Run()
        {
            var lines = new List<string> { "[PuerExecSuite][diag] start" };

            lines.Add("  project      : " + PuerExecSuiteGate.ProjectRoot);
            lines.Add("  install root : " + (PuerExecSuiteGate.IsInstalled ? PuerExecSuiteGate.InstallRoot : "(not installed)"));

            var root = PuerExecSuiteGate.InstallRoot;
            if (string.IsNullOrEmpty(root))
            {
                Debug.Log(string.Join("\n", lines));
                return;
            }

            lines.Add("  scripting    : " + (IsUsingIl2CppBackend() ? "IL2CPP" : "Mono"));
            lines.Add("  runtime      : " + Application.platform + " / " + Application.unityVersion);

            var pluginPaths = new HashSet<string>();
            foreach (var importer in PluginImporter.GetAllImporters())
            {
                pluginPaths.Add(importer.assetPath);
            }

            lines.Add("  plugins Unity knows about: " + pluginPaths.Count);
            foreach (var path in pluginPaths)
            {
                lines.Add("    - " + path);
            }

            foreach (var tail in NativePluginTails)
            {
                var relative = root + tail;
                var absolute = Path.Combine(
                    PuerExecSuiteGate.ProjectRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                lines.Add("  " + Path.GetFileName(absolute));
                lines.Add("    file exists  : " + File.Exists(absolute));

                var importer = AssetImporter.GetAtPath(relative);
                lines.Add("    importer     : " + (importer == null ? "(null)" : importer.GetType().Name));

                var plugin = importer as PluginImporter;
                if (plugin == null)
                {
                    lines.Add("    => Unity does NOT treat this file as a plugin");
                    continue;
                }

                lines.Add("    editor       : " + plugin.GetCompatibleWithEditor()
                          + "  anyPlatform=" + plugin.GetCompatibleWithAnyPlatform());
                lines.Add("    win64        : " + plugin.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)
                          + "  cpu=" + SafePlatformData(plugin, BuildTarget.StandaloneWindows64, "CPU"));
                lines.Add("    preloaded    : " + plugin.isPreloaded);
            }

            var loadedModuleNames = new List<string>();
            try
            {
                foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                {
                    var name = module.ModuleName ?? string.Empty;
                    foreach (var wanted in NativeModuleNames)
                    {
                        if (name.StartsWith(wanted, StringComparison.OrdinalIgnoreCase))
                        {
                            loadedModuleNames.Add(name + " @ " + module.FileName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lines.Add("  module scan failed: " + ex.Message);
            }

            lines.Add("  loaded native modules: " + (loadedModuleNames.Count == 0 ? "(none)" : string.Empty));
            foreach (var name in loadedModuleNames)
            {
                lines.Add("    - " + name);
            }

            lines.Add("  backend types in AppDomain:");
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name ?? string.Empty;
                if (!name.StartsWith("com.tencent.puerts", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lines.Add("    - " + name + " @ " + SafeLocation(assembly));
                foreach (var typeName in new[] { "Puerts.BackendV8", "Puerts.PuertsDLL", "Puerts.JsEnv" })
                {
                    try
                    {
                        var type = assembly.GetType(typeName, false);
                        if (type != null)
                        {
                            lines.Add("        " + typeName + " : found");
                        }
                    }
                    catch
                    {
                    }
                }
            }

            lines.Add("  probe DllImport(\"PuertsCore\") :");
            lines.Add("    " + ProbePuertsCore());

            lines.Add("[PuerExecSuite][diag] end");
            Debug.Log(string.Join("\n", lines));
        }

        private static string SafePlatformData(PluginImporter plugin, BuildTarget target, string key)
        {
            try
            {
                return plugin.GetPlatformData(target, key);
            }
            catch (Exception ex)
            {
                return "err:" + ex.GetType().Name;
            }
        }

        private static string SafeLocation(Assembly assembly)
        {
            try
            {
                return string.IsNullOrEmpty(assembly.Location) ? "(in memory)" : assembly.Location;
            }
            catch
            {
                return "(unavailable)";
            }
        }

        private static bool IsUsingIl2CppBackend()
        {
            try
            {
                return PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup)
                       == ScriptingImplementation.IL2CPP;
            }
            catch
            {
                return false;
            }
        }

        private static string ProbePuertsCore()
        {
            try
            {
                var version = NativeProbe.pesapi_get_version();
                return "ok, pesapi_get_version=" + version;
            }
            catch (Exception ex)
            {
                var inner = ex;
                var depth = 0;
                var chain = string.Empty;
                while (inner != null && depth < 4)
                {
                    chain += (depth == 0 ? string.Empty : " <- ") + inner.GetType().Name + ": " + inner.Message;
                    inner = inner.InnerException;
                    depth++;
                }

                return chain;
            }
        }

        private static class NativeProbe
        {
            [System.Runtime.InteropServices.DllImport("PuertsCore", EntryPoint = "pesapi_get_version", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
            internal static extern int pesapi_get_version();
        }
    }
}
