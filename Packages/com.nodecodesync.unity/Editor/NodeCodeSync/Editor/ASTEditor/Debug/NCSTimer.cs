using System.Text;
using System.Diagnostics;

namespace NodeCodeSync.Editor.ASTEditor
{
    public class NCSTimer
    {
        const bool DebugMode = true;

        readonly StringBuilder logBuilder = new();
        const string LogPrefix = "[NCS]";

        readonly string _logModifire = "";

        Stopwatch stopwatch = new Stopwatch();

        public NCSTimer(string logModifier)
        {
            if (DebugMode)
            {
                _logModifire = logModifier;
            }
        }

        public void Start()
        {
            if (DebugMode)
            {
                stopwatch = Stopwatch.StartNew();
            }
        }

        public void Lap(string label)
        {
            if (DebugMode)
            {
                logBuilder.AppendLine($"{LogPrefix}[TIMER] {_logModifire} {label}: {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        public string Stop(string label = "Total")
        {
            if (!DebugMode) return string.Empty;
            stopwatch.Stop();
            logBuilder.AppendLine($"{LogPrefix}[TIMER] {_logModifire} {label}: {stopwatch.ElapsedMilliseconds}ms");
            return logBuilder.ToString();
        }
    }
}
