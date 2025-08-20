using System;
using Godot;

namespace VisualNovel
{
    public partial class TypeWriter : RichTextLabel
    {
        [Export] public float PerCharSpeed = 0.03f;
        public bool IsTyping { get; private set; }

        [Signal] public delegate void StartTypingEventHandler();
        [Signal] public delegate void OnCompleteEventHandler();
        [Signal] public delegate void OnInterruptEventHandler();

        private Tween _tween;

        public void TypeText(string text, bool Immediately = false)
        {
            if (Immediately)
            {
                StopTween();
                Text = text;
                VisibleRatio = 1;
                return;
            }
        
            if (string.IsNullOrEmpty(text))
            {
                VisibleRatio = 1;
                EmitCompletion();
                return;
            }

            StopTween();

            Text = text;
            VisibleRatio = 0;
            IsTyping = true;

            CreateTypeTween(text, PerCharSpeed);
        }

        public void TypeText(string text, float TempDuration, bool Immediately = false)
        {
            if (Immediately)
            {
                StopTween();
                Text = text;
                VisibleRatio = 1;
                return;
            }
        
            if (string.IsNullOrEmpty(text))
            {
                VisibleRatio = 1;
                EmitCompletion();
                return;
            }

            StopTween();

            Text = text;
            VisibleRatio = 0;
            IsTyping = true;

            CreateTypeTween(text, TempDuration);
        }

        private void CreateTypeTween(string text, float duration)
        {
            _tween = CreateTween();
            _tween.SetTrans(Tween.TransitionType.Linear);
            _tween.TweenProperty(this, "visible_ratio", 1, duration * text.Length);
            _tween.Finished += () => {
                if (IsInstanceValid(this)) OnTweenCompleted();
            };
            EmitSignal(SignalName.StartTyping);
        }

        private void OnTweenCompleted()
        {
            EmitCompletion();
        }

        public void Interrupt()
        {
            if (!IsTyping) return;

            StopTween();
            VisibleRatio = 1;
            EmitCompletion();
        }

        private void EmitCompletion()
        {
            if (IsTyping)
            {
                IsTyping = false;
                EmitSignal(SignalName.OnComplete);
            }
        }

        private void EmitInterrupt()
        {
            if (IsTyping)
            {
                IsTyping = false;
                EmitSignal(SignalName.OnInterrupt);
            }
        }

        private void StopTween()
        {
            if (_tween != null)
            {
                _tween.Kill();
                _tween = null;
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            StopTween();
        }
    }

}