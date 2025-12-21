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

		//简并变量
		private AudioManager _am;

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

			_am = new AudioManager();
			AddChild(_am);
			
			_autoplayTimer = new Timer
			{
				OneShot = true,
				Name = "AutoplayTimer"
			};
			_autoplayTimer.Timeout += () =>
			{
				if (gameStatus == GameStatus.WaitingForInput)
					NextDialogue();
			};
			AddChild(_autoplayTimer);

			gameStatus = GameStatus.WaitingForInput;
			_currentDialogueLine = TestScenario.Get();
			NextDialogue();
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
							_am.VoiceComplete -= CompleteTask;
							_am.VoiceComplete += CompleteTask;
						}
						break;

					case TextureLine textureLine:
						if (SceneActiveTextures.TryGetValue(textureLine.ID, out var textureRef))
						{
							_pendingTasks++;
							textureRef.AnimationComplete -= CompleteTask;
							textureRef.AnimationComplete += CompleteTask;
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
			_currentDialogueLine?.Interrupt();
			CompleteTask();
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
			_currentDialogueLine.Execute();
			AddPendingTask();

			EmitSignal(SignalName.AfterExecuteStart);
		}
		#endregion

		#region Autoplay
		[ExportGroup("Autoplay")]
		[Export] public bool EnableAutoplay = false;
		[Export(PropertyHint.Range, "0.5,5")] public float AutoplayDelay = 2.0f;

		Timer _autoplayTimer;

		#region  Autoplay/Register Methods
		private void AutoplayRegistered_ExecuteComplete()
		{
			if (EnableAutoplay)
			{
				StartTimerForAutoplay();
			}
		}

		private void AutoplayRegistered_BeforeExecuteStart()
		{
			_autoplayTimer.Stop();
		}

		private void StartTimerForAutoplay()
		{
			_autoplayTimer.WaitTime = AutoplayDelay;
			_autoplayTimer.Start();
		}

		#endregion

		#endregion


	}
}
