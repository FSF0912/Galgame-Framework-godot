using System.Collections.Generic;
using Godot;

namespace VisualNovel
{
    public enum GameStatus : byte
    {
        WaitingForInput,
        PerformingAction,
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
        public bool AllowInput;
        AudioStreamPlayer2D _BGMPlayer, _VoicePlayer, _SEPlayer;

        public Dictionary<int, CrossFadeTextureRect> SceneActiveTextures = [];

        DialogueLine _currentDialogueLine;

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
        }

        public override void _Input(InputEvent @event)
        {
            if (gameStatus == GameStatus.BranchChoice || !AllowInput) return;

            if (@event.IsActionPressed("ui_accept") ||
            (@event is InputEventMouseButton mouseEvent &&
             mouseEvent.ButtonIndex == MouseButton.Left &&
             mouseEvent.Pressed))
            {
                if (gameStatus == GameStatus.PerformingAction)
                {
                    InterruptCurrentDialogue();
                }
                else NextDialogue();
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
            _BGMPlayer.Play();
        }

        public void PlayVoice(string path)
        {

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

        }



        private void InterruptCurrentDialogue()
        {
            _currentDialogueLine?.Interrupt(this);
        }

        private void NextDialogue()
        {
            InterruptCurrentDialogue();
            //update current dialogue line (waiting)
            _currentDialogueLine.Execute(this);
            gameStatus = GameStatus.PerformingAction;
        }
    }
}