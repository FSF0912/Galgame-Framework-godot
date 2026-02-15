/*
参考TextureAnimator打的注释。
本类较为独立。
*/
using Godot;

namespace VisualNovel
{
    /// <summary>
    /// 动画控制器类，作为CrossFadeTextureRect的子节点
    /// </summary>
    [GlobalClass]
    public partial class TextureAnimator : Node, IDialogueProcessable
    {
        /// <summary>
        /// 动画完成信号
        /// </summary>
        /// <param name="count"></param>
        [Signal] public delegate void AnimationCompleteEventHandler();

        public StringName CompletionSignal => SignalName.AnimationComplete;

        /// <summary>
        /// 目标TextureRect
        /// </summary>
        private VNTextureController _target;

        /// <summary>
        /// 动画Tween
        /// </summary>
        public Tween animTween { get; private set; }
        /// <summary>
        /// 是否有立即生效的动画
        /// </summary>
        bool _hasImmediate = false;

        // Shake specific fields
        private Vector2 _shakeOriginal = Vector2.Zero;
        private float _shakeElapsed = 0f;
        private float _currentShakeIntensity = 0f;

        public TextureAnimator(VNTextureController target)
        {
            _target = target;
        }

        public override void _Ready()
        {
            base._Ready();
            DialogueManager.Instance.BeforeExecuteStart += ResetState;
            DialogueManager.Instance.AfterExecuteStart += RunAnimations;
        }

        public override void _ExitTree()
        {
            DialogueManager.Instance.BeforeExecuteStart -= ResetState;
            DialogueManager.Instance.AfterExecuteStart -= RunAnimations;
            base._ExitTree();
        }


        #region Animation/Transform
        public void AddMove(Vector2 value, float duration = -1, bool parallel = true,
                    Tween.TransitionType trans = Tween.TransitionType.Sine,
                    Tween.EaseType ease = Tween.EaseType.InOut)
        {
            duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
            EnsureTween();
            if (!parallel) animTween.SetParallel(false);
            animTween.TweenProperty(_target, "position", value, duration).SetTrans(trans).SetEase(ease);
            if (!parallel) animTween.SetParallel();
        }
        
        public void AddMoveImmediately(Vector2 value)
        {
            _target.Position = value;
            _hasImmediate = true;
        }

        public void AddRotate(float degrees, float duration, bool parallel = true,
                     Tween.TransitionType trans = Tween.TransitionType.Quart,
                     Tween.EaseType ease = Tween.EaseType.Out)
        {
            duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
            EnsureTween();
            if (!parallel) animTween.SetParallel(false);
            animTween.TweenProperty(_target,
             "rotation_degrees", degrees, duration).SetTrans(trans).SetEase(ease);
            if (!parallel) animTween.SetParallel();
        }

        public void AddRotateImmediately(float degrees, bool isLocal = true)
        {
            _target.RotationDegrees = degrees;
            _hasImmediate = true;
        }

        public void AddScale(Vector2 scale, float duration, bool parallel = true,
                   Tween.TransitionType trans = Tween.TransitionType.Sine,
                   Tween.EaseType ease = Tween.EaseType.InOut)
        {
            duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
            EnsureTween();
            if (!parallel) animTween.SetParallel(false);
            animTween.TweenProperty(_target, "scale", scale, duration).SetTrans(trans).SetEase(ease);
            if (!parallel) animTween.SetParallel();
        }

        public void AddScaleImmediately(Vector2 scale)
        {
            _target.Scale = scale;
            _hasImmediate = true; 
        }

        public void AddShake(float intensity, float duration, float frequency = 10f, bool parallel = true,
                    Tween.TransitionType trans = Tween.TransitionType.Elastic,
                    Tween.EaseType ease = Tween.EaseType.Out)
        {
            duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
            EnsureTween();
            
            _shakeOriginal = _target.Position;
            _shakeElapsed = 0f;
            _currentShakeIntensity = intensity;

            if (!parallel) animTween.SetParallel(false);
            animTween!.TweenMethod(Callable.From<float>((t) =>
            {
                _shakeElapsed += (float)GetProcessDeltaTime();
                float offset = Mathf.Sin(_shakeElapsed * frequency * Mathf.Pi) * _currentShakeIntensity;
                _target.Position = _shakeOriginal + new Vector2(offset, offset * 0.6f);
                _currentShakeIntensity *= 0.95f; // 随时间减弱

                if (t >= 0.999f)
                {
                    _target.Position = _shakeOriginal; // 恢复原位置
                }
            }), 0f, 1f, duration).
            SetTrans(trans).
            SetEase(ease);
            if (!parallel) animTween.SetParallel();
        }

        #endregion

        #region Animation/Color
        public void AddColorTint(Color target, float duration, bool parallel = true,
                         Tween.TransitionType trans = Tween.TransitionType.Linear,
                         Tween.EaseType ease = Tween.EaseType.In)
        {
            duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
            EnsureTween();

            if (!parallel) animTween.SetParallel(parallel);
            animTween.TweenProperty(_target, "modulate", target, duration)
                .SetTrans(trans).SetEase(ease);

            if (!parallel) animTween.SetParallel();
        }

        public void AddColorTintImmediately(Color target)
        {
            _target.Modulate = target;
            _hasImmediate = true;
        }

        public void AddFade(float alpha, float duration, bool parallel = true,
                  Tween.TransitionType trans = Tween.TransitionType.Linear,
                  Tween.EaseType ease = Tween.EaseType.In)
        {
            duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
            EnsureTween();

            if (!parallel) animTween.SetParallel(parallel);
            animTween.TweenProperty(_target, "modulate:a", alpha, duration)
                .SetTrans(trans).SetEase(ease);

            if (!parallel) animTween.SetParallel();
        }

        public void AddFadeImmediately(float alpha)
        {
            _target.Set("modulate:a", alpha);
            _hasImmediate = true;
        }

        #endregion

        /// <summary>
        /// 确保 Tween 存在
        /// </summary>
        private void EnsureTween()
        {
            if (animTween == null || !IsInstanceValid(animTween))
            {
                animTween = CreateTween();
                animTween.SetParallel();
                animTween.Finished += OnTweenFinished;
            }
        }

        private void OnTweenFinished()
        {
            EmitSignal(SignalName.AnimationComplete);
            animTween = null;
        }

        /// <summary>
        /// 立即完成所有动画
        /// </summary>
        public void CompleteAnimations()
        {
            if (animTween != null && animTween.IsValid())
            {
                animTween.CustomStep(1000f);
                //anim tween在custom step后，直接被杀掉了。不用额外kill
                //也意味着需要自定义finished信号，不能简单地扔到tween的finished事件里去。
                //GD.Print(animTween == null);
                //animTween.Kill();
            }
            EmitSignal(SignalName.AnimationComplete);
            animTween = null;
        }

        public void RunAnimations()
        {
            if (animTween == null)
            {
                if (_hasImmediate)
                {
                    EmitSignal(SignalName.AnimationComplete);
                }
                return;
            }

            animTween.Play();
        }

        public void ResetState()
        {
            if (animTween != null && animTween.IsValid())
            {
                animTween.Kill();
            }
            animTween = null;
            _hasImmediate = false;
            _shakeElapsed = 0f;
            _currentShakeIntensity = 0f;
        }
    }
}