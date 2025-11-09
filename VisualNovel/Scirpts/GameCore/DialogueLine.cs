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

        public void Execute()
        {
            foreach (var command in Commands)
            {
                command.Execute();
            }
        }

        public void Interrupt()
        {
            foreach (var command in Commands)
            {
                command.Interrupt();
            }
        }

        public void Skip()
        {
            foreach (var command in Commands)
            {
                command.Skip();
            }
        }
    }

    public interface IDialogueCommand
    {
        public void Execute();
        public void Skip();
        public void Interrupt();
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

        public void Execute()
        {
            var dm = DialogueManager.Instance;
            dm.SpeakerNameLabel.Text = SpeakerName;
            dm.typeWriter.TypeText(SpeakContext);
        }

        public void Interrupt()
        {
            DialogueManager.Instance.typeWriter.Interrupt();
        }

        public void Skip()
        {
            var dm = DialogueManager.Instance;
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
        public VNTextureRect.TranslationType translationType = VNTextureRect.TranslationType.CrossFade;
        public string targetTexturePath;
        public bool immediate = false;
        public float fadeDuration = -1.0f;

        public TextureLine(int id, TextureMode mode,
        VNTextureRect.TranslationType translationType = VNTextureRect.TranslationType.CrossFade,
        string path = null, float fadeDuration = -1.0f, bool immediate = false)
        {
            ID = id;
            textureMode = mode;
            this.translationType = translationType;
            targetTexturePath = path;
            this.fadeDuration = fadeDuration <= 0 ? GlobalSettings.AnimationDefaultTime : fadeDuration; 
            this.immediate = immediate;
        }

        public void Execute()
        {
            var dm = DialogueManager.Instance;
            if (dm.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                switch (textureMode)
                {
                    case TextureMode.Switch:
                        targetRef.SetTexture(path: targetTexturePath, setType: translationType, duration: fadeDuration, zIndex : ID);
                        break;
                    case TextureMode.Clear:
                        targetRef.ClearTexture(fadeDuration, immediate: immediate);
                        break;
                    case TextureMode.Delete:
                        dm.SceneActiveTextures.Remove(ID);
                        targetRef.ClearTexture(fadeDuration, deleteAfterFade: true, immediate: immediate);
                        break;
                }
            }
            else
            {
                if (textureMode == TextureMode.Delete || textureMode == TextureMode.Clear) return;
                dm.CreateTextureRect(ID, fadeDuration, defaultTexPath: targetTexturePath);
                Execute();
            }
        }

        public void Interrupt()
        {
            var dm = DialogueManager.Instance;
            if (dm.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                targetRef.InterruptTranslation();
            }
        }

        public void Skip()
        {
            var dm = DialogueManager.Instance;
            if (dm.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                switch (textureMode)
                {
                    case TextureMode.Switch:
                        targetRef.SetTexture(targetTexturePath, VNTextureRect.TranslationType.Immediate, zIndex: ID);
                        break;
                    case TextureMode.Clear:
                        targetRef.ClearTexture(immediate: true);
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
                dm.CreateTextureRect(ID, 0, defaultTexPath: targetTexturePath, translationType: VNTextureRect.TranslationType.Immediate);
                Skip();
            }
        }
    }

    

    public struct TextureAnimationLine : IDialogueCommand
    {
        public enum AnimationType : byte { Move, Rotate, Scale, Shake, ColorTint, Fade, Default }
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
        public bool isParallel = true;

        public TextureAnimationLine(
            int id,
            AnimationType animationType,
            float duration,
            bool isParallel =  true,
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
            this.isParallel = isParallel;
        }

        public void Execute()
        {
            var dm = DialogueManager.Instance;
            if (dm.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
            duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
            switch (animationType)
            {
                case AnimationType.Move:
                targetRef.Animator.AddMove(
                    value: targetVector ?? Vector2.Zero,
                    duration: duration,
                    parallel: isParallel,
                    trans: transitionType,
                    ease: easeType);
                break;
                case AnimationType.Rotate:
                targetRef.Animator.AddRotate(
                    degrees: RotationDegrees ?? 0,
                    duration: duration,
                    isLocal: isLocal ?? true,
                    trans: transitionType,
                    ease: easeType,
                    parallel: isParallel);
                break;
                case AnimationType.Scale:
                targetRef.Animator.AddScale(
                    scale: targetVector ?? Vector2.Zero,
                    duration: duration,
                    trans: transitionType,
                    ease: easeType,
                    parallel: isParallel);
                break;
                case AnimationType.Shake:
                targetRef.Animator.AddShake(
                    intensity: intensity ?? 5,
                    duration: duration,
                    frequency: frequency ?? 10,
                    trans: transitionType,
                    ease: easeType,
                    parallel: isParallel);
                break;
                case AnimationType.ColorTint:
                targetRef.Animator.AddColorTint(
                    target: targetColor ?? Colors.White,
                    duration: duration,
                    trans: transitionType,
                    ease: easeType,
                    parallel: isParallel);
                break;
                case AnimationType.Fade:
                targetRef.Animator.AddFade(
                    alpha: Alpha ?? 1,
                    duration: duration,
                    trans: transitionType,
                    ease: easeType,
                    parallel: isParallel);
                break;
            }
            }
        }

        public void Interrupt()
        {
            if (DialogueManager.Instance.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                targetRef.Animator.CompleteAnimations();
            }
        }

        public void Skip()
        {
            if (DialogueManager.Instance.SceneActiveTextures.TryGetValue(ID, out var targetRef))
            {
                switch (animationType)
                {
                    case AnimationType.Shake:
                    case AnimationType.Move:
                        targetRef.Animator.AddMoveImmediately(targetVector ?? Vector2.Zero);
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
        public enum AudioType { BGM, Voice, SFX }
        public enum AudioPlayType { Play, Stop } 

        public AudioType audioType;
        public AudioPlayType audioPlayType;
        public string audioPath;
        public bool loop = false;
        public Vector2 normalizedPosition = Vector2.Zero;
        public float voice_volumeDb = 0f;
        public float duration = -1.0f;

        public Audioline(AudioType type, AudioPlayType audioPlayType,
        string path,
        bool loop = false,
        Vector2 normalizedPosition = default,
        float voice_volumeDb = 0f,
        float fadeDuration = -1.0f)
        {
            audioType = type;
            this.audioPlayType = audioPlayType;
            audioPath = path;
            this.loop = loop;
            this.normalizedPosition = normalizedPosition;
            this.voice_volumeDb = voice_volumeDb;
            this.duration = fadeDuration;
        }

        public void Execute()
        {
            var am = AudioManager.Instance;
            switch (audioType)
            {
                case AudioType.BGM:
                    if (audioPlayType == AudioPlayType.Play)
                    {
                        am.PlayBGM(audioPath, duration, loop : loop);
                    }
                    else if (audioPlayType == AudioPlayType.Stop)
                    {
                        am.StopBGM(duration);
                    }
                    break;

                case AudioType.Voice:
                    if (audioPlayType == AudioPlayType.Play)
                    {
                        am.PlayVoice(audioPath, normalizedPosition, voice_volumeDb);
                    }
                    break;

                case AudioType.SFX:
                    if (audioPlayType == AudioPlayType.Play)
                    {
                        am.PlaySFX(audioPath);
                    }
                    break;
            }
        }

        public void Interrupt()
        {
            if (audioType == AudioType.Voice && GlobalSettings.SkipVoice)
            {
                AudioManager.Instance.StopVoice();
            }
        }

        public void Skip()
        {
            var am = AudioManager.Instance;
            if (audioType == AudioType.BGM)
            {
                if (audioPlayType == AudioPlayType.Play)
                {
                    am.PlayBGM(audioPath, 0, loop);
                }
                else
                {
                    am.StopBGM();
                }
            }
        }
    }

    
}