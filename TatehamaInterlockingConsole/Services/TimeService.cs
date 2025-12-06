using System;
using System.Windows.Threading;

namespace TatehamaInterlockingConsole.Services
{
    /// <summary>
    /// 時刻を管理および更新するためのサービスクラス
    /// </summary>
    public class TimeService
    {
        private static readonly TimeService _instance = new();
        private readonly DispatcherTimer _timer; // 一定間隔で時間を更新するためのタイマー
        private DateTime _baseTime; // 基準となる現在時刻
        private TimeSpan _timeOffset; // 基準時刻からのオフセット値

        public static TimeService Instance => _instance;

        /// <summary>
        /// 時刻が更新されたときに発生するイベント
        /// </summary>
        public event Action<DateTime> TimeUpdated;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TimeService()
        {
            _baseTime = DateTime.Now; // 現在時刻を基準時刻として設定
            _timeOffset = TimeSpan.FromHours(0); // デフォルトのオフセット値を設定
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) }; // タイマーの間隔を設定

            // タイマーのTickイベントに時刻更新処理を登録
            _timer.Tick += (sender, e) =>
            {
                _baseTime = DateTime.Now; // 基準時刻を現在時刻に更新
                TimeUpdated?.Invoke(CurrentTime); // イベントをトリガーして現在時刻を通知
            };
        }

        /// <summary>
        /// 現在の基準時刻にオフセットを加えた時刻を取得
        /// </summary>
        public DateTime CurrentTime => _baseTime.Add(_timeOffset);

        /// <summary>
        /// タイマー開始
        /// </summary>
        public void Start() => _timer.Start();

        /// <summary>
        /// タイマー停止
        /// </summary>
        public void Stop() => _timer.Stop();

        /// <summary>
        /// 時刻のオフセットを指定の値に設定する
        /// </summary>
        /// <param name="newOffset"></param>
        public void SetTimeOffset(TimeSpan newOffset)
        {
            _timeOffset = newOffset; // オフセットを新しい値に更新
            TimeUpdated?.Invoke(CurrentTime); // 更新された時刻を通知
        }
    }
}
