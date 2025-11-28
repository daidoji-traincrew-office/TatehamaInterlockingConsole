using System;
using System.IO;
using System.Windows;
using TatehamaInterlockingConsole.Handlers;
using TatehamaInterlockingConsole.Helpers;
using TatehamaInterlockingConsole.Manager;
using TatehamaInterlockingConsole.ViewModels;

namespace TatehamaInterlockingConsole.Views
{
    /// <summary>
    /// StationWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class StationWindow : Window
    {
        private StationViewModel _viewModel;
        private readonly string _stationName;
        private readonly string _settingsPath;

        public StationWindow(StationViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            // ViewModelから駅名を取得
            if (viewModel is StationViewModel svm)
                _stationName = DataHelper.GetFileNameFromStationName(svm.Title.Split('|')[0].Replace("駅", "").Trim());
            else
                _stationName = "Unknown";

            _settingsPath = $"WindowSettings/{_stationName}.txt";

            Closed += Window_Closed;
            PreviewMouseUp += OnPreviewMouseUp;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            SaveWindowSettings();
        }

        private void SaveWindowSettings()
        {
            try
            {
                Directory.CreateDirectory("WindowSettings");
                using (var writer = new StreamWriter(_settingsPath, false))
                {
                    writer.WriteLine($"Width={this.Width}");
                    writer.WriteLine($"Height={this.Height}");
                    writer.WriteLine($"Left={this.Left}");
                    writer.WriteLine($"Top={this.Top}");
                }
            }
            catch (Exception ex)
            {
                LogManager.WriteLog($"ウィンドウ設定保存失敗: {_settingsPath} - {ex}");
            }
        }

        private void OnPreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // ImageHandlerのMouseUpイベントを呼び出し
            ImageHandler.Instance?.OnPreviewMouseUp(sender, e);
        }
    }
}
