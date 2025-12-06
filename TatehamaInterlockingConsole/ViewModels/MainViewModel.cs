using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OpenIddict.Client;
using TatehamaInterlockingConsole.Factories;
using TatehamaInterlockingConsole.Handlers;
using TatehamaInterlockingConsole.Manager;
using TatehamaInterlockingConsole.Models;
using TatehamaInterlockingConsole.Services;

namespace TatehamaInterlockingConsole.ViewModels
{
    /// <summary>
    /// メイン画面を管理するViewModelクラス
    /// </summary>
    public class MainViewModel : WindowViewModel
    {
        private readonly TimeService _timeService;                 // 時間管理サービス
        private readonly DataManager _dataManager;                 // データ管理を担当するクラス
        private readonly ServerCommunication _serverCommunication; // サーバ通信クラス
        private static bool _isConstructorExecuted = false;        // コンストラクタが一度だけ実行されることを保証するフラグ
        private readonly ButtonHandler _buttonHandler;             // ボタン操作処理クラス
        private readonly ImageHandler _imageHandler;               // 画像操作処理クラス
        private readonly LabelHandler _labelHandler;               // ラベル操作処理クラス
        private readonly TextBlockHandler _textBlockHandler;       // テキストブロック操作処理クラス
        private readonly Sound _sound;                             // サウンド管理クラスのインスタンス

        private string volumeText;
        /// <summary>
        /// 音量表示テキスト
        /// </summary>
        public string VolumeText { get => volumeText; set => SetProperty(ref volumeText, value); }

        private double volume;
        /// <summary>
        /// 音量(％)
        /// </summary>
        public double Volume
        {
            get => volume;
            set
            {
                if (volume != value && _sound != null)
                {
                    volume = value;
                    _sound.SetMasterVolume((float)volume * 0.01f);
                    OnPropertyChanged(nameof(Volume));
                    VolumeText = $"音量: {volume:F0}%";
                }
            }
        }

        private string serverType;
        /// <summary>
        /// サーバー種類
        /// </summary>
        public string ServerType { get => serverType; set => SetProperty(ref serverType, value); }

        /// <summary>
        /// ウィンドウの最前面表示を切り替えるコマンド
        /// </summary>
        public ICommand CheckTopMostCommand { get; }

        /// <summary>
        /// メイン画面に表示されるUI要素List
        /// </summary>
        public ObservableCollection<UIElement> MainElements { get; set; }

        /// <summary>
        /// 現在時刻を取得
        /// </summary>
        public DateTime CurrentTime => _dataManager.CurrentTime;

        /// <summary>
        /// ウィンドウの最前面表示フラグ
        /// </summary>
        public bool IsTopMost
        {
            get => _dataManager.IsTopMost;
            set
            {
                if (_dataManager.IsTopMost != value)
                {
                    _dataManager.IsTopMost = value;
                    OnPropertyChanged(nameof(IsTopMost));
                }
            }
        }

        /// <summary>
        /// ウィンドウのタイトル
        /// </summary>
        public string Title { get; set; }

