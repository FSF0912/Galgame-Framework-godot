using System;
using Godot;

namespace VisualNovel
{
    /// <summary>
    /// Texture参数类，用于设置CrossFadeTextureRect的初始参数
    /// </summary>
    public class TextureParams
    {
        public Vector2 position = Vector2.Zero;
        public float rotation_degrees = 0f;
        public Vector2 scale = Vector2.One;
        public TextureRect.ExpandModeEnum expandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        public TextureRect.StretchModeEnum stretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        public Control.LayoutPreset layoutPreset = Control.LayoutPreset.TopLeft;

        public TextureParams(Vector2 position, float rotation_degrees, Vector2 scale,
            TextureRect.ExpandModeEnum expandMode, TextureRect.StretchModeEnum stretchMode,
            Control.LayoutPreset layoutPreset = Control.LayoutPreset.TopLeft)
        {
            this.position = position;
            this.rotation_degrees = rotation_degrees;
            this.scale = scale;
            this.expandMode = expandMode;
            this.stretchMode = stretchMode;
            this.layoutPreset = layoutPreset;
        }

        /// <summary>
        /// position x阈值：约0-640 y阈值：约50-1000
        /// 
        /// </summary>
        public static TextureParams DefaultPortraitNormalDistance = new(
            position: new(0, 150),
            rotation_degrees: 0f,
            scale: new Vector2(1300f, 2000f),
            expandMode: TextureRect.ExpandModeEnum.IgnoreSize,
            stretchMode: TextureRect.StretchModeEnum.KeepAspectCentered,
            layoutPreset: Control.LayoutPreset.TopLeft
        );
    }

    public partial class CrossFadeTextureRect : TextureRect
    {
        #region Public Fields
        bool _disposeTexture;
        private void DisposeTexture(Texture tex)
        {
            if (_disposeTexture) tex?.Dispose();
        }

        public CrossFadeTextureRect() {}

        public CrossFadeTextureRect(TextureParams initParams) : this(null, initParams) { }


        /// <param name="DisposeUnusedTextures">如果为真，将会在交叉淡化等过程中自动释放掉纹理资源</param>
        public CrossFadeTextureRect(Texture2D initialTexture,
        TextureParams initParams = default,
        bool DisposeUnusedTextures = true)
        {
            SetAnchorsAndOffsetsPreset(initParams.layoutPreset, LayoutPresetMode.KeepSize);
            _disposeTexture = DisposeUnusedTextures;
            Texture = initialTexture ?? GetOrCreateEmptyTexture();
            Position = initParams.position;
            RotationDegrees = initParams.rotation_degrees;
            Size = initParams.scale;
            ExpandMode = initParams.expandMode;
            StretchMode = initParams.stretchMode;
        }

        public override void _Ready()
        {
            base._Ready();
            FadeDuration = GlobalSettings.AnimationDefaultTime;
            //InitShaderMaterial();
            DialogueManager.Instance.ExecuteStart += ResetSignalEmitedSymbol;
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            DialogueManager.Instance.ExecuteStart -= ResetSignalEmitedSymbol;
            ClearActiveFadingTween();
            _nextTexture = null;

            _animationTween?.Kill();
            
            if (Material != null)
            {
                Material.Dispose();
                Material = null;
            }
        }

        /// <summary>
        /// 在删除前清理所有Tween
        /// </summary>
        private void DeleteAfterFade()
        {
            ClearActiveFadingTween();
            if (_animationTween != null && IsInstanceValid(_animationTween))
            {
                _animationTween.Kill();
                _animationTween = null;
            }
            QueueFree();
        }

        /// <summary>
        /// 清理当前活动的Tween(交叉淡化tween)
        /// </summary>
        private void ClearActiveFadingTween()
        {
            if (_fadingTween != null && IsInstanceValid(_fadingTween))
            {
                //_fadingTween.Finished -= OnFadeComplete;
                //_fadingTween.Finished -= DeleteAfterFade;
                _fadingTween?.Kill();
                _fadingTween = null;
            }
        }
        #endregion

        #region Fading
        [Signal] public delegate void FadeCompleteEventHandler();
        public static Texture2D EmptyTex;
        private ShaderMaterial _shaderMaterial;
        private Tween _fadingTween;
        private Texture2D _nextTexture;
        /// <summary>
        /// 作为即将要删除的标志，避免重复删除
        /// </summary>
        private bool _pendingDeletion;
        public float FadeDuration;

        

