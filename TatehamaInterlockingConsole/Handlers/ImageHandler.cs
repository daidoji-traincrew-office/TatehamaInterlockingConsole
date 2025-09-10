using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using TatehamaInterlockingConsole.Manager;
using TatehamaInterlockingConsole.Models;
using TatehamaInterlockingConsole.Services;

namespace TatehamaInterlockingConsole.Handlers
{
    public class ImageHandler
    {
        private readonly DataManager _dataManager = DataManager.Instance;
        private readonly ServerCommunication _serverCommunication;
        private readonly Random _random = new();
        private UIControlSetting strMouseDownSetting;
        public static ImageHandler Instance { get; private set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="serverCommunication"></param>
        public ImageHandler(ServerCommunication serverCommunication)
        {
            _serverCommunication = serverCommunication;
            strMouseDownSetting = null;
            Instance = this;
        }

        /// <summary>
        /// Imageクリックイベント処理
        /// </summary>
        /// <param name="image"></param>
        /// <param name="setting"></param>
        public void AttachImageClick(Image image, UIControlSetting setting)
        {
            // 左クリックイベント
            image.MouseLeftButtonDown += (s, e) =>
            {
                HandleMouseDown(setting, isLeftClick: true);
            };

            // 右クリックイベント
            image.MouseRightButtonDown += (s, e) =>
            {
                HandleMouseDown(setting, isLeftClick: false);
            };
        }

        /// <summary>
        /// StationWindowのPreviewMouseUpイベント処理
        /// </summary>
        public void OnPreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (strMouseDownSetting != null)
            {
                HandleMouseUp(strMouseDownSetting);
                strMouseDownSetting = null;
            }
        }

