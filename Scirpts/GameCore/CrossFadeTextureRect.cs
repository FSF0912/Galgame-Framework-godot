using Godot;
using System;

public partial class CrossFadeTextureRect : TextureRect
{
    float FadeDuration;
    
    private ShaderMaterial _shaderMat;
    private Tween _tween;
    private Texture2D _nextTex;
    private bool _willDeleted;

    public static Texture2D EmptyTex { get; private set; }

    public CrossFadeTextureRect()
    {
        CheckEmptyTexture();
        Texture = EmptyTex;
    }

    public CrossFadeTextureRect(Texture2D target)
    {
        Texture = target;
    }

    public override void _Ready()
    {
        _shaderMat = new ShaderMaterial();
        _shaderMat.Shader = new Shader()
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
        };
        CheckEmptyTexture();

        _shaderMat.SetShaderParameter("current_tex", Texture ?? EmptyTex);
        _shaderMat.SetShaderParameter("next_tex", EmptyTex);
        _shaderMat.SetShaderParameter("progress", 0.0f);

        Material = _shaderMat;
        FadeDuration = global::GlobalSettings.AnimationDefaultTime;
    }
    
    private static void CheckEmptyTexture()
    {
        if (EmptyTex == null || !IsInstanceValid(EmptyTex))
        {
            CreateEmptyTexture();
        }
    }
    
    private static void CreateEmptyTexture()
    {
        Image img = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        img.Fill(Colors.Transparent);
        EmptyTex = ImageTexture.CreateFromImage(img);
    }
    
    /// <summary>
    /// 使用交叉淡化效果设置新纹理
    /// </summary>
    public void SetTextureWithFade(Texture2D newTexture, float duration, bool Immediately = false)
    {
        if (Immediately)
        {
            SetTextureImmediately(newTexture);
            return;
        }

        if (newTexture == null || Texture == newTexture || _willDeleted) return;
        
        ClearTween();
        CheckEmptyTexture();
        
        _shaderMat.SetShaderParameter("current_tex", Texture ?? EmptyTex);
        _shaderMat.SetShaderParameter("next_tex", newTexture);
        
        _nextTex = newTexture;
        
        _tween = CreateTween();
        
        _tween.SetEase(Tween.EaseType.Out);
        _tween.SetTrans(Tween.TransitionType.Linear);
        _tween.TweenMethod(Callable.From<float>(SetProgress), 0.0f, 1.0f, duration);
        
        _tween.Finished += OnTweenFinished;
    }

    public void SetTextureWithFade(Texture2D newTexture, bool Immediately = false)
    {
        if (Immediately)
        {
            SetTextureImmediately(newTexture);
            return;
        }

        if (newTexture == null || Texture == newTexture || _willDeleted) return;

        ClearTween();
        CheckEmptyTexture();

        _shaderMat.SetShaderParameter("current_tex", Texture ?? EmptyTex);
        _shaderMat.SetShaderParameter("next_tex", newTexture);
        
        _nextTex = newTexture;
        _tween = CreateTween();

        _tween.SetEase(Tween.EaseType.Out);
        _tween.SetTrans(Tween.TransitionType.Linear);
        _tween.TweenMethod(Callable.From<float>(SetProgress), 0.0f, 1.0f, FadeDuration);

        _tween.Finished += OnTweenFinished;
    }

    public void ClearTexture(bool delete = false, bool Immediately = false)
    {
        if (Immediately && delete)
        {
            QueueFree();
            return;
        }
        else if (Immediately)
        {
            SetTextureImmediately(EmptyTex);
            return;
        }

        if (Texture == EmptyTex || _willDeleted) return;

        ClearTween();
        CheckEmptyTexture();

        _shaderMat.SetShaderParameter("current_tex", Texture ?? EmptyTex);
        _shaderMat.SetShaderParameter("next_tex", EmptyTex);

        _nextTex = EmptyTex;
        _tween = CreateTween();

        _tween.SetEase(Tween.EaseType.Out);
        _tween.SetTrans(Tween.TransitionType.Linear);
        _tween.TweenMethod(Callable.From<float>(SetProgress), 0.0f, 1.0f, FadeDuration);

        _tween.Finished += OnTweenFinished;


        if (delete)
        {
            _willDeleted = true;
            _tween.Finished += Delete;
        }
    }

    private void SetTextureImmediately(Texture2D newTexture)
    {
        if (newTexture == null || Texture == newTexture) return;

        ClearTween();

        Texture = newTexture;
        _shaderMat.SetShaderParameter("current_tex", Texture);
        _shaderMat.SetShaderParameter("progress", 0.0f);
        
        _nextTex = null;
    }
    
    /// <summary>
    /// 立即完成当前淡化过渡
    /// </summary>
    public void CompleteFade()
    {
        if (IsTweenActive())
        {
            SetProgress(1.0f);
            OnTweenFinished();
        }
    }
    
    private void ClearTween()
    {
        if (IsTweenActive())
        {
            if (_tween.IsRunning())
            {
                _tween.Finished -= OnTweenFinished;
                _tween.Kill();
            }
        }
        _tween = null;
    }
    
    private bool IsTweenActive() => 
        _tween != null && IsInstanceValid(_tween);
    
    private void SetProgress(float value) => 
        _shaderMat.SetShaderParameter("progress", value);
    
    private void OnTweenFinished()
    {
        if (_nextTex == null) return;
        
        Texture = _nextTex;
        CheckEmptyTexture();
        
        _shaderMat.SetShaderParameter("current_tex", Texture ?? EmptyTex);
        _shaderMat.SetShaderParameter("next_tex", EmptyTex);
        _shaderMat.SetShaderParameter("progress", 0.0f);
        
        ClearTween();
    }

    private void Delete()
    {
        QueueFree();
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationPredelete)
        {
            ClearTween();
            _nextTex = null;
        }
    }
}