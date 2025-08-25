// 文件: KagParser.cs
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VisualNovel
{
    public static class KagParser
    {
        /// <summary>
        /// 异步解析 KAG 风格的脚本文件。
        /// 它会分帧处理以防止在加载大文件时游戏卡顿。
        /// </summary>
        /// <param name="filePath">脚本文件的路径 (例如 "res://scenarios/chapter1.ks")。</param>
        /// <param name="linesPerFrame">在等待下一帧之前要处理的行数。</param>
        /// <param name="onProgress">一个可选的回调，用于报告加载进度 (0.0 到 1.0)。</param>
        /// <returns>一个包含所有解析出的 DialogueLine 的任务。</returns>
        public static List<DialogueLine> ParseScript(string filePath)
        {

            // 使用 Godot 的 FileAccess API 打开文件，以支持 res:// 路径
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (FileAccess.GetOpenError() != Error.Ok)
            {
                GD.PrintErr($"KAG script not found or could not be opened: {filePath}");
                return null;
            }

            ulong fileSize = file.GetLength();
            var mainResults = new List<DialogueLine>();
            var currentCommands = new List<IDialogueCommand>();

            while (!file.EofReached())
            {
                var line = file.GetLine().Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    char firstChar = line[0];

                    if (firstChar == ';' || firstChar == '*')
                    {
                        // 忽略注释和标签行
                        // do nothing
                    }
                    else if (firstChar == '[' && line[^1] == ']')
                    {
                        /*
                        // 遇到标签时，先将之前缓存的指令打包成一个 DialogueLine
                        if (currentCommands.Count > 0)
                        {
                            results.Add(new DialogueLine(currentCommands));
                            currentCommands.Clear(); // 清空以备下一组指令
                        }
                        */
                        // Inline ParseTag
                        string innerTag = line.AsSpan(1, line.Length - 2).ToString();
                        string[] parts = innerTag.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                        string commandName = parts[0];

                        var parameters = (parts.Length > 1)
                            ? (new Func<string, Dictionary<string, string>>((paramString) =>
                            {
                                var parameters = new Dictionary<string, string>();
                                int i = 0;
                                while (i < paramString.Length)
                                {
                                    while (i < paramString.Length && char.IsWhiteSpace(paramString[i])) i++;
                                    if (i >= paramString.Length) break;

                                    int keyEnd = paramString.IndexOf('=', i);
                                    if (keyEnd == -1) break;
                                    string key = paramString.Substring(i, keyEnd - i).Trim();
                                    i = keyEnd + 1;

                                    string value;
                                    if (i < paramString.Length && paramString[i] == '"')
                                    {
                                        i++;
                                        int valueEnd = paramString.IndexOf('"', i);
                                        if (valueEnd == -1) break;
                                        value = paramString.Substring(i, valueEnd - i);
                                        i = valueEnd + 1;
                                    }
                                    else
                                    {
                                        int valueEnd = paramString.IndexOf(' ', i);
                                        if (valueEnd == -1) valueEnd = paramString.Length;
                                        value = paramString.Substring(i, valueEnd - i);
                                        i = valueEnd;
                                    }

                                    if (!string.IsNullOrEmpty(key))
                                    {
                                        parameters[key] = value;
                                    }
                                }
                                return parameters;
                            }))(parts[1])
                            : new Dictionary<string, string>();

                        switch (commandName.ToLower())
                        {
                            case "bg":
                                if (parameters.TryGetValue("storage", out var path))
                                {
                                    currentCommands.Add(new TextureLine(-100, TextureLine.TextureMode.Switch, path));
                                }
                                break;

                            case "image":
                                if (parameters.TryGetValue("layer", out var layerStr) &&
                                    parameters.TryGetValue("storage", out var imagePath))
                                {
                                    int layer = int.Parse(layerStr);

                                    if (parameters.TryGetValue("time", out var timeStr))
                                    {
                                        if (float.TryParse(timeStr, out var fadeDuration))
                                            currentCommands.Add(new TextureLine(layer, TextureLine.TextureMode.Switch, imagePath, fadeDuration));
                                        else
                                            currentCommands.Add(new TextureLine(layer, TextureLine.TextureMode.Switch, imagePath));
                                    }
                                    else
                                        currentCommands.Add(new TextureLine(layer, TextureLine.TextureMode.Switch, imagePath));
                                }
                                break;

                            case "freeimage":
                                if (parameters.TryGetValue("layer", out var freeLayerStr))
                                {
                                    int freeLayer = int.Parse(freeLayerStr);

                                    if (parameters.TryGetValue("time", out var freeTimeStr))
                                    {
                                        if (float.TryParse(freeTimeStr, out var fadeDuration))
                                            currentCommands.Add(new TextureLine(freeLayer, TextureLine.TextureMode.Delete, fadeDuration: fadeDuration));
                                        else
                                            currentCommands.Add(new TextureLine(freeLayer, TextureLine.TextureMode.Delete));
                                    }
                                    else
                                        currentCommands.Add(new TextureLine(freeLayer, TextureLine.TextureMode.Delete));
                                }
                                break;

                            case "playbgm":
                                if (parameters.TryGetValue("storage", out var bgmPath))
                                {
                                    var loop = !parameters.TryGetValue("loop", out var loopVal) || loopVal.ToLower() != "false";
                                    currentCommands.Add(new Audioline(Audioline.AudioType.BGM, Audioline.AudioPlayType.Play, bgmPath, loop));
                                }
                                break;

                            case "stopbgm":
                                if (parameters.TryGetValue("time", out var stopTimeStr))
                                {
                                    if (float.TryParse(stopTimeStr, out var fadeDuration))
                                        currentCommands.Add(new Audioline(Audioline.AudioType.BGM, Audioline.AudioPlayType.Stop, null, false, true, fadeDuration));
                                    else
                                        currentCommands.Add(new Audioline(Audioline.AudioType.BGM, Audioline.AudioPlayType.Stop, null, false, true));
                                }
                                break;
                            case "playse":
                                if (parameters.TryGetValue("storage", out var sePath))
                                {
                                    var loop = !parameters.TryGetValue("loop", out var loopVal) || loopVal.ToLower() != "false";
                                    currentCommands.Add(new Audioline(Audioline.AudioType.SE, Audioline.AudioPlayType.Play, sePath, loop));
                                }
                                break;

                            case "voice":
                                if (parameters.TryGetValue("storage", out var voicePath))
                                {
                                    currentCommands.Add(new Audioline(Audioline.AudioType.Voice, Audioline.AudioPlayType.Play, voicePath, false));
                                }
                                break;

                            case "p":
                                if (currentCommands.Count > 0)
                                {
                                    mainResults.Add(new DialogueLine(currentCommands));
                                    currentCommands.Clear(); // 清空以备下一组指令
                                }
                                break;
                        }
                    }
                    else
                    {
                        // Inline ParseDialogue
                        string speaker = null;
                        string dialogue;

                        int colonIndex = line.IndexOf('：');
                        if (colonIndex == -1) colonIndex = line.IndexOf(':');

                        if (colonIndex > 0)
                        {
                            speaker = line.Substring(0, colonIndex);
                            dialogue = line.Substring(colonIndex + 1);
                        }
                        else
                        {
                            dialogue = line;
                        }

                        currentCommands.Add(new SpeakerLine(speaker, dialogue));
                    }
                }
            }

            mainResults.Add(new DialogueLine(currentCommands)); // 添加最后一组指令
            return mainResults;
        }

        
        private static Dictionary<string, string> ParseParameters(string paramString)
        {
            var parameters = new Dictionary<string, string>();
            int i = 0;
            while (i < paramString.Length)
            {
                while (i < paramString.Length && char.IsWhiteSpace(paramString[i])) i++;
                if (i >= paramString.Length) break;

                int keyEnd = paramString.IndexOf('=', i);
                if (keyEnd == -1) break; // 格式错误
                string key = paramString.Substring(i, keyEnd - i).Trim();
                i = keyEnd + 1;

                string value;
                if (i < paramString.Length && paramString[i] == '"')
                {
                    i++; // 跳过起始引号
                    int valueEnd = paramString.IndexOf('"', i);
                    if (valueEnd == -1) break; // 格式错误
                    value = paramString.Substring(i, valueEnd - i);
                    i = valueEnd + 1;
                }
                else
                {
                    int valueEnd = paramString.IndexOf(' ', i);
                    if (valueEnd == -1) valueEnd = paramString.Length;
                    value = paramString.Substring(i, valueEnd - i);
                    i = valueEnd;
                }

                if (!string.IsNullOrEmpty(key))
                {
                    parameters[key] = value;
                }
            }
            return parameters;
        }
    }
}