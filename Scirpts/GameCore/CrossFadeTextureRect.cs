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
        public Vector2 size = Vector2.One;
        public TextureRect.ExpandModeEnum expandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        public TextureRect.StretchModeEnum stretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        public Control.LayoutPreset layoutPreset = Control.LayoutPreset.TopLeft;

        public TextureParams(Vector2 position, float rotation_degrees, Vector2 size,
            TextureRect.ExpandModeEnum expandMode, TextureRect.StretchModeEnum stretchMode,
            Control.LayoutPreset layoutPreset = Control.LayoutPreset.TopLeft)
        {
            this.position = position;
            this.rotation_degrees = rotation_degrees;
            this.size = size;
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
            size: new Vector2(1300f, 2000f),
            expandMode: TextureRect.ExpandModeEnum.IgnoreSize,
            stretchMode: TextureRect.StretchModeEnum.KeepAspectCentered,
            layoutPreset: Control.LayoutPreset.TopLeft
        );

        public static TextureParams DefaultTexture = new(
            position: new(0, 300),
            rotation_degrees: 0f,
            size: new Vector2(1920f, 1080f),
            expandMode: TextureRect.ExpandModeEnum.KeepSize,
            stretchMode: TextureRect.StretchModeEnum.KeepAspectCentered,
            layoutPreset: Control.LayoutPreset.TopLeft
        );
    }

    public partial class CrossFadeTextureRect : TextureRect
    {
        #region Public Fields

        public TextureAnimator Animator { get; private set; }
        public TextureParams TextureParams { get; private set; }
        public bool IsChild { get; private set; } = false;

        public CrossFadeTextureRect() { }

        public CrossFadeTextureRect(TextureParams initParams) : this(null, initParams) { }

        public CrossFadeTextureRect(string initTexPath = null,
        TextureParams initParams = default, bool isChild = false)
        {
            TextureParams = initParams;
            SetAnchorsAndOffsetsPreset(initParams.layoutPreset, LayoutPresetMode.KeepSize);
            Texture = GD.Load<Texture2D>(initTexPath) ?? GetEmptyTexture(initParams.size);
            Position = initParams.position;
            RotationDegrees = initParams.rotation_degrees;
            Size = initParams.size;
            ExpandMode = initParams.expandMode;
            StretchMode = initParams.stretchMode;
            IsChild = isChild;
        }

        public override void _Ready()
        {
            base._Ready();
            FadeDuration = GlobalSettings.AnimationDefaultTime;

            if (!IsChild)
            {
                // 创建动画控制器子节点
                Animator = new TextureAnimator(this)
                {
                    Name = "Animator"
                };
                AddChild(Animator);

                //InitShaderMaterial();
                DialogueManager.Instance.BeforeExecuteStart += Animator.ResetSignalEmitedSymbol;
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (!IsChild) DialogueManager.Instance.BeforeExecuteStart -= Animator.ResetSignalEmitedSymbol;
            ClearActiveFadingTween();
            _nextTexture = null;

            if (Animator != null)
            {
                Animator.QueueFree();
                Animator = null;
            }
            
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
        private ShaderMaterial _shaderMaterial;
        private Tween _fadingTween;
        private Texture2D _nextTexture;
        private Texture2D _currentEmptyTex;
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
        public virtual void SetTextureWithFade(string newTexPath, float duration = -1, bool immediate = false, int ZIndex = 0)
        {
            if (string.IsNullOrWhiteSpace(newTexPath))
            {
                if (Texture == null || _pendingDeletion) return;
                var emptyTex = GetEmptyTexture(Texture.GetSize());
                SetTextureWithFade(emptyTex, duration, immediate, ZIndex);
                return;
            }
            
            var newTexture = GD.Load<Texture2D>(newTexPath);

            if (immediate)
            {
                SetTextureImmediately(newTexture);
                return;
            }

            if (Texture == newTexture || _pendingDeletion || IsChild)
                return;

            if (newTexture == null)
            {
                if (Texture == null) return;
                newTexture = GetEmptyTexture(Texture.GetSize());
            }

            ClearActiveFadingTween();
            InitShaderMaterial();

            _shaderMaterial.SetShaderParameter("current_tex", Texture ?? GetEmptyTexture(newTexture.GetSize()));
            _shaderMaterial.SetShaderParameter("next_tex", newTexture);

            _fadingTween = CreateTween();
            _fadingTween.SetEase(Tween.EaseType.Out);
            _fadingTween.SetTrans(Tween.TransitionType.Linear);
            _fadingTween.TweenMethod(Callable.From<float>(SetProgress), 0.0f, 1.0f, duration > 0 ? duration : FadeDuration);

            _nextTexture = newTexture;
            _fadingTween.Finished += OnFadeComplete;
            
            this.ZIndex = ZIndex;
        }

        /// <summary>
        /// 淡化过渡到目标纹理
        /// </summary>
        public virtual void SetTextureWithFade(Texture2D newTexture, float duration = -1, bool immediate = false, int ZIndex = 0)
        {
            if (immediate)
            {
                SetTextureImmediately(newTexture);
                return;
            }

            if (Texture == newTexture || _pendingDeletion || IsChild)
                return;

            if (newTexture == null)
            {
                if (Texture == null) return;
                newTexture = GetEmptyTexture(Texture.GetSize());
            }

            ClearActiveFadingTween();
            InitShaderMaterial();

            _shaderMaterial.SetShaderParameter("current_tex", Texture ?? GetEmptyTexture(newTexture.GetSize()));
            _shaderMaterial.SetShaderParameter("next_tex", newTexture);

            _fadingTween = CreateTween();
            _fadingTween.SetEase(Tween.EaseType.Out);
            _fadingTween.SetTrans(Tween.TransitionType.Linear);
            _fadingTween.TweenMethod(Callable.From<float>(SetProgress), 0.0f, 1.0f, duration > 0 ? duration : FadeDuration);

            _nextTexture = newTexture;
            _fadingTween.Finished += OnFadeComplete;
            
            this.ZIndex = ZIndex;
        }
        


        /*
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
        }*/

        /// <summary>
        /// 设置纹理为空白纹理
        /// </summary>
        /// <param name="deleteAfterFade">是否在淡化完成后删除当前节点</param>
        /// <param name="immediate"></param>
        public virtual void ClearTexture(float duration, bool deleteAfterFade = false, bool immediate = false)
        {
            if (Texture == null || _pendingDeletion || IsChild) return;

            SetTextureWithFade(GetEmptyTexture(Texture.GetSize()), duration: duration, immediate: immediate);

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
        private void SetTextureImmediately(Texture2D newTexture)
        {
            if (newTexture == null || Texture == newTexture) return;

            ClearActiveFadingTween();
            InitShaderMaterial();

            Texture = newTexture;
            ResetShaderParams();
        }

        /// <summary>
        /// 获取或创建一个空白纹理
        /// </summary>
        private Texture2D GetEmptyTexture(Vector2 size)
        {
            var image = Image.CreateEmpty((int)size.X, (int)size.Y, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);
            return ImageTexture.CreateFromImage(image);
        }

        /// <summary>
        /// 立即完成当前淡化过渡
        /// </summary>
        public virtual void CompleteFade()
        {
            if ((_fadingTween != null && IsInstanceValid(_fadingTween)) || IsChild) return;

            SetProgress(1.0f);
            OnFadeComplete();
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

            _shaderMaterial.SetShaderParameter("current_tex", Texture ?? GetEmptyTexture(Vector2.One));
            _shaderMaterial.SetShaderParameter("next_tex", GetEmptyTexture(Texture == null ? Vector2.One : Texture.GetSize()));
            _shaderMaterial.SetShaderParameter("progress", 0.0f);
        }

        #endregion
    }
}