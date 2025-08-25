using System.Collections.Generic;
using Godot;

namespace VisualNovel
{
    /// <summary>
    /// 包含一个静态方法，用于生成测试剧情的对话指令序列。
    /// 【剧情扩展版 - 报错修正】
    /// </summary>
    public static class TestScenario
    {
        /// <summary>
        /// 生成并返回一个用于测试剧情的 DialogueLine 对象列表。
        /// </summary>
        /// <returns>一个可由 DialogueManager 执行的 DialogueLine 指令列表。</returns>
        static int counter = 0;
        static List<DialogueLine> dia = GetTestDialogue();
        public static DialogueLine Get()
        {
            if (counter >= dia.Count - 1) return dia[^1];

            var t = dia[counter];
            counter++;
            return t;

        }
        public static List<DialogueLine> GetTestDialogue()
        {
            var dialogue = new List<DialogueLine>
            {
                // ================== PART 1: 开场 ==================

                // 第1行: 场景淡入，显示背景图 'nine.png'，同时播放背景音乐 'bgmusic_1.mp3'。
                new DialogueLine(new List<IDialogueCommand>
                {
                    new SpeakerLine("", "欢迎来到测试场景！"),
                    new TextureLine(-100, TextureLine.TextureMode.Switch, "res://test/nine.png"),
                    new Audioline(Audioline.AudioType.BGM, audioPlayType:Audioline.AudioPlayType.Play, "res://test/bgmusic_1.mp3", loop: true)
                }),

                // 第2行: 角色 "Yoshino" 登场并说话。
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(1, TextureLine.TextureMode.Switch, "res://test/yoshino.png"),
                    new TextureAnimationLine(1, TextureAnimationLine.AnimationType.Shake, duration: 1f, isRelative: false),
                    new TextureAnimationLine(1, TextureAnimationLine.AnimationType.Move, 0.5f, new Vector2(150, 150), isRelative: false),
                    new SpeakerLine("Yoshino", "欢迎来到这个焕然一新的测试场景！")
                }),
                
