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

	[GlobalClass]
	public partial class DialogueManager : Control
	{
		public static DialogueManager Instance;


		[Signal] public delegate void AfterExecuteStartEventHandler();
		[Signal] public delegate void BeforeExecuteStartEventHandler();
		[Signal] public delegate void ExecuteCompleteEventHandler();


		public GameStatus gameStatus = GameStatus.WaitingForInput;
		[ExportGroup("References")]
		[Export] public Label SpeakerNameLabel;
		[Export] public TypeWriter typeWriter;
		[Export] public VNTextureRect BackGroundTexture;
		[Export] public VNTextureRect AvatarTexture;
		[Export] public Control TextureContainer;
		[Export] public Control BranchContainer;
		[Export] public PackedScene BranchButtonScene;

		[ExportGroup("Settings")]
		[Export] public bool AllowInput = true;

		DialogueLine _currentDialogueLine;
		public readonly Dictionary<int, VNTextureRect> SceneActiveTextures = [];

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

			SceneActiveTextures.Clear();

			SceneActiveTextures.Add(-100, BackGroundTexture);
			SceneActiveTextures.Add(-200, AvatarTexture);

			BeforeExecuteStart += AutoplayRegistered_BeforeExecuteStart;
			ExecuteComplete += AutoplayRegistered_ExecuteComplete;

			//_currentDialogueLine = TestScenario.Get();
			//_currentDialogueLine.Execute(this);
		}


		public override void _ExitTree()
		{
			Instance = null;
			base._ExitTree();
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


		public VNTextureRect CreateTextureRect(int id, float duration, TextureParams textureParams = null,
		string defaultTexPath = null,
		VNTextureRect.TranslationType translationType = VNTextureRect.TranslationType.CrossFade)
		{
			if (SceneActiveTextures.TryGetValue(id, out VNTextureRect value)) return value;

			var texture = new VNTextureRect(textureParams, defaultTexPath, translationType, id)
			{ Name = $"TextureRect_{id}" };

			TextureContainer.AddChild(texture);
			SceneActiveTextures.Add(id, texture);

			return texture;
		}

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
			IsAutoplayExecutionComplete = false;
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
