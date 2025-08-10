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
            FadeDuration = GlobalSettings.AnimationDefaultTime;
            InitShaderMaterial();
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
                    _activeTween?.Disconnect(Tween.SignalName.Finished, Callable.From(OnFadeComplete));
                    _activeTween?.Connect(Tween.SignalName.Finished, Callable.From(DeleteAfterFade));
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

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationPredelete)
            {
                ClearActiveTween();
                _nextTexture = null;
            }
        }

        #region  Animation
        [Signal] public delegate void AnimationCompleteEventHandler();
        #endregion


    }
}