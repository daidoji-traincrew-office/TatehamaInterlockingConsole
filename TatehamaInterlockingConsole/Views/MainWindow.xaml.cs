using System.ComponentModel;
using System.Windows;
using OpenIddict.Client;
using TatehamaInterlockingConsole.Models;
using TatehamaInterlockingConsole.ViewModels;
using static TatehamaInterlockingConsole.App;

namespace TatehamaInterlockingConsole.Views
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ServerCommunication _serverCommunication;

        public MainWindow(MainViewModel viewModel, OpenIddictClientService openIddictClientService)
        {
            InitializeComponent();
            DataContext = viewModel;
            _serverCommunication = new ServerCommunication(openIddictClientService);

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // DataContextの初期サイズをウィンドウに設定
            if (DataContext is MainViewModel viewModel)
            {
                Width = viewModel.WindowWidth;
                Height = viewModel.WindowHeight;
                Topmost = true;
            }
            // ユーザー認証・初期化
            await _serverCommunication.Authorize();
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                // 閉じる確認ダイアログの処理
                if (!viewModel.ConfirmClose())
                {
                    e.Cancel = true;
                    return;
                }

                // サーバーとの接続を切断
                await _serverCommunication.DisconnectAsync();

                // アプリケーション全体を終了
                Application.Current.Shutdown();
            }
        }
    }
}
