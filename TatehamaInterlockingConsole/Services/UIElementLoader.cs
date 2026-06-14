using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using TatehamaInterlockingConsole.Models;
using TatehamaInterlockingConsole.Factories;
using System.Windows.Controls;

namespace TatehamaInterlockingConsole.Services
{
    public static class UIElementLoader
    {
        /// <summary>
        /// フォルダ内のUI設定用TSVファイルを読み込み、UIControlSettingに変換して返す
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        public static List<UIControlSetting> LoadSettingsFromFolderAsUIControlSetting(string folderPath)
        {
            var allSettings = new List<UIControlSetting>();

            // 条件に一致するファイルを取得
            IEnumerable<string> matchingFiles = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
                .Where(file =>
                {
                    string fileName = Path.GetFileName(file);
                    return (fileName.StartsWith("Main") || fileName.StartsWith("TH")) && fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase);
                });

            // UIContolSettingに変換
            foreach (var filePath in matchingFiles)
            {
                try
                {
                    var settings = UIControlSettingLoader.LoadSettings(filePath);
                    allSettings.AddRange(settings);
                }
                catch (Exception ex)
                {
                    CustomMessage.Show(ex.ToString(), "エラー");
                    return new List<UIControlSetting>(); // null の代わりに空リストを返す
                }
            }
            return allSettings;
        }

        /// <summary>
        /// UIControlSettingを読み込みUIElementModelを返す
        /// </summary>
        /// <param name="allSettings"></param>
        /// <returns></returns>
        public static ObservableCollection<UIElement> CreateUIControlModels(List<UIControlSetting> allSettings, bool drawing = true)
        {
            var elements = new ObservableCollection<UIElement>();

            foreach (var setting in allSettings)
            {
                var element = ControlFactory.CreateControl(setting, allSettings, drawing);
                if (element != null)
                {
                    elements.Add(element);
                }
            }
            return elements;
        }

        /// <summary>
        /// UIControlSettingを読み込みUIElementModelを返す
        /// </summary>
        /// <param name="allSettings"></param>
        /// <returns></returns>
        public static ObservableCollection<UIElement> CreateUIControlModels(List<UIControlSetting> allSettings, string stationName, bool drawing = true)
        {
            var elements = new ObservableCollection<UIElement>();

            // 指定されたStationNumberがListに存在するか確認
            if (allSettings.Any(list => list.StationName == stationName))
            {
                var stationList = allSettings.Where(list => list.StationName == stationName).ToList();
                foreach (var setting in allSettings)
                {
                    var element = ControlFactory.CreateControl(setting, stationList, drawing);
                    if (element != null)
                    {
                        elements.Add(element);
                    }
                }
            }
            else
            {
                throw new ArgumentException($"指定されたファイル名 '{stationName}' の設定データが見つかりません。");
            }
            return elements;
        }

        /// <summary>
        /// UIElementコレクション差分比較メソッド
        /// </summary>
        /// <param name="collection1"></param>
        /// <param name="collection2"></param>
        /// <returns></returns>
        public static bool AreCollectionsEqual(ObservableCollection<UIElement> collection1, ObservableCollection<UIElement> collection2)
        {
            if (collection1.Count != collection2.Count)
                return false;

            for (int i = 0; i < collection1.Count; i++)
            {
                if (!AreUIElementsEqual(collection1[i], collection2[i]))
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// UIElement差分比較メソッド
        /// </summary>
        /// <param name="element1"></param>
        /// <param name="element2"></param>
        /// <returns></returns>
        private static bool AreUIElementsEqual(UIElement element1, UIElement element2)
        {
            // カスタムロジックを使用してUIElementを比較
            if (element1 == null || element2 == null)
                return element1 == element2;

            bool result = false;

            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (element1 is Canvas canvas1 && element2 is Canvas canvas2)
                    {
                        result = canvas1.Children.Count == canvas2.Children.Count &&
                                 CompareCanvasChildren(canvas1.Children, canvas2.Children);
                    }
                    else if (element1 is Grid grid1 && element2 is Grid grid2)
                    {
                        result = grid1.Children.Count == grid2.Children.Count;
                    }
                    else if (element1 is TextBlock text1 && element2 is TextBlock text2)
                    {
                        result = text1.Text.ToString() == text2.Text.ToString();
                    }
                    else if (element1 is Image image1 && element2 is Image image2)
                    {
                        var source1 = image1.Source?.ToString();
                        var source2 = image2.Source?.ToString();
                        result = source1 == source2;
                    }
                    else if (element1 is Label label1 && element2 is Label label2)
                    {
                        result = label1.Content.ToString() == label2.Content.ToString();
                    }
                });
            }

            return result;
        }
        private static bool CompareCanvasChildren(UIElementCollection children1, UIElementCollection children2)
        {
            if (children1.Count != children2.Count)
                return false;

            for (int i = 0; i < children1.Count; i++)
            {
                if (!AreUIElementsEqual(children1[i], children2[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
