using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TatehamaInterlockingConsole.Config;

namespace TatehamaInterlockingConsole.Views
{
    /// <summary>
    /// EnvironmentSelectWindow.xaml の相互作用ロジック
    /// 接続先環境(Dev/Prodなど)を選択するダイアログ
    /// </summary>
    public partial class EnvironmentSelectWindow : Window
    {
        private EnvironmentType? _selectedEnvironment;

        public EnvironmentType SelectedEnvironment { get; private set; }
        public string CustomLocalUrl { get; private set; }

        public EnvironmentSelectWindow()
        {
            InitializeComponent();
            SetupEnvironmentRadioButtons();
        }

        private void SetupEnvironmentRadioButtons()
        {
            // URLが空でない環境のみラジオボタンを生成(Localは公開ダイアログでは非表示)
            var availableEnvironments = EnvironmentDefinition.Available
                .Where(e => e.Type != EnvironmentType.Local)
                .ToList();

            if (!availableEnvironments.Any())
            {
                MessageBox.Show(
                    "利用可能な環境が定義されていません。\nServerAddress.csを確認してください。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                DialogResult = false;
                Close();
                return;
            }

            bool isFirst = true;
            foreach (var env in availableEnvironments)
            {
                var radioButton = new RadioButton
                {
                    Content = env.DisplayName,  // URLではなく環境名のみ表示
                    Tag = env.Type,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 14,
                    Margin = new Thickness(0, 5, 0, 5),
                    IsChecked = isFirst  // 最初の環境をデフォルト選択
                };
                radioButton.Checked += RadioButton_Checked;
                EnvironmentPanel.Children.Add(radioButton);

                if (isFirst)
                {
                    _selectedEnvironment = env.Type;
                    isFirst = false;
                }
            }

            UpdateLocalUrlVisibility();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is EnvironmentType type)
            {
                _selectedEnvironment = type;
                UpdateLocalUrlVisibility();
            }
        }

        private void UpdateLocalUrlVisibility()
        {
            bool isLocal = _selectedEnvironment == EnvironmentType.Local;
            LocalUrlPanel.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
            if (isLocal && string.IsNullOrEmpty(LocalUrlTextBox.Text))
            {
                LocalUrlTextBox.Text = ServerAddress.LocalUrl;
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedEnvironment.HasValue)
            {
                MessageBox.Show("環境を選択してください。", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Local環境の場合はカスタムURLを保存
            if (_selectedEnvironment == EnvironmentType.Local)
            {
                var customUrl = LocalUrlTextBox.Text.Trim();
                if (string.IsNullOrEmpty(customUrl))
                {
                    MessageBox.Show("ローカルURLを入力してください。", "エラー",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // URLの妥当性チェック
                if (!Uri.TryCreate(customUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    MessageBox.Show("有効なURLを入力してください。\n例: https://localhost:7232", "エラー",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CustomLocalUrl = customUrl;
            }

            SelectedEnvironment = _selectedEnvironment.Value;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
