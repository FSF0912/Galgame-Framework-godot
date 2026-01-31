using Godot;

public static class Debugger
{
    public static void PushError(string message, object context = null)
    {
        string stackTrace = System.Environment.StackTrace;
        string contextJson = "None";
        if (context != null)
        {
            contextJson = Json.Stringify(Variant.From(context), "\t");
        }

        string fullLog = $"\n[VN_ERROR]: {message}\n" +
                         $"------------------------------------------\n" +
                         $"CONTEXT:\n{contextJson}\n" +
                         $"------------------------------------------\n" +
                         $"C# STACK TRACE:\n{stackTrace}\n";

        GD.PushError(fullLog);
        GD.PrintErr(fullLog);
    }

    public static void PushDetailedWarning(string message, object context = null)
    {
        string stackTrace = System.Environment.StackTrace;
        string contextJson = "None";
        if (context != null)
        {
            contextJson = Json.Stringify(Variant.From(context), "\t");
        }

        string fullLog = $"\n[VN_WARNING]: {message}\n" +
                         $"------------------------------------------\n" +
                         $"CONTEXT:\n{contextJson}\n" +
                         $"------------------------------------------\n" +
                         $"C# STACK TRACE:\n{stackTrace}\n";

        GD.PushWarning(fullLog);
    }

    public static void PushWarning(string message)
    {
        GD.PushWarning($"[VN_WARNING]: {message}");
    }
}