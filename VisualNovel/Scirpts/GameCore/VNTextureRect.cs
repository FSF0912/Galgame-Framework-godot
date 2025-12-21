/*
比较复杂。


using System;
using System.Threading.Tasks;
using System.Threading;
using Godot;
using System.Collections.Generic;

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
        /// 默认立绘参数
        /// position x阈值：约0-640 y阈值：约50-1000
        /// </summary>
        public static TextureParams DefaultPortraitNormalDistance = new(
            position: new(0, 150),
            rotation_degrees: 0f,
            size: new Vector2(1300f, 2000f),
            expandMode: TextureRect.ExpandModeEnum.IgnoreSize,
            stretchMode: TextureRect.StretchModeEnum.KeepAspectCentered,
            layoutPreset: Control.LayoutPreset.TopLeft
        );

        /// <summary>
        /// 默认全屏背景图参数
        /// </summary>
        public static TextureParams DefaultTexture = new(
            position: new(0, 300),
            rotation_degrees: 0f,
            size: new Vector2(1920f, 1080f),
            expandMode: TextureRect.ExpandModeEnum.KeepSize,
            stretchMode: TextureRect.StretchModeEnum.KeepAspectCentered,
            layoutPreset: Control.LayoutPreset.TopLeft
        );
    }

    public partial class VNTextureRect : TextureRect
    {
        public enum TranslationType
        {
            Immediate,
            CrossFade,
            FadeOutIn
        };

        private const string BLUR_SHADER_PATH = "res://VisualNovel/Shaders/blur.gdshader";
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        
        private CancellationTokenSource _cts;
        private TaskCompletionSource<bool> _tcsTween;
        private TaskCompletionSource<bool> _tcsAnimator;

        [Signal] public delegate void AnimationCompleteEventHandler();

        private Material _blurMaterial;
        public TextureParams TextureParams { get; private set; }
        private TextureRect _fadingRect;
        private Tween _tween;
        private TranslationType _currentTranslationType = TranslationType.Immediate;
        private Texture2D _currentTransitionTex;

        public bool IsAnimatorEnabled { get; private set; } = true;
        public TextureAnimator Animator { get; private set; }

        string init_TexturePath = "";
        TranslationType init_TranslationType = TranslationType.Immediate;
        int init_ZIndex = 1;

        public VNTextureRect(TextureParams initParams, string initTexturePath = "", TranslationType initTranslationType = TranslationType.Immediate, int initZIndex = 1)
        {
            IsAnimatorEnabled = true;
            initParams ??= TextureParams.DefaultTexture;
            TextureParams = initParams;
            SetAnchorsAndOffsetsPreset(initParams.layoutPreset, LayoutPresetMode.KeepSize);
            Position = initParams.position;
            RotationDegrees = initParams.rotation_degrees;
            Size = initParams.size;
            ExpandMode = initParams.expandMode;
            StretchMode = initParams.stretchMode;
            this.init_TexturePath = initTexturePath;
            this.init_TranslationType = initTranslationType;
            this.init_ZIndex = initZIndex;
        }

        public VNTextureRect(TextureParams initParams, string initTexturePath = "", int initZIndex = 1)
        {
            IsAnimatorEnabled = false;
            initParams ??= TextureParams.DefaultTexture;
            TextureParams = initParams;
            SetAnchorsAndOffsetsPreset(initParams.layoutPreset, LayoutPresetMode.KeepSize);
            Position = initParams.position;
            RotationDegrees = initParams.rotation_degrees;
            Size = initParams.size;
            ExpandMode = initParams.expandMode;
            StretchMode = initParams.stretchMode;
            this.init_TexturePath = initTexturePath;
            this.init_ZIndex = initZIndex;
        }

        public override void _Ready()
        {
            base._Ready();
            CheckCrossFadeTex();

            // 加载 Shader
            var blurShader = GD.Load<Shader>(BLUR_SHADER_PATH);
            if (blurShader != null)
            {
                _blurMaterial = new ShaderMaterial() { Shader = blurShader };
            }

            if (IsAnimatorEnabled)
            {
                Animator = new TextureAnimator(this);
                AddChild(Animator);
            }

            SetTexture(init_TexturePath, init_TranslationType, GlobalSettings.AnimationDefaultTime, init_ZIndex);

            DialogueManager.Instance.AfterExecuteStart += StartAwait;
            DialogueManager.Instance.ExecuteComplete += StopAwait;
            Animator.AnimationComplete += OnAnimatorFinished;
        }

        public override void _ExitTree()
        {
            CancelCurrentTask(); 
            DialogueManager.Instance.AfterExecuteStart -= StartAwait;
            DialogueManager.Instance.ExecuteComplete -= StopAwait;
            Animator.AnimationComplete -= OnAnimatorFinished;
            base._ExitTree();
        }

        public virtual void SetTexture(string path, TranslationType setType = TranslationType.Immediate, float duration = 0.5f, int zIndex = 1)
        {
            _currentTranslationType = setType;
            ClearTween();
            
            var newTex = VNResloader.LoadTexture2D(path);
            this.ZIndex = zIndex;

            if (newTex == null) return;

            _currentTransitionTex = newTex;

            switch (setType)
            {
                case TranslationType.Immediate:
                    Texture = newTex;
                    SetCrossFadeTexActive(false);
                    break;

                case TranslationType.CrossFade:
                    _fadingRect.Modulate = Colors.Transparent;
                    Modulate = Colors.White;
                    SetCrossFadeTexActive(true);
                    _fadingRect.Texture = newTex;

                    _tween = CreateTween();
                    _tween.SetParallel();
                    _tween.TweenProperty(_fadingRect, "modulate:a", 1, duration);
                    _tween.TweenProperty(this, "modulate:a", 0, duration);
                    _tween.SetTrans(Tween.TransitionType.Sine);
                    _tween.SetEase(Tween.EaseType.InOut);
                    _tween.Finished += OnTweenFinished;
                    break;

                case TranslationType.FadeOutIn:
                    _fadingRect.Modulate = Colors.Transparent;
                    Modulate = Colors.White;
                    SetCrossFadeTexActive(true);
                    _fadingRect.Texture = newTex;

                    _tween = CreateTween();
                    _tween.SetParallel(false);
                    _tween.TweenProperty(this, "modulate:a", 0, duration / 2);
                    _tween.TweenProperty(_fadingRect, "modulate:a", 1, duration / 2);
                    _tween.SetTrans(Tween.TransitionType.Sine);
                    _tween.SetEase(Tween.EaseType.InOut);
                    _tween.Finished += OnTweenFinished;
                    break;
            }
        }

        public virtual void ClearTexture(float duration = -1, bool immediate = false, bool deleteAfterFade = false)
        {
             ClearTween();
             if (immediate)
             {
                Texture = null;
                SetCrossFadeTexActive(false);
                _fadingRect.Modulate = Colors.Transparent;
                if (deleteAfterFade) QueueFree();
             }
             else
             {
                duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
                _tween = CreateTween();
                _tween.TweenProperty(this, "modulate:a", 0, duration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
                _tween.Finished += OnTweenFinished;
                SetCrossFadeTexActive(false);
                if (deleteAfterFade)  _tween.Finished += QueueFree;
             }
        }

        public virtual void InterruptTranslation()
        {
            if (_currentTranslationType == TranslationType.Immediate) return;
            ClearTween(); 
            
            Texture = _currentTransitionTex;
            SetCrossFadeTexActive(false);
            _fadingRect.Modulate = Colors.Transparent;
            Modulate = Colors.White;

            _tcsTween?.TrySetResult(true);
            _tcsTween = null;
        }

        private void SetCrossFadeTexActive(bool active)
        {
            _fadingRect.Visible = active;
            _fadingRect.ProcessMode = active ? ProcessModeEnum.Always : ProcessModeEnum.Disabled;
        }

        private void ClearTween()
        {
            if (_tween != null && IsInstanceValid(_tween))
            {
                _tween.Kill(); // kill不会触发finished信号
                _tween = null;
            }
            // 手动完成tcs，防止await寄掉
            _tcsTween?.TrySetResult(true);
            _tcsTween = null;
        }

        private void OnTweenFinished()
        {
            if (_currentTranslationType == TranslationType.CrossFade || _currentTranslationType == TranslationType.FadeOutIn)
            {
                 Texture = _currentTransitionTex;
                 SetCrossFadeTexActive(false);
                 _fadingRect.Modulate = Colors.Transparent;
                 Modulate = Colors.White;
            }
            
            _tcsTween?.TrySetResult(true);
            _tcsTween = null;
        }
        

        private void OnAnimatorFinished()
        {
            _tcsAnimator?.TrySetResult(true);
            _tcsAnimator = null;
        }

        # region Signal Emitting Async
        private void StartAwait()
        {
            CancelCurrentTask();

            _cts = new CancellationTokenSource();
            _ = AwaitAnimationsAndEmitCompleteAsync(_cts.Token);
        }

        private void StopAwait()
        {
            CancelCurrentTask();
        }

        private void CancelCurrentTask()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        private async Task AwaitAnimationsAndEmitCompleteAsync(CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                await _semaphore.WaitAsync(ct);

                try
                {
                    if (ct.IsCancellationRequested) return;

                    _tcsTween?.TrySetResult(true);
                    _tcsAnimator?.TrySetResult(true);

                    bool tweenValid = _tween != null && IsInstanceValid(_tween) && _tween.IsValid();
                    bool animatorValid = IsAnimatorEnabled && Animator != null && Animator.animTween != null && IsInstanceValid(Animator.animTween);

                    if (!tweenValid && !animatorValid)
                    {
                        EmitSignal(SignalName.AnimationComplete);
                        return;
                    }

                    var tasks = new List<Task>();
                    
                    if (tweenValid)
                    {
                        _tcsTween = new TaskCompletionSource<bool>();
                        // 这里我们依赖 _tween.Finished += OnTweenFinished 或者 ClearTween() 来设置结果
                        // 为了支持取消，我们注册一个回调：如果 Token 取消了，TCS 也设为取消
                        using (ct.Register(() => _tcsTween?.TrySetCanceled()))
                        {
                            tasks.Add(_tcsTween.Task);
                        }
                    }

                    if (animatorValid)
                    {
                        _tcsAnimator = new TaskCompletionSource<bool>();
                        // 简单起见，这里假设 Animator 逻辑类似。实际需绑定 Animator 的 Finish 事件。
                        // tasks.Add(_tcsAnimator.Task); 
                        // (为了演示代码完整性，这里暂不添加 Animator 的详细绑定，原理同上)
                        using (ct.Register(() => _tcsAnimator?.TrySetCanceled()))
                        {
                            tasks.Add(_tcsAnimator.Task);
                        }
                    }
                    
                    // 等待所有动画完成
                    // 如果 ct 被取消，TCS 会变为 Canceled 状态，Task.WhenAll 会抛出异常
                    await Task.WhenAll(tasks);

                    // 动画都跑完了，最后检查一次，如果没有取消，才发信号
                    if (!ct.IsCancellationRequested)
                    {
                        EmitSignal(SignalName.AnimationComplete);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // 这是一个预期的异常。
                // 当 StopAwait 被调用时，代码会跳到这里。
                // 我们什么都不做，"默默地" 销毁任务，不发射信号。
                // GD.Print("Task Cancelled successfully.");
            }
            catch (Exception e)
            {
                GD.PrintErr($"[VNTextureRect] Error: {e.Message}");
            }
        }
        
        private void CheckCrossFadeTex()
        {
            if (_fadingRect == null || !IsInstanceValid(_fadingRect))
            {
                _fadingRect = new TextureRect()
                {
                    Name = "CrossFadeRect",
                    Position = Vector2.Zero,
                    Size = TextureParams.size,
                    ExpandMode = ExpandModeEnum.KeepSize,
                    StretchMode = StretchModeEnum.KeepAspectCentered,
                    AnchorsPreset = (int)LayoutPreset.FullRect,
                    Modulate = Colors.Transparent
                };
                AddChild(_fadingRect);
            }
        }
        #endregion
    }
}
*/

