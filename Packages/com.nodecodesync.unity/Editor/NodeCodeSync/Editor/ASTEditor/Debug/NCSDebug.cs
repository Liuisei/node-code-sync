using System.Text;

namespace NodeCodeSync.Editor.ASTEditor
{
    public class NCSDebug
    {
        const bool DebugMode = true;

        StringBuilder logBuilder = new StringBuilder();
        const string LogPrefix = "[NCS]";
        public void Log(string message)
        {
            if (DebugMode)
            {
                logBuilder.AppendLine($"{LogPrefix}[LOG] {message}");
            }
        }

        public void LogWarning(string message)
        {
            if (DebugMode)
            {
                logBuilder.AppendLine($"{LogPrefix}[WARNING] {message}");
            }
        }

        public void LogError(string message)
        {
            if (DebugMode)
            {
                logBuilder.AppendLine($"{LogPrefix}[ERROR] {message}");
            }
        }
        public string PrintLog()
        {
            if (DebugMode)
            {
                var result = logBuilder.ToString();
                logBuilder.Clear();
                return result;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
