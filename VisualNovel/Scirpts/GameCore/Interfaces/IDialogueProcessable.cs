using Godot;

namespace VisualNovel
{
    public interface IDialogueProcessable
    {
        StringName CompletionSignal { get; }
    }
}