using System.Collections.Generic;
using Godot;

namespace VisualNovel
{

    public class DialogueLine : IDialogueCommand
    {
        public List<IDialogueCommand> Commands = new List<IDialogueCommand>();

        public DialogueLine(List<IDialogueCommand> commands)
        {
            Commands = commands;
        }

        public void Execute(DialogueManager dm)
        {
            foreach (var command in Commands)
            {
                command.Execute(dm);
            }
        }

        public void Interrupt(DialogueManager dm)
        {
            foreach (var command in Commands)
            {
                command.Interrupt(dm);
            }
        }

        public void Skip(DialogueManager dm)
        {
            foreach (var command in Commands)
            {
                command.Skip(dm);
            }
        }
    }

    public interface IDialogueCommand
    {
        public void Execute(DialogueManager dm);
        public void Skip(DialogueManager dm);
        public void Interrupt(DialogueManager dm);
    }

    public interface AnimationCommand : IDialogueCommand
    {
        public int ID { get; set; }
        Tween Tween { get; set; }
        CrossFadeTextureRect Target { get; set; }
    }

    public struct SpeakerLine : IDialogueCommand
    {
        public string SpeakerName;
        public string SpeakContext;

        public SpeakerLine(string SpeakerName, string SpeakContext)
        {
            this.SpeakContext = SpeakContext;
            this.SpeakerName = SpeakerName;
        }

        public void Execute(DialogueManager dm)
        {
            dm.SpeakerNameLabel.Name = SpeakerName;
            dm.typeWriter.TypeText(SpeakContext);
        }

        public void Interrupt(DialogueManager dm)
        {
            dm.typeWriter.Interrupt();
        }

        public void Skip(DialogueManager dm)
        {
            dm.typeWriter.TypeText(SpeakContext, true);
            dm.SpeakerNameLabel.Name = SpeakerName;
        }
    }

    public struct TextureLine : IDialogueCommand
    {
        public enum TextureMode { Switch, Clear, Delete }

        public int ID;
        public TextureMode textureMode;
        public Texture2D targetTexture;

        public TextureLine(int id, TextureMode mode, Texture2D texture = null)
        {
            ID = id;
            textureMode = mode;
            targetTexture = texture;
        }

        public void Execute(DialogueManager dm)
        {
            if (targetTexture == null)
            {
                GD.PrintErr("TextureLine targetTexture is null, ID: " + ID);
                return;
            }

            if (dm.SceneActiveTextures.TryGetValue(ID, out var target))
            {
                switch (textureMode)
                {
                    case TextureMode.Switch:
                        target.SetTextureWithFade(targetTexture);
                        break;
                    case TextureMode.Clear:
                        target.ClearTexture();
                        break;
                    case TextureMode.Delete:
                        dm.SceneActiveTextures.Remove(ID);
                        target.ClearTexture(true);
                        break;
                }
            }
            else
            {
                if (textureMode == TextureMode.Delete || textureMode == TextureMode.Clear) return;
                dm.CreateTexture(ID, targetTexture);
            }
        }

        public void Interrupt(DialogueManager dm)
        {
            if (dm.SceneActiveTextures.TryGetValue(ID, out var target))
            {
                target.CompleteFade();
            }
        }

        public void Skip(DialogueManager dm)
        {
            if (dm.SceneActiveTextures.TryGetValue(ID, out var target))
            {
                switch (textureMode)
                {
                    case TextureMode.Switch:
                        target.SetTextureWithFade(targetTexture, immediate:true);
                        break;
                    case TextureMode.Clear:
                        target.SetTextureWithFade(CrossFadeTextureRect.EmptyTex, immediate:true);
                        break;
                    case TextureMode.Delete:
                        dm.SceneActiveTextures.Remove(ID);
                        target.QueueFree();
                        break;
                }
            }
            else
            {
                if (textureMode == TextureMode.Delete || textureMode == TextureMode.Clear) return;
                dm.CreateTexture(ID, targetTexture, true);
            }
        }
    }

