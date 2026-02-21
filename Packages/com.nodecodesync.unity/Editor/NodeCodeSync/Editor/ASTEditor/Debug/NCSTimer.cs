using System.Text;
using System.Diagnostics;

namespace NodeCodeSync.Editor.ASTEditor
{
    public class NCSTimer
    {
        const bool DebugMode = true;

        StringBuilder logBuilder = new StringBuilder();
        const string LogPrefix = "[NCS]";

        Stopwatch stopwatch = new Stopwatch();

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
                logBuilder.AppendLine($"{LogPrefix}[TIMER] {label}: {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        public string Stop(string label = "Total")
        {
            if (!DebugMode) return string.Empty;
            stopwatch.Stop();
            logBuilder.AppendLine($"{LogPrefix}[TIMER] {label}: {stopwatch.ElapsedMilliseconds}ms");
            return logBuilder.ToString();
        }
    }
}