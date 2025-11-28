using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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
                dataFromServer.Signals.Any(s => s.Name == item.ServerName) ||
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
                    var signal = dataFromServer.Signals
                        .FirstOrDefault(s => s.Name == item.ServerName);
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
                        case "信号機表示灯":
                            if (signal != null)
                            {
                                // 進行信号
                                item.ImageIndex = signal.Phase != EnumData.Phase.R ? 1 : 0;
                            }
                            break;
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
                var approachingAlarmList = _dataManager.ApproachingAlarmConditionList;
                var activeStationsList = _dataManager.ActiveStationsList;
                var stationSettingList = _dataManager.StationSettingList;
                var OnTrack = new DatabaseOperational.TrackCircuitData();
                var conditionsTrack = new DatabaseOperational.TrackCircuitData();
                var conditionsPoint = new DatabaseOperational.SwitchData();
                var conditionsSignal = new DatabaseOperational.SignalData();
                var conditionsDirection = new DatabaseOperational.DirectionData();

                // 接近警報条件リストを処理
                foreach (var alarm in approachingAlarmList)
                {
                    // 接近警報条件と一致するサーバー情報のみ抽出
                    OnTrack = dataFromServer.TrackCircuits
                        .FirstOrDefault(p => p.Name == alarm.TrackName.Name);
                    conditionsTrack = dataFromServer.TrackCircuits
                        .FirstOrDefault(p => alarm.ConditionsList
                        .Any(c => (c.Type == "Track") && (c.Name == p.Name)));
                    conditionsPoint = dataFromServer.Points
                        .FirstOrDefault(p => alarm.ConditionsList
                        .Any(c => (c.Type == "Point") && (c.Name == p.Name)));
                    conditionsSignal = dataFromServer.Signals
                        .FirstOrDefault(p => alarm.ConditionsList
                        .Any(c => (c.Type == "Signal") && (c.Name == p.Name)));
                    conditionsDirection = dataFromServer.Directions
                        .FirstOrDefault(p => alarm.ConditionsList
                        .Any(c => (c.Type == "Direction") && (c.Name == p.Name)));

                    // 各パラメータの接近警報鳴動条件を満たしているか判定
                    bool IsOnTrack = (OnTrack != null) && OnTrack.On;
                    bool IsTrackState = IsTrackCircuitConditionMet(conditionsTrack, alarm);
                    bool IsPointState = IsPointConditionMet(conditionsPoint, alarm);
                    bool IsSignalState = IsSignalConditionMet(conditionsSignal, alarm);
                    bool IsDirectionState = IsDirectionConditionMet(conditionsDirection, alarm);
                    bool IsRetsubanState = IsRetsubanConditionMet(conditionsTrack, alarm);

                    // 接近警報鳴動条件が全て満たされている場合に鳴動判定
                    if (IsOnTrack && IsTrackState && IsPointState && IsSignalState && IsDirectionState && IsRetsubanState)
                        alarm.IsAlarmConditionMet = true;
                    else
                        alarm.IsAlarmConditionMet = false;
                }

                // 接近警報鳴動処理
                foreach (var activeStation in activeStationsList)
                {
                    // 起動している駅と一致する接近警報リストを抽出
                    var alarmList = approachingAlarmList
                        .Where(a => a.StationName == activeStation).ToList();
                    // 起動している駅と一致する駅設定リストを抽出
                    var stationList = stationSettingList
                        .Where(s => s.StationNumber == activeStation).ToList();

                    // 接近警報リストを処理
                    foreach (var alarm in alarmList)
                    {
                        var station = stationList.FirstOrDefault(s => s.StationNumber == alarm.StationName);

                        // 接近警報鳴動条件が満たされている場合
                        if (alarm.IsAlarmConditionMet)
                        {
                            // 接近警報音声が鳴動済みでない場合
                            if (!alarm.IsAlarmPlayed)
                            {
                                // 鳴動リストに追加
                                if (!DataManager.Instance.ActiveAlarmsList
                                    .Any(a => (a.StationName == DataHelper.GetStationNameFromStationNumber(alarm.StationName)) && (a.IsUpSide == alarm.IsUpSide)))
                                {
                                    DataManager.Instance.ActiveAlarmsList.Add(new ActiveAlarmList
                                    {
                                        StationName = DataHelper.GetStationNameFromStationNumber(alarm.StationName),
                                        IsUpSide = alarm.IsUpSide
                                    });
                                }
                                // 音声再生済みフラグを立てる
                                alarm.IsAlarmPlayed = true;
                            }
                        }
                        // 接近警報鳴動条件が満たされていない場合
                        else
                        {
                            // 接近警報音声が鳴動済みの場合
                            if (alarm.IsAlarmPlayed)
                            {
                                // 鳴動フラグを解除
                                alarm.IsAlarmPlayed = false;
                            }
                        }
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
        /// 駅扱切換表示灯の更新処理
        /// </summary>
        /// <param name="item"></param>
        /// <param name="physicalLever"></param>
        private void UpdateStationSwitchIndicator(UIControlSetting item, DatabaseOperational.KeyLeverData physicalKeyLever)
        {
            if (physicalKeyLever != null)
            {
                // ランプ"PY"
                if (item.UniqueName.Contains("PY") && physicalKeyLever.State == EnumData.LNR.Left)
                    item.ImageIndex = 1;
                // ランプ"PG"
                else if (item.UniqueName.Contains("PG") && physicalKeyLever.State == EnumData.LNR.Right)
                    item.ImageIndex = 1;
                else
                    item.ImageIndex = 0;
            }
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
        /// 接近警報鳴動条件判定(軌道回路)
        /// </summary>
        /// <param name="track"></param>
        /// <param name="alarm"></param>
        /// <returns></returns>
        private bool IsTrackCircuitConditionMet(DatabaseOperational.TrackCircuitData track, ApproachingAlarmSetting alarm)
        {
            // 軌道回路条件が存在しない場合は条件を満たす
            if (track == null) return true;

            // 軌道回路条件を抽出
            bool isReverse = alarm.ConditionsList.FirstOrDefault(p => p.Name == track.Name).IsReversePosition;

            // 軌道回路と設定内容が在線同士、または非在線同士の場合は条件を満たす
            if (isReverse == track.On)
                return true;
            // それ以外は条件を満たさない
            else
                return false;
        }

        /// <summary>
        /// 接近警報鳴動条件判定(転てつ器)
        /// </summary>
        /// <param name="point"></param>
        /// <param name="alarm"></param>
        /// <returns></returns>
        private bool IsPointConditionMet(DatabaseOperational.SwitchData point, ApproachingAlarmSetting alarm)
        {
            // 転てつ器条件が存在しない場合は条件を満たす
            if (point == null) return true;

            // 転てつ器条件を抽出
            bool isReverse = alarm.ConditionsList.FirstOrDefault(p => p.Name == point.Name).IsReversePosition;

            // 転てつ器と設定内容が反位同士の場合は条件を満たす
            if (isReverse == (point.State == EnumData.NRC.Reversed))
                return true;
            // 転てつ器と設定内容が定位同士の場合は条件を満たす
            else if (!isReverse == (point.State == EnumData.NRC.Normal))
                return true;
            // それ以外は条件を満たさない
            else
                return false;
        }

        /// <summary>
        /// 接近警報鳴動条件判定(信号機)
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="alarm"></param>
        /// <returns></returns>
        private bool IsSignalConditionMet(DatabaseOperational.SignalData signal, ApproachingAlarmSetting alarm)
        {
            // 信号機条件が存在しない場合は条件を満たす
            if (signal == null) return true;

            // 信号機条件を抽出
            bool isReverse = alarm.ConditionsList.FirstOrDefault(p => p.Name == signal.Name).IsReversePosition;

            // 信号機と設定内容が定位同士、または反位同士の場合は条件を満たす
            if (isReverse == (signal.Phase != EnumData.Phase.R))
                return true;
            // それ以外は条件を満たさない
            else
                return false;
        }

        /// <summary>
        /// 接近警報鳴動条件判定(方向てこ)
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="alarm"></param>
        /// <returns></returns>
        private bool IsDirectionConditionMet(DatabaseOperational.DirectionData direction, ApproachingAlarmSetting alarm)
        {
            // 方向てこ条件が存在しない場合は条件を満たす
            if (direction == null) return true;

            // 方向てこ条件を抽出
            bool isReverse = alarm.ConditionsList.FirstOrDefault(p => p.Name == direction.Name).IsReversePosition;

            // 方向てこと設定内容がR同士の場合は条件を満たす
            if (isReverse == (direction.State == EnumData.LNR.Right))
                return true;
            // 方向てこと設定内容がL同士の場合は条件を満たす
            else if (!isReverse == (direction.State == EnumData.LNR.Left))
                return true;
            // それ以外は条件を満たさない
            else
                return false;
        }

        /// <summary>
        /// 接近警報鳴動条件判定(列車番号)
        /// </summary>
        /// <param name="retsuban"></param>
        /// <param name="alarm"></param>
        /// <returns></returns>
        private bool IsRetsubanConditionMet(DatabaseOperational.TrackCircuitData retsuban, ApproachingAlarmSetting alarm)
        {
            // 列車番号条件が存在しない場合は条件を満たす
            if (retsuban == null) return true;

            // 列車番号条件を抽出
            var condition = alarm.ConditionsList.FirstOrDefault(p => p.Name.Contains("列番"));
            bool isEven = condition != null && condition.Name.Contains("偶数");

            // 列車番号が未設定、または[9999]の場合は条件を満たさない
            if (string.IsNullOrEmpty(retsuban.Last) || retsuban.Last.Contains("9999") || retsuban.Last.Contains("溝月"))
                return false;
            // 列車番号と設定内容が偶数同士、または奇数同士の場合は条件を満たす
            else if (isEven == DataHelper.IsEvenNumberInString(retsuban.Last))
                return true;
            // それ以外は条件を満たさない
            else
                return false;
        }
    }
}
