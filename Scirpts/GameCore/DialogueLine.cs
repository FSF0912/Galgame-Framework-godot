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

    public struct TextureAnimationLine : IDialogueCommand
    {
        public int ID;

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

    public struct Audioline : IDialogueCommand
    {
        public enum AudioType { BGM, Voice, SE }

        public AudioType audioType;
        public string audioPath;
        public bool loop = false;
        public bool smoothStop = false;
        public float fadeDuration = -1.0f;

        public Audioline(AudioType type, string path, bool loop = false, bool smoothStop = false, float fadeDuration = -1.0f)
        {
            audioType = type;
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
                    dm.PlayBGM(audioPath, loop);
                    break;
                case AudioType.Voice:
                    dm.PlayVoice(audioPath, loop);
                    break;
                case AudioType.SE:
                    dm.PlaySE(audioPath, loop);
                    break;
            }
        }

        public void Interrupt(DialogueManager dm)
        {
            switch (audioType)
            {
                case AudioType.BGM:
                    dm.StopBGM(smoothStop, fadeDuration);
                    break;
                case AudioType.Voice:
                    if (GlobalSettings.SkipVoice)
                        dm.StopVoice();
                    break;
                case AudioType.SE:
                    dm.StopSE();
                    break;
            }
        }

        public void Skip(DialogueManager dm) { }
    }

    
}