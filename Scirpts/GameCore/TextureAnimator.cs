using Godot;

namespace VisualNovel
{
    /// <summary>
    /// 动画控制器类，作为CrossFadeTextureRect的子节点
    /// </summary>
    public partial class TextureAnimator : Node
    {
        [Signal] public delegate void AnimationCompleteEventHandler(uint count);
        
        private TextureRect _target;
        private Tween _animationTween;
        
        /// <summary>
        /// 只用于抖动动画的纹理初始位置标记,在抖动动画完成后恢复到原始位置
        /// </summary>
        private Vector2 _originalPosition;
        
        /// <summary>
        /// 标志是否立即发出动画完成信号
        /// 用于避免在立即执行动画时重复发出信号
        /// </summary>
        private bool _signalEmitted_ImmadiatelySymbol = false;
        private uint _animationCount = 0;

        public TextureAnimator(TextureRect target)
        {
            _target = target;
        }

        public void ResetSignalEmitedSymbol()
        {
            _signalEmitted_ImmadiatelySymbol = false;
        }

        /// <summary>
        /// 创建一个新的动画Tween(动画为并行执行)
        /// </summary>
        public void CreateAnimTween()
        {
            if (_animationTween != null && IsInstanceValid(_animationTween))
            {
                _animationTween.Kill();
            }

            _animationTween = CreateTween();
            _animationTween.SetParallel();
            _animationTween.Finished += () =>
            {
                _animationTween = null;
                EmitSignal(SignalName.AnimationComplete, _animationCount);
            };
            _animationCount = 0;
        }

        #region Animation/Transform
        public void AddMove(Vector2 target, float duration, bool isRelative = false,
                    Tween.TransitionType trans = Tween.TransitionType.Sine,
                    Tween.EaseType ease = Tween.EaseType.InOut)
        {
            if (_animationTween == null || !IsInstanceValid(_animationTween))
            {
                CreateAnimTween();
            }

            if (isRelative)
            {
                _animationTween.TweenProperty(_target, "position", _target.Position + target, duration).SetTrans(trans).SetEase(ease);
            }
            else
            {
                _animationTween.TweenProperty(_target, "position", target, duration).SetTrans(trans).SetEase(ease);
            }

            _animationCount++;
        }
        
        public void AddMoveImmediately(Vector2 target, bool isRelative = false)
        {
            if (isRelative)
                _target.Position += target;
            else
                _target.Position = target;

            if (_signalEmitted_ImmadiatelySymbol) return;
            _signalEmitted_ImmadiatelySymbol = true;
        }

        public void AddRotate(float degrees, float duration, bool isLocal = true,
                     Tween.TransitionType trans = Tween.TransitionType.Quart,
                     Tween.EaseType ease = Tween.EaseType.Out)
        {
            if (_animationTween == null || !IsInstanceValid(_animationTween))
            {
                CreateAnimTween();
            }

            if (isLocal)
            {
                _animationTween.TweenProperty(_target, "rotation_degrees", degrees, duration).SetTrans(trans).SetEase(ease);
            }
            else
            {
                // 通过中间节点实现世界空间旋转
                var pivot = new Node2D();
                pivot.Position = _target.GlobalPosition;
                _target.GetParent().AddChild(pivot);
                _target.Reparent(pivot);

                _animationTween.TweenProperty(pivot, "rotation_degrees", degrees, duration)
                    .SetTrans(trans).SetEase(ease)
                .Finished += () =>
                {
                    _target.GlobalPosition = pivot.GlobalPosition;
                    _target.Reparent(pivot.GetParent());
                    pivot.QueueFree();
                };
            }

            _animationCount++;
        }

        public void AddRotateImmediately(float degrees, bool isLocal = true)
        {
            if (isLocal)
            {
                _target.RotationDegrees = degrees;
            }
            else
            {
                var pivot = new Node2D();
                pivot.Position = _target.GlobalPosition;
                _target.GetParent().AddChild(pivot);
                _target.Reparent(pivot);
                pivot.RotationDegrees = degrees;
                _target.GlobalPosition = pivot.GlobalPosition;
                _target.Reparent(pivot.GetParent());
                pivot.QueueFree();
            }

            if (_signalEmitted_ImmadiatelySymbol) return;
            _signalEmitted_ImmadiatelySymbol = true;
        }

