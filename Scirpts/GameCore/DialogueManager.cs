using System.Collections.Generic;
using Godot;

namespace VisualNovel
{
    public enum GameStatus : byte
    {
        WaitingForInput,
        PerformingAction,
        CutScene,
        BranchChoice
    };

    public partial class DialogueManager : Node
    {
        public GameStatus gameStatus = GameStatus.WaitingForInput;
        [Export] public Label SpeakerNameLabel;
        [Export] public TypeWriter typeWriter;
        [Export] public CrossFadeTextureRect BackGroundTexture;
        [Export] public CrossFadeTextureRect AvatarTexture;
        [Export] public Control TextureContainer;
        [Export] public Control BranchContainer;
        [Export] public PackedScene BranchButtonScene;
        public bool AllowInput = true;
        AudioStreamPlayer2D _BGMPlayer, _VoicePlayer, _SEPlayer;

        public Dictionary<int, CrossFadeTextureRect> SceneActiveTextures = [];

        DialogueLine _currentDialogueLine;

        int testC = -1;

        #region test
        public static class ResourcePaths
        {
            // 背景
            public const string BackgroundCastle = "res://test/nine.png";

            // 角色
            public const string CharacterHero = "res://test/drwind.jpg";
            public const string CharacterVillain = "res://test/milk.jpg";

            // 音乐
            public const string MusicCastleTheme = "res://test/bgmusic_1.mp3";
            public const string MusicBattleTheme = "res://test/bgmusic_1.mp3";
            public const string MusicVictoryTheme = "res://test/haoyunlai_song.mp3";

            // 音效
            public const string SFXWindHowl = "res://test/manbo.mp3";
            public const string SFXDarkPower = "res://test/manbo.mp3";
            public const string SFXLightPower = "res://test/manbo.mp3";
            public const string SFXExplosion = "res://test/manbo.mp3";

            // 语音
            public const string VoiceVillainLaugh = "res://test/laugh.wav";
        }


List<DialogueLine> tests = new List<DialogueLine>
{
    // 场景1：城堡背景 + 英雄登场
    new DialogueLine(
        new List<IDialogueCommand>
        {
            new TextureLine(-100, TextureLine.TextureMode.Switch, 
                GD.Load<Texture2D>(ResourcePaths.BackgroundCastle)),
            
            new TextureLine(1, TextureLine.TextureMode.Switch, 
                GD.Load<Texture2D>(ResourcePaths.CharacterHero)),
            
            new Audioline(Audioline.AudioType.BGM, ResourcePaths.MusicCastleTheme),
            
            new SpeakerLine("勇士", "这座城堡看起来阴森森的..."),
            
            new Audioline(Audioline.AudioType.SE, ResourcePaths.SFXWindHowl)
        }
    ),
    
    // 场景2：反派登场
    new DialogueLine(
        new List<IDialogueCommand>
        {
            new TextureLine(2, TextureLine.TextureMode.Switch, 
                GD.Load<Texture2D>(ResourcePaths.CharacterVillain)),
            
            new MoveAnimation(2, new Vector2(800, 200), 1.0f),
            
            new SpeakerLine("黑暗领主", "哈哈哈！你终于来了，勇士！"),
            
            new Audioline(Audioline.AudioType.Voice, ResourcePaths.VoiceVillainLaugh)
        }
    ),
    
    // 场景3：战斗开始
    new DialogueLine(
        new List<IDialogueCommand>
        {
            new CompositeAnimation(1)
                .Add(new ScaleAnimation(1, new Vector2(1.2f, 1.2f), 0.3f))
                .Add(new ScaleAnimation(1, new Vector2(1.0f, 1.0f), 0.3f)),
            
            new SpeakerLine("勇士", "是你！黑暗领主！"),
            
            new CompositeAnimation(2)
                .Add(new RotationAnimation(2, 15f, 0.5f))
                .Add(new RotationAnimation(2, -15f, 0.5f))
                .Add(new RotationAnimation(2, 0f, 0.5f)),
            
            new SpeakerLine("黑暗领主", "准备好迎接你的末日了吗？"),
            
            new Audioline(Audioline.AudioType.BGM, ResourcePaths.MusicBattleTheme),
            
            new ShakeAnimation(0, 20f, 30f, 1.5f)
        }
    ),
    
    // 场景4：勇士受伤
    new DialogueLine(
        new List<IDialogueCommand>
        {
            new ColorTintAnimation(1, 
                new Color(1.0f, 0.7f, 0.7f), 0.5f),
            
            new SpeakerLine("勇士", "呃啊！这黑暗力量..."),
            
            new ScaleAnimation(2, new Vector2(1.5f, 1.5f), 1.0f),
            
            new SpeakerLine("黑暗领主", "感受到绝望了吗？"),
            
            new Audioline(Audioline.AudioType.SE, ResourcePaths.SFXDarkPower),
            
            new SingleAnimation(-100, 0.7f, 0.5f)
        }
    ),
    
    // 场景5：勇士反击
    new DialogueLine(
        new List<IDialogueCommand>
        {
            new ColorTintAnimation(1, 
                Colors.White, 0.5f),
            
            new ColorTintAnimation(1, 
                new Color(1.2f, 1.2f, 0.8f), 0.3f),
            
            new SpeakerLine("勇士", "不！光明之力与我同在！"),
            
            new Audioline(Audioline.AudioType.SE, ResourcePaths.SFXLightPower),
            
            new ScaleAnimation(1, new Vector2(1.3f, 1.3f), 0.5f),
            
            new SingleAnimation(-100, 1.0f, 0.5f)
        }
    ),
    
    // 场景6：最终对决
    new DialogueLine(
        new List<IDialogueCommand>
        {
            new CompositeAnimation(0)
                .Add(new ShakeAnimation(1, 15f, 25f, 1.0f))
                .Add(new ShakeAnimation(2, 15f, 25f, 1.0f)),
            
            new SpeakerLine("勇士", "接受光明的审判吧！"),
            
            new SpeakerLine("黑暗领主", "不可能！我的力量..."),
            
            new SingleAnimation(-100, 2.0f, 0.2f),
            
            new Audioline(Audioline.AudioType.SE, ResourcePaths.SFXExplosion),
            
            new ShakeAnimation(0, 30f, 40f, 2.0f)
        }
    ),
    
    // 场景7：结局
    new DialogueLine(
        new List<IDialogueCommand>
        {
            new SingleAnimation(1, 0.0f, 1.0f),
            
            new SingleAnimation(2, 0.0f, 1.0f),
            
            new TextureLine(1, TextureLine.TextureMode.Delete),
            new TextureLine(2, TextureLine.TextureMode.Delete),
            
            new SingleAnimation(-100, 1.0f, 0.5f),
            
            
            new Audioline(Audioline.AudioType.BGM, ResourcePaths.MusicVictoryTheme),
            
            new SpeakerLine("旁白", "勇士战胜了黑暗领主，王国恢复了和平...")
        }
    )
};
        #endregion

