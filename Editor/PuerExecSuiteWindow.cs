using System.IO;
using UnityEditor;
using UnityEngine;

namespace PuerExecSuite
{
    /// <summary>
    /// Control panel for the vendored PuerExec stack: current state on top, one button per
    /// action, one short line of copy under each. Long explanations live behind a foldout so
    /// the default view answers "is it on, and what do I click" in one glance.
    /// </summary>
    public sealed class PuerExecSuiteWindow : EditorWindow
    {
        private const float RepaintInterval = 0.5f;

        private const string FontScaleKey = "PuerExecSuite.FontScale";

        private const float BasePointSize = 13f;

        private static readonly string[] ScaleNames = { "标准", "大", "特大" };

        private static readonly float[] ScaleValues = { 1f, 1.22f, 1.45f };

        private static readonly GUIContent Title = new GUIContent("Puer Exec Suite");

        private Vector2 _scroll;

        private bool _showAdvanced;

        private bool _showAbout;

        private double _nextRepaint;

        private int _scaleIndex;

        private int _stylesScaleIndex = -1;

        private GUIStyle _sectionTitle;

        private GUIStyle _body;

        private GUIStyle _caption;

        private GUIStyle _callout;

        private GUIStyle _path;

        private GUIStyle _primaryButton;

        private GUIStyle _button;

        private GUIStyle _foldout;

        private int _pointSize;

        public static void Open()
        {
            var window = GetWindow<PuerExecSuiteWindow>(false, "Puer Exec Suite", true);
            window.minSize = new Vector2(460f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = Title;
            _scaleIndex = Mathf.Clamp(EditorPrefs.GetInt(FontScaleKey, 1), 0, ScaleNames.Length - 1);
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!hasFocus || EditorApplication.timeSinceStartup < _nextRepaint)
            {
                return;
            }

            _nextRepaint = EditorApplication.timeSinceStartup + RepaintInterval;
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawFontScaleBar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawStatus();
            EditorGUILayout.Space(8f);
            DrawActions();
            EditorGUILayout.Space(8f);
            DrawStateRows();
            EditorGUILayout.Space(8f);
            DrawTeamSwitch();
            EditorGUILayout.Space(8f);
            DrawCli();
            EditorGUILayout.Space(10f);
            DrawAbout();
            DrawAdvanced();

            EditorGUILayout.EndScrollView();
        }

