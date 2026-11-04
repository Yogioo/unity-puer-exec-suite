using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
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
    /// for that activation on every Editor start, from three files:
    ///
    ///   UserSettings/PuerExecSuite/enabled       this machine only, never committed
    ///   UserSettings/PuerExecSuite/disabled      this machine only, overrides the project file
    ///   ProjectSettings/PuerExecSuite/enabled    committed on purpose, applies to the team
    ///
    /// With none of them present the suite stays silent. See PuerExecSuiteWindow for the UI.
    /// </summary>
    [InitializeOnLoad]
    public static class PuerExecSuiteGate
    {
        public const string MenuPath = "Tools/Puer Exec Suite/";

        public const string PackageName = "com.yogioo.puer-exec-suite";

        public const string AssetsInstallRoot = "Assets/Editor/PuerExecSuite";

        public const string PackageInstallRoot = "Packages/com.yogioo.puer-exec-suite";

        public const string ActivationSwitch = "-unityPuerExecControl";

        public const string UpstreamActivateMenuItem = "Tools/UnityPuerExec/Activate CLI Control (this session)";

        public const string MachineEnabledPath = "UserSettings/PuerExecSuite/enabled";

        public const string MachineDisabledPath = "UserSettings/PuerExecSuite/disabled";

        public const string ProjectEnabledPath = "ProjectSettings/PuerExecSuite/enabled";

        public const string EndpointFilePath = "Temp/UnityPuerExec/endpoint.json";

        private const string CliTail = "/Vendor/com.txcombo.unity-puer-exec/CLI~/unity-puer-exec.exe";

        private const string UpstreamPackageJsonTail = "/Vendor/com.txcombo.unity-puer-exec/package.json";

        private const int MaxActivationAttempts = 20;

        private const string LabelEnableMachine = MenuPath + "本机开启（之后每次启动自动激活）";

        private const string LabelDisableMachine = MenuPath + "本机关闭";

        private const string LabelEnableProject = MenuPath + "允许全队默认开启（写 ProjectSettings，需提交）";

        private const string LabelDisableProject = MenuPath + "撤销全队默认开启";

        private const string LabelActivateNow = MenuPath + "仅本次会话立即激活";

        private static readonly Regex PortPattern = new Regex("\"port\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);

        private static readonly Regex VersionPattern = new Regex("\"version\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        private static bool _warned;

        private static string _versionCache;

        private static string _versionCacheRoot;

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

        public static bool IsInstalled
        {
            get
            {
                return Directory.Exists(ProjectPath(AssetsInstallRoot))
                       || Directory.Exists(ProjectPath(PackageInstallRoot));
            }
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

        public static string BridgeVersion
        {
            get
            {
                var root = InstallRoot;
                if (string.IsNullOrEmpty(root))
                {
                    return string.Empty;
                }

                if (_versionCache != null && _versionCacheRoot == root)
                {
                    return _versionCache;
                }

                var value = string.Empty;
                try
                {
                    var path = ProjectPath(root + UpstreamPackageJsonTail);
                    if (File.Exists(path))
                    {
                        var match = VersionPattern.Match(File.ReadAllText(path));
                        if (match.Success)
                        {
                            value = match.Groups[1].Value;
                        }
                    }
                }
                catch
                {
                }

                _versionCacheRoot = root;
                _versionCache = value;
                return value;
            }
        }

        public static bool MachineEnabled
        {
            get { return Exists(MachineEnabledPath); }
        }

        public static bool MachineVeto
        {
            get { return Exists(MachineDisabledPath); }
        }

        public static bool ProjectEnabled
        {
            get { return Exists(ProjectEnabledPath); }
        }

        public static bool IsEnabled
        {
            get { return EnabledSource != "none"; }
        }

        public static string EnabledSource
        {
            get
            {
                if (MachineVeto)
                {
                    return "none";
                }

                if (MachineEnabled)
                {
                    return "machine";
                }

                return ProjectEnabled ? "project" : "none";
            }
        }

        public static bool IsActive
        {
            get { return ActivatedByCommandLine() || ActivatedByUpstream(); }
        }

        public static string EndpointPath
        {
            get { return ProjectPath(EndpointFilePath); }
        }

        public static bool EndpointPublished
        {
            get { return Exists(EndpointFilePath); }
        }

        public static int EndpointPort
        {
            get
            {
                try
                {
                    var path = EndpointPath;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        var match = PortPattern.Match(File.ReadAllText(path));
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var port))
                        {
                            return port;
                        }
                    }
                }
                catch
                {
                }

                return 0;
            }
        }

        public static void EnableForMachine()
        {
            WriteFlag(MachineEnabledPath);
            DeleteFlag(MachineDisabledPath);
            AutoActivate(0);
            Debug.Log("[PuerExecSuite] 已在本机开启。端点：" + (IsActive ? "监听中" : "尚未监听"));
        }

        public static void DisableForMachine()
        {
            WriteFlag(MachineDisabledPath);
            DeleteFlag(MachineEnabledPath);

            if (ActivatedByUpstream() && !ActivatedByCommandLine())
            {
                Debug.LogWarning("[PuerExecSuite] 本机已关闭，但本次会话已经打开的端口要重启编辑器才会释放。");
            }
        }

        public static void EnableForProject()
        {
            WriteFlag(ProjectEnabledPath);
            Debug.Log("[PuerExecSuite] 已写入 " + ProjectEnabledPath + "，提交后对全队生效。");
            AutoActivate(0);
        }

        public static void DisableForProject()
        {
            DeleteFlag(ProjectEnabledPath);
        }

        public static void ActivateNow()
        {
            AutoActivate(0, true);
        }

        public static void LogStatus()
        {
            Debug.Log(string.Join(
                Environment.NewLine,
                "[PuerExecSuite] status",
                "  project        : " + ProjectRoot,
                "  install root   : " + (IsInstalled ? InstallRoot : "(not installed)"),
                "  upstream       : unity-puer-exec " + (string.IsNullOrEmpty(BridgeVersion) ? "?" : BridgeVersion),
                "  enabled source : " + EnabledSource,
                "  machine marker : " + ProjectPath(MachineEnabledPath) + (MachineEnabled ? "  (present)" : "  (absent)"),
                "  machine veto   : " + ProjectPath(MachineDisabledPath) + (MachineVeto ? "  (present)" : "  (absent)"),
                "  project marker : " + ProjectPath(ProjectEnabledPath) + (ProjectEnabled ? "  (present)" : "  (absent)"),
                "  endpoint       : " + (IsActive ? "listening" : "not listening")
                + (EndpointPublished ? "  on port " + EndpointPort : "  (not published)"),
                "  cli            : " + CliPath + (File.Exists(CliPath) ? string.Empty : "  (missing)")));
        }

        [MenuItem(MenuPath + "控制面板 (Control Panel)", false, 0)]
        private static void MenuOpenWindow()
        {
            PuerExecSuiteWindow.Open();
        }

        [MenuItem(LabelEnableMachine, true, 10)]
        private static bool ValidateEnableMachine()
        {
            var on = EnabledSource == "machine";
            Menu.SetChecked(LabelEnableMachine, on);
            return !on;
        }

        [MenuItem(LabelEnableMachine, false, 10)]
        private static void MenuEnableMachine()
        {
            EnableForMachine();
        }

        [MenuItem(LabelDisableMachine, true, 11)]
        private static bool ValidateDisableMachine()
        {
            Menu.SetChecked(LabelDisableMachine, MachineVeto);
            return !MachineVeto;
        }

        [MenuItem(LabelDisableMachine, false, 11)]
        private static void MenuDisableMachine()
        {
            DisableForMachine();
        }

        [MenuItem(LabelEnableProject, true, 12)]
        private static bool ValidateEnableProject()
        {
            Menu.SetChecked(LabelEnableProject, ProjectEnabled);
            return !ProjectEnabled;
        }

        [MenuItem(LabelEnableProject, false, 12)]
        private static void MenuEnableProject()
        {
            EnableForProject();
        }

        [MenuItem(LabelDisableProject, true, 13)]
        private static bool ValidateDisableProject()
        {
            return ProjectEnabled;
        }

        [MenuItem(LabelDisableProject, false, 13)]
        private static void MenuDisableProject()
        {
            DisableForProject();
        }

        [MenuItem(LabelActivateNow, true, 20)]
        private static bool ValidateActivateNow()
        {
            return !IsActive;
        }

        [MenuItem(LabelActivateNow, false, 20)]
        private static void MenuActivateNow()
        {
            ActivateNow();
        }

        [MenuItem(MenuPath + "状态：打印到 Console", false, 30)]
        private static void MenuStatus()
        {
            LogStatus();
        }

        [MenuItem(MenuPath + "定位 CLI 文件", false, 31)]
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

            PuerExecNativeLoader.EnsureLoaded();

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
