using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TatehamaInterlockingConsole.Handlers;
using TatehamaInterlockingConsole.Manager;
using TatehamaInterlockingConsole.Models;

namespace TatehamaInterlockingConsole.Factories
{
    public static class RetsubanFactory
    {
        private static readonly DataManager _dataManager = DataManager.Instance;

        /// <summary>
        /// Retsubanコントロール作成処理
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="allSettings"></param>
        /// <param name="drawing"></param>
        /// <returns></returns>
        public static UIElement CreateRetsubanImageControl(UIControlSetting setting, bool drawing)
        {
            var canvas = new Canvas();

            // 列番情報を基に各画像を設定
            var imagePathList = ConvertRetsubanToImagePath(setting.Retsuban);

            // ユーティリティメソッドで設定を生成
            var controls = new (string UniqueNameSuffix, int X, int Y, string ImagePath)[]
            {
                ("_HeadSymbol", 1, 1, imagePathList[0]),
                ("_Number1000", 28, 1, imagePathList[1]),
                ("_Number100", 47, 1, imagePathList[2]),
                ("_Number10", 66, 1, imagePathList[3]),
                ("_Number1", 85, 1, imagePathList[4]),
                ("_TailSymbol", 104, 1, imagePathList[5])
            };

            foreach (var control in controls)
            {
                if (control.ImagePath == null) continue;

                var imageSource = ImageCacheManager.GetImage(control.ImagePath);

                if (imageSource == null)
                {
                    imageSource = new BitmapImage(new Uri(control.ImagePath, UriKind.RelativeOrAbsolute))
                    {
                        CacheOption = BitmapCacheOption.OnLoad
                    };
                    ImageCacheManager.AddImage(control.ImagePath, imageSource); // キャッシュに追加
                }
                int pixelWidth = 0;
                int pixelHeight = 0;
                if (imageSource is BitmapSource bitmapSource)
                {
                    pixelWidth = bitmapSource.PixelWidth;
                    pixelHeight = bitmapSource.PixelHeight;
                }

                var image = new Image
                {
                    Source = imageSource,
                    Width = pixelWidth,
                    Height = pixelHeight
                };

                // イベントが設定されている場合は、イベントをアタッチ
                if (!string.IsNullOrEmpty(setting.ClickEventName))
                {
                    ImageHandler.Instance.AttachImageClick(image, setting);
                }

                Canvas.SetLeft(image, control.X);
                Canvas.SetTop(image, control.Y);
                canvas.Children.Add(image);
            }

            // ベースイメージを追加
            if (drawing)
            {
                var baseImage = ImageFactory.CreateImageControl(setting, true);
                canvas.Children.Insert(0, baseImage);
            }
            return canvas;
        }

