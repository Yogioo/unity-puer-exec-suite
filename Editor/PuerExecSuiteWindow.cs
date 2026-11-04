using System.IO;
using UnityEditor;
using UnityEngine;

namespace PuerExecSuite
{
    /// <summary>
    /// Human-facing control panel for the vendored PuerExec stack: what the switch does,
    /// what is on right now, and one button per action with a plain-language caption.
    ///
    /// Every style is built from an explicit pixel size instead of Unity's mini styles, and
    /// the size itself is a persisted preference, because the default editor label size is
    /// unreadable for a lot of people.
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

        private bool _showHowItWorks = true;

        private double _nextRepaint;

        private int _scaleIndex;

        private int _stylesScaleIndex = -1;

        private GUIStyle _headline;

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
            window.minSize = new Vector2(520f, 560f);
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

            DrawStatusCard();
            EditorGUILayout.Space(10f);
            DrawPrimaryActions();
            EditorGUILayout.Space(10f);
            DrawSections();
            EditorGUILayout.Space(10f);
            DrawForAgents();
            EditorGUILayout.Space(12f);
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

            _headline = new GUIStyle(EditorStyles.boldLabel) { fontSize = _pointSize + 3, wordWrap = true };
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
            _primaryButton = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = _pointSize,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 8, 8),
            };
            _button = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = _pointSize,
                padding = new RectOffset(10, 10, 6, 6),
            };
            _foldout = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = _pointSize + 1,
                fontStyle = FontStyle.Bold,
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
                GUILayout.Label("工具菜单：Tools ▸ Puer Exec Suite", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawStatusCard()
        {
            if (!PuerExecSuiteGate.IsInstalled)
            {
                DrawCallout(
                    "找不到模块本体。\n\n它应该位于 Assets/Editor/PuerExecSuite 或 Packages/com.yogioo.puer-exec-suite。\n"
                    + "重装命令（在工程根目录执行）：\n"
                    + "git clone https://github.com/Yogioo/unity-puer-exec-suite.git Assets/Editor/PuerExecSuite",
                    MessageType.Error);
                return;
            }

            var enabled = PuerExecSuiteGate.IsEnabled;
            var active = PuerExecSuiteGate.IsActive;
            var port = PuerExecSuiteGate.EndpointPort;

            string headline;
            string detail;
            MessageType type;

            if (enabled && active)
            {
                headline = "已开启，控制端口正在监听" + (port > 0 ? "（127.0.0.1:" + port + "）" : string.Empty);
                detail = "AI agent / CLI 现在可以驱动这个编辑器。";
                type = MessageType.Info;
            }
            else if (enabled && !active)
            {
                headline = "已开启，但本次会话还没激活";
                detail = "点下面的「立即激活本次会话」，或者重新打开一次编辑器。";
                type = MessageType.Warning;
            }
            else if (!enabled && active)
            {
                headline = "已关闭，但本次会话的控制端口还开着";
                detail = "上游的激活是会话级的，重启编辑器后才会真正释放。";
                type = MessageType.Warning;
            }
            else
            {
                headline = "已关闭（默认状态）";
                detail = "没有任何端口在监听，CLI 连不上，对这台机器没有任何影响。";
                type = MessageType.None;
            }

            DrawCallout(headline + "\n" + detail, type);

            EditorGUILayout.LabelField(
                "开关来源：" + DescribeSource()
                + "　　上游版本：unity-puer-exec "
                + (string.IsNullOrEmpty(PuerExecSuiteGate.BridgeVersion) ? "?" : PuerExecSuiteGate.BridgeVersion),
                _caption);
        }

        private static string DescribeSource()
        {
            switch (PuerExecSuiteGate.EnabledSource)
            {
                case "machine":
                    return "本机开启（只影响这台电脑）";
                case "project":
                    return "项目设置开启（提交后影响全队）";
                default:
                    return PuerExecSuiteGate.MachineVeto ? "本机否决（即使项目开启也关着）" : "未开启";
            }
        }

        private void DrawPrimaryActions()
        {
            var active = PuerExecSuiteGate.IsActive;
            var buttonHeight = _pointSize * 2.6f;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(PuerExecSuiteGate.MachineEnabled))
                {
                    if (GUILayout.Button("开启（只影响我）", _primaryButton, GUILayout.Height(buttonHeight)))
                    {
                        PuerExecSuiteGate.EnableForMachine();
                    }
                }

                using (new EditorGUI.DisabledScope(PuerExecSuiteGate.MachineVeto))
                {
                    if (GUILayout.Button("关闭（只影响我）", _primaryButton, GUILayout.Height(buttonHeight)))
                    {
                        PuerExecSuiteGate.DisableForMachine();
                    }
                }

                using (new EditorGUI.DisabledScope(active))
                {
                    if (GUILayout.Button("立即激活本次会话", _primaryButton, GUILayout.Height(buttonHeight)))
                    {
                        PuerExecSuiteGate.ActivateNow();
                    }
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "<b>开启（只影响我）</b>：在这台电脑上放开控制通道，写的是 UserSettings/ 下的标记文件，"
                + "不进 git。点一次就够，之后每次打开这个工程都会自动激活。\n"
                + "<b>关闭（只影响我）</b>：本机否决，优先级高于下面「允许全队默认开启」；"
                + "已经打开的端口要重启编辑器才释放。\n"
                + "<b>立即激活本次会话</b>：不改变任何开关，只是让当前这个编辑器把端口开起来。",
                _caption);
        }

        private void DrawSections()
        {
            _showHowItWorks = EditorGUILayout.Foldout(_showHowItWorks, "这是什么？为什么要三层开关？", true, _foldout);
            if (_showHowItWorks)
            {
                DrawCallout(
                    "这是给 AI agent 用的编辑器控制通道：本机回环上一个 HTTP 端口，CLI 通过它执行 C#/JS、"
                    + "读编译错误、跑测试。\n\n"
                    + "它不是常开服务，三层全部默认关闭：\n"
                    + "① 模块不在工程里 → 什么都不编译；\n"
                    + "② 没有开启标记 → 编辑器启动时不激活；\n"
                    + "③ 没有启动参数 → 进程不监听端口。\n\n"
                    + "所以把它提交进 git 也没关系：同事拉到了模块，也依然什么都没有发生。",
                    MessageType.None);
                EditorGUILayout.Space(6f);
            }

            EditorGUILayout.LabelField("开关现状", _sectionTitle);
            EditorGUILayout.Space(2f);

            DrawRow(
                "① 模块本体",
                PuerExecSuiteGate.InstallRoot,
                "目录在，代码会编译（仅编辑器，不进玩家包体）。删掉目录就是卸载。");

            DrawRow(
                "② 启动时激活",
                PuerExecSuiteGate.MachineEnabled
                    ? "已开启（本机）"
                    : PuerExecSuiteGate.ProjectEnabled && !PuerExecSuiteGate.MachineVeto
                        ? "已开启（项目设置）"
                        : "未开启",
                "决定「每次打开工程要不要自动开端口」。本机开启写 UserSettings/，项目开启写 ProjectSettings/。");

            DrawRow(
                "③ 本次会话端口",
                PuerExecSuiteGate.IsActive
                    ? "监听中" + (PuerExecSuiteGate.EndpointPort > 0 ? "（:" + PuerExecSuiteGate.EndpointPort + "）" : string.Empty)
                    : "未监听",
                "真正的端口状态。用「立即激活本次会话」或启动参数 -unityPuerExecControl 打开。");

            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField("全队开关", _sectionTitle);
            EditorGUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(PuerExecSuiteGate.ProjectEnabled))
                {
                    if (GUILayout.Button("允许全队默认开启", _button, GUILayout.Height(_pointSize * 2.2f)))
                    {
                        PuerExecSuiteGate.EnableForProject();
                    }
                }

                using (new EditorGUI.DisabledScope(!PuerExecSuiteGate.ProjectEnabled))
                {
                    if (GUILayout.Button("撤销全队默认开启", _button, GUILayout.Height(_pointSize * 2.2f)))
                    {
                        PuerExecSuiteGate.DisableForProject();
                    }
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "<b>允许全队默认开启</b>：写 ProjectSettings/PuerExecSuite/enabled，把它提交进仓库后，"
                + "所有人拉下来打开编辑器都会自动激活。不想被开的人在自己机器上点「关闭（只影响我）」。\n"
                + "<b>撤销全队默认开启</b>：删掉那个项目级标记。",
                _caption);
        }

        private void DrawRow(string label, string value, string explanation)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(label + "：" + value, _body);
            EditorGUILayout.LabelField(explanation, _caption);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4f);
        }

        private void DrawForAgents()
        {
            EditorGUILayout.LabelField("给 AI agent / CLI 用", _sectionTitle);
            EditorGUILayout.Space(2f);

            var cli = PuerExecSuiteGate.CliPath;
            var exists = File.Exists(cli);
            EditorGUILayout.LabelField(exists ? cli : "找不到 CLI（模块可能不完整）：\n" + cli, _path);

            var buttonHeight = _pointSize * 2.2f;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!exists))
                {
                    if (GUILayout.Button("复制 CLI 路径", _button, GUILayout.Height(buttonHeight)))
                    {
                        EditorGUIUtility.systemCopyBuffer = cli;
                        ShowNotification(new GUIContent("CLI 路径已复制"));
                    }

                    if (GUILayout.Button("打开所在文件夹", _button, GUILayout.Height(buttonHeight)))
                    {
                        EditorUtility.RevealInFinder(cli);
                    }
                }

                if (GUILayout.Button("复制冷启动命令", _button, GUILayout.Height(buttonHeight)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildColdStartCommand();
                    ShowNotification(new GUIContent("冷启动命令已复制"));
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "冷启动命令会用独立日志文件重新打开这个工程，agent 才能按字节读取日志；"
                + "从 Unity Hub 手点开的编辑器做不到日志隔离。",
                _caption);
        }

        private static string BuildColdStartCommand()
        {
            return "\"" + EditorApplication.applicationPath + "\" -projectPath \"" + PuerExecSuiteGate.ProjectRoot + "\" "
                   + PuerExecSuiteGate.ActivationSwitch
                   + " -logFile \""
                   + Path.Combine(PuerExecSuiteGate.ProjectRoot, "Temp", "UnityPuerExec", "Editor.log")
                   + "\"";
        }

        private void DrawAdvanced()
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "详细信息（排查问题用）", true, _foldout);
            if (!_showAdvanced)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("工程　　" + PuerExecSuiteGate.ProjectRoot, _path);
            EditorGUILayout.LabelField("安装位置　" + PuerExecSuiteGate.InstallRoot, _path);
            EditorGUILayout.LabelField(
                "端点文件　" + PuerExecSuiteGate.EndpointPath
                + (PuerExecSuiteGate.EndpointPublished ? "　（已发布）" : "　（未发布）"),
                _path);
            EditorGUILayout.LabelField("生效来源　" + PuerExecSuiteGate.EnabledSource, _path);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("标记文件（● 存在即生效）", _body);
            EditorGUILayout.LabelField("本机开启　" + Mark(PuerExecSuiteGate.MachineEnabledPath, PuerExecSuiteGate.MachineEnabled), _path);
            EditorGUILayout.LabelField("本机否决　" + Mark(PuerExecSuiteGate.MachineDisabledPath, PuerExecSuiteGate.MachineVeto), _path);
            EditorGUILayout.LabelField("项目开启　" + Mark(PuerExecSuiteGate.ProjectEnabledPath, PuerExecSuiteGate.ProjectEnabled), _path);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("把完整状态打印到 Console", _button, GUILayout.Height(_pointSize * 2.2f)))
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
