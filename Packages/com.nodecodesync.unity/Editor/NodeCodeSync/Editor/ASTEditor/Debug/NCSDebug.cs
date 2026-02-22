using System.Text;

namespace NodeCodeSync.Editor.ASTEditor
{
    public class NCSDebug
    {
        const bool DebugMode = true;

        readonly StringBuilder _logBuilder = new StringBuilder();
        const string LogPrefix = "[NCS]";

        readonly string _logModifire;

        public NCSDebug(string logmodifier)
        {
            if (DebugMode)
            {
                _logModifire = logmodifier;
            }
        }

        public void Log(string message)
        {
            if (DebugMode)
            {
                _logBuilder.AppendLine($"{LogPrefix} [LOG] {_logModifire} {message}");
            }
        }

        public void LogWarning(string message)
        {
            if (DebugMode)
            {
                _logBuilder.AppendLine($"{LogPrefix} [WARNING] {_logModifire} {message}");
            }
        }

        public void LogError(string message)
        {
            if (DebugMode)
            {
                _logBuilder.AppendLine($"{LogPrefix} [ERROR] {_logModifire} {message}");
            }
        }
        public string PrintLog()
        {
            if (DebugMode)
            {
                var result = _logBuilder.ToString();
                _logBuilder.Clear();
                return result;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
