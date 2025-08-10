using System.Collections.Generic;
using Godot;

namespace VisualNovel
{
    public enum GameStatus : byte
    {
        WaitingForInput,
        PerformingAction,
        Skip,
        CutScene,
        BranchChoice
    }

    public partial class DialogueManager : Node
    {
        public GameStatus gameStatus = GameStatus.WaitingForInput;
        [ExportGroup("References")]
        [Export] public Label SpeakerNameLabel;
        [Export] public TypeWriter typeWriter;
        [Export] public CrossFadeTextureRect BackGroundTexture;
        [Export] public CrossFadeTextureRect AvatarTexture;
        [Export] public Control TextureContainer;
        [Export] public Control BranchContainer;
        [Export] public PackedScene BranchButtonScene;

        [ExportGroup("Settings")]
        [Export] public bool AllowInput = true;

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
                    InterruptCurrentDialogue();
                else if (gameStatus == GameStatus.WaitingForInput)
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
                textureRef.SetTextureWithFade(defaultTex, immediate: immediate);
        }

        #region Audio Control

        AudioStreamPlayer _BGMPlayer, _VoicePlayer, _SEPlayer;
        Tween _BGMFadeTween;

        public void PlayBGM(string path, bool loop = true)
        {
            var audioStream = GD.Load<AudioStream>(path);
            if (audioStream == null) return;

            _BGMPlayer.Stop();
            _BGMPlayer.Stream = audioStream;
            _BGMPlayer.VolumeDb = 0;
            HandleLooping(audioStream, loop);
            _BGMPlayer.Play();
        }

        public void StopBGM(bool smooth = false, float fadeDuration = -1.0f)
        {
            if (smooth)
            {
                if (_BGMFadeTween != null && IsInstanceValid(_BGMFadeTween))
                {
                    _BGMFadeTween.Kill();
                    _BGMFadeTween = null;
                }
                _BGMFadeTween = CreateTween();
                _BGMFadeTween.TweenProperty(_BGMPlayer, "volume_db", -80f,
                fadeDuration > 0 ? fadeDuration : GlobalSettings.AnimationDefaultTime).
                SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                _BGMFadeTween.TweenCallback(Callable.From(StopAndClearStream));
            }
            else
            {
                StopAndClearStream();
            }

            void StopAndClearStream() {
                _BGMPlayer.Stop();
                _BGMPlayer.Stream = null;
            }
        }

        public void PlayVoice(string path, bool loop = false)
        {
            var audioStream = GD.Load<AudioStream>(path);
            if (audioStream == null) return;

            _VoicePlayer.Stop();
            _VoicePlayer.Stream = audioStream;
            HandleLooping(audioStream, loop);
            _VoicePlayer.Play();
        }

        public void StopVoice()
        {
            _VoicePlayer.Stop();
            _VoicePlayer.Stream = null;
        }

        public void PlaySE(string path, bool loop = false)
        {
            var audioStream = GD.Load<AudioStream>(path);
            if (audioStream == null) return;

            _SEPlayer.Stop();
            _SEPlayer.Stream = audioStream;
            HandleLooping(audioStream, loop);
            _SEPlayer.Play();
        }

        public void StopSE()
        {
            _SEPlayer.Stop();
            _SEPlayer.Stream = null;
        }

        private void HandleLooping(AudioStream stream, bool loop)
        {
            if (!loop) return;

            switch (stream)
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
        #endregion

        #region State Management

        uint _pendingTasks = 0;

        //invoke after execute
        private void AddPendingTask()
        {
            foreach (var line in _currentDialogueLine.Commands)
            {
                switch (line)
                {
                    case SpeakerLine:
                        _pendingTasks++;
                        typeWriter.OnComplete -= CompleteTask;
                        typeWriter.OnComplete += CompleteTask;
                        break;

                    case Audioline audioline:
                        if (audioline.audioType == Audioline.AudioType.Voice)
                        {
                            _pendingTasks++;
                            _VoicePlayer.Finished -= CompleteTask;
                            _VoicePlayer.Finished += CompleteTask;
                        }
                        break;

                    case TextureLine textureLine:
                        _pendingTasks++;
                        var textureRef = SceneActiveTextures[textureLine.ID];
                        textureRef.FadeComplete -= CompleteTask;
                        textureRef.FadeComplete += CompleteTask;
                        break;

                    case TextureAnimationLine animLine:
                        _pendingTasks++;
                        var textureRef1 = SceneActiveTextures[animLine.ID];
                        textureRef1.AnimationComplete -= CompleteTask;
                        textureRef1.AnimationComplete += CompleteTask;
                        break;
                }
            }
        }

        private void CompleteTask()
        {
            if (_pendingTasks > 0) _pendingTasks--;
            if (_pendingTasks == 0 && gameStatus == GameStatus.PerformingAction)
                    gameStatus = GameStatus.WaitingForInput;
        }

        private void InterruptCurrentDialogue()
        {
            _pendingTasks = 0;
            gameStatus = GameStatus.WaitingForInput;
            _currentDialogueLine?.Interrupt(this);
        }

        public void NextDialogue()
        {
            if (_currentDialogueLine == null) return;

            InterruptCurrentDialogue();
            //switch to next dialogue line
            gameStatus = GameStatus.PerformingAction;
            _currentDialogueLine.Execute(this);
            AddPendingTask();
        }
        #endregion
    }
}