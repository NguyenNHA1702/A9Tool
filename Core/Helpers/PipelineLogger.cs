using System;
using System.Text;

namespace Core.Helpers
{
    /// <summary>
    /// Utility helper for generating colored ANSI progress bars, cache status badges,
    /// and tree-structured logging pipelines for object lifecycle tracking in the console.
    /// </summary>
    public static class PipelineLogger
    {
        // ANSI escape codes for 24-bit/16-color virtual terminal output
        public const string Reset = "\u001b[0m";
        public const string Bold = "\u001b[1m";
        public const string Dim = "\u001b[2m";
        public const string Cyan = "\u001b[96m";
        public const string DarkCyan = "\u001b[36m";
        public const string Green = "\u001b[92m";
        public const string DarkGreen = "\u001b[32m";
        public const string Yellow = "\u001b[93m";
        public const string Orange = "\u001b[33m";
        public const string Red = "\u001b[91m";
        public const string Magenta = "\u001b[95m";
        public const string Blue = "\u001b[94m";
        public const string Gray = "\u001b[90m";

        /// <summary>
        /// Renders a colored percentage progress bar (e.g. "[ 40%] [████░░░░░░]").
        /// </summary>
        /// <param name="percent">Percentage value from 0 to 100.</param>
        /// <param name="width">Total character width of the progress indicator bar (default 10).</param>
        /// <returns>ANSI formatted progress bar string.</returns>
        public static string RenderProgressBar(int percent, int width = 10)
        {
            percent = Math.Clamp(percent, 0, 100);
            int filled = (int)Math.Round((double)percent / 100 * width);
            int empty = width - filled;

            var bar = new StringBuilder();
            bar.Append(Yellow).Append($"[{percent,3}%]").Append(Reset).Append(" ");
            bar.Append(Gray).Append("[").Append(Reset);
            bar.Append(Green).Append(new string('█', filled)).Append(Reset);
            bar.Append(Gray).Append(new string('░', empty)).Append("]").Append(Reset);

            return bar.ToString();
        }

        /// <summary>
        /// Badge indicating instant retrieval from in-memory dictionary cache (~0ms).
        /// </summary>
        public static string TagRamHit() => $"{DarkGreen}[CACHE: RAM HIT]{Reset}";

        /// <summary>
        /// Badge indicating retrieval from local persistent disk JSON file (~1-10ms).
        /// </summary>
        public static string TagDiskHit(string? fileName = null, long sizeBytes = 0)
        {
            if (string.IsNullOrEmpty(fileName))
                return $"{Green}[CACHE: DISK HIT]{Reset}";

            string sizeStr = sizeBytes > 0 ? $" ({sizeBytes / 1024} KB)" : "";
            return $"{Green}[CACHE: DISK HIT: {fileName}{sizeStr}]{Reset}";
        }

        /// <summary>
        /// Badge indicating retrieval from static immutable local definition tables.
        /// </summary>
        public static string TagStatic(string? note = null)
        {
            string detail = !string.IsNullOrEmpty(note) ? $" ({note})" : "";
            return $"{Blue}[CACHE: STATIC JSON{detail}]{Reset}";
        }

        /// <summary>
        /// Badge indicating cache miss or eviction triggering remote GitHub fetch.
        /// </summary>
        public static string TagFetch(string? source = null)
        {
            string detail = !string.IsNullOrEmpty(source) ? $" ({source})" : "";
            return $"{Orange}[CACHE: MISS -> FETCH{detail}]{Reset}";
        }

        /// <summary>
        /// Badge indicating dynamically evaluated or computed client-side values.
        /// </summary>
        public static string TagComputed() => $"{Magenta}[COMPUTED]{Reset}";

        /// <summary>
        /// Formats the header banner of an object loading pipeline.
        /// </summary>
        public static string FormatHeader(string title, string objectName, string objectId)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Cyan}┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━{Reset}");
            sb.Append($"{Cyan}┃{Reset} {Bold}{title}{Reset}: {Cyan}'{objectName}'{Reset} {Gray}({objectId}){Reset}");
            return sb.ToString();
        }

        /// <summary>
        /// Formats a single step line within the pipeline tree.
        /// </summary>
        public static string FormatStep(int percent, string cacheTag, string stepName, string details, long elapsedMs = -1)
        {
            string bar = RenderProgressBar(percent, 10);
            string timeStr = elapsedMs >= 0 ? $" {Gray}({elapsedMs}ms){Reset}" : "";
            return $"{Cyan}┣━━{Reset} {bar} {cacheTag} {Bold}{stepName}:{Reset} {details}{timeStr}";
        }

        /// <summary>
        /// Formats a nested sub-item row under a step.
        /// </summary>
        public static string FormatSubItem(string label, string details, bool isLast = false)
        {
            string branch = isLast ? "└─" : "├─";
            return $"{Cyan}┃{Reset}        {branch} {Bold}[{label}]{Reset} {details}";
        }

        /// <summary>
        /// Formats the completion banner of the pipeline.
        /// </summary>
        public static string FormatFooter(string objectName, long totalElapsedMs, int errorCount = 0, int warningCount = 0)
        {
            var sb = new StringBuilder();
            if (errorCount > 0)
            {
                sb.AppendLine($"{Cyan}┗━━{Reset} {Red}{Bold}[PIPELINE FAILED]{Reset} '{objectName}' finished with {Red}{errorCount} Error(s){Reset} in {totalElapsedMs}ms");
            }
            else if (warningCount > 0)
            {
                sb.AppendLine($"{Cyan}┗━━{Reset} {Yellow}{Bold}[PIPELINE WARNING]{Reset} '{objectName}' loaded with {Yellow}{warningCount} Warning(s){Reset} in {totalElapsedMs}ms");
            }
            else
            {
                sb.AppendLine($"{Cyan}┗━━{Reset} {Green}{Bold}[PIPELINE SUCCESS]{Reset} '{objectName}' fully loaded in {Green}{totalElapsedMs}ms ✓{Reset} (0 Errors, 0 Warnings)");
            }
            sb.Append($"{Cyan}┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━{Reset}");
            return sb.ToString();
        }
    }
}
