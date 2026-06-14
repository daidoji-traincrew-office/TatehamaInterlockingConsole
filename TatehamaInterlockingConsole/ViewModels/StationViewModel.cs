using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TatehamaInterlockingConsole.Factories;
using TatehamaInterlockingConsole.Manager;
using TatehamaInterlockingConsole.Helpers;
using TatehamaInterlockingConsole.Services;
using TatehamaInterlockingConsole.Models;
using System.Threading.Tasks;

namespace TatehamaInterlockingConsole.ViewModels
{
    /// <summary>
    /// 駅毎の連動盤画面を管理するViewModelクラス
    /// </summary>
    public class StationViewModel : WindowViewModel
    {
        private readonly DataManager _dataManager; // データ管理クラス
        private readonly Sound _sound; // サウンド管理クラスのインスタンス
        private readonly DataUpdateViewModel _dataUpdateViewModel;
        private string _stationName; // 駅名
        private string _stationNumber; // 駅番号

        /// <summary>
        /// 表示モードを切り替えるコマンド
        /// </summary>
        public ICommand ToggleModeCommand { get; }

        /// <summary>
        /// ウィンドウを閉じる際のコマンド
        /// </summary>
        public ICommand ClosingCommand { get; }

        /// <summary>
        /// ウィンドウのタイトル
        /// </summary>
        public string Title { get; set; }

        private ObservableCollection<UIElement> _stationElements = new ObservableCollection<UIElement>();
        /// <summary>
        /// 駅毎の連動盤のUI要素のコレクション
        /// </summary>
        public ObservableCollection<UIElement> StationElements
        {
            get => _stationElements;
            set
            {
                if (_stationElements != value)
                {
                    _stationElements = value;
                    OnPropertyChanged(nameof(StationElements));
                }
            }
        }

        private bool _isFitMode = true;
        /// <summary>
        /// 表示モードがFitModeかどうか
        /// </summary>
        public bool IsFitMode
        {
            get => _isFitMode;
            set
            {
                if (_isFitMode != value)
                {
                    _isFitMode = value;
                    OnPropertyChanged(nameof(IsFitMode));
                }
            }
        }

