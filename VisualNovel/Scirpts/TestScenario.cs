using System.Collections.Generic;
using Godot;

namespace VisualNovel
{
    /// <summary>
    /// 包含一个静态方法，用于生成测试剧情的对话指令序列。
    /// 【剧情重塑版 - 温柔腹黑的冬日博士与芳乃】
    /// </summary>
    public static class TestScenario
    {
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
                // ================== PART 1: 实验室的午后 ==================

                new DialogueLine(new List<IDialogueCommand>
                {
                    new SpeakerLine("", "午后的实验室，阳光有些慵懒地洒在精密仪器上。")
                }),

                // 冬日博士登场
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(2, TextureLine.TextureMode.Switch, path:"res://test/drwind.jpg"),
                    // new TextureAnimationLine(2, TextureAnimationLine.AnimationType.Move, 0.5f, targetVector:new Vector2(850, 150), isRelative: false),
                    new SpeakerLine("冬日博士", "芳乃，工作辛苦了。在那张实验台旁，我给你留了些好东西哦。")
                }),
                
                // 芳乃登场
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(1, TextureLine.TextureMode.Switch, path:"res://test/yoshino.png"),
                    // new TextureAnimationLine(1, TextureAnimationLine.AnimationType.Move, 0.5f, targetVector:new Vector2(150, 150), isRelative: false),
                    // new TextureAnimationLine(1, TextureAnimationLine.AnimationType.Shake, duration: 0.5f, isRelative: false),
                    new SpeakerLine("芳乃", "欸？博士竟然会特意犒劳我……我有种不祥的预感。")
                }),

                new DialogueLine(new List<IDialogueCommand>
                {
                    // new TextureAnimationLine(2, TextureAnimationLine.AnimationType.Shake, 0.4f, targetVector: null, intensity: 5f),
                    new SpeakerLine("冬日博士", "呵呵，真失礼呢。我只是觉得，适当的糖分有助于大脑进化。")
                }),

                // ================== PART 2: 神秘的“礼物” ==================
                
                // 牛奶出现
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(3, TextureLine.TextureMode.Switch, path:"res://test/milk.jpg"),
                    // new TextureAnimationLine(3, TextureAnimationLine.AnimationType.Move, 0f, targetVector:new Vector2(600, 300), isRelative: false),
                    // new TextureAnimationLine(3, TextureAnimationLine.AnimationType.Scale, 0.3f, targetVector:new Vector2(1.1f, 1.1f), easeType: Tween.EaseType.Out),
                    new SpeakerLine("芳乃", "哇，是包装很可爱的牛奶！博士万岁！")
                }),

                new DialogueLine(new List<IDialogueCommand>
                {
                    // new TextureAnimationLine(3, TextureAnimationLine.AnimationType.Scale, 0.3f, targetVector:new Vector2(1.0f, 1.0f)),
                    new SpeakerLine("芳乃", "咕嘟咕嘟……哈，感觉浑身都充满了不可思议的力量！")
                }),

                // 博士露出深意的笑容（切换表情）
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(2, TextureLine.TextureMode.Switch, path:"res://test/drwindextra.jpg"),
                    // new TextureAnimationLine(2, TextureAnimationLine.AnimationType.Move, 0.8f, targetVector:new Vector2(700, 150), isRelative: false),
                    new SpeakerLine("冬日博士", "哎呀……芳乃，那是刚才从异世界缝隙里提取的『观测液体』哦。")
                }),
                
                // ================== PART 3: 观测者的异变 ==================

                // 环境变色，音乐变紧张
                new DialogueLine(new List<IDialogueCommand>
                {
                    // new TextureAnimationLine(-100, TextureAnimationLine.AnimationType.ColorTint, 0.5f, targetVector: null, targetColor: new Color(1, 0.6f, 0.6f)),
                    // new Audioline(Audioline.AudioType.SFX, Audioline.AudioPlayType.Play, path:"res://test/manbo.mp3", loop: true),
                    // new TextureAnimationLine(3, TextureAnimationLine.AnimationType.ColorTint, 0.5f, targetVector: null, targetColor: new Color(1.5f, 1.5f, 1.5f)),
                    new SpeakerLine("芳乃", "诶？！博、博士，天花板在跳舞，世界好像在融化！")
                }),

                new DialogueLine(new List<IDialogueCommand>
                {
                    // new TextureAnimationLine(1, TextureAnimationLine.AnimationType.Shake, 0.8f, targetVector: null, intensity: 15f, frequency: 20f),
                    new SpeakerLine("冬日博士", "看来身体的排斥反应比预想中更有趣呢。别担心，这只是『重塑』的过程。")
                }),

                // ================== PART 4: 降临新世界 ==================

                // 博士解决危机（或引导进化）
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(2, TextureLine.TextureMode.Switch, path:"res://test/drwind.jpg"),
                    // new Audioline(Audioline.AudioType.SFX, Audioline.AudioPlayType.Play, path:"res://test/laugh.wav"),
                    // new TextureAnimationLine(3, TextureAnimationLine.AnimationType.Fade, 0.5f, targetVector: null, alpha: 0f),
                    new SpeakerLine("冬日博士", "既然这样，就让我们去那个世界看看吧。")
                }),

                // 场景切换到 9nine 背景
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(-100, TextureLine.TextureMode.Switch, path:"res://test/nine.png"),
                    // new TextureAnimationLine(-100, TextureAnimationLine.AnimationType.ColorTint, 1.0f, targetVector: null, targetColor: new Color(1, 1, 1)),
                    // new Audioline(Audioline.AudioType.BGM, Audioline.AudioPlayType.Play, path:"res://test/bgmusic_1.mp3", loop: true),
                    // new TextureAnimationLine(2, TextureAnimationLine.AnimationType.Move, 0.8f, targetVector:new Vector2(850, 150), isRelative: false),
                    new SpeakerLine("芳乃", "这里是……学校的街道？刚才的实验室竟然消失了……")
                }),

                // 两人淡出，留下余韵
                new DialogueLine(new List<IDialogueCommand>
                {
                    // new TextureAnimationLine(1, TextureAnimationLine.AnimationType.Fade, 1.0f, targetVector: null, alpha: 0f),
                    // new TextureAnimationLine(2, TextureAnimationLine.AnimationType.Fade, 1.0f, targetVector: null, alpha: 0f),
                    new SpeakerLine("冬日博士", "欢迎来到属于我们的‘观测剧场’。以后也请多多指教了，芳乃。")
                }),
                
                // 清理所有资源
                new DialogueLine(new List<IDialogueCommand>
                {
                    new TextureLine(-100, TextureLine.TextureMode.Clear),
                    new TextureLine(1, TextureLine.TextureMode.Delete),
                    new TextureLine(2, TextureLine.TextureMode.Delete),
                    new TextureLine(3, TextureLine.TextureMode.Delete)
                    // new Audioline(Audioline.AudioType.BGM, Audioline.AudioPlayType.Stop, null, fadeDuration: 1.5f) { audioPlayType = Audioline.AudioPlayType.Stop }
                }),
                
                new DialogueLine(new List<IDialogueCommand>
                {
                    new SpeakerLine("", "—— [测试剧本：降临篇] 完 ——")
                })
            };

            return dialogue;
        }
    }
}