        private void EnsureStyles()
        {
            if (_stylesScaleIndex == _scaleIndex && _body != null)
            {
                return;
            }

            _stylesScaleIndex = _scaleIndex;
            _pointSize = Mathf.RoundToInt(BasePointSize * ScaleValues[_scaleIndex]);

            _sectionTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = _pointSize + 1 };
            _body = new GUIStyle(EditorStyles.label) { fontSize = _pointSize, wordWrap = true, richText = true };
            _caption = new GUIStyle(EditorStyles.label)
            {
                fontSize = _pointSize,
                wordWrap = true,
                richText = true,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.72f, 0.72f, 0.72f) : new Color(0.35f, 0.35f, 0.35f) },
                padding = new RectOffset(0, 0, 1, 2),
            };
            _callout = new GUIStyle(EditorStyles.label)
            {
                fontSize = _pointSize + 1,
                wordWrap = true,
                richText = true,
                padding = new RectOffset(12, 12, 10, 10),
            };
            _path = new GUIStyle(EditorStyles.label) { fontSize = _pointSize - 1, wordWrap = true };
            _primaryButton = ButtonStyle(_pointSize, true);
            _button = ButtonStyle(_pointSize, false);
            _foldout = new GUIStyle(EditorStyles.foldout) { fontSize = _pointSize, fontStyle = FontStyle.Bold };
        }

        private static GUIStyle ButtonStyle(int fontSize, bool bold)
        {
            // EditorStyles.miniButton carries fixedHeight = 16, which clips any content taller
            // than that no matter what GUILayout.Height asks for.
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 6, 6),
                fixedHeight = 0f,
                fixedWidth = 0f,
                stretchHeight = true,
                stretchWidth = true,
                wordWrap = false,
            };
        }

        private void DrawFontScaleBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("字号", EditorStyles.miniLabel, GUILayout.Width(28f));
                var next = GUILayout.Toolbar(_scaleIndex, ScaleNames, EditorStyles.toolbarButton, GUILayout.Width(150f));
                if (next != _scaleIndex)
                {
                    _scaleIndex = next;
                    EditorPrefs.SetInt(FontScaleKey, next);
                    Repaint();
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawStatus()
        {
            if (!PuerExecSuiteGate.IsInstalled)
            {
                DrawCallout(
                    "找不到模块\n\n应该在 Assets/Editor/PuerExecSuite。重装（工程根目录）：\n"
                    + "git clone https://github.com/Yogioo/unity-puer-exec-suite.git Assets/Editor/PuerExecSuite",
                    MessageType.Error);
                return;
            }

            var enabled = PuerExecSuiteGate.IsEnabled;
            var active = PuerExecSuiteGate.IsActive;
            var port = PuerExecSuiteGate.EndpointPort;

            string headline;
            string hint;
            MessageType type;

            if (enabled && active)
            {
                headline = "已开启" + (port > 0 ? " · 端口 " + port + " 在监听" : " · 正在监听");
                hint = "AI 现在可以连上这个编辑器。";
                type = MessageType.Info;
            }
            else if (enabled && !active)
            {
                headline = "已开启 · 本次还没生效";
                hint = "点下面的「立即激活本次会话」。";
                type = MessageType.Warning;
            }
            else if (active)
            {
                headline = "已关闭 · 但端口还开着";
                hint = "重启编辑器后才会真的关掉。";
                type = MessageType.Warning;
            }
            else
            {
                headline = "已关闭";
                hint = "没有任何端口在监听。";
                type = MessageType.None;
            }

            DrawCallout(headline + "\n" + hint, type);

            EditorGUILayout.LabelField(
                "来源：" + DescribeSource() + "　版本：" + (string.IsNullOrEmpty(PuerExecSuiteGate.BridgeVersion) ? "?" : PuerExecSuiteGate.BridgeVersion),
                _caption);
        }

        private static string DescribeSource()
        {
            switch (PuerExecSuiteGate.EnabledSource)
            {
                case "machine":
                    return "本机";
                case "project":
                    return "项目设置";
                default:
                    return PuerExecSuiteGate.MachineVeto ? "本机否决" : "未开启";
            }
        }

        private void DrawActions()
        {
            var height = _pointSize * 2.6f;

            using (new EditorGUI.DisabledScope(PuerExecSuiteGate.MachineEnabled))
            {
                if (GUILayout.Button("开启（只影响我）", _primaryButton, GUILayout.Height(height)))
                {
                    PuerExecSuiteGate.EnableForMachine();
                }
            }

            EditorGUILayout.LabelField("点一次，以后每次打开 Unity 都自动生效。", _caption);

            using (new EditorGUI.DisabledScope(PuerExecSuiteGate.MachineVeto))
            {
                if (GUILayout.Button("关闭（只影响我）", _primaryButton, GUILayout.Height(height)))
                {
                    PuerExecSuiteGate.DisableForMachine();
                }
            }

            EditorGUILayout.LabelField("本机不再自动打开。", _caption);

            using (new EditorGUI.DisabledScope(PuerExecSuiteGate.IsActive))
            {
                if (GUILayout.Button("立即激活本次会话", _primaryButton, GUILayout.Height(height)))
                {
                    PuerExecSuiteGate.ActivateNow();
                }
            }

            EditorGUILayout.LabelField("只开这一次，不改任何设置。", _caption);
        }

        private void DrawStateRows()
        {
            EditorGUILayout.LabelField("当前状态", _sectionTitle);

            DrawStateRow(
                "模块",
                PuerExecSuiteGate.InstallRoot,
                string.Empty);

            DrawStateRow(
                "自动开启",
                PuerExecSuiteGate.MachineEnabled
                    ? "是（本机）"
                    : PuerExecSuiteGate.ProjectEnabled && !PuerExecSuiteGate.MachineVeto
                        ? "是（项目设置）"
                        : "否",
                string.Empty);

            DrawStateRow(
                "端口",
                PuerExecSuiteGate.IsActive
                    ? (PuerExecSuiteGate.EndpointPort > 0 ? "监听中（" + PuerExecSuiteGate.EndpointPort + "）" : "监听中")
                    : "未监听",
                string.Empty);
        }

        private void DrawStateRow(string label, string value, string note)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(label, _body, GUILayout.Width(_pointSize * 5f));
                GUILayout.Label(value, _body);
            }

            if (!string.IsNullOrEmpty(note))
            {
                EditorGUILayout.LabelField(note, _caption);
            }
        }

        private void DrawTeamSwitch()
        {
            EditorGUILayout.LabelField("全队默认开启", _sectionTitle);

            var buttonHeight = _pointSize * 2.4f;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(PuerExecSuiteGate.ProjectEnabled))
                {
                    if (GUILayout.Button("打开", _button, GUILayout.Height(buttonHeight)))
                    {
                        PuerExecSuiteGate.EnableForProject();
                    }
                }

                using (new EditorGUI.DisabledScope(!PuerExecSuiteGate.ProjectEnabled))
                {
                    if (GUILayout.Button("撤销", _button, GUILayout.Height(buttonHeight)))
                    {
                        PuerExecSuiteGate.DisableForProject();
                    }
                }
            }

            EditorGUILayout.LabelField("会写进项目设置，提交后其他人拉下来也会自动生效。", _caption);
        }

        private void DrawCli()
        {
            EditorGUILayout.LabelField("AI 调用", _sectionTitle);

            var cli = PuerExecSuiteGate.CliPath;
            var exists = File.Exists(cli);
            EditorGUILayout.LabelField(exists ? cli : "找不到 CLI：\n" + cli, _path);

            var buttonHeight = _pointSize * 2.4f;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!exists))
                {
                    if (GUILayout.Button("复制路径", _button, GUILayout.Height(buttonHeight)))
                    {
                        EditorGUIUtility.systemCopyBuffer = cli;
                        ShowNotification(new GUIContent("已复制"));
                    }

                    if (GUILayout.Button("打开文件夹", _button, GUILayout.Height(buttonHeight)))
                    {
                        EditorUtility.RevealInFinder(cli);
                    }
                }
            }

            if (GUILayout.Button("复制冷启动命令", _button, GUILayout.Height(buttonHeight)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildColdStartCommand();
                ShowNotification(new GUIContent("已复制"));
            }

            EditorGUILayout.LabelField("冷启动命令能带上独立日志，方便 AI 读日志。", _caption);
        }

        private static string BuildColdStartCommand()
        {
            return "\"" + EditorApplication.applicationPath + "\" -projectPath \"" + PuerExecSuiteGate.ProjectRoot + "\" "
                   + PuerExecSuiteGate.ActivationSwitch
                   + " -logFile \""
                   + Path.Combine(PuerExecSuiteGate.ProjectRoot, "Temp", "UnityPuerExec", "Editor.log")
                   + "\"";
        }

        private void DrawAbout()
        {
            _showAbout = EditorGUILayout.Foldout(_showAbout, "它是干什么的", true, _foldout);
            if (_showAbout)
            {
                DrawCallout(
                    "给 AI 用的编辑器控制口。AI 通过它读编译错误、跑 JS、跑测试。\n"
                    + "默认全关：不开就没有任何端口，等于没装。",
                    MessageType.None);
            }
        }

        private void DrawAdvanced()
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "详细信息", true, _foldout);
            if (!_showAdvanced)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("工程　　" + PuerExecSuiteGate.ProjectRoot, _path);
            EditorGUILayout.LabelField(
                "端点　　" + PuerExecSuiteGate.EndpointPath
                + (PuerExecSuiteGate.EndpointPublished ? "（已发布）" : "（未发布）"),
                _path);
            EditorGUILayout.LabelField("本机开启　" + Mark(PuerExecSuiteGate.MachineEnabledPath, PuerExecSuiteGate.MachineEnabled), _path);
            EditorGUILayout.LabelField("本机否决　" + Mark(PuerExecSuiteGate.MachineDisabledPath, PuerExecSuiteGate.MachineVeto), _path);
            EditorGUILayout.LabelField("项目开启　" + Mark(PuerExecSuiteGate.ProjectEnabledPath, PuerExecSuiteGate.ProjectEnabled), _path);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("打印完整状态到 Console", _button, GUILayout.Height(_pointSize * 2.4f)))
            {
                PuerExecSuiteGate.LogStatus();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCallout(string text, MessageType type)
        {
            var color = type == MessageType.Error
                ? new Color(0.79f, 0.24f, 0.22f, 0.30f)
                : type == MessageType.Warning
                    ? new Color(0.90f, 0.71f, 0.15f, 0.25f)
                    : type == MessageType.Info
                        ? new Color(0.24f, 0.49f, 0.90f, 0.22f)
                        : new Color(0.5f, 0.5f, 0.5f, 0.14f);

            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(rect, color);
            EditorGUILayout.LabelField(text, _callout);
            EditorGUILayout.EndVertical();
        }

        private static string Mark(string relative, bool present)
        {
            var root = PuerExecSuiteGate.ProjectRoot;
            var path = string.IsNullOrEmpty(root)
                ? relative
                : Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            return (present ? "● " : "○ ") + path;
        }
    }
}