        private bool _connectionStatus;
        /// <summary>
        /// 通信状態ステータス
        /// </summary>
        public bool ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged(nameof(ConnectionStatus));
                }
            }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="timeService"></param>
        /// <param name="dataManager"></param>
        /// <param name="openIddictClientService"></param>
        public MainViewModel(TimeService timeService, DataManager dataManager, OpenIddictClientService openIddictClientService)
        {
            try
            {
                // 初回呼び出し時のみコンストラクタ処理を実行
                if (!_isConstructorExecuted)
                {
                    _isConstructorExecuted = true;

                    _timeService = timeService;
                    _dataManager = dataManager;
                    _dataManager.Initialize(timeService);
                    _serverCommunication = new ServerCommunication(openIddictClientService, timeService);
                    _buttonHandler = new ButtonHandler(_serverCommunication);
                    _imageHandler = new ImageHandler(_serverCommunication);
                    _labelHandler = new LabelHandler(_serverCommunication);
                    _textBlockHandler = new TextBlockHandler(_serverCommunication);

                    Title = "連動盤選択 | 連動盤 - ダイヤ運転会";
                    CheckTopMostCommand = new RelayCommand(() => ToggleAllTopMost());

                    // フラグタイマーの初期化
                    StartFlagTimerAsync();

                    // 時間更新イベントを購読
                    _dataManager.TimeUpdated += (currentTime) => OnPropertyChanged(nameof(CurrentTime));
                    // サーバー接続状態イベントを購読
                    _serverCommunication.ConnectionStatusChanged += (status) => ConnectionStatus = status;
                    // 初期化処理
                    Initialize();

                    // 音声管理クラスのインスタンスを取得
                    _sound = Sound.Instance;
                    _sound.SoundInit();
                    Volume = 100;
                    VolumeText = $"音量: {Volume}%";

                    // サーバー種類の設定
                    ServerType = ServerAddress.SignalAddress.Contains("dev") ? "Dev" : "Prod";
                }
            }
            catch (Exception ex)
            {
                CustomMessage.Show(ex.ToString(), "エラー");
                throw;
            }
        }

        /// <summary>
        /// 非同期タイマーを開始するメソッド
        /// </summary>
        private async void StartFlagTimerAsync()
        {
            while (true)
            {
                await Task.Delay(250);
                _dataManager.FlagValue = !_dataManager.FlagValue;
            }
        }

        /// <summary>
        /// 初期化処理
        /// </summary>
        private void Initialize()
        {
            var tsvFolderPath = "TSV";
            var retsubanFolderPath = "Image/Retsuban";

            // 駅設定データをリストに格納
            _dataManager.StationSettingList = StationSettingLoader.LoadSettings(tsvFolderPath, "StationSettingList.tsv");
            // 近接警報条件データをリストに格納
            _dataManager.ApproachingAlarmConditionList = ApproachingAlarmSettingLoader.LoadSettings(tsvFolderPath, "ApproachingAlarmConditionList.tsv");
            // UI設定データをリストに格納
            _dataManager.AllControlSettingList = UIElementLoader.LoadSettingsFromFolderAsUIControlSetting(tsvFolderPath);
            // 列番表示画像Pathを辞書に格納
            _dataManager.RetsubanImagePathDictionary = RetsubanFactory.GetRetsubanImagePath(retsubanFolderPath);

            // メイン画面用のUI要素を取得
            var mainControlSettingList = _dataManager.AllControlSettingList.FindAll(list => list.StationName == "Main_UIList");
            MainElements = UIElementLoader.CreateUIControlModels(mainControlSettingList);

            // 表示時間更新サービス開始
            _timeService.Start();
        }

        /// <summary>
        /// 生成されている全てのフォームを取得し、一括でTopMostを設定するメソッド
        /// </summary>
        public void ToggleAllTopMost()
        {
            // StationFormのTopMostを設定
            foreach (var stationName in _dataManager.ActiveStationsList)
            {
                var matchingStations = _dataManager.StationSettingList
                    .Where(s => s.StationNumber == stationName)
                    .Select(s => s.StationName);

                foreach (var titleName in matchingStations)
                {
                    var forms = GetFormsByTitleName(titleName);
                    foreach (var form in forms)
                    {
                        form.Topmost = IsTopMost;
                    }
                }
            }

            // メインフォームのTopMostを設定
            var mainForms = GetFormsByTitleName("連動盤選択");
            foreach (var mainForm in mainForms)
            {
                mainForm.Topmost = IsTopMost;
            }
        }

        /// <summary>
        /// 指定した文字列をタイトルに含む全てのフォームを取得するメソッド
        /// </summary>
        /// <param name="titleName">検索するタイトル名</param>
        /// <returns>該当するフォームのリスト</returns>
        public IEnumerable<Window> GetFormsByTitleName(string titleName)
        {
            if (string.IsNullOrEmpty(titleName))
            {
                return Enumerable.Empty<Window>();
            }

            return Application.Current.Windows
                .OfType<Window>()
                .Where(w => w.Title.Contains(titleName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// ウィンドウを閉じる際の確認処理
        /// </summary>
        /// <returns>ウィンドウを閉じて良い場合はtrue、それ以外はfalse</returns>
        public bool ConfirmClose()
        {
            var result = CustomMessage.Show("全ての連動盤を閉じます。よろしいですか？",
                "終了確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                return false;
            }

            // リソース解放処理
            _dataManager.ServerConnected = false;
            MainElements.Clear();
            _timeService.Stop();
            ImageCacheManager.ClearCache();
            _dataManager.TimeUpdated -= (currentTime) => OnPropertyChanged(nameof(CurrentTime));
            _serverCommunication.ConnectionStatusChanged -= (status) => OnPropertyChanged(nameof(ConnectionStatus));
            Sound.Instance.Dispose();
            return true;
        }
    }
}