    public struct Audioline : IDialogueCommand
    {
        public enum AudioType { BGM, Voice, SE }

        public AudioType audioType;
        public string audioPath;

        public Audioline(AudioType type, string path)
        {
            audioType = type;
            audioPath = path;
        }

        public void Execute(DialogueManager dm)
        {
            switch (audioType)
            {
                case AudioType.BGM:
                    dm.PlayBGM(audioPath);
                    break;
                case AudioType.Voice:
                    dm.PlayVoice(audioPath);
                    break;
                case AudioType.SE:
                    dm.PlaySE(audioPath);
                    break;
            }
        }

        public void Interrupt(DialogueManager dm)
        {
            if (audioType == AudioType.Voice && GlobalSettings.SkipVoice) dm.StopVoice();
        }

        public void Skip(DialogueManager dm) { }
    }

    #region Animation
    public struct SingleAnimation : AnimationCommand
    {
        public int ID { get; set; }
        public Tween Tween { get; set; }
        public CrossFadeTextureRect Target { get; set; }

        public float FadeTime;
        public float TargetFading;

        public SingleAnimation(int id, float targetFading)
        {
            ID = id;
            FadeTime = GlobalSettings.AnimationDefaultTime;
            TargetFading = targetFading;
        }

        public SingleAnimation(int id, float fadeTime, float targetFading)
        {
            ID = id;
            FadeTime = fadeTime;
            TargetFading = targetFading;
        }

        public void Execute(DialogueManager dm)
        {
            if (TryGetTarget(dm))
            {
                Tween = Target.CreateTween();
                Tween.TweenProperty(Target, "modulate:a", TargetFading, FadeTime).SetTrans(Tween.TransitionType.Linear);
            }

        }

        public void Skip(DialogueManager dm)
        {
            Interrupt(dm);
        }

        public void Interrupt(DialogueManager dm)
        {
            Tween?.Kill();
            if (TryGetTarget(dm))
            {
                Target.Modulate = new Color(Target.Modulate.R, Target.Modulate.G, Target.Modulate.B, TargetFading);
            }
        }

        bool TryGetTarget(DialogueManager dm)
        {
            if (Target == null && dm.SceneActiveTextures.TryGetValue(ID, out var t))
            {
                Target = t;
                return true;
            }
            return Target != null;
        }
    }
    

    public struct MoveAnimation : AnimationCommand
    {
        public int ID { get; set; }
        public Tween Tween { get; set; }
        public CrossFadeTextureRect Target { get; set; }

        public Vector2 TargetPosition;
        public float Duration;
        public Tween.TransitionType TransitionType;

        public MoveAnimation(int id, Vector2 position, float duration = -1, 
                            Tween.TransitionType transition = Tween.TransitionType.Quad)
        {
            ID = id;
            TargetPosition = position;
            Duration = duration > 0 ? duration : GlobalSettings.AnimationDefaultTime;
            TransitionType = transition;
            Tween = null;
            Target = null;
        }

        public void Execute(DialogueManager dm)
        {
            if (TryGetTarget(dm))
            {
                Tween = Target.CreateTween();
                Tween.TweenProperty(Target, "position", TargetPosition, Duration)
                    .SetTrans(TransitionType);
            }
        }

        public void Skip(DialogueManager dm)
        {
            Interrupt(dm);
        }

        public void Interrupt(DialogueManager dm)
        {
            Tween?.Kill();
            if (TryGetTarget(dm))
            {
                Target.Position = TargetPosition;
            }
        }

        bool TryGetTarget(DialogueManager dm)
        {
            if (Target == null && dm.SceneActiveTextures.TryGetValue(ID, out var t))
            {
                Target = t;
                return true;
            }
            return Target != null;
        }
    }

    public struct ScaleAnimation : AnimationCommand
    {
        public int ID { get; set; }
        public Tween Tween { get; set; }
        public CrossFadeTextureRect Target { get; set; }