        public override void _Ready()
        {
            base._Ready();
            _BGMPlayer = new AudioStreamPlayer2D();
            AddChild(_BGMPlayer);
            _BGMPlayer.Name = "BGMPlayer";
            _VoicePlayer = new AudioStreamPlayer2D();
            AddChild(_VoicePlayer);
            _VoicePlayer.Name = "VoicePlayer";
            _SEPlayer = new AudioStreamPlayer2D();
            AddChild(_SEPlayer);
            _SEPlayer.Name = "SEPlayer";

            SceneActiveTextures.Clear();
            SceneActiveTextures.Add(-100, BackGroundTexture);
            SceneActiveTextures.Add(-200, AvatarTexture);
            NextDialogue();
        }

/*
        public override void _Input(InputEvent @event)
        {
            if (gameStatus == GameStatus.BranchChoice || !AllowInput) return;

            if (@event.IsActionPressed("ui_accept") ||
            (@event is InputEventMouseButton mouseEvent &&
             mouseEvent.ButtonIndex == MouseButton.Left &&
             mouseEvent.Pressed)
             || Input.IsKeyPressed(Key.Space))
            {
                if (gameStatus != GameStatus.CutScene) NextDialogue();
            }
        }*/

        public override void _Process(double delta)
        {
            base._Process(delta);
            if (gameStatus == GameStatus.BranchChoice || !AllowInput) return;

            if (Input.IsActionPressed("ui_accept") ||
                Input.IsMouseButtonPressed(MouseButton.Left)
             || Input.IsKeyPressed(Key.Space))
            {
                GD.Print("Input detected, proceeding with dialogue.");
                if (gameStatus != GameStatus.CutScene) NextDialogue();
            }
        }

        public void CreateTexture(int ID, Texture2D defaultTex = null, bool Immediately = false)
        {
            if (SceneActiveTextures.ContainsKey(ID)) return;

            var texSceneRef = new CrossFadeTextureRect();
            TextureContainer.AddChild(texSceneRef);
            SceneActiveTextures.Add(ID, texSceneRef);
            if (defaultTex != null)
            {
                texSceneRef.SetTextureWithFade(defaultTex, Immediately);
            }
        }


        public void PlayBGM(string path, bool loop = true)
        {
            if (_BGMPlayer.IsPlaying())
            {
                _BGMPlayer.Stop();
            }

            var audioStream = GD.Load<AudioStream>(path);
            if (audioStream == null)
            {
                GD.PrintErr("Failed to load BGM from path: " + path);
                return;
            }

            _BGMPlayer.Stream = audioStream;
            
            switch (_BGMPlayer.Stream)
            {
                case AudioStreamOggVorbis ogg:
                    ogg.Loop = loop;
                    break;
                case AudioStreamMP3 mp3:
                    mp3.Loop = loop;
                    break;
                case AudioStreamWav wav:
                    wav.LoopMode = loop ?
                    AudioStreamWav.LoopModeEnum.Forward :
                    AudioStreamWav.LoopModeEnum.Disabled;
                    break;
            }

            _BGMPlayer.Play();
        }

        public void PlayVoice(string path)
        {
            if (_VoicePlayer.IsPlaying())
            {
                _VoicePlayer.Stop();
            }

            var audioStream = GD.Load<AudioStream>(path);
            if (audioStream == null)
            {
                GD.PrintErr("Failed to load Voice from path: " + path);
                return;
            }

            _VoicePlayer.Stream = audioStream;
            _VoicePlayer.Play();
        }

        public void StopVoice()
        {
            if (_VoicePlayer.IsPlaying())
            {
                _VoicePlayer.Stop();
            }
        }

        public void PlaySE(string path)
        {
            if (_SEPlayer.IsPlaying())
            {
                _SEPlayer.Stop();
            }

            var audioStream = GD.Load<AudioStream>(path);
            if (audioStream == null)
            {
                GD.PrintErr("Failed to load SE from path: " + path);
                return;
            }

            _SEPlayer.Stream = audioStream;
            _SEPlayer.Play();
        }



        private void InterruptCurrentDialogue()
        {
            _currentDialogueLine?.Interrupt(this);
        }

        private void NextDialogue()
        {
            InterruptCurrentDialogue();
            testC++;
            if (testC >= tests.Count)
            {
                GD.Print("Dialogue sequence completed.");
                return;
            }
            _currentDialogueLine = tests[testC];
            _currentDialogueLine.Execute(this);
            gameStatus = GameStatus.PerformingAction;
        }
    }
}