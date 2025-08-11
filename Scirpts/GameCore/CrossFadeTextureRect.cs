using Godot;

namespace VisualNovel
{
    public class TextureInitParams
    {
        public Vector2 position = Vector2.Zero;
        public float rotation_degrees = 0f;
        public Vector2 scale = Vector2.One;
        public TextureRect.ExpandModeEnum expandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        public TextureRect.StretchModeEnum stretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        public Control.LayoutPreset layoutPreset = Control.LayoutPreset.TopLeft;

        public TextureInitParams(Vector2 position, float rotation_degrees, Vector2 scale,
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
        public static TextureInitParams DefaultPortraitNormalDistance = new (
            position: new (0, 150),
            rotation_degrees: 0f,
            scale: new Vector2(1300f, 2000f),
            expandMode: TextureRect.ExpandModeEnum.IgnoreSize,
            stretchMode: TextureRect.StretchModeEnum.KeepAspectCentered,
            layoutPreset: Control.LayoutPreset.TopLeft
        );
    }

    public partial class CrossFadeTextureRect : TextureRect
    {
        public enum AnchorMode : byte
        {
            Local,
            Global
        }

        [Signal] public delegate void FadeCompleteEventHandler();
        public static Texture2D EmptyTex;
        private ShaderMaterial _shaderMaterial;
        private Tween _activeTween;
        private Texture2D _nextTexture;
        private bool _pendingDeletion;

        public float FadeDuration;

        public CrossFadeTextureRect(TextureInitParams initParams) : this(null, initParams) { }

        public CrossFadeTextureRect(Texture2D initialTexture, TextureInitParams initParams = default)
        {
            Texture = initialTexture ?? GetOrCreateEmptyTexture();
            Position = initParams.position;
            RotationDegrees = initParams.rotation_degrees;
            Scale = initParams.scale;
            ExpandMode = initParams.expandMode;
            StretchMode = initParams.stretchMode;
            SetAnchorsAndOffsetsPreset(initParams.layoutPreset, LayoutPresetMode.KeepSize);
        }

        public override void _Ready()
        {
            base._Ready();
            FadeDuration = GlobalSettings.AnimationDefaultTime;
            InitShaderMaterial();
            DialogueManager.Instance.ExecuteStart += CreateAnimTween;
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            DialogueManager.Instance.ExecuteStart -= CreateAnimTween;
            ClearActiveTween();
            _nextTexture = null;

            _animationTween?.Kill();
        }

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

            Material = _shaderMaterial;
            ResetShaderParams();
        }

        public void SetTextureWithFade(Texture2D newTexture, float duration = -1, bool immediate = false)
        {
            if (immediate)
            {
                SetTextureImmediately(newTexture);
                return;
            }

            if (newTexture == null || Texture == newTexture || _pendingDeletion)
                return;

            ClearActiveTween();
            InitShaderMaterial();

            _shaderMaterial.SetShaderParameter("current_tex", Texture ?? GetOrCreateEmptyTexture());
            _shaderMaterial.SetShaderParameter("next_tex", newTexture);

            _nextTexture = newTexture;

            _activeTween = CreateTween();
            _activeTween.SetEase(Tween.EaseType.Out);
            _activeTween.SetTrans(Tween.TransitionType.Linear);
            _activeTween.TweenMethod(Callable.From<float>(SetProgress), 0.0f, 1.0f, duration > 0 ? duration : FadeDuration);
            _activeTween.Finished += OnFadeComplete;
        }

        public void SetTextureByFading(Texture2D newTexture, float duration = -1, bool doubleTime = true)
        {
            if (newTexture == null || Texture == newTexture || _pendingDeletion)
                return;

            duration = duration <= 0 ? doubleTime ? FadeDuration : FadeDuration / 2 : doubleTime ? duration : duration / 2;

            ClearActiveTween();
            InitShaderMaterial();

            _shaderMaterial.SetShaderParameter("current_tex", Texture ?? GetOrCreateEmptyTexture());
            _shaderMaterial.SetShaderParameter("next_tex", EmptyTex);

            _nextTexture = EmptyTex;

            _activeTween = CreateTween();
            _activeTween.SetEase(Tween.EaseType.Out);
            _activeTween.SetTrans(Tween.TransitionType.Linear);
            _activeTween.TweenMethod(Callable.From<float>(SetProgress), 0.0f, 1.0f, duration > 0 ? duration : FadeDuration);

            GetTree().CreateTimer(duration).Timeout += () =>
            {
                _activeTween?.Kill();
                _activeTween = null;
                SetTextureWithFade(newTexture, duration);
            };
        }

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
                    _activeTween.Finished -= OnFadeComplete;
                    _activeTween.Finished += DeleteAfterFade;
                }
            }
        }

        public void SetTextureImmediately(Texture2D newTexture)
        {
            if (newTexture == null || Texture == newTexture) return;

            ClearActiveTween();
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
            if (!IsTweenActive()) return;

            SetProgress(1.0f);
            OnFadeComplete();
        }

        private void SetProgress(float value) =>
            _shaderMaterial?.SetShaderParameter("progress", value);

        private void OnFadeComplete()
        {
            if (_nextTexture == null) return;

            Texture = _nextTexture;
            ResetShaderParams();
            ClearActiveTween();
            EmitSignal(SignalName.FadeComplete);
        }

        private void DeleteAfterFade()
        {
            ClearActiveTween();
            QueueFree();
        }

        private void ClearActiveTween()
        {
            if (!IsTweenActive()) return;

            _activeTween.Finished -= OnFadeComplete;
            _activeTween.Finished -= DeleteAfterFade;
            _activeTween.Kill();
            _activeTween = null;
        }

        private bool IsTweenActive() =>
            _activeTween != null && IsInstanceValid(_activeTween);

        private void ResetShaderParams()
        {
            if (_shaderMaterial == null) return;

            _shaderMaterial.SetShaderParameter("current_tex", Texture ?? GetOrCreateEmptyTexture());
            _shaderMaterial.SetShaderParameter("next_tex", GetOrCreateEmptyTexture());
            _shaderMaterial.SetShaderParameter("progress", 0.0f);
        }

        private static Texture2D GetOrCreateEmptyTexture()
        {
            if (EmptyTex != null && IsInstanceValid(EmptyTex))
                return EmptyTex;

            var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);
            EmptyTex = ImageTexture.CreateFromImage(image);
            return EmptyTex;
        }

        #region Animation
        [Signal] public delegate void AnimationCompleteEventHandler();
        Tween _animationTween;
        Vector2 _originalPosition;

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

        public void AddRotate(float degrees, float duration, AnchorMode anchorMode = AnchorMode.Local,
                     Tween.TransitionType trans = Tween.TransitionType.Quart,
                     Tween.EaseType ease = Tween.EaseType.Out)
        {
            if (_animationTween == null || !IsInstanceValid(_animationTween))
            {
                CreateAnimTween();
            }

            switch (anchorMode)
            {
                case AnchorMode.Local:
                    _animationTween.TweenProperty(this, "rotation_degrees", degrees, duration).SetTrans(trans).SetEase(ease);
                    break;

                case AnchorMode.Global:
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
                    break;
            }
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
        public void AddColorTween(Color target, float duration,
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

        public void CompleteAnimations()
        {
            if (_animationTween != null && IsInstanceValid(_animationTween))
            {
                _animationTween.CustomStep(1000f);
                _animationTween.EmitSignal(Tween.SignalName.Finished);
                _animationTween.Kill();
                _animationTween = null;
            }

            EmitSignal(SignalName.AnimationComplete);
        }
        
        #endregion

        #endregion

    }
}