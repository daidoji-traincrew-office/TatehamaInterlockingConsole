using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security.Claims;
using System.Windows;
using System.Windows.Controls;
using TatehamaInterlockingConsole.Helpers;
using TatehamaInterlockingConsole.Manager;
using TatehamaInterlockingConsole.Models;
using TatehamaInterlockingConsole.Services;

namespace TatehamaInterlockingConsole.ViewModels
{
    /// <summary>
    /// UIControlSettingList更新クラス
    /// </summary>
    public class DataUpdateViewModel : BaseViewModel
    {
        private static readonly DataUpdateViewModel _instance = new();
        private readonly DataManager _dataManager;  // データ管理を担当するクラス
        private readonly Sound _sound;              // 音声再生クラス
        private readonly Random _random = new();    // 乱数生成クラス
        public static DataUpdateViewModel Instance => _instance;

        /// <summary>
        /// 変更通知イベント
        /// </summary>
        public event Action<List<UIControlSetting>> NotifyUpdateControlEvent;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public DataUpdateViewModel()
        {
            _dataManager = DataManager.Instance;
            _sound = Sound.Instance;
        }

        /// <summary>
        /// 指定したUIControlSettingをコントロールに反映
        /// </summary>
        /// <param name="setting"></param>
        public void SetControlsetting(UIControlSetting setting)
        {
            try
            {
                var allSettingList = new List<UIControlSetting>(_dataManager.AllControlSettingList);

                int index = allSettingList.FindIndex(list => list.StationName == setting.StationName && list.UniqueName == setting.UniqueName);
                if (index >= 0)
                {
                    // UIスレッドとして実行
                    if (Application.Current?.Dispatcher != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // コントロール画像更新
                            allSettingList[index].ImageIndex = setting.ImageIndex;
                            allSettingList[index].Retsuban = setting.Retsuban;

                            // 変更通知イベント発火
                            var handler = NotifyUpdateControlEvent;
                            handler?.Invoke(allSettingList);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessage.Show(ex.ToString(), "エラー");
                throw;
            }
        }

        /// <summary>
        /// サーバー受信毎にコントロール更新
        /// </summary>
        public void UpdateControl(DatabaseOperational.DataFromServer dataFromServer)
        {
            // コントロール更新処理
            var updateList = UpdateControlsetting(dataFromServer);

            // 接近警報更新処理
            UpdateApproachingAlarm(dataFromServer);

            // 変更通知イベント発火
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var handler = NotifyUpdateControlEvent;
                    handler?.Invoke(updateList);
                });
            }
        }

