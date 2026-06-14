namespace TatehamaInterlockingConsole.Models
{
    /// <summary>
    /// サーバーアドレス設定クラス
    /// </summary>
    public static class ServerAddress
    {
        /// <summary>
        /// デバッグモードフラグ（trueの場合は認証をスキップ）
        /// </summary>
        public static bool IsDebug { get; } = true; // 本番環境では false

        /// <summary>
        /// SignalRサーバーアドレス
        /// </summary>
        public static string SignalAddress { get; } = "https://localhost:5001"; // 実際のサーバーURL
    }
}