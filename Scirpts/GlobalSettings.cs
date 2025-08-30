using System.Collections.Generic;
using Godot;

namespace VisualNovel
{
    public class CharacterMacro
    {
        public readonly string SymbolizedName;
        readonly Dictionary<string, string> portraits = new Dictionary<string, string>();
        readonly Dictionary<string, string> faces = new Dictionary<string, string>();
        readonly Dictionary<string, string> effects = new Dictionary<string, string>();

        public CharacterMacro(string name,
        Dictionary<string, string> portraits,
        Dictionary<string, string> faces,
        Dictionary<string, string> effects)
        {
            SymbolizedName = name;
            this.portraits = portraits;
            this.faces = faces;
            this.effects = effects;
        }

        public bool TryGetPortrait(string key, out string valuePath)
        {
            return portraits.TryGetValue(key, out valuePath);
        }

        public bool TryGetFace(string key, out string valuePath)
        {
            return faces.TryGetValue(key, out valuePath);
        }

        public bool TryGetEffect(string key, out string valuePath)
        {
            return effects.TryGetValue(key, out valuePath);
        }
    }

    public static class GlobalSettings
    {
        public static bool SkipVoice = false;
        public static float AnimationDefaultTime = 0.3f;
        public static List<CharacterMacro> CharacterList = new List<CharacterMacro>()
        {

        };

        public static readonly Vector2 LeftPosition = new Vector2(0.25f, 1);
        public static readonly Vector2 CenterPosition = new Vector2(0.5f, 1);
        public static readonly Vector2 RightPosition = new Vector2(0.75f, 1);
    }
}