        /// <summary>
        /// 種類別のマウスダウン処理
        /// </summary>
        /// <param name="control"></param>
        /// <param name="isLeftClick"></param>
        private void HandleMouseDown(UIControlSetting control, bool isLeftClick)
        {
            switch (control.ControlType)
            {
                case "LeverImage":
                    HandleLeverImageMouseDown(control, isLeftClick);
                    break;
                case "KeyImage":
                    HandleKeyImageMouseDown(control, isLeftClick);
                    break;
                case "ButtonImage":
                    HandleButtonImageMouseDown(control);
                    strMouseDownSetting = control;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 種類別のマウスアップ処理
        /// </summary>
        /// <param name="control"></param>
        private void HandleMouseUp(UIControlSetting control)
        {
            switch (control.ControlType)
            {
                case "ButtonImage":
                    HandleButtonImageMouseUp(control);
                    strMouseDownSetting = null;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// LeverImageマウスダウン処理
        /// </summary>
        /// <param name="control"></param>
        /// <param name="isLeftClick"></param>
        private void HandleLeverImageMouseDown(UIControlSetting control, bool isLeftClick)
        {
            int newIndex = control.ImageIndex;

            // LRパターンのみ別処理
            if (control.ImagePatternSymbol == "LR")
            {
                if (newIndex == -1 && !isLeftClick)
                {
                    newIndex = 1;
                }
                else if (newIndex == 1 && isLeftClick)
                {
                    newIndex = -1;
                }
                else
                {
                    return;
                }
            }
            else
            {
                int change = isLeftClick ? -1 : 1;
                newIndex += change;

                if (!IsValidIndex(control.ImagePatternSymbol, newIndex))
                {
                    return;
                }
            }

            // サーバーへリクエスト送信
            if (control.ServerType != string.Empty)
            {
                // 操作中判定
                control.IsHandling = true;

                var leverData = new DatabaseOperational.LeverData
                {
                    Name = control.ServerName,
                    State = EnumData.ConvertToLCR(newIndex)
                };
                _ = _serverCommunication.SendLeverEventDataRequestToServerAsync(leverData);
                LogManager.WriteLog($"てこ操作 : {leverData.Name} : {leverData.State}");
                //CustomMessage.Show($"Name: {leverData.Name} State: {leverData.State}",
                //    "サーバー送信",
                //    System.Windows.MessageBoxButton.OK,
                //    System.Windows.MessageBoxImage.Information
                //    );
            }
        }

        /// <summary>
        /// KeyImageマウスダウン処理
        /// </summary>
        /// <param name="control"></param>
        /// <param name="isLeftClick"></param>
        private async Task HandleKeyImageMouseDown(UIControlSetting control, bool isLeftClick)
        {
            int newIndex = control.ImageIndex;
            bool newKeyInserted = control.KeyInserted;

            // Shiftキーが押されているかを判定
            bool isShiftPressed = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
            if (isShiftPressed)
            {
                // Shiftキーが押されている場合、KeyInsertedを切り替え
                if (newKeyInserted)
                {
                    if (newIndex >= 0)
                    {
                        newIndex -= 10;
                    }
                    else
                    {
                        newIndex += 10;
                    }
                }
                else
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
                newKeyInserted = !newKeyInserted;

                // サーバーへリクエスト送信
                if (control.ServerType != string.Empty)
                {
                    // 操作中判定
                    control.IsHandling = true;
                    // 鍵抜き差し操作中判定
                    control.IsKeyHandling = true;

                    var keyLeverData = new DatabaseOperational.KeyLeverData
                    {
                        Name = control.ServerName,
                        State = EnumData.ConvertToLNR(newIndex),
                        IsKeyInserted = newKeyInserted
                    };
                    control.IsAuthentication = await _serverCommunication.SendKeyLeverEventDataRequestToServerAsync(keyLeverData);
                    LogManager.WriteLog($"鍵てこ操作 : {keyLeverData.Name} : {keyLeverData.State} : 鍵{(keyLeverData.IsKeyInserted ? "挿入" : "抜取")}");
                    //CustomMessage.Show($"Name: {keyLeverData.Name} State: {keyLeverData.State} Key: {keyLeverData.IsKeyInserted}",
                    //    "サーバー送信",
                    //    System.Windows.MessageBoxButton.OK,
                    //    System.Windows.MessageBoxImage.Information
                    //    );
                }
            }
            else if (control.KeyInserted)
            {
                // LRパターンのみ別処理
                if (control.ImagePatternSymbol == "KeyLR")
                {
                    if (newIndex == -11 && !isLeftClick)
                    {
                        newIndex = 11;
                    }
                    else if (newIndex == 11 && isLeftClick)
                    {
                        newIndex = -11;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    int change = isLeftClick ? -1 : 1;
                    newIndex = newIndex + change;

                    if (!IsValidIndex(control.ImagePatternSymbol, newIndex))
                    {
                        return;
                    }
                }

                // サーバーへリクエスト送信
                if (control.ServerType != string.Empty)
                {
                    // 操作中判定
                    control.IsHandling = true;

                    var keyLeverData = new DatabaseOperational.KeyLeverData
                    {
                        Name = control.ServerName,
                        State = EnumData.ConvertToLNR(newIndex),
                        IsKeyInserted = newKeyInserted
                    };
                    control.IsAuthentication = await _serverCommunication.SendKeyLeverEventDataRequestToServerAsync(keyLeverData);
                    LogManager.WriteLog($"鍵てこ操作 : {keyLeverData.Name} : {keyLeverData.State} : 鍵{(keyLeverData.IsKeyInserted ? "挿入" : "抜取")}");
                    //CustomMessage.Show($"Name: {keyLeverData.Name} State: {keyLeverData.State} Key: {keyLeverData.IsKeyInserted}",
                    //    "サーバー送信",
                    //    System.Windows.MessageBoxButton.OK,
                    //    System.Windows.MessageBoxImage.Information
                    //    );
                }
            }
        }

        /// <summary>
        /// ButtonImageマウスダウン処理
        /// </summary>
        /// <param name="control"></param>
        private void HandleButtonImageMouseDown(UIControlSetting control)
        {
            int newIndex = control.ImageIndex;

            if (newIndex != 1)
            {
                newIndex = 1;

                // サーバーへリクエスト送信
                if (control.ServerType != string.Empty)
                {
                    // ボタン操作中(押し)判定
                    control.IsButtionRaised = true;

                    control.ImageIndex = newIndex;

                    var destinationButtonData = new DatabaseOperational.DestinationButtonData
                    {
                        Name = control.ServerName,
                        IsRaised = EnumData.ConvertToRaiseDrop(newIndex),
                        OperatedAt = DateTime.Now
                    };
                    _ = _serverCommunication.SendButtonEventDataRequestToServerAsync(destinationButtonData);
                    LogManager.WriteLog($"ボタン操作 : {destinationButtonData.Name} : {destinationButtonData.IsRaised}");
                    //CustomMessage.Show($"Name: {destinationButtonData.Name} State: {destinationButtonData.IsRaised}",
                    //    "サーバー送信",
                    //    System.Windows.MessageBoxButton.OK,
                    //    System.Windows.MessageBoxImage.Information
                    //    );
                }
                // 接近ボタンの場合は接近警報停止処理
                else if (control.UniqueName.Contains("接近"))
                {
                    control.ImageIndex = newIndex;

                    var StationName = control.StationName;
                    if (StationName.Contains("江ノ原検車区"))
                    {
                        StationName = "江ノ原検車区表示盤";
                    }

                    _dataManager.ActiveAlarmsList
                        .RemoveAll(alarm => alarm.StationName == StationName && alarm.IsUpSide == control.UniqueName.Contains("上り"));

                    Sound.Instance.LoopSoundAllStop(StationName, control.UniqueName.Contains("上り"));

                    // 音声再生
                    string randomPushSoundIndex = _random.Next(1, 13).ToString("00");
                    Sound.Instance.SoundPlay($"push_{randomPushSoundIndex}", false);
                }
            }
        }

        /// <summary>
        /// ButtonImageマウスアップ処理
        /// </summary>
        /// <param name="control"></param>
        private void HandleButtonImageMouseUp(UIControlSetting control)
        {
            int newIndex = control.ImageIndex;

            if (newIndex != 0)
            {
                newIndex = 0;

                // サーバーへリクエスト送信
                if (control.ServerType != string.Empty)
                {
                    // ボタン操作中(離し)判定
                    control.IsButtionDroped = true;

                    control.ImageIndex = newIndex;

                    var destinationButtonData = new DatabaseOperational.DestinationButtonData
                    {
                        Name = control.ServerName,
                        IsRaised = EnumData.ConvertToRaiseDrop(newIndex),
                        OperatedAt = DateTime.Now
                    };
                    _ = _serverCommunication.SendButtonEventDataRequestToServerAsync(destinationButtonData);
                    LogManager.WriteLog($"ボタン操作 : {destinationButtonData.Name} : {destinationButtonData.IsRaised}");
                    //CustomMessage.Show($"Name: {destinationButtonData.Name} State: {destinationButtonData.IsRaised}",
                    //    "サーバー送信",
                    //    System.Windows.MessageBoxButton.OK,
                    //    System.Windows.MessageBoxImage.Information
                    //    );
                }
                // 接近ボタンの場合は音声再生
                else if (control.UniqueName.Contains("接近"))
                {
                    control.ImageIndex = newIndex;

                    // 音声再生
                    string randomPullSoundIndex = _random.Next(1, 13).ToString("00");
                    Sound.Instance.SoundPlay($"pull_{randomPullSoundIndex}", false);
                }
            }
        }

        /// <summary>
        /// Index有効判定
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private bool IsValidIndex(string pattern, int index)
        {
            switch (pattern)
            {
                case "NR":
                    return index == 0 || index == 1;
                case "LN":
                    return index == -1 || index == 0;
                case "LR":
                    return index == -1 || index == 1;
                case "LNR":
                    return index == -1 || index == 0 || index == 1;
                case "KeyNR":
                    return index == 0 || index == 1 || index == 10 || index == 11;
                case "KeyLN":
                    return index == -1 || index == 0 || index == -11 || index == 10;
                case "KeyLR":
                    return index == -1 || index == 1 || index == -11 || index == 11;
                case "KeyLNR":
                    return index == -1 || index == 0 || index == 1 || index == -11 || index == 10 || index == 11;
                default:
                    return false;
            }
        }
    }
}