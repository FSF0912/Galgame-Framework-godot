using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using System.Linq;
using System.Threading;

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
		[Export] private VNTextureController BackGroundTexture;
		[Export] private VNTextureController AvatarTexture;
		[Export] private Control TextureContainer;
		[Export] private Control BranchContainer;
		[ExportGroup("Scenes")]
		[Export] private PackedScene VNTextureControllerScene;
		[Export] private PackedScene BranchButtonScene;

		[ExportGroup("Settings")]
		[Export] private bool AllowInput = true;

		DialogueLine _currentDialogueLine;
		public readonly Dictionary<int, VNTextureController> SceneActiveTextures = [];

		//简并变量
		private AudioManager _am;

		private CancellationTokenSource _cts;

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

			_am = new AudioManager();
			AddChild(_am);

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


		public VNTextureController CreateTextureRect(int id, float duration = 0f, 
		Vector2 position = default, float rotation_degrees = 0f, Vector2 scale = default,
		string defaultTexPath = null,
		VNTextureController.TranslationType translationType = VNTextureController.TranslationType.CrossFade)
		{
			if (SceneActiveTextures.TryGetValue(id, out VNTextureController value)) return value;

			duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;

			var texture = VNTextureControllerScene.Instantiate<VNTextureController>();
			TextureContainer.AddChild(texture);
			SceneActiveTextures.Add(id, texture);

			texture.Position = position;
			texture.RotationDegrees = rotation_degrees;
			texture.Size = scale;
			texture.SetTextureOrdered(defaultTexPath, translationType, duration);
			return texture;
		}

		#region State Management


		private async Task RunDialogueLineAsync(CancellationToken cancellationToken)
		{
			try
			{
				await Task.WhenAll(_currentDialogueLine.Execute()
				.Where(s => IsInstanceValid(s.Item1) && s.Item2 != null && s.Item1.HasSignal(s.Item2) && s.Item2 != string.Empty)
				.Select(async s => await ToSignal(s.Item1, s.Item2)));

				EmitSignal(SignalName.ExecuteComplete);
			}
			catch (OperationCanceledException)
			{
				
			}
            finally
            {
                gameStatus = GameStatus.WaitingForInput;
            }
		}

		private void InterruptCurrentDialogue()
		{
			if (_currentDialogueLine == null) return;

			_cts?.Cancel();
			_currentDialogueLine.Interrupt();
		}

		public void NextDialogue()
		{
			if (_currentDialogueLine == null) return;

			EmitSignal(SignalName.BeforeExecuteStart);

			_cts?.Cancel();
			_cts = new CancellationTokenSource();
			//
			_currentDialogueLine = TestScenario.Get();
			//
			gameStatus = GameStatus.PerformingAction;
			_ = RunDialogueLineAsync(_cts.Token);
			EmitSignal(SignalName.AfterExecuteStart);
		}
		#endregion
	}
}
