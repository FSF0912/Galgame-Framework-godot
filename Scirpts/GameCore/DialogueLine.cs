using System.Collections.Generic;
using Godot;

namespace VisualNovel
{

    public class DialogueLine : IDialogueCommand
    {
        public List<IDialogueCommand> Commands = new List<IDialogueCommand>();

        public DialogueLine(List<IDialogueCommand> commands)
        {
            Commands = new (commands);
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

    /*public interface IAnimative
    {
        public TextureAnimator animator { get; set; }
        public void AddMove(Vector2 targetPosition, float duration, bool isRelative = false, Tween.TransitionType transitionType = Tween.TransitionType.Linear, Tween.EaseType easeType = Tween.EaseType.InOut);
        public void AddMoveImmediately(Vector2 targetPosition, bool isRelative = false);
        public void AddRotate(float rotationDegrees, float duration, bool isLocal = true, Tween.TransitionType transitionType = Tween.TransitionType.Linear, Tween.EaseType easeType = Tween.EaseType.InOut);
        public void AddRotateImmediately(float rotationDegrees, bool isLocal = true);
        public void AddScale(Vector2 targetScale, float duration, Tween.TransitionType transitionType = Tween.TransitionType.Linear, Tween.EaseType easeType = Tween.EaseType.InOut);
        public void AddScaleImmediately(Vector2 targetScale);
        public void AddShake(float intensity, float duration, float frequency = 10, Tween.TransitionType transitionType = Tween.TransitionType.Linear, Tween.EaseType easeType = Tween.EaseType.InOut);
        public void AddColorTint(Color targetColor, float duration, Tween.TransitionType transitionType = Tween.TransitionType.Linear, Tween.EaseType easeType = Tween.EaseType.InOut);
        public void AddColorTintImmediately(Color targetColor);
        public void AddFade(float targetAlpha, float duration, Tween.TransitionType transitionType = Tween.TransitionType.Linear, Tween.EaseType easeType = Tween.EaseType.InOut);
        public void AddFadeImmediately(float targetAlpha);
        public void CompleteAnimations();
    }*/

    public struct SpeakerLine : IDialogueCommand
    {
        public string SpeakerName;
        public string SpeakContext;

        public SpeakerLine(string SpeakerName, string SpeakContext)
        {
            this.SpeakContext = SpeakContext;
            this.SpeakerName = SpeakerName;
            CheckContent();
        }

        public void Execute(DialogueManager dm)
        {
            dm.SpeakerNameLabel.Text = SpeakerName;
            dm.typeWriter.TypeText(SpeakContext);
        }

        public void Interrupt(DialogueManager dm)
        {
            dm.typeWriter.Interrupt();
        }

        public void Skip(DialogueManager dm)
        {
            dm.typeWriter.TypeText(SpeakContext, true);
            dm.SpeakerNameLabel.Text = SpeakerName;
        }

        private void CheckContent()
        {
            if (string.IsNullOrEmpty(SpeakContext))
            {
                SpeakContext = "\u3000";
            }
            if (string.IsNullOrEmpty(SpeakerName))
            {
                SpeakerName = "\u3000";
            }
        }
    }

    public struct TextureLine : IDialogueCommand
    {
        public enum TextureMode { Switch, Clear, Delete }

        public int ID;
        public TextureMode textureMode;
        public string targetTexturePath;
        public float fadeDuration = -1.0f;

        public TextureLine(int id, TextureMode mode, string path = null, float fadeDuration = -1.0f)
        {
            ID = id;
            textureMode = mode;
            targetTexturePath = path;
            this.fadeDuration = fadeDuration <= 0 ? GlobalSettings.AnimationDefaultTime : fadeDuration; 
        }

        public void Execute(DialogueManager dm)
        {
            Texture2D targetTexture;
            
            if (!string.IsNullOrEmpty(targetTexturePath))
            {
                targetTexture = ResourceLoader.Load<Texture2D>(targetTexturePath);
            }
            else if(textureMode == TextureMode.Switch)
            {
                targetTexture = null;
            }
            else return;

            if (dm.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                switch (textureMode)
                {
                    case TextureMode.Switch:
                        targetRef.SetTextureWithFade(targetTexture, fadeDuration, immediate:false, ZIndex:ID);
                        break;
                    case TextureMode.Clear:
                        targetRef.ClearTexture(fadeDuration);
                        break;
                    case TextureMode.Delete:
                        dm.SceneActiveTextures.Remove(ID);
                        targetRef.ClearTexture(fadeDuration, deleteAfterFade:true);
                        break;
                    }
            }
            else
            {
                if (textureMode == TextureMode.Delete || textureMode == TextureMode.Clear) return;
                dm.CreateTexture(ID, fadeDuration, defaultTex:targetTexture);
            }
        }

        public void Interrupt(DialogueManager dm)
        {
            if (dm.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                targetRef.CompleteFade();
            }
        }

        public void Skip(DialogueManager dm)
        {
            Texture2D targetTexture = null;
            if (!string.IsNullOrEmpty(targetTexturePath))
            {
                targetTexture = ResourceLoader.Load<Texture2D>(targetTexturePath);
            }
            
            if (dm.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                switch (textureMode)
                {
                    case TextureMode.Switch:
                        targetRef.SetTextureWithFade(targetTexture, immediate:true);
                        break;
                    case TextureMode.Clear:
                        targetRef.SetTextureWithFade(null, immediate:true);
                        break;
                    case TextureMode.Delete:
                        dm.SceneActiveTextures.Remove(ID);
                        targetRef.QueueFree();
                        break;
                }
            }
            else
            {
                if (textureMode == TextureMode.Delete || textureMode == TextureMode.Clear) return;
                dm.CreateTexture(ID, 0, defaultTex:targetTexture, immediate:true);
            }
        }
    }

    public struct TextureAnimationLine : IDialogueCommand
    {
        public enum AnimationType : byte { Move, Rotate, Scale, Shake, ColorTint, Fade, }
        public int ID;
        public AnimationType animationType;

        public float duration;
        public Vector2? targetVector;
        public bool? isRelative;
        public bool? isLocal;
        public Color? targetColor;
        public float? Alpha = 1;
        public float? RotationDegrees;
        public float? intensity;
        public float? frequency;
        public Tween.TransitionType transitionType;
        public Tween.EaseType easeType;

        public TextureAnimationLine(
            int id,
            AnimationType animationType,
            float duration,
            Vector2? targetVector = null,
            bool? isRelative = true,
            bool? isLocal = true,
            Color? targetColor = null,
            float? alpha = 1,
            float? rotationDegrees = 0,
            float? intensity = 5,
            float? frequency = 10,
            Tween.TransitionType transitionType = Tween.TransitionType.Linear,
            Tween.EaseType easeType = Tween.EaseType.InOut
        )
        {
            ID = id;
            this.animationType = animationType;
            this.duration = duration;
            this.targetVector = targetVector;
            this.isRelative = isRelative;
            this.isLocal = isLocal;
            this.targetColor = targetColor;
            this.Alpha = alpha;
            this.RotationDegrees = rotationDegrees;
            this.intensity = intensity;
            this.frequency = frequency;
            this.transitionType = transitionType;
            this.easeType = easeType;
        }

        public void Execute(DialogueManager dm)
        {
            if (dm.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                switch (animationType)
                {
                    case AnimationType.Move:
                        targetRef.Animator.AddMove(targetVector ?? Vector2.Zero, duration, isRelative ?? false, transitionType, easeType);
                        break;
                    case AnimationType.Rotate:
                        targetRef.Animator.AddRotate(RotationDegrees ?? 0, duration, isLocal ?? true, transitionType, easeType);
                        break;
                    case AnimationType.Scale:
                        targetRef.Animator.AddScale(targetVector ?? Vector2.Zero, duration, transitionType, easeType);
                        break;
                    case AnimationType.Shake:
                        targetRef.Animator.AddShake(intensity ?? 5, duration, frequency ?? 10, transitionType, easeType);
                        break;
                    case AnimationType.ColorTint:
                        targetRef.Animator.AddColorTint(targetColor ?? Colors.White, duration, transitionType, easeType);
                        break;
                    case AnimationType.Fade:
                        targetRef.Animator.AddFade(Alpha ?? 1, duration, transitionType, easeType);
                        break;
                }
            }
        }

        public void Interrupt(DialogueManager dm)
        {
            if (dm.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                targetRef.Animator.CompleteAnimations();
            }
        }

        public void Skip(DialogueManager dm)
        {
            if (dm.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                switch (animationType)
                {
                    case AnimationType.Shake:
                    case AnimationType.Move:
                        targetRef.Animator.AddMoveImmediately(targetVector ?? Vector2.Zero, isRelative ?? false);
                        break;
                    case AnimationType.Rotate:
                        targetRef.Animator.AddRotateImmediately(RotationDegrees ?? 0, isLocal ?? true);
                        break;
                    case AnimationType.Scale:
                        targetRef.Animator.AddScaleImmediately(targetVector ?? Vector2.Zero);
                        break;
                    case AnimationType.ColorTint:
                        targetRef.Animator.AddColorTintImmediately(targetColor ?? Colors.White);
                        break;
                    case AnimationType.Fade:
                        targetRef.Animator.AddFadeImmediately(Alpha ?? 1);
                        break;
                }
            }
        }
    }

    public struct Audioline : IDialogueCommand
    {
        public enum AudioType { BGM, Voice, SE }
        public enum AudioPlayType { Play, Stop} 

        public AudioType audioType;
        public AudioPlayType audioPlayType;
        public string audioPath;
        public bool loop = false;
        public bool smoothStop = false;
        public float fadeDuration = -1.0f;

        public Audioline(AudioType type, AudioPlayType audioPlayType, string path, bool loop = false, bool smoothStop = false, float fadeDuration = -1.0f)
        {
            audioType = type;
            this.audioPlayType = audioPlayType;
            audioPath = path;
            this.loop = loop;
            this.smoothStop = smoothStop;
            this.fadeDuration = fadeDuration;
        }

        public void Execute(DialogueManager dm)
        {
            switch (audioType)
            {
                case AudioType.BGM:
                    if (audioPlayType == AudioPlayType.Play)
                    {
                        dm.PlayBGM(audioPath, loop);
                    }
                    else if (audioPlayType == AudioPlayType.Stop)
                    {
                        dm.StopBGM(smoothStop, fadeDuration);
                    }
                    break;

                case AudioType.Voice:
                    if (audioPlayType == AudioPlayType.Play)
                    {
                        dm.PlayVoice(audioPath, loop);
                    }
                    break;

                case AudioType.SE:
                    if (audioPlayType == AudioPlayType.Play)
                    {
                        dm.PlaySE(audioPath, loop);
                    }
                    break;
            }
        }

        public void Interrupt(DialogueManager dm)
        {
            if (audioType == AudioType.Voice && GlobalSettings.SkipVoice)
            {
                dm.StopVoice();
            }
        }

        public void Skip(DialogueManager dm)
        {
            if (audioType == AudioType.BGM)
            {
                if (audioPlayType == AudioPlayType.Play)
                {
                    dm.PlayBGM(audioPath, loop);
                }
                else
                {
                    dm.StopBGM();
                }
            }
        }
    }

    
}