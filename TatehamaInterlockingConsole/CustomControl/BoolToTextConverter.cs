using System;
using System.Globalization;
using System.Windows.Data;

namespace TatehamaInterlockingConsole.CustomControl
{
    /// <summary>
    /// bool値を任意の2つの文字列に変換する汎用コンバーター
    /// ConverterParameter 例: "有効,無効" → true:有効, false:無効
    /// ※ ConverterParameter は必須
    /// </summary>
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string paramStr)
                throw new ArgumentException("ConverterParameter を 'true時の文字列,false時の文字列' の形式で指定してください。");

            var parts = paramStr.Split(',');
            if (parts.Length != 2)
                throw new ArgumentException("ConverterParameter は 'true時の文字列,false時の文字列' の形式で指定してください。");

            if (value is bool boolean)
            {
                return boolean ? parts[0] : parts[1];
            }
            return "不明";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