        /// <summary>
        /// 信号機データ受信時のコントロール更新
        /// </summary>
        public void UpdateSignalControl()
        {
            // コントロール更新処理
            var updateList = UpdateSignalControlsetting();

            // 変更通知イベント発火
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var handler = NotifyUpdateControlEvent;
                    handler?.Invoke(updateList);
                });
            }
        }

        /// <summary>
        /// サーバー情報を基にコントロールの状態更新
        /// </summary>
        /// <returns></returns>
        private List<UIControlSetting> UpdateControlsetting(DatabaseOperational.DataFromServer dataFromServer)
        {
            // データ取得
            var allSettingList = new List<UIControlSetting>(_dataManager.AllControlSettingList);
            var activeStationsList = _dataManager.ActiveStationsList;
            var activeStationSettingList = allSettingList.Where(setting => activeStationsList.Contains(setting.StationNumber)).ToList();
            var physicalButtonStateList = _dataManager.PhysicalButtonOldList;
            var directionStateList = _dataManager.DirectionStateList;

            // 音声再生用の乱数生成
            Dictionary<string, string> randomIndex = new Dictionary<string, string>
            {
                { "keychain", _random.Next(1, 10).ToString("00") },
                { "insert", _random.Next(1, 6).ToString("00") },
                { "remove", _random.Next(1, 6).ToString("00") },
                { "reject", _random.Next(1, 4).ToString("00") },
                { "switch", _random.Next(1, 9).ToString("00") },
                { "push", _random.Next(1, 13).ToString("00") },
                { "pull", _random.Next(1, 13).ToString("00") }
            };

            try
            {
                // サーバーから受信したデータに基づいて更新
                var relevantSettings = activeStationSettingList.Where(item =>
                dataFromServer.TrackCircuits.Any(t => t.Name == item.ServerName) ||
                dataFromServer.Points.Any(p => p.Name == item.PointNameA || p.Name == item.PointNameB) ||
                dataFromServer.Directions.Any(d => d.Name == item.DirectionName) ||
                dataFromServer.PhysicalLevers.Any(l => l.Name == item.ServerName) ||
                dataFromServer.PhysicalKeyLevers.Any(l => l.Name == item.ServerName) ||
                dataFromServer.PhysicalButtons.Any(b => b.Name == item.ServerName) ||
                dataFromServer.Retsubans.Any(r => r.Name == item.ServerName) ||
                dataFromServer.Lamps.ContainsKey(item.ServerName)
                ).ToList();

                foreach (var item in relevantSettings)
                {
                    // コントロールと一致するサーバー情報のみ抽出
                    var trackCircuit = dataFromServer.TrackCircuits
                        .FirstOrDefault(t => t.Name == item.ServerName);
                    var pointA = dataFromServer.Points
                        .FirstOrDefault(p => p.Name == item.PointNameA);
                    var pointB = dataFromServer.Points
                        .FirstOrDefault(p => p.Name == item.PointNameB);
                    var direction = dataFromServer.Directions
                        .FirstOrDefault(l => l.Name == item.DirectionName);
                    var physicalLever = dataFromServer.PhysicalLevers
                        .FirstOrDefault(l => l.Name == item.ServerName);
                    var physicalKeyLever = dataFromServer.PhysicalKeyLevers
                       .FirstOrDefault(l => l.Name == item.ServerName);
                    var physicalButton = dataFromServer.PhysicalButtons
                        .FirstOrDefault(b => b.Name == item.ServerName);
                    var retsuban = dataFromServer.Retsubans
                        .FirstOrDefault(r => r.Name == item.ServerName);
                    var lamp = dataFromServer.Lamps
                        .TryGetValue(item.ServerName, out bool value) ? value : false;

                    // サーバー分類毎に処理
                    switch (item.ServerType)
                    {
                        case "転てつ器表示灯":
                            UpdatePointIndicator(item, pointA, pointB);
                            break;
                        case "軌道回路表示灯":
                            UpdateTrackCircuitIndicator(item, trackCircuit, pointA, pointB);
                            break;
                        case "方向てこ表示灯":
                            UpdateDirectionIndicator(item, trackCircuit, direction, directionStateList);
                            break;
                        case "状態表示灯":
                            {
                                item.ImageIndex = lamp ? 1 : 0;
                            }
                            break;
                        case "駅扱切換表示灯":
                            UpdateStationSwitchIndicator(item, lamp);
                            break;
                        case "解放表示灯":
                            if (physicalKeyLever != null)
                            {
                                item.ImageIndex = physicalKeyLever.State == EnumData.LNR.Right ? 1 : 0;
                            }
                            break;
                        case "物理てこ":
                            UpdatePhysicalLever(item, physicalLever, randomIndex);
                            break;
                        case "物理鍵てこ":
                            UpdatePhysicalKeyLever(item, physicalKeyLever, randomIndex);
                            break;
                        case "着点ボタン":
                            UpdateDestinationButton(item, physicalButton, physicalButtonStateList, randomIndex);
                            break;
                        case "列車番号":
                            UpdateRetsuban(item, retsuban);
                            break;
                        default:
                            break;
                    }
                }

                // 起動している駅ウィンドウのデータを全コントロール設定データに反映
                foreach (var activeSetting in activeStationSettingList)
                {
                    var index = allSettingList.FindIndex(setting => setting.StationNumber == activeSetting.StationNumber && setting.UniqueName == activeSetting.UniqueName);
                    if (index >= 0)
                    {
                        allSettingList[index] = activeSetting;
                    }
                }
                return allSettingList;
            }
            catch (Exception ex)
            {
                CustomMessage.Show(ex.ToString(), "エラー");
                throw;
            }
        }

        /// <summary>
        /// 接近警報更新
        /// </summary>
        private void UpdateApproachingAlarm(DatabaseOperational.DataFromServer dataFromServer)
        {
            try
            {
                var tmpActiveAlarmsList = new List<ActiveAlarmList>();

                // 接近警報リストを処理
                foreach (var alert in dataFromServer.RingingApproachAlerts)
                {
                    tmpActiveAlarmsList.Add(new ActiveAlarmList
                    {
                        StationName = DataHelper.GetStationNameFromStationNumber(alert.StationId),
                        IsUpSide = alert.IsUp
                    });
                }
                DataManager.Instance.ActiveAlarmsList = tmpActiveAlarmsList;
            }
            catch (Exception ex)
            {
                CustomMessage.Show(ex.ToString(), "エラー");
                throw;
            }
        }

        /// <summary>
        /// 転てつ器表示灯の更新処理
        /// </summary>
        /// <param name="item"></param>
        /// <param name="pointA"></param>
        /// <param name="pointB"></param>
        private void UpdatePointIndicator(UIControlSetting item, DatabaseOperational.SwitchData pointA, DatabaseOperational.SwitchData pointB)
        {
            // A, B転てつ器条件が存在する場合
            if (pointA != null && pointB != null)
                item.ImageIndex = (pointA.State == item.PointValueA && pointB.State == item.PointValueB) ? 1 : 0;
            // B転てつ器条件のみ存在する場合
            else if (pointB != null)
                item.ImageIndex = (pointB.State == item.PointValueB) ? 1 : 0;
            // A転てつ器条件のみ存在する場合
            else if (pointA != null)
                item.ImageIndex = (pointA.State == item.PointValueA) ? 1 : 0;
        }

        /// <summary>
        /// 軌道回路表示灯の更新処理
        /// </summary>
        /// <param name="item"></param>
        /// <param name="trackCircuit"></param>
        /// <param name="pointA"></param>
        /// <param name="pointB"></param>
        private void UpdateTrackCircuitIndicator(UIControlSetting item, DatabaseOperational.TrackCircuitData trackCircuit, DatabaseOperational.SwitchData pointA, DatabaseOperational.SwitchData pointB)
        {
            if (trackCircuit != null)
            {
                // A, B転てつ器条件が存在する場合
                if (pointA != null && pointB != null)
                    item.ImageIndex = (pointA.State == item.PointValueA && pointB.State == item.PointValueB) ? trackCircuit.On ? 2 : (trackCircuit.Lock ? 1 : 0) : 0;
                // B転てつ器条件のみ存在する場合
                else if (pointB != null)
                    item.ImageIndex = (pointB.State == item.PointValueB) ? trackCircuit.On ? 2 : (trackCircuit.Lock ? 1 : 0) : 0;
                // A転てつ器条件のみ存在する場合
                else if (pointA != null)
                    item.ImageIndex = (pointA.State == item.PointValueA) ? trackCircuit.On ? 2 : (trackCircuit.Lock ? 1 : 0) : 0;
                // 転てつ器条件なし
                else
                    item.ImageIndex = trackCircuit.On ? 2 : (trackCircuit.Lock ? 1 : 0);
            }
        }

        /// <summary>
        /// 方向てこ表示灯の更新処理
        /// </summary>
        /// <param name="item"></param>
        /// <param name="trackCircuit"></param>
        /// <param name="direction"></param>
        /// <param name="directionStateList"></param>
        private void UpdateDirectionIndicator(UIControlSetting item, DatabaseOperational.TrackCircuitData trackCircuit, DatabaseOperational.DirectionData direction, List<DirectionStateList> directionStateList)
        {
            if ((trackCircuit != null) && (direction != null))
            {
                var directionState = directionStateList.FirstOrDefault(d => d.Name == direction.Name);

                // 方向てこ条件あり
                if ((item.DirectionValue != EnumData.LNR.Normal) && (direction.State == item.DirectionValue))
                {
                    // 方向てこ状態が変化してから2秒以内なら赤点灯
                    item.ImageIndex = (DateTime.Now - directionState.UpdateTime).TotalSeconds < 2.0d ? 2 : (trackCircuit.On ? 2 : 1);
                }
                // 方向てこ条件なし
                else if (item.DirectionValue == EnumData.LNR.Normal)
                {
                    // 方向てこ状態が変化してから2秒以内なら赤点灯
                    item.ImageIndex = (DateTime.Now - directionState.UpdateTime).TotalSeconds < 2.0d ? 2 : (trackCircuit.On ? 2 : 1);
                }
                else
                {
                    item.ImageIndex = 0;
                }
            }
            else
            {
                item.ImageIndex = trackCircuit?.On == true ? 2 : 1;
            }
        }

        /// <summary>
        /// 駅扱切換表示灯の更新処理
        /// </summary>
        /// <param name="item"></param>
        /// <param name="lamp"></param>
        private void UpdateStationSwitchIndicator(UIControlSetting item, bool lamp)
        {
            item.ImageIndex = lamp ? 1 : 0;
        }

        /// <summary>
        /// 物理てこの更新処理
        /// </summary>
        /// <param name="item"></param>
        /// <param name="physicalLever"></param>
        /// <param name="randomIndex"></param>
        private void UpdatePhysicalLever(UIControlSetting item, DatabaseOperational.LeverData physicalLever, Dictionary<string, string> randomIndex)
        {
            if (physicalLever != null)
            {
                // てこが操作中で、物理てこの状態がUIとサーバーで異なる場合に更新
                if (item.IsHandling && physicalLever.State != EnumData.ConvertToLCR(item.ImageIndex))
                {
                    item.ImageIndex = EnumData.ConvertFromLCR(physicalLever.State);

                    // 操作判定を解除
                    item.IsHandling = false;
                    // 音声再生
                    _sound.SoundPlay($"switch_{randomIndex["switch"]}", false);
                }
                // てこが操作中ではなく、物理てこの状態がUIとサーバーで異なる場合に更新
                else if (!item.IsHandling && physicalLever.State != EnumData.ConvertToLCR(item.ImageIndex))
                {
                    item.ImageIndex = EnumData.ConvertFromLCR(physicalLever.State);

                    // 音声再生
                    _sound.SoundPlay($"switch_{randomIndex["switch"]}", false);
                }
            }
        }

        /// <summary>
        /// 物理鍵てこの更新処理
        /// </summary>
        /// <param name="item"></param>
        /// <param name="physicalKeyLever"></param>
        /// <param name="randomIndex"></param>
        private void UpdatePhysicalKeyLever(UIControlSetting item, DatabaseOperational.KeyLeverData physicalKeyLever, Dictionary<string, string> randomIndex)
        {
            if (physicalKeyLever != null)
            {
                // 鍵てこが鍵抜き差し操作中で、認証に失敗した場合に更新
                if (item.IsHandling && item.IsKeyHandling && !item.IsAuthentication)
                {
                    // 操作判定を解除
                    item.IsHandling = false;
                    // 鍵抜き差し操作判定を解除
                    item.IsKeyHandling = false;
                    // 認証情報を初期化
                    item.IsAuthentication = true;

                    // 音声再生
                    _sound.SoundPlay($"keychain_{randomIndex["keychain"]}", false);
                    _sound.SoundPlay($"reject_{randomIndex["reject"]}", false);
                }
                // 鍵てこが操作中で、物理鍵てこの状態がUIとサーバーで異なる場合に更新
                else if (item.IsHandling
                    && (physicalKeyLever.State != EnumData.ConvertToLNR(item.ImageIndex) || physicalKeyLever.IsKeyInserted != item.KeyInserted))
                {
                    // 操作判定を解除
                    item.IsHandling = false;
                    // 鍵抜き差し操作判定を解除
                    item.IsKeyHandling = false;

                    var newIndex = EnumData.ConvertFromLNR(physicalKeyLever.State);

                    // 鍵挿入状態をImageIndexに変換
                    if (physicalKeyLever.IsKeyInserted)
                    {
                        if (newIndex >= 0)
                        {
                            newIndex += 10;
                        }
                        else
                        {
                            newIndex -= 10;
                        }
                    }

                    // 鍵状態が変化したら音声再生
                    if (item.KeyInserted != physicalKeyLever.IsKeyInserted)
                    {
                        if (physicalKeyLever.IsKeyInserted)
                        {
                            // 音声再生
                            _sound.SoundPlay($"keychain_{randomIndex["keychain"]}", false);
                            _sound.SoundPlay($"insert_{randomIndex["insert"]}", false);
                        }
                        else
                        {
                            // 音声再生
                            _sound.SoundPlay($"keychain_{randomIndex["keychain"]}", false);
                            _sound.SoundPlay($"remove_{randomIndex["remove"]}", false);
                        }
                    }
                    // てこ状態が変化したら音声再生
                    else if (item.ImageIndex != newIndex)
                    {
                        // 音声再生
                        _sound.SoundPlay($"keychain_{randomIndex["keychain"]}", false);
                        _sound.SoundPlay($"switch_{randomIndex["switch"]}", false);
                    }

                    // 鍵てこ状態を反映
                    item.ImageIndex = newIndex;
                    item.KeyInserted = physicalKeyLever.IsKeyInserted;
                }
                // 鍵てこが操作中ではなく、物理鍵てこの状態がUIとサーバーで異なる場合に更新
                else if (!item.IsHandling
                    && (physicalKeyLever.State != EnumData.ConvertToLNR(item.ImageIndex) || (physicalKeyLever.IsKeyInserted != item.KeyInserted)))
                {
                    var newIndex = EnumData.ConvertFromLNR(physicalKeyLever.State);

                    // 鍵挿入状態をImageIndexに変換
                    if (physicalKeyLever.IsKeyInserted)
                    {
                        if (newIndex >= 0)
                        {
                            newIndex += 10;
                        }
                        else
                        {
                            newIndex -= 10;
                        }
                    }

                    // 鍵状態が変化したら音声再生
                    if (item.KeyInserted != physicalKeyLever.IsKeyInserted)
                    {
                        if (physicalKeyLever.IsKeyInserted)
                        {
                            // 音声再生
                            _sound.SoundPlay($"keychain_{randomIndex["keychain"]}", false);
                            _sound.SoundPlay($"insert_{randomIndex["insert"]}", false);
                        }
                        else
                        {
                            // 音声再生
                            _sound.SoundPlay($"keychain_{randomIndex["keychain"]}", false);
                            _sound.SoundPlay($"remove_{randomIndex["remove"]}", false);
                        }
                    }
                    // てこ状態が変化したら音声再生
                    else if (item.ImageIndex != newIndex)
                    {
                        // 音声再生
                        _sound.SoundPlay($"keychain_{randomIndex["keychain"]}", false);
                        _sound.SoundPlay($"switch_{randomIndex["switch"]}", false);
                    }

                    // 鍵てこ状態を反映
                    item.ImageIndex = newIndex;
                    item.KeyInserted = physicalKeyLever.IsKeyInserted;
                }
            }
        }

        /// <summary>
        /// 着点ボタンの更新処理
        /// </summary>
        /// <param name="item"></param>
        /// <param name="physicalButton"></param>
        /// <param name="physicalButtonOldList"></param>
        /// <param name="randomIndex"></param>
        private void UpdateDestinationButton(UIControlSetting item, DatabaseOperational.DestinationButtonData physicalButton, List<DatabaseOperational.DestinationButtonData> physicalButtonOldList, Dictionary<string, string> randomIndex)
        {
            if (physicalButton != null)
            {
                DatabaseOperational.DestinationButtonData physicalButtonOld;

                // 前回受信の着点ボタン情報取得判定 
                if (physicalButtonOldList == null || physicalButtonOldList.Count == 0)
                {
                    physicalButtonOld = new DatabaseOperational.DestinationButtonData
                    {
                        Name = physicalButton.Name,
                        IsRaised = physicalButton.IsRaised,
                        OperatedAt = physicalButton.OperatedAt
                    };
                }
                else
                {
                    // 前回受信の着点ボタン情報を取得
                    physicalButtonOld = physicalButtonOldList.FirstOrDefault(d => d.Name == physicalButton.Name);

                    // 前回受信の着点ボタン情報が存在しない場合は新規作成
                    if (physicalButtonOld == null)
                    {
                        physicalButtonOld = new DatabaseOperational.DestinationButtonData
                        {
                            Name = physicalButton.Name,
                            IsRaised = physicalButton.IsRaised,
                            OperatedAt = physicalButton.OperatedAt
                        };
                    }
                }

                // ボタンが[押し]操作中で、着点ボタンの状態がUIとサーバーで同じ場合に更新
                if (item.IsButtionRaised && physicalButton.IsRaised == EnumData.ConvertToRaiseDrop(item.ImageIndex))
                {
                    // [押し]操作判定を解除
                    item.IsButtionRaised = false;

                    // 音声再生
                    _sound.SoundPlay($"push_{randomIndex["push"]}", false);
                }
                // ボタンが[離し]操作中で、着点ボタンの状態がUIとサーバーで同じ場合に更新
                else if (item.IsButtionDroped && physicalButton.IsRaised == EnumData.ConvertToRaiseDrop(item.ImageIndex))
                {
                    // [離し]操作判定を解除
                    item.IsButtionDroped = false;

                    // 音声再生
                    _sound.SoundPlay($"pull_{randomIndex["pull"]}", false);
                }
                // ボタンが操作中ではなく、着点ボタンの状態がUIとサーバーで同じ、かつ直前の操作時間が変化した場合に音声再生
                else if (!item.IsButtionRaised && !item.IsButtionDroped && physicalButton.IsRaised == EnumData.ConvertToRaiseDrop(item.ImageIndex)
                    && (physicalButtonOld.OperatedAt != physicalButton.OperatedAt))
                {
                    // 音声再生
                    if (physicalButton.IsRaised == EnumData.ConvertToRaiseDrop(1))
                        _sound.SoundPlay($"push_{randomIndex["push"]}", false);
                    else
                        _sound.SoundPlay($"pull_{randomIndex["pull"]}", false);
                }
                // ボタンが操作中ではなく、着点ボタンの状態がUIとサーバーで異なる場合に更新
                else if (!item.IsButtionRaised && !item.IsButtionDroped && physicalButton.IsRaised != EnumData.ConvertToRaiseDrop(item.ImageIndex))
                {
                    item.ImageIndex = EnumData.ConvertFromRaiseDrop(physicalButton.IsRaised);

                    // 音声再生
                    if (item.ImageIndex == 1)
                        _sound.SoundPlay($"push_{randomIndex["push"]}", false);
                    else
                        _sound.SoundPlay($"pull_{randomIndex["pull"]}", false);
                }
            }
        }

        /// <summary>
        /// 列車番号の更新処理
        /// </summary>
        /// <param name="item"></param>
        /// <param name="retsuban"></param>
        private void UpdateRetsuban(UIControlSetting item, DatabaseOperational.RetsubanData retsuban)
        {
            if (retsuban != null)
                item.Retsuban = retsuban.Retsuban;
            else
                item.Retsuban = string.Empty;
        }

        /// <summary>
        /// 信号機情報を基にコントロールの状態更新
        /// </summary>
        /// <returns></returns>
        private List<UIControlSetting> UpdateSignalControlsetting()
        {
            // データ取得
            var allSettingList = new List<UIControlSetting>(_dataManager.AllControlSettingList);
            var activeStationsList = _dataManager.ActiveStationsList;
            var activeStationSettingList = allSettingList.Where(setting => activeStationsList.Contains(setting.StationNumber)).ToList();

            try
            {
                // 信号機データを取得
                var signals = DatabaseOperational.Instance.Signals;

                // 信号機表示灯のみを抽出
                var relevantSettings = activeStationSettingList.Where(item =>
                    item.ServerType == "信号機表示灯" &&
                    signals.Any(s => s.Name == item.ServerName)
                ).ToList();

                foreach (var item in relevantSettings)
                {
                    // コントロールと一致する信号機情報のみ抽出
                    var signal = signals.FirstOrDefault(s => s.Name == item.ServerName);

                    if (signal != null)
                    {
                        // 進行信号
                        item.ImageIndex = signal.Phase != EnumData.Phase.R ? 1 : 0;
                    }
                }

                // 起動している駅ウィンドウのデータを全コントロール設定データに反映
                foreach (var activeSetting in activeStationSettingList)
                {
                    var index = allSettingList.FindIndex(setting => setting.StationNumber == activeSetting.StationNumber && setting.UniqueName == activeSetting.UniqueName);
                    if (index >= 0)
                    {
                        allSettingList[index] = activeSetting;
                    }
                }
                return allSettingList;
            }
            catch (Exception ex)
            {
                CustomMessage.Show(ex.ToString(), "エラー");
                throw;
            }
        }
    }
}
