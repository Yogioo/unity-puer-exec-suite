using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PuerExecSuite
{
    /// <summary>
    /// Startup switch for the vendored PuerExec stack.
    ///
    /// Nothing here listens on a port by itself: upstream only opens its control
    /// endpoint when the Editor is launched with <c>-unityPuerExecControl</c> or when a
    /// human picks the upstream menu action. This gate decides whether to ask upstream
    /// for that activation on every Editor start, from two files:
    ///
    ///   UserSettings/PuerExecSuite/enabled       this machine only, never committed
    ///   UserSettings/PuerExecSuite/disabled      this machine only, overrides the project file
    ///   ProjectSettings/PuerExecSuite/enabled    committed on purpose, applies to the team
    ///
    /// With none of them present the suite stays silent.
    /// </summary>
    [InitializeOnLoad]
    public static class PuerExecSuiteGate
    {
        public const string PackageName = "com.yogioo.puer-exec-suite";

        public const string AssetsInstallRoot = "Assets/Editor/PuerExecSuite";

        public const string PackageInstallRoot = "Packages/com.yogioo.puer-exec-suite";

        public const string ActivationSwitch = "-unityPuerExecControl";

        public const string UpstreamActivateMenuItem = "Tools/UnityPuerExec/Activate CLI Control (this session)";

        public const string MachineEnabledPath = "UserSettings/PuerExecSuite/enabled";

        public const string MachineDisabledPath = "UserSettings/PuerExecSuite/disabled";

        public const string ProjectEnabledPath = "ProjectSettings/PuerExecSuite/enabled";

        private const string CliTail = "/Vendor/com.txcombo.unity-puer-exec/CLI~/unity-puer-exec.exe";

        private const string MenuRoot = "Tools/Puer Exec Suite/";

        private const int MaxActivationAttempts = 20;

        private static bool _warned;

        static PuerExecSuiteGate()
        {
            EditorApplication.delayCall += () => AutoActivate(0);
        }

        public static string ProjectRoot
        {
            get
            {
                var dataPath = Application.dataPath;
                return string.IsNullOrEmpty(dataPath) ? string.Empty : Path.GetDirectoryName(dataPath) ?? string.Empty;
            }
        }

        public static bool IsEnabled
        {
            get { return EnabledSource != "none"; }
        }

        public static string EnabledSource
        {
            get
            {
                if (Exists(MachineDisabledPath))
                {
                    return "none";
                }

                if (Exists(MachineEnabledPath))
                {
                    return "machine";
                }

                return Exists(ProjectEnabledPath) ? "project" : "none";
            }
        }

        public static bool IsActive
        {
            get { return ActivatedByCommandLine() || ActivatedByUpstream(); }
        }

        public static string InstallRoot
        {
            get
            {
                if (Directory.Exists(ProjectPath(AssetsInstallRoot)))
                {
                    return AssetsInstallRoot;
                }

                return Directory.Exists(ProjectPath(PackageInstallRoot)) ? PackageInstallRoot : string.Empty;
            }
        }

        public static bool IsInstalled
        {
            get
            {
                return Directory.Exists(ProjectPath(AssetsInstallRoot))
                       || Directory.Exists(ProjectPath(PackageInstallRoot));
            }
        }

        public static string CliPath
        {
            get
            {
                foreach (var root in new[] { AssetsInstallRoot, PackageInstallRoot })
                {
                    var candidate = ProjectPath(root + CliTail);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                return ProjectPath(AssetsInstallRoot + CliTail);
            }
        }

        public static void EnableForMachine()
        {
            WriteFlag(MachineEnabledPath);
            DeleteFlag(MachineDisabledPath);
            AutoActivate(0);
        }

        public static void DisableForMachine()
        {
            WriteFlag(MachineDisabledPath);
            DeleteFlag(MachineEnabledPath);

            if (ActivatedByUpstream() && !ActivatedByCommandLine())
            {
                Debug.LogWarning("[PuerExecSuite] The control endpoint stays open until this Editor restarts.");
            }
        }

        public static void EnableForProject()
        {
            WriteFlag(ProjectEnabledPath);
            Debug.Log("[PuerExecSuite] " + ProjectEnabledPath + " written. Commit it to share the opt-in with the team.");
            AutoActivate(0);
        }

        public static void DisableForProject()
        {
            DeleteFlag(ProjectEnabledPath);
        }

        [MenuItem(MenuRoot + "Enable On This Machine", false, 10)]
        private static void MenuEnableForMachine()
        {
            EnableForMachine();
        }

        [MenuItem(MenuRoot + "Disable On This Machine", false, 11)]
        private static void MenuDisableForMachine()
        {
            DisableForMachine();
        }

        [MenuItem(MenuRoot + "Enable For This Project (committed)", false, 12)]
        private static void MenuEnableForProject()
        {
            EnableForProject();
        }

        [MenuItem(MenuRoot + "Disable For This Project (committed)", false, 13)]
        private static void MenuDisableForProject()
        {
            DisableForProject();
        }

        [MenuItem(MenuRoot + "Status", false, 20)]
        private static void MenuStatus()
        {
            var report = string.Join(
                Environment.NewLine,
                "[PuerExecSuite] status",
                "  project        : " + ProjectRoot,
                "  install root   : " + (IsInstalled ? InstallRoot : "(not installed)"),
                "  enabled source : " + EnabledSource,
                "  machine marker : " + ProjectPath(MachineEnabledPath) + (Exists(MachineEnabledPath) ? "  (present)" : "  (absent)"),
                "  machine veto   : " + ProjectPath(MachineDisabledPath) + (Exists(MachineDisabledPath) ? "  (present)" : "  (absent)"),
                "  project marker : " + ProjectPath(ProjectEnabledPath) + (Exists(ProjectEnabledPath) ? "  (present)" : "  (absent)"),
                "  endpoint       : " + (IsActive ? "listening" : "not listening"),
                "  cli            : " + CliPath + (File.Exists(CliPath) ? string.Empty : "  (missing)"));
            Debug.Log(report);
        }

        [MenuItem(MenuRoot + "Activate Now (this session)", false, 21)]
        private static void MenuActivateNow()
        {
            AutoActivate(0, true);
        }

        [MenuItem(MenuRoot + "Reveal CLI", false, 30)]
        private static void MenuRevealCli()
        {
            var cli = CliPath;
            if (File.Exists(cli))
            {
                EditorUtility.RevealInFinder(cli);
                return;
            }

            Debug.LogError("[PuerExecSuite] CLI not found: " + cli);
        }

        private static void AutoActivate(int attempt, bool force = false)
        {
            if (IsActive || (!force && !IsEnabled))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Retry(attempt);
                return;
            }

            if (EditorApplication.ExecuteMenuItem(UpstreamActivateMenuItem) || ActivatedByUpstream())
            {
                Debug.Log("[PuerExecSuite] Control endpoint activated for this session.");
                return;
            }

            if (ActivateByReflection())
            {
                Debug.Log("[PuerExecSuite] Control endpoint activated for this session (reflection path).");
                return;
            }

            if (Retry(attempt))
            {
                return;
            }

            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning(
                    "[PuerExecSuite] Opt-in is on but the upstream activation entry point was not found. "
                    + "Launch the Editor with " + ActivationSwitch + " instead.");
            }
        }

        private static bool Retry(int attempt)
        {
            if (attempt >= MaxActivationAttempts)
            {
                return false;
            }

            EditorApplication.delayCall += () => AutoActivate(attempt + 1);
            return true;
        }

        private static bool ActivatedByCommandLine()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                if (args == null)
                {
                    return false;
                }

                foreach (var arg in args)
                {
                    if (string.Equals(arg, ActivationSwitch, StringComparison.OrdinalIgnoreCase))
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

        private static bool ActivatedByUpstream()
        {
            var method = FindUpstreamMethod("IsActivated");
            if (method == null)
            {
                return false;
            }

            try
            {
                return method.Invoke(null, null) is bool active && active;
            }
            catch
            {
                return false;
            }
        }

        private static bool ActivateByReflection()
        {
            var method = FindUpstreamMethod("ActivateForSession");
            if (method == null)
            {
                return false;
            }

            try
            {
                method.Invoke(null, null);
            }
            catch
            {
                return false;
            }

            return ActivatedByUpstream();
        }

        private static MethodInfo FindUpstreamMethod(string name)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("UnityPuerExec.UnityPuerExecActivation", false);
                if (type == null)
                {
                    continue;
                }

                var method = type.GetMethod(name, flags);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        private static void WriteFlag(string relative)
        {
            var path = ProjectPath(relative);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[PuerExecSuite] Cannot resolve the project path.");
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "enabled" + Environment.NewLine);
                Debug.Log("[PuerExecSuite] marker written: " + path);
            }
            catch (Exception ex)
            {
                Debug.LogError("[PuerExecSuite] Failed to write " + path + ": " + ex.Message);
            }
        }

        private static void DeleteFlag(string relative)
        {
            var path = ProjectPath(relative);
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log("[PuerExecSuite] marker removed: " + path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[PuerExecSuite] Failed to delete " + path + ": " + ex.Message);
            }
        }

        private static bool Exists(string relative)
        {
            try
            {
                var path = ProjectPath(relative);
                return !string.IsNullOrEmpty(path) && File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static string ProjectPath(string relative)
        {
            var root = ProjectRoot;
            if (string.IsNullOrEmpty(root))
            {
                return string.Empty;
            }

            return Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
