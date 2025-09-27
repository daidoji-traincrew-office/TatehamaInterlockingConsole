using System;
using System.IO;
using System.Linq;
using System.Windows;
using TatehamaInterlockingConsole.Helpers;
using TatehamaInterlockingConsole.Manager;
using TatehamaInterlockingConsole.Services;
using TatehamaInterlockingConsole.Views;

namespace TatehamaInterlockingConsole.ViewModels
{
    public static class WindowAction
    {
        public static void ShowStationWindow(string stationName)
        {
            try
            {
                // ウィンドウタイトルを設定
                var station = DataHelper.GetStationNameFromEnglishName(stationName);
                var titleText = $"{station} | 連動盤 - ダイヤ運転会";

                // 既に同じタイトルのウィンドウが存在するかチェック
                var existingWindow = Application.Current.Windows
                    .OfType<StationWindow>()
                    .FirstOrDefault(w => w.Title == titleText);
                if (existingWindow != null)
                {
                    existingWindow.Activate();
                    return;
                }

                // ウィンドウが存在しない場合、新しく作成して表示
                var viewModel = new StationViewModel(titleText, $"TSV/{stationName}_UIList.tsv", DataManager.Instance, Sound.Instance, DataUpdateViewModel.Instance);
                var window = new StationWindow(viewModel)
                {
                    DataContext = viewModel,
                    Title = titleText,
                    Topmost = DataManager.Instance.IsTopMost,
                };

                // ウィンドウ設定ファイル読み込み
                var settingsPath = $"WindowSettings/{stationName}.txt";
                if (File.Exists(settingsPath))
                {
                    var lines = File.ReadAllLines(settingsPath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=');
                        if (parts.Length != 2) continue;
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        switch (key)
                        {
                            case "Width":
                                if (double.TryParse(value, out var w)) window.Width = w;
                                break;
                            case "Height":
                                if (double.TryParse(value, out var h)) window.Height = h;
                                break;
                            case "Left":
                                if (double.TryParse(value, out var l)) window.Left = l;
                                break;
                            case "Top":
                                if (double.TryParse(value, out var t)) window.Top = t;
                                break;
                        }
                    }
                }
                // 設定ファイルがない場合、デフォルトサイズを設定
                else
                {
                    window.Width = 1280;
                    window.Height = 720;
                }

                window.Show();
            }
            catch (Exception ex)
            {
                CustomMessage.Show(ex.Message, "エラー");
            }
        }
    }
}
