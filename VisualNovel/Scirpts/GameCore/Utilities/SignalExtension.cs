using System;
using Godot;

namespace VisualNovel.GameCore.Utilities
{

    public static class SignalExtensions
    {
        public static void ConnectOnce(this GodotObject source, string signalName, Action action)
        {
            if (action == null) return;

            Callable callable = Callable.From(action);
            Error err = source.Connect(signalName, callable, (uint)GodotObject.ConnectFlags.OneShot);

            if (err != Error.Ok)
            {
                GD.PrintErr($"[SignalExtensions] 无法连接信号 {signalName}, 错误代码: {err}");
            }
        }
    }
}