        public Vector2 TargetScale;
        public float Duration;
        public Tween.EaseType EaseType;

        public ScaleAnimation(int id, Vector2 scale, float duration = -1, 
                            Tween.EaseType ease = Tween.EaseType.InOut)
        {
            ID = id;
            TargetScale = scale;
            Duration = duration > 0 ? duration : GlobalSettings.AnimationDefaultTime;
            EaseType = ease;
            Tween = null;
            Target = null;
        }

        public void Execute(DialogueManager dm)
        {
            if (TryGetTarget(dm))
            {
                Tween = Target.CreateTween();
                Tween.TweenProperty(Target, "scale", TargetScale, Duration)
                    .SetEase(EaseType);
            }
        }

        public void Skip(DialogueManager dm)
        {
            Interrupt(dm);
        }

        public void Interrupt(DialogueManager dm)
        {
            Tween?.Kill();
            if (TryGetTarget(dm))
            {
                Target.Scale = TargetScale;
            }
        }

        bool TryGetTarget(DialogueManager dm)
        {
            if (Target == null && dm.SceneActiveTextures.TryGetValue(ID, out var t))
            {
                Target = t;
                return true;
            }
            return Target != null;
        }
    }

    public struct ColorTintAnimation : AnimationCommand
    {
        public int ID { get; set; }
        public Tween Tween { get; set; }
        public CrossFadeTextureRect Target { get; set; }

        public Color TargetColor;
        public float Duration;
        public Tween.TransitionType TransitionType;

        public ColorTintAnimation(int id, Color color, float duration = -1, 
                                Tween.TransitionType transition = Tween.TransitionType.Linear)
        {
            ID = id;
            TargetColor = color;
            Duration = duration > 0 ? duration : GlobalSettings.AnimationDefaultTime;
            TransitionType = transition;
            Tween = null;
            Target = null;
        }

        public void Execute(DialogueManager dm)
        {
            if (TryGetTarget(dm))
            {
                Tween = Target.CreateTween();
                Tween.TweenProperty(Target, "modulate", TargetColor, Duration)
                    .SetTrans(TransitionType);
            }
        }

        public void Skip(DialogueManager dm)
        {
            Interrupt(dm);
        }

        public void Interrupt(DialogueManager dm)
        {
            Tween?.Kill();
            if (TryGetTarget(dm))
            {
                Target.Modulate = TargetColor;
            }
        }

        bool TryGetTarget(DialogueManager dm)
        {
            if (Target == null && dm.SceneActiveTextures.TryGetValue(ID, out var t))
            {
                Target = t;
                return true;
            }
            return Target != null;
        }
    }

    public struct RotationAnimation : AnimationCommand
    {
        public int ID { get; set; }
        public Tween Tween { get; set; }
        public CrossFadeTextureRect Target { get; set; }

        public float TargetRotation_Degrees;
        public float Duration;
        public Tween.EaseType EaseType;

        public RotationAnimation(int id, float rotationDegrees, float duration = -1, 
                                Tween.EaseType ease = Tween.EaseType.InOut)
        {
            ID = id;
            TargetRotation_Degrees = rotationDegrees;
            Duration = duration > 0 ? duration : GlobalSettings.AnimationDefaultTime;
            EaseType = ease;
            Tween = null;
            Target = null;
        }

        public void Execute(DialogueManager dm)
        {
            if (TryGetTarget(dm))
            {
                Tween = Target.CreateTween();
                Tween.TweenProperty(Target, "rotation_degrees", TargetRotation_Degrees, Duration)
                    .SetEase(EaseType);
            }
        }

        public void Skip(DialogueManager dm)
        {
            Interrupt(dm);
        }

        public void Interrupt(DialogueManager dm)
        {
            Tween?.Kill();
            if (TryGetTarget(dm))
            {
                Target.Rotation = TargetRotation_Degrees;
            }
        }

