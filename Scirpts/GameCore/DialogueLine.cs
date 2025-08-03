using System.Collections.Generic;
using Godot;
using VisualNovel;

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
        public int ID { get; }
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

        public TextureLine(int id, TextureMode mode, Texture2D texture)
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
                        target.SetTextureWithFade(targetTexture, true);
                        break;
                    case TextureMode.Clear:
                        target.SetTextureWithFade(CrossFadeTextureRect.EmptyTex, true);
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

    public struct AnimationLine : IDialogueCommand
    {
        public AnimationCommand[] animations;

        public AnimationLine(AnimationCommand[] animations)
        {
            this.animations = animations;
        }

        public void Execute(DialogueManager dm)
        {
            throw new System.NotImplementedException();
        }

        public void Interrupt(DialogueManager dm)
        {
            throw new System.NotImplementedException();
        }

        public void Skip(DialogueManager dm)
        {
            throw new System.NotImplementedException();
        }
    }

    public struct SingleAnimation : AnimationCommand
    {
        public int ID { get; }
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
            throw new System.NotImplementedException();
        }

        public void Skip(DialogueManager dm)
        {
            throw new System.NotImplementedException();
        }

        public void Interrupt(DialogueManager dm)
        {
            throw new System.NotImplementedException();
        }
    }

    #endregion Animation
}