using Godot;
using System;

namespace VisualNovel
{
    public static class VNUtility
    {
        public static readonly Vector2I DefaultScreenSize = new Vector2I(1920, 1080);
        
        public static Vector2 FitScreenSize(this Vector2 vector)
        {
            Vector2I screenSize = DisplayServer.ScreenGetSize();
            if (DefaultScreenSize == screenSize) return vector;

            return new Vector2(screenSize.X / DefaultScreenSize.X * vector.X,
                                screenSize.Y / DefaultScreenSize.Y * vector.Y);
        }
    }
}
