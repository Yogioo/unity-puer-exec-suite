using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PuerExecSuite
{
    /// <summary>
    /// Per-machine switch for the vendored PuerExec stack.
    ///
    /// The package never starts anything by itself: upstream only opens its control
    /// endpoint when the Editor is launched with <c>-unityPuerExecControl</c> or when a
    /// human picks the upstream menu action. This gate adds one more layer that is
    /// local to the machine and never committed: an opt-in marker file under
    /// <c>UserSettings/</c>. Marker absent means the suite stays silent even when the
    /// package folder is present.
    /// </summary>
    [InitializeOnLoad]
    public static class PuerExecSuiteGate
    {
        public const string PackageName = "com.yogioo.puer-exec-suite";

        public const string ActivationSwitch = "-unityPuerExecControl";

        public const string UpstreamActivateMenuItem = "Tools/UnityPuerExec/Activate CLI Control (this session)";

        public const string FlagRelativePath = "UserSettings/PuerExecSuite/enabled";

        public const string CliRelativePath =
            "Packages/" + PackageName + "/Vendor/com.txcombo.unity-puer-exec/CLI~/unity-puer-exec.exe";

        private const string MenuRoot = "Tools/Puer Exec Suite/";

        static PuerExecSuiteGate()
        {
            EditorApplication.delayCall += AutoActivateIfEnabled;
        }

        public static string ProjectRoot
        {
            get
            {
                var dataPath = Application.dataPath;
                return string.IsNullOrEmpty(dataPath) ? string.Empty : Path.GetDirectoryName(dataPath) ?? string.Empty;
            }
        }

        public static string FlagPath
        {
            get { return ProjectPath(FlagRelativePath); }
        }

        public static string CliPath
        {
            get { return ProjectPath(CliRelativePath); }
        }

        public static bool IsEnabledOnThisMachine
        {
            get
            {
                try
                {
                    return File.Exists(FlagPath);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool IsActive
        {
            get { return ActivatedByCommandLine() || ActivatedByUpstream(); }
        }

        public static void Enable()
        {
            var path = FlagPath;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[PuerExecSuite] Cannot resolve the project path.");
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "enabled" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogError("[PuerExecSuite] Failed to write the opt-in marker: " + ex.Message);
                return;
            }

            Debug.Log("[PuerExecSuite] Opt-in marker written: " + path);
            AutoActivateIfEnabled();
        }

        public static void Disable()
        {
            var path = FlagPath;
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log("[PuerExecSuite] Opt-in marker removed: " + path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[PuerExecSuite] Failed to delete the opt-in marker: " + ex.Message);
            }

            if (ActivatedByUpstream() && !ActivatedByCommandLine())
            {
                Debug.LogWarning("[PuerExecSuite] The control endpoint stays open until this Editor restarts.");
            }
        }

        [MenuItem(MenuRoot + "Enable On This Machine", false, 10)]
        private static void MenuEnable()
        {
            Enable();
        }

        [MenuItem(MenuRoot + "Disable On This Machine", false, 11)]
        private static void MenuDisable()
        {
            Disable();
        }

        [MenuItem(MenuRoot + "Status", false, 12)]
        private static void MenuStatus()
        {
            var report = string.Join(
                Environment.NewLine,
                "[PuerExecSuite] status",
                "  project      : " + ProjectRoot,
                "  opt-in marker: " + FlagPath + (IsEnabledOnThisMachine ? "  (present)" : "  (absent)"),
                "  endpoint     : " + (IsActive ? "listening" : "not listening"),
                "  cli          : " + CliPath + (File.Exists(CliPath) ? string.Empty : "  (missing)"));
            Debug.Log(report);
        }

        [MenuItem(MenuRoot + "Reveal CLI", false, 20)]
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

        private static void AutoActivateIfEnabled()
        {
            if (!IsEnabledOnThisMachine || IsActive)
            {
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

            Debug.LogWarning(
                "[PuerExecSuite] Opt-in marker is present but the upstream activation entry point was not found. "
                + "Launch the Editor with " + ActivationSwitch + " instead.");
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
