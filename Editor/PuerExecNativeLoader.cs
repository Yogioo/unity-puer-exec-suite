using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PuerExecSuite
{
    /// <summary>
    /// Loads the vendored puerts native libraries by absolute path before puerts asks for
    /// them with a bare <c>DllImport("PuertsCore")</c>.
    ///
    /// Unity resolves a bare plugin name through the map it built when the Editor started.
    /// When the module is installed into a running Editor (the normal first-install case),
    /// that map still describes the previous location, so every DllImport fails with
    /// DllNotFoundException even though Unity lists the plugin as Editor-compatible.
    /// Loading the files ourselves puts modules with the right base names into the process,
    /// and Windows then satisfies the bare-name lookups with the already-loaded modules.
    /// </summary>
    public static class PuerExecNativeLoader
    {
        private const uint LoadWithAlteredSearchPath = 0x00000008;

        private static readonly string[] LoadOrder =
        {
            "/Vendor/com.tencent.puerts.core/Plugins/x86_64/PuertsCore.dll",
            "/Vendor/com.tencent.puerts.core/Plugins/x86_64/WSPPAddon.dll",
            "/Vendor/com.tencent.puerts.v8/Plugins/x86_64/PapiV8.dll",
        };

        private static bool _attempted;

        private static string _lastResult = "(not attempted)";

        public static string LastResult
        {
            get { return _lastResult; }
        }

        public static bool EnsureLoaded(bool force = false)
        {
            if (_attempted && !force)
            {
                return _lastResult.StartsWith("ok", StringComparison.Ordinal);
            }

            _attempted = true;

            var root = PuerExecSuiteGate.InstallRoot;
            if (string.IsNullOrEmpty(root))
            {
                _lastResult = "skipped: module not installed";
                return false;
            }

            var handles = 0;
            var problems = 0;
            var report = string.Empty;

            foreach (var tail in LoadOrder)
            {
                var path = Path.Combine(
                    PuerExecSuiteGate.ProjectRoot,
                    (root + tail).Replace('/', Path.DirectorySeparatorChar));
                var name = Path.GetFileName(path);

                if (!File.Exists(path))
                {
                    report += "\n  " + name + ": missing";
                    problems++;
                    continue;
                }

                if (IsAlreadyLoaded(name))
                {
                    report += "\n  " + name + ": already in process";
                    handles++;
                    continue;
                }

                try
                {
                    var handle = LoadLibraryExW(path, IntPtr.Zero, LoadWithAlteredSearchPath);
                    if (handle == IntPtr.Zero)
                    {
                        report += "\n  " + name + ": LoadLibraryEx failed, win32=" + Marshal.GetLastWin32Error();
                        problems++;
                    }
                    else
                    {
                        report += "\n  " + name + ": loaded";
                        handles++;
                    }
                }
                catch (Exception ex)
                {
                    report += "\n  " + name + ": " + ex.GetType().Name + " " + ex.Message;
                    problems++;
                }
            }

            _lastResult = (problems == 0 ? "ok" : "partial") + " (" + handles + " loaded)" + report;

            if (problems > 0)
            {
                Debug.LogWarning("[PuerExecSuite] native preload incomplete:" + report);
            }

            return problems == 0;
        }

        private static bool IsAlreadyLoaded(string moduleName)
        {
            try
            {
                foreach (System.Diagnostics.ProcessModule module in System.Diagnostics.Process.GetCurrentProcess().Modules)
                {
                    if (string.Equals(module.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        [DllImport("kernel32", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryExW(string fileName, IntPtr file, uint flags);
    }
}
