using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace VisualNovel
{
    #if false
    /// <summary>
    /// 默认立绘参数
    /// position x阈值：约0-640 y阈值：约50-1000
    /// </summary>
    public class TextureParams
    {
        public Vector2 position = Vector2.Zero;
        public float rotation_degrees = 0f;
        public Vector2 size = Vector2.One;
        //public TextureRect.ExpandModeEnum expandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        //public TextureRect.StretchModeEnum stretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        //public Control.LayoutPreset layoutPreset = Control.LayoutPreset.TopLeft;

        public TextureParams(Vector2 position, float rotation_degrees, Vector2 size)
            //TextureRect.ExpandModeEnum expandMode, TextureRect.StretchModeEnum stretchMode,
            //Control.LayoutPreset layoutPreset = Control.LayoutPreset.TopLeft
        {
            this.position = position;
            this.rotation_degrees = rotation_degrees;
            this.size = size;
            //this.expandMode = expandMode;
            //this.stretchMode = stretchMode;
            //this.layoutPreset = layoutPreset;
        }

        /// <summary>
        /// 默认立绘参数
        /// position x阈值：约0-640 y阈值：约50-1000
        /// </summary>
        public static TextureParams DefaultPortraitNormalDistance = new(
            position: new(0, 150),
            rotation_degrees: 0f,
            size: new Vector2(1300f, 2000f)
        );

        /// <summary>
        /// 默认全屏背景图参数
        /// </summary>
        public static TextureParams DefaultTextureBackGround = new(
            position: new(0, 300),
            rotation_degrees: 0f,
            size: new Vector2(1920f, 1080f)
        );

        /// <summary>
        /// 默认tex参数
        /// </summary>
        public static TextureParams DefaultTexture = new(
            position: new(960, 540),
            rotation_degrees: 0f,
            size: new Vector2(100, 100)
        );
    }
    #endif

    

    [GlobalClass]
    public partial class VNTextureController : Control, ISignalNotifier
    {
        protected record SingleTranslation(string path, TranslationType translationType, float duration, int zIndex);
        public enum TranslationType
        {
            Immediate,
            CrossFade,
            FadeOutIn
        };
        [Signal] public delegate void AnimationCompleteEventHandler();

        public StringName CompletionSignal => SignalName.AnimationComplete;
        //texture
        private TextureRect _mainTexRect;
        private TextureRect _fadingRect;
        private Tween _tween;
        
        //use when translating
        private TranslationType _currentTranslationType = TranslationType.Immediate;
        private Texture2D _currentTransitionTex;
        private SingleTranslation _currentSetter;
        //animator
        public bool IsAnimatorEnabled { get; private set; } = true;
        public TextureAnimator Animator { get; private set; }

        /// <summary>
        /// 动画队列
        /// </summary>
        private readonly Queue<SingleTranslation> _translationQueue = new();
        private bool isProcessingQueue;
        private TaskCompletionSource<bool> _current_tcs = null;

        //init params(constructor use only)
        string init_TexturePath = "";
        TranslationType init_TranslationType = TranslationType.Immediate;
        int init_ZIndex = 1;

        public VNTextureController() { }

        public VNTextureController(Vector2 position, float rotation_degrees, Vector2 scale, string initTexturePath = "", TranslationType initTranslationType = TranslationType.Immediate, int initZIndex = 1)
        {
            IsAnimatorEnabled = true;
            Position = position;
            RotationDegrees = rotation_degrees;
            Size = scale;
            this.init_TexturePath = initTexturePath;
            this.init_TranslationType = initTranslationType;
            this.init_ZIndex = initZIndex;
        }

        public override void _Ready()
        {
            base._Ready();
            _mainTexRect = GetNode<TextureRect>("MainTexRect");
            _fadingRect = GetNode<TextureRect>("FadingRect");
            if (IsAnimatorEnabled)
            {
                Animator = new TextureAnimator(this);
                AddChild(Animator);
            }

            if (!string.IsNullOrEmpty(init_TexturePath))
                SetTextureOrdered(init_TexturePath, init_TranslationType, GlobalSettings.AnimationDefaultTime, init_ZIndex);
        }

        public override void _ExitTree()
        {
            _tween?.Kill();
            _current_tcs?.TrySetResult(true);
            
            base._ExitTree();
        }

        //所有的SetTexture调用都会在同一帧传入，随后添加到队列，排队执行
        public virtual void SetTextureOrdered(string path, TranslationType setType = TranslationType.Immediate, float duration = 0.5f, int zIndex = 1)
        {
            _translationQueue.Enqueue(new SingleTranslation(path, setType, duration, zIndex));
            if (!isProcessingQueue)
            {
                isProcessingQueue = true;
                _ = ProcessingTransQueueAsync();
            }
        }

        private async Task ProcessingTransQueueAsync()
        {
            while (_translationQueue.Count > 0)
            {
                await SetTextureAtOnceAsync(_translationQueue.Dequeue());
            }
            EmitSignal(SignalName.AnimationComplete);
        }

        protected async virtual Task SetTextureAtOnceAsync(SingleTranslation translation)
        {
            _currentTranslationType = translation.translationType;
            var newTex = GD.Load<Texture2D>(translation.path);
            ZIndex = translation.zIndex;

            float duration = translation.duration <= 0 ? GlobalSettings.AnimationDefaultTime : translation.duration;

            if (newTex == null) return;

            _currentTransitionTex = newTex;

            switch (translation.translationType)
            {
                case TranslationType.Immediate:
                    _mainTexRect.Modulate = Colors.White;
                    _fadingRect.Visible = false;
                    _mainTexRect.Texture = newTex;
                    break;

                case TranslationType.CrossFade:
                    _current_tcs = new TaskCompletionSource<bool>();

                    _fadingRect.Modulate = Colors.Transparent;
                    _mainTexRect.Modulate = Colors.White;
                    _fadingRect.Visible = true;
                    _fadingRect.Texture = newTex;

                    _tween = CreateTween();
                    _tween.SetParallel();
                    _tween.TweenProperty(_fadingRect, "modulate:a", 1, duration);
                    _tween.TweenProperty(_mainTexRect, "modulate:a", 0, duration);
                    _tween.SetTrans(Tween.TransitionType.Sine);
                    _tween.SetEase(Tween.EaseType.InOut);

                    await Start_Await();
                    break;

                case TranslationType.FadeOutIn:
                    _current_tcs = new TaskCompletionSource<bool>();

                    _fadingRect.Modulate = Colors.Transparent;
                    _mainTexRect.Modulate = Colors.White;
                    _fadingRect.Visible = true;
                    _fadingRect.Texture = newTex;

                    _tween = CreateTween();
                    _tween.SetParallel(false);
                    _tween.TweenProperty(_mainTexRect, "modulate:a", 0, duration / 2);
                    _tween.TweenProperty(_fadingRect, "modulate:a", 1, duration / 2);
                    _tween.SetTrans(Tween.TransitionType.Sine);
                    _tween.SetEase(Tween.EaseType.InOut);
                   
                   await Start_Await();
                    break;
            }

            async Task Start_Await()
            {
                _tween.Connect(Tween.SignalName.Finished, Callable.From(() =>
                {
                    _current_tcs?.TrySetResult(true);
                    _mainTexRect.Texture = _currentTransitionTex;
                    _mainTexRect.Modulate = Colors.White;
                    _fadingRect.Visible = false;
                    _fadingRect.Modulate = Colors.Transparent;

                }), (uint)ConnectFlags.OneShot);

                await _current_tcs.Task;//等待补间完成
            }
            
        }

        public virtual void DeleteTexture()
        {
            QueueFree();
        }

        public virtual void InterruptTranslation()
        {
            CancelCurrent();

            if (_translationQueue.Count == 0)
            {
                if (_currentSetter == null) return;
                _mainTexRect.Texture = _currentSetter.path != "" ? GD.Load<Texture2D>(_currentSetter.path) : null;
                return;
            }

            if (_translationQueue.Count > 1)
            {
                while (_translationQueue.Count > 1)
                {
                    _translationQueue.Dequeue();
                }
            }
            var last = _translationQueue.Dequeue();
            _mainTexRect.Texture = last.path != "" ? GD.Load<Texture2D>(last.path) : null;
        }

        /// <summary>
        /// 停止队列处理
        /// 不清空队列,主任务不会被取消
        /// </summary>
        private void CancelCurrent()
        {
            if (_tween != null && IsInstanceValid(_tween))
            {
                _tween.Kill(); // kill不会触发finished信号
                _tween = null;
            }

            _current_tcs?.TrySetResult(true);
            _current_tcs = null;

            _mainTexRect.Modulate = Colors.White;
            _fadingRect.Modulate = Colors.Transparent;
            _fadingRect.Visible = false;
        
        }
    }
}