// 文件: KagParser.cs
// 版本: 重构优化版 v2.0
using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VisualNovel
{
    public static class KagParser
    {
        // 使用正则表达式来解析 key="value" 或 key=value 格式的参数
        // 核心逻辑:
        // ([a-zA-Z0-9_]+)  : 匹配并捕获参数名 (键)
        // \s*=\s* : 匹配等号，允许前后有任意空格
        // (?: ... | ...)    : 匹配两种可能的值格式
        //   "([^"]*)"      : 格式1: 匹配并捕获双引号内的所有内容
        //   (\S+)          : 格式2: 匹配并捕获一个不含空格的连续字符串
        private static readonly Regex ParamRegex = new Regex(@"([a-zA-Z0-9_]+)\s*=\s*(?:""([^""]*)""|(\S+))", RegexOptions.Compiled);

        public static List<DialogueLine> ParseScript(string scenarioPath)
        {
            using var file = FileAccess.Open(scenarioPath, FileAccess.ModeFlags.Read);
            if (FileAccess.GetOpenError() != Error.Ok)
            {
                GD.PrintErr($"KAG脚本加载失败: 无法打开文件 '{scenarioPath}'");
                return null;
            }

            var mainResults = new List<DialogueLine>();
            var currentCommands = new List<IDialogueCommand>();
            int lineNumber = 0;

            while (!file.EofReached())
            {
                lineNumber++;
                var line = file.GetLine().Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('*'))
                {
                    // 忽略空行、注释行和标签行
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    // 解析指令标签
                    ParseTag(line, lineNumber, ref currentCommands, mainResults);
                }
                else
                {
                    // 解析对话文本
                    ParseDialogue(line, ref currentCommands);
                }
            }

            // 将最后一组指令打包
            if (currentCommands.Count > 0)
            {
                mainResults.Add(new DialogueLine(currentCommands));
            }

            GD.Print($"KAG脚本 '{scenarioPath}' 解析完成，共 {lineNumber} 行，生成 {mainResults.Count} 个指令块。");
            return mainResults;
        }

        /// <summary>
        /// 解析指令标签, 如 [bg storage="bg.png"]
        /// </summary>
        private static void ParseTag(string line, int lineNumber, ref List<IDialogueCommand> currentCommands, List<DialogueLine> mainResults)
        {
            string innerTag = line.Substring(1, line.Length - 2);
            string[] parts = innerTag.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string commandName = parts[0].ToLower();
            string paramString = parts.Length > 1 ? parts[1] : string.Empty;
            
            var parameters = ParseParameters(paramString);

            switch (commandName)
            {
                case "p": // 换页指令
                case "endlink": // 兼容旧指令
                    if (currentCommands.Count > 0)
                    {
                        mainResults.Add(new DialogueLine(currentCommands));
                        currentCommands = new List<IDialogueCommand>(); // 开始新的一组指令
                    }
                    break;

                case "bg":
                    HandleBgTag(parameters, lineNumber, line, ref currentCommands);
                    break;

                case "image":
                    HandleImageTag(parameters, lineNumber, line, ref currentCommands);
                    break;
                
                case "freeimage":
                    HandleFreeImageTag(parameters, lineNumber, line, ref currentCommands);
                    break;
                
                case "playbgm":
                    HandlePlayBgmTag(parameters, lineNumber, line, ref currentCommands);
                    break;

                case "stopbgm":
                    HandleStopBgmTag(parameters, ref currentCommands);
                    break;
                
                case "playse":
                    HandlePlaySeTag(parameters, lineNumber, line, ref currentCommands);
                    break;

                case "voice":
                     if (TryGetStringParam(parameters, "storage", out var voicePath, lineNumber, line))
                     {
                         currentCommands.Add(new Audioline(Audioline.AudioType.Voice, Audioline.AudioPlayType.Play, voicePath, false));
                     }
                     break;
                
                case "anim":
                    HandleAnimTag(parameters, lineNumber, line, ref currentCommands);
                    break;
                
                default:
                    GD.Print($"警告 (行 {lineNumber}): 无法识别的KAG指令 '{commandName}'");
                    break;
            }
        }

        /// <summary>
        /// 使用正则表达式解析参数字符串
        /// </summary>
        private static Dictionary<string, string> ParseParameters(string paramString)
        {
            var parameters = new Dictionary<string, string>();
            var matches = ParamRegex.Matches(paramString);
            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value;
                // 正则表达式捕获组: 组2是带引号的值, 组3是不带引号的值
                string value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                parameters[key] = value;
            }
            return parameters;
        }

        /// <summary>
        /// 解析对话行
        /// </summary>
        private static void ParseDialogue(string line, ref List<IDialogueCommand> currentCommands)
        {
            string speaker = null;
            string dialogue = line;

            // 兼容中文和英文冒号
            int colonIndex = line.IndexOf('：');
            if (colonIndex == -1) colonIndex = line.IndexOf(':');

            if (colonIndex > 0)
            {
                speaker = line.Substring(0, colonIndex);
                dialogue = line.Substring(colonIndex + 1);
            }
            
            currentCommands.Add(new SpeakerLine(speaker, dialogue));
        }

        #region Tag Handlers

        private static void HandleBgTag(Dictionary<string, string> parameters, int lineNumber, string line, ref List<IDialogueCommand> commands)
        {
            if (TryGetStringParam(parameters, "storage", out var path, lineNumber, line))
            {
                commands.Add(new TextureLine(-100, TextureLine.TextureMode.Switch, path));
            }
        }

        private static void HandleImageTag(Dictionary<string, string> parameters, int lineNumber, string line, ref List<IDialogueCommand> commands)
        {
            if (TryGetIntParam(parameters, "layer", out int layer, lineNumber, line) && 
                TryGetStringParam(parameters, "storage", out var path, lineNumber, line))
            {
                float fadeDuration = -1f;
                // time参数是可选的
                if (parameters.ContainsKey("time"))
                {
                    TryGetFloatParam(parameters, "time", out fadeDuration, lineNumber, line);
                    fadeDuration /= 1000f; // KAG的时间单位是毫秒
                }
                commands.Add(new TextureLine(layer, TextureLine.TextureMode.Switch, path, fadeDuration));
            }
        }
        
        private static void HandleFreeImageTag(Dictionary<string, string> parameters, int lineNumber, string line, ref List<IDialogueCommand> commands)
        {
            if (TryGetIntParam(parameters, "layer", out int layer, lineNumber, line))
            {
                float fadeDuration = -1f;
                if (parameters.ContainsKey("time"))
                {
                    TryGetFloatParam(parameters, "time", out fadeDuration, lineNumber, line);
                    fadeDuration /= 1000f;
                }
                commands.Add(new TextureLine(layer, TextureLine.TextureMode.Delete, fadeDuration: fadeDuration));
            }
        }

        private static void HandlePlayBgmTag(Dictionary<string, string> parameters, int lineNumber, string line, ref List<IDialogueCommand> commands)
        {
             if (TryGetStringParam(parameters, "storage", out var path, lineNumber, line))
             {
                 bool loop = true;
                 if (parameters.TryGetValue("loop", out var loopVal) && loopVal.ToLower() == "false")
                 {
                     loop = false;
                 }
                 commands.Add(new Audioline(Audioline.AudioType.BGM, Audioline.AudioPlayType.Play, path, loop));
             }
        }

        private static void HandleStopBgmTag(Dictionary<string, string> parameters, ref List<IDialogueCommand> commands)
        {
             float fadeDuration = -1f;
             bool smoothStop = parameters.ContainsKey("time");
             if (smoothStop)
             {
                 TryGetFloatParam(parameters, "time", out fadeDuration, -1, ""); // 出错也关系不大
                 fadeDuration /= 1000f;
             }
             commands.Add(new Audioline(Audioline.AudioType.BGM, Audioline.AudioPlayType.Stop, null, false, smoothStop, fadeDuration));
        }

        private static void HandlePlaySeTag(Dictionary<string, string> parameters, int lineNumber, string line, ref List<IDialogueCommand> commands)
        {
             if (TryGetStringParam(parameters, "storage", out var path, lineNumber, line))
             {
                 bool loop = false;
                 if (parameters.TryGetValue("loop", out var loopVal) && loopVal.ToLower() == "true")
                 {
                     loop = true;
                 }
                 commands.Add(new Audioline(Audioline.AudioType.SE, Audioline.AudioPlayType.Play, path, loop));
             }
        }

        private static void HandleAnimTag(Dictionary<string, string> parameters, int lineNumber, string line, ref List<IDialogueCommand> commands)
        {
            if (!TryGetIntParam(parameters, "layer", out int layer, lineNumber, line) ||
                !TryGetStringParam(parameters, "type", out var animTypeStr, lineNumber, line))
            {
                return; // 缺少必要参数
            }

            // 尝试将字符串转为枚举，失败则报错
            if (!Enum.TryParse<TextureAnimationLine.AnimationType>(animTypeStr, true, out var animType))
            {
                GD.PrintErr($"错误 (行 {lineNumber}): 无效的动画类型(type) '{animTypeStr}'。 行内容: '{line}'");
                return;
            }

            Tween.EaseType easeType = Tween.EaseType.InOut;
            Tween.TransitionType transType = Tween.TransitionType.Sine;
            if (parameters.TryGetValue("ease", out var easeStr))
            {
                _ = Enum.TryParse(easeStr, true, out easeType);
            }

            if (parameters.TryGetValue("trans", out var transStr))
            {
                _ = Enum.TryParse(transStr, true, out transType);
            }
            
            float duration = -1f;
            if (parameters.ContainsKey("time"))
            {
                TryGetFloatParam(parameters, "time", out duration, lineNumber, line);
                duration /= 1000f; // 转换为秒
            }

            // ... (解析其他动画参数)
            parameters.TryGetValue("relative", out var relStr);
            bool isRelative = relStr?.ToLower() == "true";

            parameters.TryGetValue("local", out var localStr);
            bool isLocal = localStr?.ToLower() != "false"; // 默认为true

            Vector2? targetVector = null;
            if (parameters.ContainsKey("pos") && TryParseVector2(parameters["pos"], out var posVec)) targetVector = posVec;
            if (parameters.ContainsKey("scale") && TryParseVector2(parameters["scale"], out var scaleVec)) targetVector = scaleVec;

            float? rotationDegrees = null;
            if (parameters.ContainsKey("degrees"))
            {
                TryGetFloatParam(parameters, "degrees", out float rot, lineNumber, line);
                rotationDegrees = rot;
            }
            
            float? alpha = null;
            if (parameters.ContainsKey("alpha"))
            {
                TryGetFloatParam(parameters, "alpha", out float a, lineNumber, line);
                alpha = a;
            }
            
            Color? targetColor = null;
            if (parameters.ContainsKey("color"))
            {
                targetColor = Color.FromString(parameters["color"], Colors.White);
            }
            
            float? intensity = null;
            if (parameters.ContainsKey("intensity"))
            {
                TryGetFloatParam(parameters, "intensity", out float i, lineNumber, line);
                intensity = i;
            }
            
            float? frequency = null;
            if (parameters.ContainsKey("frequency"))
            {
                TryGetFloatParam(parameters, "frequency", out float f, lineNumber, line);
                frequency = f;
            }
            
            commands.Add(new TextureAnimationLine(
                layer, animType, duration,
                targetVector: targetVector,
                isRelative: isRelative, isLocal: isLocal,
                targetColor: targetColor,
                alpha: alpha,
                rotationDegrees: rotationDegrees,
                intensity: intensity,
                frequency: frequency
            ));
        }

        #endregion

        #region Parameter Helper
        
        // 尝试获取字符串参数，如果缺少则报错
        private static bool TryGetStringParam(Dictionary<string, string> parameters, string key, out string value, int lineNumber, string line)
        {
            if (parameters.TryGetValue(key, out value))
            {
                return true;
            }
            GD.PrintErr($"错误 (行 {lineNumber}): KAG指令缺少必要的 '{key}' 参数。 行内容: '{line}'");
            value = null;
            return false;
        }

        // 尝试获取并解析int参数，如果缺少或格式错误则报错
        private static bool TryGetIntParam(Dictionary<string, string> parameters, string key, out int value, int lineNumber, string line)
        {
            if (TryGetStringParam(parameters, key, out string strValue, lineNumber, line))
            {
                if (int.TryParse(strValue, out value))
                {
                    return true;
                }
                GD.PrintErr($"错误 (行 {lineNumber}): 参数 '{key}' 的值 '{strValue}' 不是一个有效的整数。 行内容: '{line}'");
            }
            value = 0;
            return false;
        }

        // 尝试获取并解析float参数
        private static bool TryGetFloatParam(Dictionary<string, string> parameters, string key, out float value, int lineNumber, string line)
        {
            if (parameters.TryGetValue(key, out string strValue))
            {
                if (float.TryParse(strValue, out value))
                {
                    return true;
                }
                GD.PrintErr($"错误 (行 {lineNumber}): 参数 '{key}' 的值 '{strValue}' 不是一个有效的浮点数。 行内容: '{line}'");
            }
            value = 0f;
            return false;
        }

        // 尝试解析Vector2
        private static bool TryParseVector2(string text, out Vector2 value)
        {
            string[] parts = text.Split(new[]{',', '，'}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y))
            {
                value = new Vector2(x, y);
                return true;
            }
            value = Vector2.Zero;
            return false;
        }
        #endregion
    }
}