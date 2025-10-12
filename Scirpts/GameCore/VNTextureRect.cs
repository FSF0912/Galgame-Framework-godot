using System;
using System.Runtime.Intrinsics.Arm;
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

    public partial class VNTextureRect : TextureRect
    {
        /// <summary>
        /// 设置图片时使用的过渡类型
        /// </summary>
        public enum TranslationType
        {
            Immediate,
            CrossFade,
            FadeOutIn
        };

        /// <summary>
        /// 纹理参数
        /// </summary>
        public TextureParams TextureParams { get; private set; }
        /// <summary>
        /// 用于过渡效果的TextureRect
        /// </summary>
        private TextureRect _fadingRect;
        /// <summary>
        /// 用于过渡效果的Tween
        /// </summary>
        private Tween _tween;
        /// <summary>
        /// 作为打断动画时的标志
        /// </summary>
        private TranslationType _currentTranslationType = TranslationType.Immediate;
        /// <summary>
        /// 打断时恢复的tex
        /// </summary>
        private Texture2D _transitionTex;

        //public VNTextureRect() { }

        /// <summary>
        /// init params
        /// </summary>
        /// <param name="initParams"></param>
        public VNTextureRect(
        TextureParams initParams)
        {
            if (initParams == null) initParams = TextureParams.DefaultTexture;
            TextureParams = initParams;
            SetAnchorsAndOffsetsPreset(initParams.layoutPreset, LayoutPresetMode.KeepSize);
            Position = initParams.position;
            RotationDegrees = initParams.rotation_degrees;
            Size = initParams.size;
            ExpandMode = initParams.expandMode;
            StretchMode = initParams.stretchMode;
        }

        public override void _Ready()
        {
            base._Ready();
            CheckCrossFadeTex();
        }

        /*public override void _ExitTree()
        {



            base._ExitTree();
        }*/

        /// <summary>
        /// 设置图片
        /// </summary>
        /// <param name="path">图片路径</param>
        /// <param name="setType">过渡效果</param>
        /// <param name="duration">在过渡效果不为immediate时的过渡时间</param>
        public virtual void SetTexture(string path, TranslationType setType = TranslationType.Immediate, float duration = 0.5f)
        {
            _currentTranslationType = setType;
            ClearTween();
            var newTex = VNResloader.LoadTexture2D(path);

            if (newTex == null)
            {
                GD.PrintErr($"Failed to load texture from {path} by VNTextureRect.\n{this}");
                return;
            }


            _transitionTex = newTex;

            switch (setType)
            {
                case TranslationType.Immediate:
                    // 立即切换
                    Texture = newTex;
                    SetCrossFadeTexActive(false);
                    break;

                case TranslationType.CrossFade:
                    //交叉淡化前的准备
                    _fadingRect.Modulate = Colors.Transparent;
                    Modulate = Colors.White;

                    SetCrossFadeTexActive(true);
                    _fadingRect.Texture = newTex;

                    //开始交叉淡化
                    _tween = CreateTween();
                    //设置动画为并行模式，
                    //在原图像淡出的同时淡入新图像。
                    _tween.SetParallel();
                    _tween.TweenProperty(_fadingRect, "modulate:a", 1, duration);
                    _tween.TweenProperty(this, "modulate:a", 0, duration);
                    _tween.SetTrans(Tween.TransitionType.Sine);
                    _tween.SetEase(Tween.EaseType.InOut);
                    //动画完成后回调
                    _tween.Connect("finished", Callable.From(() =>
                    {
                        Texture = newTex;
                        SetCrossFadeTexActive(false);
                        _fadingRect.Modulate = Colors.Transparent;
                        Modulate = Colors.White;
                    }));
                    break;


                case TranslationType.FadeOutIn:
                    //准备
                    //半程回调太麻烦了，，直接用两个tex
                    //而且interrupt也可以直接复用。
                    _fadingRect.Modulate = Colors.Transparent;
                    Modulate = Colors.White;

                    SetCrossFadeTexActive(true);
                    _fadingRect.Texture = Texture;
                    _tween = CreateTween();
                    //淡出当前图像
                    _tween.TweenProperty(this, "modulate:a", 0, duration / 2);
                    //淡入新图像
                    _tween.TweenProperty(_fadingRect, "modulate:a", 1, duration / 2);
                    _tween.SetTrans(Tween.TransitionType.Sine);
                    _tween.SetEase(Tween.EaseType.InOut);
                    //回调
                    _tween.Connect("finished", Callable.From(() =>
                    {
                        Texture = newTex;
                        SetCrossFadeTexActive(false);
                        _fadingRect.Modulate = Colors.Transparent;
                        Modulate = Colors.White;
                    }));
                    break;
            }

        }

        public virtual void InterruptTranslation()
        {
            if (_currentTranslationType == TranslationType.Immediate) return;
            if (!_tween.IsValid()) return;

            ClearTween();
            Texture = _transitionTex;
            SetCrossFadeTexActive(false);
            _fadingRect.Modulate = Colors.Transparent;
            Modulate = Colors.White;
        }

        /// <summary>
        /// 设置用于CrossFade的TextureRect的激活状态
        /// </summary>
        /// <param name="active"></param>
        private void SetCrossFadeTexActive(bool active)
        {
            _fadingRect.Visible = active;
            _fadingRect.ProcessMode = active ? ProcessModeEnum.Always : ProcessModeEnum.Disabled;
        }

        /// <summary>
        /// 清理tween
        /// </summary>
        private void ClearTween()
        {
            if (_tween != null && IsInstanceValid(_tween))
            {
                _tween.Kill();
                _tween = null;
            }
        }

        /// <summary>
        /// 检查并初始化用于CrossFade的TextureRect
        /// </summary>
        private void CheckCrossFadeTex()
        {
            if (_fadingRect == null || !IsInstanceValid(_fadingRect))
            {
                _fadingRect = new TextureRect()
                {
                    Name = "CrossFadeRect",
                    Position = Vector2.Zero,
                    Size = TextureParams.size,
                    RotationDegrees = 0f,
                    Scale = Vector2.One,
                    ExpandMode = ExpandModeEnum.KeepSize,
                    StretchMode = StretchModeEnum.KeepAspectCentered,
                    Modulate = Colors.Transparent,
                    PivotOffset = PivotOffset
                };
                AddChild(_fadingRect);
            }
        }


        
    }
}