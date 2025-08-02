using System;
using Godot;

#pragma warning disable CS8981 // 该类型名称仅包含小写 ascii 字符。此类名称可能会成为该语言的保留值。
public partial class test : Node
#pragma warning restore CS8981 // 该类型名称仅包含小写 ascii 字符。此类名称可能会成为该语言的保留值。
{
    [Export] CrossFadeTextureRect target;
    Texture2D dr, drextra;
    bool isdr = true;

    public override void _Ready()
    {
        base._Ready();
        dr = ResourceLoader.Load<Texture2D>("res://Arts/Textures/Test/drwind.jpg");
        drextra = ResourceLoader.Load<Texture2D>("res://Arts/Textures/Test/drwindextra.jpg");
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (Input.IsKeyPressed(Key.A))
        {
            target.SetTextureWithFade(isdr ? drextra : dr);
            isdr = !isdr;
        }
        else if (Input.IsKeyPressed(Key.S))
        {
            target.CompleteFade();
        }
    }
}