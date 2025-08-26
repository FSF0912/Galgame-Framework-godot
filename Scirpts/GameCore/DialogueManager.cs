using System;
using System.Collections.Generic;
using Godot;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

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

	public partial class DialogueManager : Control
	{
		public static DialogueManager Instance;

		/// <summary>
		/// Signal emitted after a new dialogue line already starts executing.
		/// </summary>
		[Signal] public delegate void AfterExecuteStartEventHandler();

		/// <summary>
		/// Signal emitted before a new dialogue line already starts executing.
		/// </summary>
		[Signal] public delegate void BeforeExecuteStartEventHandler();

		[Signal] public delegate void ExecuteCompleteEventHandler();


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

		public override void _EnterTree()
		{
			base._EnterTree();
			if (Instance == null)
				Instance = this;
			else
				QueueFree();
		}

		public override void _Ready()
		{
			base._Ready();
			_BGMPlayer = new AudioStreamPlayer { Name = "BGMPlayer" };
			_VoicePlayer = new AudioStreamPlayer { Name = "VoicePlayer" };
			_SEPlayer = new AudioStreamPlayer { Name = "SEPlayer" };
			SceneActiveTextures.Clear();

			AddChild(_BGMPlayer);
			AddChild(_VoicePlayer);
			AddChild(_SEPlayer);

			SceneActiveTextures.Add(-100, BackGroundTexture);
			SceneActiveTextures.Add(-200, AvatarTexture);

			typeWriter.StartTyping += EnableDialogueSign;
			typeWriter.OnComplete += DisableDialogueSign;

			BeforeExecuteStart += AutoplayRegistered_BeforeExecuteStart;
			ExecuteComplete += AutoplayRegistered_ExecuteComplete;

			_currentDialogueLine = TestScenario.Get();
			_currentDialogueLine.Execute(this);
		}


		public override void _ExitTree()
		{
			base._ExitTree();
			typeWriter.StartTyping -= EnableDialogueSign;
			typeWriter.OnComplete -= DisableDialogueSign;
			Instance = null;
		}

		public override void _Input(InputEvent @event)
		{
			if (gameStatus == GameStatus.BranchChoice || !AllowInput || gameStatus == GameStatus.CutScene)
				return;

			if (@event.IsActionPressed("ui_accept")) HandleDialogue();

			else if (@event is InputEventMouseButton mouseEvent &&
				 (mouseEvent.ButtonIndex == MouseButton.Left || mouseEvent.ButtonIndex == MouseButton.WheelDown) &&
				 mouseEvent.Pressed) HandleDialogue();


			void HandleDialogue()
			{
				if (gameStatus == GameStatus.PerformingAction)
					InterruptCurrentDialogue();
				else if (gameStatus == GameStatus.WaitingForInput)
					NextDialogue();

				GetViewport().SetInputAsHandled();
			}
		}


		public CrossFadeTextureRect CreateTexture(int id, float duration, TextureParams textureParams = null, Texture2D defaultTex = null, bool immediate = false)
		{
			if (SceneActiveTextures.TryGetValue(id, out CrossFadeTextureRect value)) return value;

			var textureRef = new CrossFadeTextureRect(textureParams ?? TextureParams.DefaultPortraitNormalDistance)
			{ Name = $"Texture_{id}" };

			TextureContainer.AddChild(textureRef);
			SceneActiveTextures.Add(id, textureRef);

			if (defaultTex != null)
				textureRef.SetTextureWithFade(defaultTex, duration:duration, immediate: immediate, ZIndex: id);

			return textureRef;
		}

		public CrossFadeTextureRect CreateTexture(int id, float duration, TextureParams textureParams = null, string defaultTexPath = null, bool immediate = false)
		{
			if (SceneActiveTextures.TryGetValue(id, out CrossFadeTextureRect value)) return value;

			var textureRef = new CrossFadeTextureRect(textureParams ?? TextureParams.DefaultPortraitNormalDistance)
			{ Name = $"Texture_{id}" };

			TextureContainer.AddChild(textureRef);
			SceneActiveTextures.Add(id, textureRef);

			if (!string.IsNullOrWhiteSpace(defaultTexPath))
				textureRef.SetTextureWithFade(defaultTexPath, duration:duration, immediate: immediate, ZIndex: id);

			return textureRef;
		}

		private void EnableDialogueSign() { }
		private void DisableDialogueSign() { }

		#region Audio Control

		AudioStreamPlayer _BGMPlayer, _VoicePlayer, _SEPlayer;
		Tween _BGMFadeTween;

		public void PlayBGM(string path, bool loop = true)
		{
			var audioStream = GD.Load<AudioStream>(path);
			if (audioStream == null) return;

			StopAndClearStream(_BGMPlayer);
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
				_BGMFadeTween.TweenCallback(Callable.From(() => StopAndClearStream(_BGMPlayer)));
			}
			else
			{
				StopAndClearStream(_BGMPlayer);
			}
		}

		public void PlayVoice(string path, bool loop = false)
		{
			var audioStream = GD.Load<AudioStream>(path);
			if (audioStream == null) return;

			StopAndClearStream(_VoicePlayer);
			_VoicePlayer.Stream = audioStream;
			HandleLooping(audioStream, loop);
			_VoicePlayer.Play();
		}

		public void StopVoice()
		{
			StopAndClearStream(_VoicePlayer);
		}


		public void PlaySE(string path, bool loop = false)
		{
			var audioStream = GD.Load<AudioStream>(path);
			if (audioStream == null) return;

			StopAndClearStream(_SEPlayer);
			_SEPlayer.Stream = audioStream;
			HandleLooping(audioStream, loop);
			_SEPlayer.Play();
		}

		public void StopSE()
		{
			StopAndClearStream(_SEPlayer);
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

		private void StopAndClearStream(AudioStreamPlayer player)
		{
			if (player == null) return;

			player.Stop();
			player.Stream?.Dispose();
			player.Stream = null;
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
						if (SceneActiveTextures.TryGetValue(textureLine.ID, out var textureRef))
						{
							_pendingTasks++;
							textureRef.FadeComplete -= CompleteTask;
							textureRef.FadeComplete += CompleteTask;
						}
						break;

					case TextureAnimationLine animLine:
						if (SceneActiveTextures.TryGetValue(animLine.ID, out var textureRef1))
						{
							_pendingTasks++;
							textureRef1.Animator.AnimationComplete -= CompleteTask;
							textureRef1.Animator.AnimationComplete += CompleteTask; 
						}
						break;
				}
			}
			GD.Print($"Pending tasks: {_pendingTasks}");
		}

		private void CompleteTask()
		{
			if (_pendingTasks > 0) _pendingTasks--;
			
			if (_pendingTasks == 0 && gameStatus == GameStatus.PerformingAction)
			{
				EmitSignal(SignalName.ExecuteComplete);
				gameStatus = GameStatus.WaitingForInput;
				GD.Print("All tasks completed, waiting for input.");
			}
		}

		private void CompleteTask(uint count)
		{
			if (_pendingTasks > 0) _pendingTasks -= count;

			if (_pendingTasks <= 0 && gameStatus == GameStatus.PerformingAction)
			{
				EmitSignal(SignalName.ExecuteComplete);
				gameStatus = GameStatus.WaitingForInput;
				GD.Print("All tasks completed, waiting for input.");
			}
		}

		private void InterruptCurrentDialogue()
		{
			_pendingTasks = 0;
			gameStatus = GameStatus.WaitingForInput;
			_currentDialogueLine?.Interrupt(this);
			//总是发射完成信号
			EmitSignal(SignalName.ExecuteComplete);
		}

		public void NextDialogue()
		{
			if (_currentDialogueLine == null) return;

			EmitSignal(SignalName.BeforeExecuteStart);

			InterruptCurrentDialogue();
			//switch to next dialogue line
			_currentDialogueLine = TestScenario.Get();
			//
			gameStatus = GameStatus.PerformingAction;
			_currentDialogueLine.Execute(this);
			AddPendingTask();

			EmitSignal(SignalName.AfterExecuteStart);
		}
		#endregion

		#region Autoplay
		[ExportGroup("Autoplay")]
		[Export] public bool EnableAutoplay = false;
		[Export(PropertyHint.Range, "0.5,5")] public float AutoplayDelay = 2.0f;

		bool _autoplay_ExecutionComplete = false;
		/// <summary>
		/// 表示是否所有行为已经播放完成。
		/// </summary>
		bool IsAutoplayExecutionComplete
		{
			get => _autoplay_ExecutionComplete;
			set
			{
				_autoplay_ExecutionComplete = value;
				if (EnableAutoplay && value)
				{
					StartTimerForAutoplay();
				}
			}
		}

		Timer _autoplayTimer;

		#region  Autoplay/Register Methods
		private void AutoplayRegistered_ExecuteComplete()
		{
			IsAutoplayExecutionComplete = true;
		}

		private void AutoplayRegistered_BeforeExecuteStart()
		{
			IsAutoplayExecutionComplete	 = false;
		}

		private void StartTimerForAutoplay()
		{
			/*
			//1
			GetTree().CreateTimer(AutoplayDelay).Timeout += () =>
			{
				if (gameStatus == GameStatus.WaitingForInput)
				{
					NextDialogue();
				}
			};
			*/

			_autoplayTimer?.QueueFree();
			_autoplayTimer = null;
			_autoplayTimer = new Timer
			{
				WaitTime = AutoplayDelay,
				OneShot = true,
				Name = "AutoplayTimer"
			};
			_autoplayTimer.Timeout += () =>
			{
				if (gameStatus == GameStatus.WaitingForInput)
					NextDialogue();
			};
			AddChild(_autoplayTimer);
			_autoplayTimer.Start();
		}

		#endregion

		#endregion


	}
}
