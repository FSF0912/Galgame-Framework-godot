//删了。
/*
上面的代码块我设想的是本类全部接管Animator的生命周期，但写完才发现这样会导致耦合过高，且不易维护。
各司其职即可。
Animator的生命周期交给自己管理即可。

按照DialogueManager的生命周期设计思路，一般地，调用set_tex，clear_tex时都应在单个DialogueLine的执行过程。
也就是说，调用这些方法时，可以看作本类开启了新的生命周期，上一个生命周期会被销毁。

故没有考虑多个方法同时调用的情况。

本类暴露Animator属性，允许外部直接操作Animator。
暴露动画完成信号，DialogueManager只需订阅本类的动画完成信号即可，不用考虑边界等。
应该。

251130
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using VisualNovel.GameCore.Utilities;

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

    public class SingleTextureSetter
    {
        public string path;
        public VNTextureRect.TranslationType translationType;
        public float duration;
        public int zIndex;

        public SingleTextureSetter(string path, VNTextureRect.TranslationType translationType, float duration, int zIndex)
        {
            this.path = path;
            this.translationType = translationType;
            this.duration = duration;
            this.zIndex = zIndex;
        }
    }

    [GlobalClass]
    public partial class VNTextureRect : TextureRect, ISignalNotifier
    {
        public enum TranslationType
        {
            Immediate,
            CrossFade,
            FadeOutIn
        };
        [Signal] public delegate void AnimationCompleteEventHandler();

        public StringName CompletionSignal => SignalName.AnimationComplete;

        public TextureParams TextureParams { get; private set; }
        private TextureRect _fadingRect;
        private Tween _tween;
        
        //use when translating
        private TranslationType _currentTranslationType = TranslationType.Immediate;
        private Texture2D _currentTransitionTex;
        private SingleTextureSetter _currentSetter;

        public bool IsAnimatorEnabled { get; private set; } = true;
        public TextureAnimator Animator { get; private set; }

        /// <summary>
        /// 动画队列
        /// </summary>
        private readonly Queue<SingleTextureSetter> _translationQueue = new();

        private bool _isProcessingQueue = false;
        private bool isProcessingQueue
        {
            get => _isProcessingQueue;
            set
            {
                _isProcessingQueue = value;
                if (value)
                {
                    //开始处理队列
                    _current_tcs?.TrySetResult(true);
                    _current_tcs = null;
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource = new CancellationTokenSource();
                    _ = ProcessTranslationQueue(_cancellationTokenSource);
                }
            }
        }

        private TaskCompletionSource<bool> _current_tcs = null;
        private CancellationTokenSource _cancellationTokenSource = null;

        //init params(constructor use only)
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
            if (IsAnimatorEnabled)
            {
                Animator = new TextureAnimator(this);
                AddChild(Animator);
            }

            SetTexture(init_TexturePath, init_TranslationType, GlobalSettings.AnimationDefaultTime, init_ZIndex);
        }

        //所有的SetTexture调用都会在同一帧传入，随后添加到队列，排队执行
        public virtual void SetTexture(string path, TranslationType setType = TranslationType.Immediate, float duration = 0.5f, int zIndex = 1)
        {
            _translationQueue.Enqueue(new SingleTextureSetter(path, setType, duration, zIndex));
            if (!isProcessingQueue) isProcessingQueue = true;//setter访问器开始处理队列
        }

        private async Task ProcessTranslationQueue(CancellationTokenSource token)
        {
            try
            {
                while (_translationQueue.Count > 0)
                {
                    if (token.IsCancellationRequested) return;
                    var setter = _translationQueue.Dequeue();
                    _currentSetter = setter;
                    await SetTextureAtOnceAsync(setter.path, setter.translationType, setter.duration, setter.zIndex);
                }
                EmitSignal(SignalName.AnimationComplete);
            }
            finally
            {
                //处理被取消的情况
                isProcessingQueue = false;
            }
            
        }

        protected async virtual Task SetTextureAtOnceAsync(string path, TranslationType setType = TranslationType.Immediate, float duration = 0.5f, int zIndex = 1)
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
                    New_Tcs();

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

                    await Start_Await();
                    break;

                case TranslationType.FadeOutIn:
                    New_Tcs();

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
                   
                   await Start_Await();
                    break;
            }

            void New_Tcs()
            {
                _current_tcs?.TrySetResult(true);
                _current_tcs = null;
                _current_tcs = new TaskCompletionSource<bool>();
            }

            async Task Start_Await()
            {
                _tween.Finished += () => _current_tcs.TrySetResult(true);

                await _current_tcs.Task;//等待补间完成
                OnOperationFinished();//收尾工作，还原状态
            }
            
        }

        public virtual void ClearTexture(float duration = -1, bool immediate = false, bool deleteAfterFade = false)
        {
             ClearTween();
             if (immediate)
             {
                StopProcessingQueue();
                _translationQueue.Clear();
                ClearTween();

                Texture = null;
                SetCrossFadeTexActive(false);
                _fadingRect.Modulate = Colors.Transparent;
                if (deleteAfterFade) QueueFree();
             }
             else
             {
                //取消所有队列处理，淡化当前显示的纹理

                StopProcessingQueue();
                _translationQueue.Clear();
                ClearTween();

                duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
                _tween = CreateTween();
                _tween.TweenProperty(this, "modulate:a", 0, duration);
                _tween.TweenProperty(_fadingRect, "modulate:a", 1, duration);
                _tween.SetTrans(Tween.TransitionType.Sine);
                _tween.SetEase(Tween.EaseType.InOut);
                _tween.Connect(Tween.SignalName.Finished, Callable.From(() =>
                {
                    Texture = null;
                    SetCrossFadeTexActive(false);
                    Modulate = Colors.Transparent;
                    _fadingRect.Modulate = Colors.Transparent;

                    if (deleteAfterFade) QueueFree();
                }), (uint)ConnectFlags.OneShot);
             }
        }

        public virtual void InterruptTranslation()
        {
            /*if (_currentTranslationType == TranslationType.Immediate) return;
            ClearTween(); 
            
            Texture = _currentTransitionTex;
            SetCrossFadeTexActive(false);
            _fadingRect.Modulate = Colors.Transparent;
            Modulate = Colors.White;
            EmitSignal(SignalName.AnimationComplete);*/

            StopProcessingQueue();
            ClearTween();
            SingleTextureSetter setter;

            if (_translationQueue.Count == 0)
            {
                if (_currentSetter == null) return;
                _translationQueue.Clear();
                _currentTransitionTex = _currentSetter.path != "" ? VNResloader.LoadTexture2D(_currentSetter.path) : null;
                OnOperationFinished();
                return;
            }

            if (_translationQueue.Count > 1)
            {
                while (_translationQueue.Count > 1)
                {
                    _translationQueue.Dequeue();
                }
            }
            var temp = _translationQueue.Dequeue();
            setter = new SingleTextureSetter(temp.path, temp.translationType, temp.duration, temp.zIndex);
            _translationQueue.Clear();
            _currentTransitionTex = setter.path != "" ? VNResloader.LoadTexture2D(setter.path) : null;
            OnOperationFinished();
        }

        private void SetCrossFadeTexActive(bool active)
        {
            _fadingRect.Visible = active;
            _fadingRect.ProcessMode = active ? ProcessModeEnum.Always : ProcessModeEnum.Disabled;
        }

        private void StopProcessingQueue()
        {
            _current_tcs?.TrySetResult(true);
            _current_tcs = null;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = null;
        }

        private void ClearTween()
        {
            if (_tween != null && IsInstanceValid(_tween))
            {
                _tween.Kill(); // kill不会触发finished信号
                _tween = null;
            }
        }

        private void OnOperationFinished()
        {
            if (_cancellationTokenSource?.IsCancellationRequested == true) return;

            if (_currentTranslationType == TranslationType.CrossFade || _currentTranslationType == TranslationType.FadeOutIn)
            {
                Texture = _currentTransitionTex;
                SetCrossFadeTexActive(false);
                _fadingRect.Modulate = Colors.Transparent;
                Modulate = Colors.White;
            }
        }
    }
}