        bool TryGetTarget(DialogueManager dm)
        {
            if (Target == null && dm.SceneActiveTextures.TryGetValue(ID, out var t))
            {
                Target = t;
                return true;
            }
            return Target != null;
        }
    }

    public struct ShakeAnimation : AnimationCommand
    {
        public int ID { get; set; }
        public Tween Tween { get; set; }
        public CrossFadeTextureRect Target { get; set; }

        public float Intensity;
        public float Frequency;
        public float Duration;
        private Vector2 OriginalPosition;

        public ShakeAnimation(int id, float intensity = 10.0f, float frequency = 20.0f, float duration = 0.5f)
        {
            ID = id;
            Intensity = intensity;
            Frequency = frequency;
            Duration = duration;
            Tween = null;
            Target = null;
            OriginalPosition = Vector2.Zero;
        }

        public void Execute(DialogueManager dm)
        {
            if (TryGetTarget(dm))
            {
                OriginalPosition = Target.Position;
                Tween = Target.CreateTween();
                
                int loops = (int)(Duration * Frequency);
                Tween.SetLoops(loops);
                
                for (int i = 0; i < 4; i++)
                {
                    Tween.TweenProperty(Target, "position", 
                        OriginalPosition + new Vector2(
                            (float)GD.RandRange(-Intensity, Intensity), 
                            (float)GD.RandRange(-Intensity, Intensity)
                        ), 
                        0.9f / Frequency
                    );
                }
                
                Tween.TweenProperty(Target, "position", OriginalPosition, 0.1f / Frequency);
            }
        }

        public void Skip(DialogueManager dm)
        {
            Interrupt(dm);
        }

        public void Interrupt(DialogueManager dm)
        {
            Tween?.Kill();
            if (TryGetTarget(dm) && OriginalPosition != Vector2.Zero)
            {
                Target.Position = OriginalPosition;
            }
        }

        bool TryGetTarget(DialogueManager dm)
        {
            if (Target == null && dm.SceneActiveTextures.TryGetValue(ID, out var t))
            {
                Target = t;
                return true;
            }
            return Target != null;
        }
    }

    public struct CompositeAnimation : AnimationCommand
    {
        public int ID { get; set; }
        public Tween Tween { get; set; }
        public CrossFadeTextureRect Target { get; set; }
        
        public List<AnimationCommand> Animations = new List<AnimationCommand>();
        
        public CompositeAnimation(int id, params AnimationCommand[] animations)
        {
            ID = id;
            Animations = [.. animations];
            Tween = null;
            Target = null;
        }
        
        public CompositeAnimation Add(AnimationCommand animation)
        {
            Animations.Add(animation);
            return this;
        }

        public void Execute(DialogueManager dm)
        {
            if (!TryGetTarget(dm)) return;
            
            Tween = Target.CreateTween();
            Tween.SetParallel();
            
            foreach (var anim in Animations)
            {
                SetAnimationTarget(anim, ID, Target);
                anim.ID = ID;
                anim.Target = Target;
                anim.Execute(dm);
            }
        }

        public void Skip(DialogueManager dm)
        {
            foreach (var anim in Animations)
            {
                anim.Interrupt(dm);
            }
        }

        public void Interrupt(DialogueManager dm)
        {
            Tween?.Kill();
        }
        
        private void SetAnimationTarget(AnimationCommand anim, int id, CrossFadeTextureRect target)
        {
            // 使用反射设置目标（实际项目中应考虑添加接口方法）
            var type = anim.GetType();
            type.GetField("ID")?.SetValue(anim, id);
            type.GetProperty("Target")?.SetValue(anim, target);
        }

        bool TryGetTarget(DialogueManager dm)
        {
            if (Target == null && dm.SceneActiveTextures.TryGetValue(ID, out var t))
            {
                Target = t;
                return true;
            }
            return Target != null;
        }
    }

    #endregion Animation
}