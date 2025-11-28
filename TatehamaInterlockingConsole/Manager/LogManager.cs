using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TatehamaInterlockingConsole.Manager
{
    public static class LogManager
    {
        private static readonly string LogFilePath = ".\\InterlockingActionLog.txt";

        public static void WriteLog(string logText)
        {
            try
            {
                // 12時間以上前ならリセット
                if (File.Exists(LogFilePath))
                {
                    var lastWrite = File.GetLastWriteTime(LogFilePath);
                    if ((DateTime.Now - lastWrite).TotalHours >= 12)
                    {
                        File.WriteAllText(LogFilePath, string.Empty, Encoding.UTF8);
                    }
                }

                var log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {logText}";
                File.AppendAllText(LogFilePath, log + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ログ書き込み失敗: {ex.Message}");
            }
        }
    }
}