        private string _toggleButtonText;
        /// <summary>
        /// トグルボタンのテキスト
        /// </summary>
        public string ToggleButtonText
        {
            get { return _toggleButtonText; }
            set
            {
                if (_toggleButtonText != value)
                {
                    _toggleButtonText = value;
                    OnPropertyChanged(nameof(ToggleButtonText));
                }
            }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="title"></param>
        /// <param name="filePath"></param>
        /// <param name="dataManager"></param>
        /// <param name="sound"></param>
        /// <param name="dataUpdateViewModel"></param>
        public StationViewModel(string title, string filePath, DataManager dataManager, Sound sound, DataUpdateViewModel dataUpdateViewModel)
        {
            try
            {
                _dataManager = dataManager;                                                     // データ管理クラスのインスタンス
                _sound = sound;                                                                 // サウンド管理クラスのインスタンス
                _dataUpdateViewModel = dataUpdateViewModel;                                     // データ更新ViewModelのインスタンス
                _dataUpdateViewModel.NotifyUpdateControlEvent += OnNotifyUpdateControlEvent;    // データ更新イベントの登録
                _dataUpdateViewModel.ForceUpdateControlEvent += OnForceUpdateControlEvent;  // 追加
                _stationName = DataHelper.ExtractStationNameFromFilePath(filePath);             // ファイルパスから駅名を抽出
                _stationNumber = DataHelper.GetStationNumberFromStationName(_stationName);      // 駅名から駅番号を取得

                // 駅名をリストに追加
                if (!_dataManager.ActiveStationsList.Contains(_stationNumber))
                {
                    _dataManager.AddActiveStation(_stationNumber);
                }

                // 時計更新タイマーの初期化
                StartClockUpdateTimerAsync();

                IsFitMode = false;
                ToggleButtonText = "フィット表示に切り替え";
                Title = title;

                ClosingCommand = new RelayCommand(OnClosing);
                ToggleModeCommand = new RelayCommand(ToggleMode);

                // 駅毎の連動盤に対応する設定データを取得
                var stationSettingList = _dataManager.AllControlSettingList.FindAll(list => list.StationName == _stationName);
                StationElements = UIElementLoader.CreateUIControlModels(stationSettingList);
                // 列番表示画像Pathをキャッシュに追加
                RetsubanFactory.SetRetsubanImagePathToCache(_dataManager.RetsubanImagePathDictionary);

                // ウィンドウサイズと描画領域の設定
                WindowWidth = 1280;
                WindowHeight = 720;
                DrawingWidth = BackImageFactory.BackImageWidth;
                DrawingHeight = BackImageFactory.BackImageHeight;
            }
            catch (Exception ex)
            {
                CustomMessage.Show(ex.ToString(), "エラー");
                throw;
            }
        }

        /// <summary>
        /// DataUpdateViewModelでの変更通知受け取り処理
        /// </summary>
        /// <param name="updateList"></param>
        public void OnNotifyUpdateControlEvent(List<UIControlSetting> updateList)
        {
            ClearStationCache();

            // 駅毎の連動盤に対応する設定データを取得
            var stationSettingList = updateList.FindAll(list => list.StationName == _stationName);
            var newElements = UIElementLoader.CreateUIControlModels(stationSettingList);
            var newCollection = new ObservableCollection<UIElement>(newElements);

            // 差分比較
            if (!UIElementLoader.AreCollectionsEqual(StationElements, newCollection))
            {
                // 差分がある場合のみ更新
                StationElements = newCollection;
            }
        }

        /// <summary>
        /// DataUpdateViewModelでの強制更新通知受け取り処理（差分比較なし）
        /// </summary>
        /// <param name="updateList"></param>
        public void OnForceUpdateControlEvent(List<UIControlSetting> updateList)
        {
            // 駅毎の連動盤に対応する設定データを取得
            var stationSettingList = updateList.FindAll(list => list.StationName == _stationName);
            var newElements = UIElementLoader.CreateUIControlModels(stationSettingList);

            // 差分比較せず強制更新
            StationElements = new ObservableCollection<UIElement>(newElements);
        }

        /// <summary>
        /// 非同期タイマーを開始するメソッド
        /// </summary>
        private async void StartClockUpdateTimerAsync()
        {
            while (true)
            {
                await Task.Delay(1000);
                OnClockUpdate();
            }
        }

        /// <summary>
        /// 時計更新処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClockUpdate()
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 時計UI要素の更新処理
                    foreach (var element in StationElements)
                    {
                        if (element is Canvas clockCanvas)
                        {
                            ClockImageFactory.UpdateClockHands(clockCanvas);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// ウィンドウを閉じる際の処理
        /// </summary>
        public void OnClosing()
        {
            // UI要素をクリア
            ClearStationCache();
            StationElements.Clear();

            // キャッシュをクリア
            ImageCacheManager.ClearCache();

            // イベント解除
            _dataUpdateViewModel.NotifyUpdateControlEvent -= OnNotifyUpdateControlEvent;
            _dataUpdateViewModel.ForceUpdateControlEvent -= OnForceUpdateControlEvent;  // 追加

            // 駅名をリストから削除
            _dataManager.RemoveActiveStation(_stationNumber);
        }

        /// <summary>
        /// キャッシュからUI要素の画像を削除
        /// </summary>
        private void ClearStationCache()
        {
            foreach (var setting in StationElements)
            {
                if (setting is Image image && image.Source is BitmapImage bitmapImage)
                {
                    string imagePath = bitmapImage.UriSource.ToString();
                    ImageCacheManager.RemoveImage(imagePath); // キャッシュから削除
                }
            }
        }

        /// <summary>
        /// 表示モード切り替え
        /// </summary>
        private void ToggleMode()
        {
            IsFitMode = !IsFitMode;
            ToggleButtonText = IsFitMode ? "原寸大表示に切り替え" : "フィット表示に切り替え";
        }
    }
}