/*
上面的代码块我设想的是本类全部接管Animator的生命周期，但写完才发现这样会导致耦合过高，且不易维护。
各司其职即可。
Animator的生命周期交给自己管理即可。

按照DialogueManager的生命周期设计思路，一般地，调用settex，cleartex时都应在单个DialogueLine的执行过程。
也就是说，调用这些方法时，可以看作本类开启了新的生命周期，上一个生命周期会被销毁。

故没有考虑多个方法同时调用的情况。

本类暴露Animator属性，允许外部直接操作Animator。
暴露动画完成信号，DialogueManager只需订阅本类的动画完成信号即可，不用考虑边界等。
应该。

tex blur暂时没写。
251130
*/

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
        /// 默认立绘参数
        /// position x阈值：约0-640 y阈值：约50-1000
        /// </summary>
        public static TextureParams DefaultPortraitNormalDistance = new(
            position: new(0, 150),
            rotation_degrees: 0f,
            size: new Vector2(1300f, 2000f),
            expandMode: TextureRect.ExpandModeEnum.IgnoreSize,
            stretchMode: TextureRect.StretchModeEnum.KeepAspectCentered,
            layoutPreset: Control.LayoutPreset.TopLeft
        );

        /// <summary>
        /// 默认全屏背景图参数
        /// </summary>
        public static TextureParams DefaultTexture = new(
            position: new(0, 300),
            rotation_degrees: 0f,
            size: new Vector2(1920f, 1080f),
            expandMode: TextureRect.ExpandModeEnum.KeepSize,
            stretchMode: TextureRect.StretchModeEnum.KeepAspectCentered,
            layoutPreset: Control.LayoutPreset.TopLeft
        );
    }

    [GlobalClass]
    public partial class VNTextureRect : TextureRect
    {
        public enum TranslationType
        {
            Immediate,
            CrossFade,
            FadeOutIn
        };

        private const string BLUR_SHADER_PATH = "res://VisualNovel/Shaders/blur.gdshader";
        [Signal] public delegate void AnimationCompleteEventHandler();

        private Material _blurMaterial;
        public TextureParams TextureParams { get; private set; }
        private TextureRect _fadingRect;
        private Tween _tween;
        private TranslationType _currentTranslationType = TranslationType.Immediate;
        private Texture2D _currentTransitionTex;

        public bool IsAnimatorEnabled { get; private set; } = true;
        public TextureAnimator Animator { get; private set; }

        string init_TexturePath = "";
        TranslationType init_TranslationType = TranslationType.Immediate;
        int init_ZIndex = 1;

        public VNTextureRect(TextureParams initParams, string initTexturePath = "", TranslationType initTranslationType = TranslationType.Immediate, int initZIndex = 1)
        {
            IsAnimatorEnabled = true;
            initParams ??= TextureParams.DefaultTexture;
            TextureParams = initParams;
            SetAnchorsAndOffsetsPreset(initParams.layoutPreset, LayoutPresetMode.KeepSize);
            Position = initParams.position;
            RotationDegrees = initParams.rotation_degrees;
            Size = initParams.size;
            ExpandMode = initParams.expandMode;
            StretchMode = initParams.stretchMode;
            this.init_TexturePath = initTexturePath;
            this.init_TranslationType = initTranslationType;
            this.init_ZIndex = initZIndex;
        }

        public VNTextureRect(TextureParams initParams, string initTexturePath = "", int initZIndex = 1)
        {
            IsAnimatorEnabled = false;
            initParams ??= TextureParams.DefaultTexture;
            TextureParams = initParams;
            SetAnchorsAndOffsetsPreset(initParams.layoutPreset, LayoutPresetMode.KeepSize);
            Position = initParams.position;
            RotationDegrees = initParams.rotation_degrees;
            Size = initParams.size;
            ExpandMode = initParams.expandMode;
            StretchMode = initParams.stretchMode;
            this.init_TexturePath = initTexturePath;
            this.init_ZIndex = initZIndex;
        }

        public override void _Ready()
        {
            base._Ready();
            CheckCrossFadingTex();

            // 加载 Shader
            var blurShader = GD.Load<Shader>(BLUR_SHADER_PATH);
            if (blurShader != null)
            {
                _blurMaterial = new ShaderMaterial() { Shader = blurShader };
            }

            if (IsAnimatorEnabled)
            {
                Animator = new TextureAnimator(this);
                AddChild(Animator);
            }

            SetTexture(init_TexturePath, init_TranslationType, GlobalSettings.AnimationDefaultTime, init_ZIndex);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
        }

        public virtual void SetTexture(string path, TranslationType setType = TranslationType.Immediate, float duration = 0.5f, int zIndex = 1)
        {
            _currentTranslationType = setType;
            ClearTween();
            
            var newTex = VNResloader.LoadTexture2D(path);
            this.ZIndex = zIndex;

            if (newTex == null) return;

            _currentTransitionTex = newTex;

            switch (setType)
            {
                case TranslationType.Immediate:
                    Texture = newTex;
                    SetCrossFadeTexActive(false);
                    break;

                case TranslationType.CrossFade:
                    _fadingRect.Modulate = Colors.Transparent;
                    Modulate = Colors.White;
                    SetCrossFadeTexActive(true);
                    _fadingRect.Texture = newTex;

                    _tween = CreateTween();
                    _tween.SetParallel();
                    _tween.TweenProperty(_fadingRect, "modulate:a", 1, duration);
                    _tween.TweenProperty(this, "modulate:a", 0, duration);
                    _tween.SetTrans(Tween.TransitionType.Sine);
                    _tween.SetEase(Tween.EaseType.InOut);
                    _tween.Finished += OnTweenFinished;
                    break;

                case TranslationType.FadeOutIn:
                    _fadingRect.Modulate = Colors.Transparent;
                    Modulate = Colors.White;
                    SetCrossFadeTexActive(true);
                    _fadingRect.Texture = newTex;

                    _tween = CreateTween();
                    _tween.SetParallel(false);
                    _tween.TweenProperty(this, "modulate:a", 0, duration / 2);
                    _tween.TweenProperty(_fadingRect, "modulate:a", 1, duration / 2);
                    _tween.SetTrans(Tween.TransitionType.Sine);
                    _tween.SetEase(Tween.EaseType.InOut);
                    _tween.Finished += OnTweenFinished;
                    break;
            }
        }

        public virtual void ClearTexture(float duration = -1, bool immediate = false, bool deleteAfterFade = false)
        {
             ClearTween();
             if (immediate)
             {
                Texture = null;
                SetCrossFadeTexActive(false);
                _fadingRect.Modulate = Colors.Transparent;
                if (deleteAfterFade) QueueFree();
             }
             else
             {
                duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
                _tween = CreateTween();
                _tween.TweenProperty(this, "modulate:a", 0, duration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
                _tween.Finished += OnTweenFinished;
                SetCrossFadeTexActive(false);
                if (deleteAfterFade)  _tween.Finished += QueueFree;
             }
        }

        public virtual void InterruptTranslation()
        {
            if (_currentTranslationType == TranslationType.Immediate) return;
            ClearTween(); 
            
            Texture = _currentTransitionTex;
            SetCrossFadeTexActive(false);
            _fadingRect.Modulate = Colors.Transparent;
            Modulate = Colors.White;
            //EmitSignal(SignalName.AnimationComplete);
        }

        private void SetCrossFadeTexActive(bool active)
        {
            _fadingRect.Visible = active;
            _fadingRect.ProcessMode = active ? ProcessModeEnum.Always : ProcessModeEnum.Disabled;
        }

        private void ClearTween()
        {
            if (_tween != null && IsInstanceValid(_tween))
            {
                _tween.Kill(); // kill不会触发finished信号
                _tween = null;
            }
        }

        private void OnTweenFinished()
        {
            if (_currentTranslationType == TranslationType.CrossFade || _currentTranslationType == TranslationType.FadeOutIn)
            {
                Texture = _currentTransitionTex;
                SetCrossFadeTexActive(false);
                _fadingRect.Modulate = Colors.Transparent;
                Modulate = Colors.White;
            }
        }


        private void CheckCrossFadingTex()
        {
            if (_fadingRect == null || !IsInstanceValid(_fadingRect))
            {
                _fadingRect = new TextureRect()
                {
                    Name = "CrossFadeRect",
                    Position = Vector2.Zero,
                    Size = TextureParams.size,
                    ExpandMode = ExpandModeEnum.KeepSize,
                    StretchMode = StretchModeEnum.KeepAspectCentered,
                    AnchorsPreset = (int)LayoutPreset.FullRect,
                    Modulate = Colors.Transparent
                };
                AddChild(_fadingRect);
            }
        }

    }
}