        /// <summary>
        /// Retsuban文字列を画像Pathに変換
        /// </summary>
        /// <param name="retsuban"></param>
        /// <returns></returns>
        public static string[] ConvertRetsubanToImagePath(string retsuban)
        {
            var imagePaths = new string[6];

            // 溝月レイルのみ別処理
            if (retsuban.Contains("溝月"))
            {
                // 先頭文字の画像
                var headSymbolKey_MZ = "16dot_Mizo";
                if (_dataManager.RetsubanImagePathDictionary.TryGetValue(headSymbolKey_MZ, out var headKey_MZ))
                {
                    imagePaths[0] = headKey_MZ;
                }

                // 数字部分の画像
                for (int i = 0; i < 4; i++)
                {
                    var charKey_MZ = "7seg_Null";
                    if (_dataManager.RetsubanImagePathDictionary.TryGetValue(charKey_MZ, out var digitKey_MZ))
                    {
                        imagePaths[i + 1] = digitKey_MZ;
                    }
                }

                // 接尾文字の画像
                var tailSymbolKey_MZ = "16dot_Tsuki";
                if (_dataManager.RetsubanImagePathDictionary.TryGetValue(tailSymbolKey_MZ, out var tailKey_MZ))
                {
                    imagePaths[5] = tailKey_MZ;
                }

                return imagePaths;
            }

            // 正規表現パターンの定義
            var pattern = @"([回試臨]?)([0-9]{0,4})(A|AX|AY|AZ|B|BX|BY|BZ|C|CX|CY|CZ|D|DX|DY|DZ|K|KX|KY|KZ|S|SA|SAX|SAY|SAZ|SB|SBX|SBY|SBZ|SC|SCX|SCY|SCZ|SD|SDX|SDY|SDZ|SK|SKX|SKY|SKZ|SX|SY|SZ|T|TA|TAX|TAY|TAZ|TB|TBX|TBY|TBZ|TC|TCX|TCY|TCZ|TD|TDX|TDY|TDZ|TK|TKX|TKY|TKZ|TX|TY|TZ|X|Y|Z)?$";
            var match = Regex.Match(retsuban, pattern);

            // マッチしない場合は、接尾文字に「？」を設定
            if (retsuban.Length > 0 && match.Length == 0)
            {
                // 先頭文字の画像
                var headSymbolKey_MZ = "16dot_Null";
                if (_dataManager.RetsubanImagePathDictionary.TryGetValue(headSymbolKey_MZ, out var headKey_MZ))
                {
                    imagePaths[0] = headKey_MZ;
                }

                // 数字部分の画像
                for (int i = 0; i < 4; i++)
                {
                    var charKey_MZ = "7seg_Null";
                    if (_dataManager.RetsubanImagePathDictionary.TryGetValue(charKey_MZ, out var digitKey_MZ))
                    {
                        imagePaths[i + 1] = digitKey_MZ;
                    }
                }

                // 接尾文字の画像
                var tailSymbolKey_Q = "16dot_Question";
                if (_dataManager.RetsubanImagePathDictionary.TryGetValue(tailSymbolKey_Q, out var tailKey_Q))
                {
                    imagePaths[5] = tailKey_Q;
                }

                return imagePaths;
            }
            else
            {
                // 先頭文字の画像
                var headSymbolKey = GetHeadSymbolKey(match.Groups[1].Value);
                if (_dataManager.RetsubanImagePathDictionary.TryGetValue(headSymbolKey, out var headKey))
                {
                    imagePaths[0] = headKey;
                }

                // 数字部分の画像
                var digits = match.Groups[2].Value.PadLeft(4, ' ');
                for (int i = 0; i < digits.Length; i++)
                {
                    var charKey = digits[i] == ' ' ? "7seg_Null" : $"7seg_{digits[i]}";
                    if (_dataManager.RetsubanImagePathDictionary.TryGetValue(charKey, out var digitKey))
                    {
                        imagePaths[i + 1] = digitKey;
                    }
                }

                // 接尾文字の画像
                var tailSymbolKey = GetTailSymbolKey(match.Groups[3].Value);
                if (_dataManager.RetsubanImagePathDictionary.TryGetValue(tailSymbolKey, out var tailKey))
                {
                    imagePaths[5] = tailKey;
                }

                return imagePaths;
            }
        }

        /// <summary>
        /// 先頭文字を取得するヘルパーメソッド
        /// </summary>
        /// <param name="head"></param>
        /// <returns></returns>
        private static string GetHeadSymbolKey(string head)
        {
            if (head == "回") return "16dot_Kai";
            if (head == "臨回") return "16dot_Rinkai";
            if (head == "試") return "16dot_Shi";
            if (head == "臨") return "16dot_Rin";
            return "16dot_Null";
        }