                // 第3行: 第二个角色 "Dr. Wind" 登场并说话。
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(2, TextureLine.TextureMode.Switch, "res://test/drwind.jpg"),
                    new TextureAnimationLine(2, TextureAnimationLine.AnimationType.Move, 0.5f, new Vector2(850, 150), isRelative: false),
                    new SpeakerLine("Dr. Wind", "嗯，这里的资源都已经换上了我们自己的测试文件。")
                }),

                // 第4行: Yoshino 的立绘晃动，并说出下一句台词。
                new DialogueLine(new List<IDialogueCommand>
                {
                    // [修正] 为 Shake 动画的 targetVector 参数传入 null
                    new TextureAnimationLine(1, TextureAnimationLine.AnimationType.Shake, 0.4f, targetVector: null, intensity: 10f),
                    new SpeakerLine("Yoshino", "是的！动画效果看起来非常棒！")
                }),

                // ================== PART 2: 神秘的牛奶 ==================
                
                // 第5行: 神秘物品 'milk.jpg' 登场。
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(3, TextureLine.TextureMode.Switch, "res://test/milk.jpg"),
                    new TextureAnimationLine(3, TextureAnimationLine.AnimationType.Scale, 0.3f, new Vector2(1.1f, 1.1f), easeType: Tween.EaseType.Out),
                    new TextureAnimationLine(3, TextureAnimationLine.AnimationType.Move, 0f, new Vector2(600, 300), isRelative: false),
                    new SpeakerLine("Dr. Wind", "说起来，这是什么东西？")
                }),

                // 第6行: 牛奶恢复正常大小，Yoshino 做出解释。
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureAnimationLine(3, TextureAnimationLine.AnimationType.Scale, 0.3f, new Vector2(1.0f, 1.0f)),
                    new SpeakerLine("Yoshino", "是牛奶哦！看上去能补充不少体力！")
                }),

                // 第7行: 博士靠近牛奶，表情变得警惕。
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(2, TextureLine.TextureMode.Switch, "res://test/drwindextra.jpg"),
                    new TextureAnimationLine(2, TextureAnimationLine.AnimationType.Move, 0.8f, new Vector2(700, 150), isRelative: false),
                    new SpeakerLine("Dr. Wind", "等等...这个东西感觉有点不太对劲...")
                }),
                
                // ================== PART 3: 突变 ==================

                // 第8行: 场景突变，背景变红，音乐切换为紧张的 'manbo.mp3'。
                new DialogueLine(new List<IDialogueCommand>
                {
                    // [修正] 为 ColorTint 动画的 targetVector 参数传入 null
                    new TextureAnimationLine(-100, TextureAnimationLine.AnimationType.ColorTint, 0.5f, targetVector: null, targetColor: new Color(1, 0.6f, 0.6f)),
                    new Audioline(Audioline.AudioType.SE, Audioline.AudioPlayType.Play, "res://test/manbo.mp3", loop: true),
                    // [修正] 为 ColorTint 动画的 targetVector 参数传入 null
                    new TextureAnimationLine(3, TextureAnimationLine.AnimationType.ColorTint, 0.5f, targetVector: null, targetColor: new Color(1.5f, 1.5f, 1.5f)),
                    new SpeakerLine("Yoshino", "呀！发、发生了什么事？！")
                }),

                // 第9行: Yoshino 害怕地晃动。
                new DialogueLine(new List<IDialogueCommand>
                {
                    // [修正] 为 Shake 动画的 targetVector 参数传入 null
                    new TextureAnimationLine(1, TextureAnimationLine.AnimationType.Shake, 0.8f, targetVector: null, intensity: 15f, frequency: 20f),
                    new SpeakerLine("Dr. Wind", "别慌，看来是这个牛奶搞的鬼！")
                }),

                // ================== PART 4: 解决与尾声 ==================

                // 第10行: 博士解决问题，牛奶消失，场景恢复。
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(2, TextureLine.TextureMode.Switch, "res://test/drwind.jpg"),
                    new Audioline(Audioline.AudioType.SE, Audioline.AudioPlayType.Play, "res://test/laugh.wav"),
                    // [修正] 为 Fade 动画的 targetVector 参数传入 null
                    new TextureAnimationLine(3, TextureAnimationLine.AnimationType.Fade, 0.5f, targetVector: null, alpha: 0f),
                    new SpeakerLine("Dr. Wind", "看我的！这样就解决了。")
                }),

                // 第11行: 场景和音乐恢复正常。
                new DialogueLine(new List<IDialogueCommand>
                {
                    // [修正] 为 ColorTint 动画的 targetVector 参数传入 null
                    new TextureAnimationLine(-100, TextureAnimationLine.AnimationType.ColorTint, 1.0f, targetVector: null, targetColor: new Color(1, 1, 1)),
                    new Audioline(Audioline.AudioType.BGM, Audioline.AudioPlayType.Play, "res://test/bgmusic_1.mp3", loop: true),
                    new TextureAnimationLine(2, TextureAnimationLine.AnimationType.Move, 0.8f, new Vector2(850, 150), isRelative: false),
                    new SpeakerLine("Yoshino", "哇...你好厉害，博士！")
                }),

                // 第12行: 所有角色淡出。
                new DialogueLine(new List<IDialogueCommand>
                {
                    // [修正] 为 Fade 动画的 targetVector 参数传入 null
                    new TextureAnimationLine(1, TextureAnimationLine.AnimationType.Fade, 1.0f, targetVector: null, alpha: 0f),
                    // [修正] 为 Fade 动画的 targetVector 参数传入 null
                    new TextureAnimationLine(2, TextureAnimationLine.AnimationType.Fade, 1.0f, targetVector: null, alpha: 0f),
                    new SpeakerLine("Dr. Wind", "小事一桩。好了，我们的功能测试也该结束了。")
                }),
                
                // 第13行: 最后清理工作。
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(-100, TextureLine.TextureMode.Clear),
                    new TextureLine(1, TextureLine.TextureMode.Delete),
                    new TextureLine(2, TextureLine.TextureMode.Delete),
                    new TextureLine(3, TextureLine.TextureMode.Delete),
                    new Audioline(Audioline.AudioType.BGM, Audioline.AudioPlayType.Stop, null, smoothStop: true, fadeDuration: 1.5f) { audioPlayType = Audioline.AudioPlayType.Stop }
                }),
                
                // 第14行: 显示结束语。
                new DialogueLine(new List<IDialogueCommand>
                {
                    new SpeakerLine("", "扩展测试剧本圆满结束。")
                })
            };

            return dialogue;
        }
    }
}