        /// <summary>
        /// 初始化material及shader
        /// </summary>
        private void InitShaderMaterial()
        {
            if (_shaderMaterial != null) return;

            _shaderMaterial = new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = @"
                    shader_type canvas_item;
                    uniform sampler2D current_tex;
                    uniform sampler2D next_tex;
                    uniform float progress : hint_range(0, 1);
                    
                    void fragment() {
                        vec4 curr = texture(current_tex, UV);
                        vec4 next = texture(next_tex, UV);
                        COLOR = mix(curr, next, progress);
                    }
                "
                }
            };

            var oldMaterial = Material;
            Material = _shaderMaterial;
            oldMaterial?.Dispose();
            ResetShaderParams();
        }

    /// <summary>
    /// 淡化过渡到目标纹理
    /// </summary>
    /// <param name="newTexture"></param>
    /// <param name="duration"></param>
    /// <param name="immediate"></param>
        public void SetTextureWithFade(Texture2D newTexture, float duration = -1, bool immediate = false)
        {
            if (immediate)
            {
                SetTextureImmediately(newTexture);
                return;
            }

            if (newTexture == null || Texture == newTexture || _pendingDeletion)
                return;

            ClearActiveFadingTween();
            InitShaderMaterial();

            _shaderMaterial.SetShaderParameter("current_tex", Texture ?? GetOrCreateEmptyTexture());
            _shaderMaterial.SetShaderParameter("next_tex", newTexture);

            _nextTexture = newTexture;

            _fadingTween = CreateTween();
            _fadingTween.SetEase(Tween.EaseType.Out);
            _fadingTween.SetTrans(Tween.TransitionType.Linear);
            _fadingTween.TweenMethod(Callable.From<float>(SetProgress), 0.0f, 1.0f, duration > 0 ? duration : FadeDuration);
            _fadingTween.Finished += OnFadeComplete;
        }

        /// <summary>
        /// 将原纹理过渡到空白纹理后再过渡到目标纹理
        /// </summary>
        /// <param name="newTexture"></param>
        /// <param name="duration"></param>
        /// <param name="doubleTime">是否将过渡时间加倍,如为真,则将总体淡化过程设置为目标时间的两倍</param>
        /// <param name="OnCompleteHalf">半程完成时的回调</param>
        /// <param name="immediate"></param>
        public void SetTextureByFading(Texture2D newTexture, float duration = -1, bool doubleTime = true, Action OnCompleteHalf = null)
        {
            if (newTexture == null || Texture == newTexture || _pendingDeletion)
                return;

            duration = duration <= 0 ? doubleTime ? FadeDuration : FadeDuration / 2 : doubleTime ? duration : duration / 2;

            ClearActiveFadingTween();
            InitShaderMaterial();

            _shaderMaterial.SetShaderParameter("current_tex", Texture ?? GetOrCreateEmptyTexture());
            _shaderMaterial.SetShaderParameter("next_tex", EmptyTex);

            _nextTexture = EmptyTex;

            _fadingTween = CreateTween();
            _fadingTween.SetEase(Tween.EaseType.Out);
            _fadingTween.SetTrans(Tween.TransitionType.Linear);
            _fadingTween.TweenMethod(Callable.From<float>(SetProgress), 0.0f, 1.0f, duration > 0 ? duration : FadeDuration);

            GetTree().CreateTimer(duration).Timeout += () =>
            {
                if (!IsInstanceValid(this))
                    return;

                _fadingTween?.Kill();
                _fadingTween = null;
                OnCompleteHalf?.Invoke();
                SetTextureWithFade(newTexture, duration);
            };
        }

        /// <summary>
        /// 设置纹理为空白纹理
        /// </summary>
        /// <param name="deleteAfterFade">是否在淡化完成后删除当前节点</param>
        /// <param name="immediate"></param>
        public void ClearTexture(bool deleteAfterFade = false, bool immediate = false)
        {
            SetTextureWithFade(GetOrCreateEmptyTexture(), immediate: immediate);

            if (deleteAfterFade)
            {
                if (immediate)
                {
                    QueueFree();
                }
                else
                {
                    _pendingDeletion = true;
                    _fadingTween.Finished -= OnFadeComplete;
                    _fadingTween.Finished += DeleteAfterFade;
                }
            }
        }

        /// <summary>
        /// 立即设置纹理为新纹理，不进行淡化过渡
        /// </summary>
        /// <param name="newTexture"></param>
        public void SetTextureImmediately(Texture2D newTexture)
        {
            if (newTexture == null || Texture == newTexture) return;

            ClearActiveFadingTween();
            InitShaderMaterial();

            Texture = newTexture;
            ResetShaderParams();
            _nextTexture = null;
        }

        /// <summary>
        /// 立即完成当前淡化过渡
        /// </summary>
        public void CompleteFade()
        {
            if (_fadingTween != null && IsInstanceValid(_fadingTween)) return;

            SetProgress(1.0f);
            OnFadeComplete();
        }

        /// <summary>
        /// 运行时调整，将不会应用transform参数
        /// </summary>
        /// <param name="initParams"></param>
        public void SetTexParams(TextureParams initParams)
        {
            ExpandMode = initParams.expandMode;
            StretchMode = initParams.stretchMode;
            SetAnchorsAndOffsetsPreset(initParams.layoutPreset, LayoutPresetMode.KeepSize);
        }

        /// <summary>
        /// 设置Material shader的progress参数
        /// </summary>
        /// <param name="value"></param>
        private void SetProgress(float value) =>
            _shaderMaterial?.SetShaderParameter("progress", value);

        /// <summary>
        /// 在淡化完成后的收尾工作
        /// 清除tween，设置当前纹理为下一个纹理，并重置shader参数
        /// </summary>
        private void OnFadeComplete()
        {
            if (_nextTexture == null) return;

            Texture = _nextTexture;
            ResetShaderParams();
            ClearActiveFadingTween();
            EmitSignal(SignalName.FadeComplete);
        }

        /// <summary>
        /// 重置shader参数到默认状态
        /// </summary>
        private void ResetShaderParams()
        {
            if (_shaderMaterial == null) return;

            _shaderMaterial.SetShaderParameter("current_tex", Texture ?? GetOrCreateEmptyTexture());
            _shaderMaterial.SetShaderParameter("next_tex", GetOrCreateEmptyTexture());
            _shaderMaterial.SetShaderParameter("progress", 0.0f);
        }

        /// <summary>
        /// 获取或创建一个空白纹理
        /// 该纹理为1x1像素的透明纹理，用于淡化过渡时的占位符
        /// </summary>
        /// <returns></returns>
        private static Texture2D GetOrCreateEmptyTexture()
        {
            if (EmptyTex != null && IsInstanceValid(EmptyTex))
                return EmptyTex;

            var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);
            EmptyTex = ImageTexture.CreateFromImage(image);
            return EmptyTex;
        }

        #endregion

        #region Animation
        [Signal] public delegate void AnimationCompleteEventHandler();
        Tween _animationTween;

        /// <summary>
        /// 只用于抖动动画的纹理初始位置标记,在抖动动画完成后恢复到原始位置
        /// </summary>
        Vector2 _originalPosition;

        /// <summary>
        /// 标志是否立即发出动画完成信号
        /// 用于避免在立即执行动画时重复发出信号
        /// </summary>
        bool _signalEmitted_ImmadiatelySymbol = false;

        /// <summary>
        /// 用于设置_signalEmitted_ImmadiatelySymbol为false的回调方法
        /// </summary>
        private void ResetSignalEmitedSymbol() => _signalEmitted_ImmadiatelySymbol = false;

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
                EmitSignal(SignalName.AnimationComplete);
            };
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
                _animationTween.TweenProperty(this, "position", Position + target, duration).SetTrans(trans).SetEase(ease);
            }
            else
            {
                _animationTween.TweenProperty(this, "position", target, duration).SetTrans(trans).SetEase(ease);
            }
        }
        
        public void AddMoveImmediately(Vector2 target, bool isRelative = false)
        {
            if (isRelative)
                Position += target;
            else
                Position = target;

            if (_signalEmitted_ImmadiatelySymbol) return;
            _signalEmitted_ImmadiatelySymbol = true;
            EmitSignal(SignalName.AnimationComplete);
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
                _animationTween.TweenProperty(this, "rotation_degrees", degrees, duration).SetTrans(trans).SetEase(ease);
            }
            else
            {
                // 通过中间节点实现世界空间旋转
                var pivot = new Node2D();
                pivot.Position = GlobalPosition;
                GetParent().AddChild(pivot);
                Reparent(pivot);

                _animationTween.TweenProperty(pivot, "rotation_degrees", degrees, duration)
                    .SetTrans(trans).SetEase(ease)
                .Finished += () =>
                {
                    GlobalPosition = pivot.GlobalPosition;
                    Reparent(pivot.GetParent());
                    pivot.QueueFree();
                };
            }
        }

        public void AddRotateImmediately(float degrees, bool isLocal = true)
        {
            if (isLocal)
            {
                RotationDegrees = degrees;
            }
            else
            {
                var pivot = new Node2D();
                pivot.Position = GlobalPosition;
                GetParent().AddChild(pivot);
                Reparent(pivot);
                pivot.RotationDegrees = degrees;
                GlobalPosition = pivot.GlobalPosition;
                Reparent(pivot.GetParent());
                pivot.QueueFree();
            }

            if (_signalEmitted_ImmadiatelySymbol) return;
            _signalEmitted_ImmadiatelySymbol = true;
            EmitSignal(SignalName.AnimationComplete);
        }

        public void AddScale(Vector2 scale, float duration,
                   Tween.TransitionType trans = Tween.TransitionType.Sine,
                   Tween.EaseType ease = Tween.EaseType.InOut)
        {
            if (_animationTween == null || !IsInstanceValid(_animationTween))
            {
                CreateAnimTween();
            }

            _animationTween.TweenProperty(this, "scale", scale, duration)
                .SetTrans(trans).SetEase(ease);
        }

        public void AddScaleImmediately(Vector2 scale)
        {
            Scale = scale;

            if (_signalEmitted_ImmadiatelySymbol) return;
            _signalEmitted_ImmadiatelySymbol = true;
            EmitSignal(SignalName.AnimationComplete);
        }

        public void AddShake(float intensity, float duration, float frequency = 10f,
                    Tween.TransitionType trans = Tween.TransitionType.Elastic,
                    Tween.EaseType ease = Tween.EaseType.Out)
        {
            if (_animationTween == null || !IsInstanceValid(_animationTween))
            {
                CreateAnimTween();
            }

            _originalPosition = Position;
            float elapsed = 0f;

            _animationTween.TweenMethod(Callable.From<float>((t) =>
            {
                elapsed += (float)GetProcessDeltaTime();
                float offset = Mathf.Sin(elapsed * frequency * Mathf.Pi) * intensity;
                Position = _originalPosition + new Vector2(offset, offset * 0.6f);
                intensity *= 0.95f; // 随时间减弱
            }), 0f, 1f, duration).
            SetTrans(trans).
            SetEase(ease).
            Finished += () =>
            {
                Position = _originalPosition; // 恢复原位置
            };
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

            _animationTween.TweenProperty(this, "modulate", target, duration)
                .SetTrans(trans).SetEase(ease);
        }

        public void AddColorImmediately(Color target)
        {
            Modulate = target;

            if (_signalEmitted_ImmadiatelySymbol) return;
            _signalEmitted_ImmadiatelySymbol = true;
            EmitSignal(SignalName.AnimationComplete);
        }

        public void AddFade(float alpha, float duration,
                  Tween.TransitionType trans = Tween.TransitionType.Linear,
                  Tween.EaseType ease = Tween.EaseType.In)
        {
            if (_animationTween == null || !IsInstanceValid(_animationTween))
            {
                CreateAnimTween();
            }

            var targetColor = Modulate;
            targetColor.A = alpha;

            _animationTween.TweenProperty(this, "modulate", targetColor, duration)
                .SetTrans(trans).SetEase(ease);
        }

        public void AddFadeImmediately(float alpha)
        {
            var targetColor = Modulate;
            targetColor.A = alpha;
            Modulate = targetColor;

            if (_signalEmitted_ImmadiatelySymbol) return;
            _signalEmitted_ImmadiatelySymbol = true;
            EmitSignal(SignalName.AnimationComplete);
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
        
        #endregion
    }
}