        /// <summary>
        /// 接尾文字を取得するヘルパーメソッド
        /// </summary>
        /// <param name="tail"></param>
        /// <returns></returns>
        private static string GetTailSymbolKey(string tail)
        {
            if (tail == "A") return "16dot_A";
            if (tail == "AX") return "16dot_AX";
            if (tail == "AY") return "16dot_AY";
            if (tail == "AZ") return "16dot_AZ";
            if (tail == "B") return "16dot_B";
            if (tail == "BX") return "16dot_BX";
            if (tail == "BY") return "16dot_BY";
            if (tail == "BZ") return "16dot_BZ";
            if (tail == "C") return "16dot_C";
            if (tail == "CX") return "16dot_CX";
            if (tail == "CY") return "16dot_CY";
            if (tail == "CZ") return "16dot_CZ";
            if (tail == "D") return "16dot_D";
            if (tail == "DX") return "16dot_DX";
            if (tail == "DY") return "16dot_DY";
            if (tail == "DZ") return "16dot_DZ";
            if (tail == "K") return "16dot_K";
            if (tail == "KX") return "16dot_KX";
            if (tail == "KY") return "16dot_KY";
            if (tail == "KZ") return "16dot_KZ";
            if (tail == "S") return "16dot_S";
            if (tail == "SX") return "16dot_SX";
            if (tail == "SY") return "16dot_SY";
            if (tail == "SZ") return "16dot_SZ";
            if (tail == "SA") return "16dot_SA";
            if (tail == "SAX") return "16dot_SAX";
            if (tail == "SAY") return "16dot_SAY";
            if (tail == "SAZ") return "16dot_SAZ";
            if (tail == "SB") return "16dot_SB";
            if (tail == "SBX") return "16dot_SBX";
            if (tail == "SBY") return "16dot_SBY";
            if (tail == "SBZ") return "16dot_SBZ";
            if (tail == "SC") return "16dot_SC";
            if (tail == "SCX") return "16dot_SCX";
            if (tail == "SCY") return "16dot_SCY";
            if (tail == "SCZ") return "16dot_SCZ";
            if (tail == "SD") return "16dot_SD";
            if (tail == "SDX") return "16dot_SDX";
            if (tail == "SDY") return "16dot_SDY";
            if (tail == "SDZ") return "16dot_SDZ";
            if (tail == "SK") return "16dot_SK";
            if (tail == "SKX") return "16dot_SKX";
            if (tail == "SKY") return "16dot_SKY";
            if (tail == "SKZ") return "16dot_SKZ";
            if (tail == "T") return "16dot_T";
            if (tail == "TA") return "16dot_TA";
            if (tail == "TAX") return "16dot_TAX";
            if (tail == "TAY") return "16dot_TAY";
            if (tail == "TAZ") return "16dot_TAZ";
            if (tail == "TB") return "16dot_TB";
            if (tail == "TBX") return "16dot_TBX";
            if (tail == "TBY") return "16dot_TBY";
            if (tail == "TBZ") return "16dot_TBZ";
            if (tail == "TC") return "16dot_TC";
            if (tail == "TCX") return "16dot_TCX";
            if (tail == "TCY") return "16dot_TCY";
            if (tail == "TCZ") return "16dot_TCZ";
            if (tail == "TD") return "16dot_TD";
            if (tail == "TDX") return "16dot_TDX";
            if (tail == "TDY") return "16dot_TDY";
            if (tail == "TDZ") return "16dot_TDZ";
            if (tail == "TX") return "16dot_TX";
            if (tail == "TXX") return "16dot_TXX";
            if (tail == "TXY") return "16dot_TXY";
            if (tail == "TXZ") return "16dot_TXZ";
            if (tail == "X") return "16dot_X";
            if (tail == "Y") return "16dot_Y";
            if (tail == "Z") return "16dot_Z";
            if (tail == "TX") return "16dot_X";
            if (tail == "TY") return "16dot_Y";
            if (tail == "TZ") return "16dot_Z";
            return "16dot_Null";
        }

        /// <summary>
        /// 列番表示画像Pathの一覧を取得
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> GetRetsubanImagePath(string folderPath)
        {
            var retsubanDictionary = new Dictionary<string, string>();

            foreach (var filePath in Directory.EnumerateFiles(folderPath, "*.png"))
            {
                var normalizedPath = Path.GetFullPath(filePath).Replace('\\', '/');
                retsubanDictionary.Add(Path.GetFileNameWithoutExtension(filePath), normalizedPath);
            }
            return retsubanDictionary;
        }

        /// <summary>
        /// 列番表示画像Pathの一覧をキャッシュに追加
        /// </summary>
        /// <param name="retsubanDictionary"></param>
        public static void SetRetsubanImagePathToCache(Dictionary<string, string> retsubanDictionary)
        {
            foreach (var path in retsubanDictionary.Values)
            {
                var imageSource = new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute))
                {
                    CacheOption = BitmapCacheOption.OnLoad
                };
                ImageCacheManager.AddImage(path, imageSource); // キャッシュに追加
            }
        }
    }
}
