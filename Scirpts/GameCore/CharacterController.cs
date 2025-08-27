using Godot;

namespace VisualNovel
{
    public partial class CharacterController : CrossFadeTextureRect
    {
        private CrossFadeTextureRect _face;
        private CrossFadeTextureRect _effect;

        public CharacterController(TextureParams textureParams = default) : base(initParams: textureParams)
        {
            
        }

        public override void _Ready()
        {
            base._Ready();

            _face = new CrossFadeTextureRect(initParams: TextureParams, isChild: true)
            {
                Name = "Face",
                ZIndex = 1 // 确保脸部在身体之上
            };
            AddChild(_face);

            _effect = new CrossFadeTextureRect(initParams: TextureParams, isChild: true)
            {
                Name = "Effect",
                ZIndex = 2 // 确保特效在脸部之上
            };
        }

        #region  override Methods
        override public void SetTextureWithFade(Texture2D newTexture, float duration = -1, bool immediate = false, int ZIndex = 0)
        {
            throw new System.NotImplementedException("Use SetBody, SetFace, or SetEffect instead.");
        }

        override public void SetTextureWithFade(string newTexturePath, float duration = -1, bool immediate = false, int ZIndex = 0)
        {
            throw new System.NotImplementedException("Use SetBody, SetFace, or SetEffect instead.");
        }

        override public void ClearTexture(float duration, bool deleteAfterFade = false, bool immediate = false)
        {
            base.ClearTexture(duration, deleteAfterFade, immediate);
            _face.ClearTexture(duration, deleteAfterFade, immediate);
            _effect.ClearTexture(duration, deleteAfterFade, immediate);
        }

        override public void CompleteFade()
        {
            base.CompleteFade();
            _face.CompleteFade();
            _effect.CompleteFade();
        }
        #endregion

        public void SetBody(string chara_name, string chara_portrait_name, string chara_face_name, float duration = -1, bool immediate = false)
        {
            if (GlobalSettings.CharacterList.Find(c => c.SymbolizedName == chara_name) is CharacterMacro chara)
            {
                if (chara.TryGetPortrait(chara_portrait_name, out string portPath) && chara.TryGetFace(chara_face_name, out string facePath))
                {
                    base.SetTextureWithFade(portPath, duration, immediate);
                    _face.SetTextureWithFade(facePath, duration, immediate);
                }
                else
                {
                    GD.PrintErr($"Portrait or face '{chara_face_name}' not found for character '{chara_name}'.");
                }
            }
            else
            {
                GD.PrintErr($"Character '{chara_name}' not found in GlobalSettings.");
            }
        }

        public void SetFace(string chara_name, string chara_face_name, float duration = -1, bool immediate = false)
        {
            if (GlobalSettings.CharacterList.Find(c => c.SymbolizedName == chara_name) is CharacterMacro chara)
            {
                if (chara.TryGetFace(chara_face_name, out string facePath))
                {
                    _face.SetTextureWithFade(facePath, duration, immediate);
                }
                else
                {
                    GD.PrintErr($"Face '{chara_face_name}' not found for character '{chara_name}'.");
                }
            }
            else
            {
                GD.PrintErr($"Character '{chara_name}' not found in GlobalSettings.");
            }
        }
        
        public void SetEffect(string chara_name, string chara_effect_name, float duration = -1, bool immediate = false)
        {
            if (GlobalSettings.CharacterList.Find(c => c.SymbolizedName == chara_name) is CharacterMacro chara)
            {
                if (chara.TryGetEffect(chara_effect_name, out string effectPath))
                {
                    _effect.SetTextureWithFade(effectPath, duration, immediate);
                }
                else
                {
                    GD.PrintErr($"Effect '{chara_effect_name}' not found for character '{chara_name}'.");
                }
            }
            else
            {
                GD.PrintErr($"Character '{chara_name}' not found in GlobalSettings.");
            }
        }

    }
}