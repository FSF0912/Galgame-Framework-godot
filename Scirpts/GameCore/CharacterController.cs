using Godot;

namespace VisualNovel
{
    public partial class CharacterController : CrossFadeTextureRect
    {
        private CrossFadeTextureRect _face;
        private CrossFadeTextureRect _effect;
        public override void _Ready()
        {
            base._Ready();

            _face = new CrossFadeTextureRect(TextureParams)
            {
                Name = "Face",
                ZIndex = 1 // 确保脸部在身体之上
            };
            AddChild(_face);

            _effect = new CrossFadeTextureRect(TextureParams)
            {
                Name = "Effect",
                ZIndex = 2 // 确保特效在脸部之上
            };
        }

        
    }
}