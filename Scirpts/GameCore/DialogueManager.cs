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
    }

    public partial class DialogueManager : Node
    {
        public GameStatus gameStatus = GameStatus.WaitingForInput;
        [ExportGroup("UI References")]
        [Export] public Label SpeakerNameLabel;
        [Export] public TypeWriter typeWriter;

        [ExportGroup("Visual Elements")]
        [Export] public CrossFadeTextureRect BackGroundTexture;
        [Export] public CrossFadeTextureRect AvatarTexture;

        [ExportGroup("Containers")]
        [Export] public Control TextureContainer;
        [Export] public Control BranchContainer;

        [ExportGroup("Branching System")]
        [Export] public PackedScene BranchButtonScene;

        [ExportGroup("Settings")]
        [Export] public bool AllowInput = true;
        
        AudioStreamPlayer _BGMPlayer, _VoicePlayer, _SEPlayer;
        DialogueLine _currentDialogueLine;
        public readonly Dictionary<int, CrossFadeTextureRect> SceneActiveTextures = [];
        
        public override void _Ready()
        {
            base._Ready();
            _BGMPlayer = new AudioStreamPlayer { Name = "BGMPlayer" };
            _VoicePlayer = new AudioStreamPlayer { Name = "VoicePlayer" };
            _SEPlayer = new AudioStreamPlayer { Name = "SEPlayer" };
            
            AddChild(_BGMPlayer);
            AddChild(_VoicePlayer);
            AddChild(_SEPlayer);

            SceneActiveTextures.Add(-100, BackGroundTexture);
            SceneActiveTextures.Add(-200, AvatarTexture);
        }

        public override void _Input(InputEvent @event)
        {
            if (gameStatus == GameStatus.BranchChoice || !AllowInput || gameStatus == GameStatus.CutScene)
                return;

            if (@event.IsActionPressed("ui_accept"))
            {
                HandleDialogue();
                GetViewport().SetInputAsHandled();
            }
            else if (@event is InputEventMouseButton mouseEvent &&
                 (mouseEvent.ButtonIndex == MouseButton.Left || mouseEvent.ButtonIndex == MouseButton.WheelDown) &&
                 mouseEvent.Pressed)
            {
                HandleDialogue();
                GetViewport().SetInputAsHandled();
            }
            

            void HandleDialogue() { 
                if (gameStatus == GameStatus.PerformingAction)
                {
                    InterruptCurrentDialogue();
                    return;
                }
                NextDialogue();
            }
        }

        public void CreateTexture(int id, Texture2D defaultTex = null, bool immediate = false)
        {
            if (SceneActiveTextures.ContainsKey(id)) return;

            var textureRef = new CrossFadeTextureRect(TextureInitParams.DefaultPortraitNormalDistance)
            { Name = $"Texture_{id}" };

            TextureContainer.AddChild(textureRef);
            SceneActiveTextures.Add(id, textureRef);
            
            if (defaultTex != null)
                textureRef.SetTextureWithFade(defaultTex, immediate:immediate);
        }

        public void PlayBGM(string path, bool loop = true)
        {
            var audioStream = GD.Load<AudioStream>(path);
            if (audioStream == null) return;
            
            _BGMPlayer.Stream = audioStream;

            if (loop)
            {
                switch (audioStream)
                {
                    case AudioStreamOggVorbis oggStream:
                        oggStream.Loop = true;
                        break;
                    case AudioStreamMP3 mp3Stream:
                        mp3Stream.Loop = true;
                        break;
                    case AudioStreamWav wavStream:
                        wavStream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                        break;
                }
            }
            
            _BGMPlayer.Play();
        }

        public void PlayVoice(string path)
        {
            var audioStream = GD.Load<AudioStream>(path);
            if (audioStream == null) return;

            _VoicePlayer.Stream = audioStream;
            _VoicePlayer.Play();
        }

        public void StopVoice() => _VoicePlayer.Stop();

        public void PlaySE(string path)
        {
            var audioStream = GD.Load<AudioStream>(path);
            if (audioStream == null) return;
            
            _SEPlayer.Stream = audioStream;
            _SEPlayer.Play();
        }

        private void InterruptCurrentDialogue()
        {
            _currentDialogueLine?.Interrupt(this);
            gameStatus = GameStatus.WaitingForInput;
        }

        public void NextDialogue()
        {
            if (_currentDialogueLine == null) return;
            
            InterruptCurrentDialogue();
            gameStatus = GameStatus.PerformingAction;
            _currentDialogueLine.Execute(this);
        }

        public void SetCurrentDialogueLine(DialogueLine line) => 
            _currentDialogueLine = line;
    }
}