        public void AddScale(Vector2 scale, float duration,
                   Tween.TransitionType trans = Tween.TransitionType.Sine,
                   Tween.EaseType ease = Tween.EaseType.InOut)
        {
            if (_animationTween == null || !IsInstanceValid(_animationTween))
            {
                CreateAnimTween();
            }

            _animationTween.TweenProperty(_target, "scale", scale, duration)
                .SetTrans(trans).SetEase(ease);

            _animationCount++;
        }

        public void AddScaleImmediately(Vector2 scale)
        {
            _target.Scale = scale;

            if (_signalEmitted_ImmadiatelySymbol) return;
            _signalEmitted_ImmadiatelySymbol = true;
        }

        public void AddShake(float intensity, float duration, float frequency = 10f,
                    Tween.TransitionType trans = Tween.TransitionType.Elastic,
                    Tween.EaseType ease = Tween.EaseType.Out)
        {
            if (_animationTween == null || !IsInstanceValid(_animationTween))
            {
                CreateAnimTween();
            }

            _originalPosition = _target.Position;
            float elapsed = 0f;

            _animationTween.TweenMethod(Callable.From<float>((t) =>
            {
                elapsed += (float)GetProcessDeltaTime();
                float offset = Mathf.Sin(elapsed * frequency * Mathf.Pi) * intensity;
                _target.Position = _originalPosition + new Vector2(offset, offset * 0.6f);
                intensity *= 0.95f; // 随时间减弱
            }), 0f, 1f, duration).
            SetTrans(trans).
            SetEase(ease).
            Finished += () =>
            {
                _target.Position = _originalPosition; // 恢复原位置
            };

            _animationCount++;
        }

        #endregion

        #region Animation/Color
        public void AddColorTint(Color target, float duration,
                         Tween.TransitionType trans = Tween.TransitionType.Linear,
                         Tween.EaseType ease = Tween.EaseType.In)
        {
            if (_animationTween == null || !IsInstanceValid(_animationTween))
            {
                CreateAnimTween();
            }

            _animationTween.TweenProperty(_target, "modulate", target, duration)
                .SetTrans(trans).SetEase(ease);

            _animationCount++;
        }

        public void AddColorTintImmediately(Color target)
        {
            _target.Modulate = target;

            if (_signalEmitted_ImmadiatelySymbol) return;
            _signalEmitted_ImmadiatelySymbol = true;
        }

        public void AddFade(float alpha, float duration,
                  Tween.TransitionType trans = Tween.TransitionType.Linear,
                  Tween.EaseType ease = Tween.EaseType.In)
        {
            if (_animationTween == null || !IsInstanceValid(_animationTween))
            {
                CreateAnimTween();
            }

            var targetColor = _target.Modulate;
            targetColor.A = alpha;

            _animationTween.TweenProperty(_target, "modulate", targetColor, duration)
                .SetTrans(trans).SetEase(ease);

            _animationCount++;
        }

        public void AddFadeImmediately(float alpha)
        {
            var targetColor = _target.Modulate;
            targetColor.A = alpha;
            _target.Modulate = targetColor;

            if (_signalEmitted_ImmadiatelySymbol) return;
            _signalEmitted_ImmadiatelySymbol = true;
        }

        #endregion

        /// <summary>
        /// 立即完成所有动画
        /// </summary>
        public void CompleteAnimations()
        {
            var targetTween = _animationTween;

            if (targetTween != null && IsInstanceValid(targetTween))
            {
                _animationTween = null;
                targetTween.CustomStep(1000f);
                targetTween.EmitSignal(Tween.SignalName.Finished);
                targetTween.Kill();
            }
        }
    }
}