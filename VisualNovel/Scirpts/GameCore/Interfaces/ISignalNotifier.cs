using Godot;

namespace VisualNovel
{
    public interface ISignalNotifier
    {
        StringName CompletionSignal { get; }
    }
}