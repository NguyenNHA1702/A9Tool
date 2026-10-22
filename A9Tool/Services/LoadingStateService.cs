using System;
using System.Collections.Generic;

namespace A9Tool.Services
{
    public class LoadingStateService
    {
        public bool IsVisible { get; private set; }
        public string Title { get; private set; } = "CONNECTING TO THE NEURAL NETWORK OF RHODES ISLAND";
        public string SubTitle { get; private set; } = "SYSTEM CACHE SYNCHRONIZATION IN PROGRESS";
        public int ProgressPercent { get; private set; } = 0;
        public List<string> LogLines { get; private set; } = new();

        public event Action? OnChange;

        public void Show(string title = "CONNECTING TO THE NEURAL NETWORK OF RHODES ISLAND", string subtitle = "SYSTEM CACHE SYNCHRONIZATION IN PROGRESS", int progress = 0)
        {
            IsVisible = true;
            Title = title;
            SubTitle = subtitle;
            ProgressPercent = progress;
            LogLines.Clear();
            LogLines.Add("// PRTS://SYS_INITIALIZE_KERNEL... [OK]");
            OnChange?.Invoke();
        }

        public void UpdateProgress(int progress, string? logMessage = null)
        {
            ProgressPercent = Math.Clamp(progress, 0, 100);
            if (!string.IsNullOrEmpty(logMessage))
            {
                LogLines.Add(logMessage);
                if (LogLines.Count > 5) LogLines.RemoveAt(0);
            }
            OnChange?.Invoke();
        }

        public void Hide()
        {
            IsVisible = false;
            OnChange?.Invoke();
        }
    }
}
