using System;
using System.Collections.Generic;

namespace TatehamaInterlockingConsole.Models
{
    /// <summary>
    /// サーバーデータ格納クラス
    /// </summary>
    public class DatabaseOperational
    {
        private static readonly DatabaseOperational _instance = new();
        public static DatabaseOperational Instance => _instance;

        /// <summary>
        /// 受信用データクラス
        /// </summary>
        public class DataFromServer
        {
            /// <summary>
            /// 軌道回路情報リスト
            /// </summary>
            public List<TrackCircuitData> TrackCircuits { get; set; }

            /// <summary>
            /// 転てつ器情報リスト
            /// </summary>
            public List<SwitchData> Points { get; set; }

            /// <summary>
            /// 信号機情報リスト
            /// </summary>
            public List<SignalData> Signals { get; set; }

            /// <summary>
            /// 物理てこ情報リスト
            /// </summary>
            public List<LeverData> PhysicalLevers { get; set; }

            /// <summary>
            /// 物理鍵てこ情報リスト
            /// </summary>
            public List<KeyLeverData> PhysicalKeyLevers { get; set; }

            /// <summary>
            /// 着点ボタン情報リスト
            /// </summary>
            public List<DestinationButtonData> PhysicalButtons { get; set; }

            /// <summary>
            /// 方向てこ情報リスト
            /// </summary>
            public List<DirectionData> Directions { get; set; }

            /// <summary>
            /// 列番情報リスト
            /// </summary>
            public List<RetsubanData> Retsubans { get; set; }

            /// <summary>
            /// 表示灯情報リスト
            /// </summary>
            public Dictionary<string, bool> Lamps { get; set; }

            /// <summary>
            /// TST時差
            /// </summary>
            public int TimeOffset { get; set; }
        }

        /// <summary>
        /// 方向てこデータクラス
        /// </summary>
        public class DirectionData
        {
            /// <summary>
            /// 方向てこ名称
            /// </summary>
            public string Name { get; set; } = "";
            /// <summary>
            /// 方向てこの値
            /// </summary>
            public EnumData.LNR State { get; set; } = EnumData.LNR.Left;
        }

        /// <summary>
        /// 軌道回路データクラス
        /// </summary>
        public class TrackCircuitData
        {
            /// <summary>
            /// 在線状態    
            /// </summary>
            public bool On { get; set; } = false;
            /// <summary>
            /// 鎖錠状態
            /// </summary>
            public bool Lock { get; set; } = false;
            /// <summary>
            /// 軌道回路を踏んだ列車の名前
            /// </summary>
            public string Last { get; set; } = null;
            /// <summary>
            /// 軌道回路名称
            /// </summary>
            public string Name { get; set; } = "";

            public override string ToString()
            {
                return $"{Name}";
            }
        }

        /// <summary>
        /// 転てつ器データクラス
        /// </summary>
        public class SwitchData
        {
            /// <summary>
            /// 転てつ器状態
            /// </summary>
            public EnumData.NRC State { get; set; } = EnumData.NRC.Center;
            /// <summary>
            /// 転てつ器名称
            /// </summary>
            public string Name { get; set; } = "";
        }

        /// <summary>
        /// 信号機データクラス
        /// </summary>
        public class SignalData
        {
            /// <summary>
            /// 信号機名称
            /// </summary>
            public string Name { get; init; } = "";
            /// <summary>
            /// 信号機現示
            /// </summary>
            public EnumData.Phase Phase { get; init; } = EnumData.Phase.None;
        }

        /// <summary>
        /// 列番データクラス
        /// </summary>
        public class RetsubanData
        {
            /// <summary>
            /// 列番名称
            /// </summary>
            public string Name { get; set; } = "";
            /// <summary>
            /// 列番
            /// </summary>
            public string Retsuban { get; set; } = "";
        }

        /// <summary>
        /// 物理てこデータクラス
        /// </summary>
        public class LeverData
        {
            /// <summary>
            /// 物理てこ名称
            /// </summary>
            public string Name { get; set; } = "";
            /// <summary>
            /// 物理てこの状態
            /// </summary>
            public EnumData.LCR State { get; set; } = EnumData.LCR.Center;
        }

        /// <summary>
        /// 物理鍵てこデータクラス
        /// </summary>
        public class KeyLeverData
        {
            /// <summary>
            /// 物理鍵てこ名称
            /// </summary>
            public string Name { get; set; } = "";
            /// <summary>
            /// 物理鍵てこの状態
            /// </summary>
            public EnumData.LNR State { get; set; } = EnumData.LNR.Normal;
            /// <summary>
            /// 物理鍵てこの鍵挿入状態
            /// </summary>
            public bool IsKeyInserted { get; set; } = false;
        }

        /// <summary>
        /// 着点ボタンデータクラス
        /// </summary>
        public class DestinationButtonData
        {
            /// <summary>
            /// 着点ボタン名称
            /// </summary>
            public required string Name { get; init; } = "";
            /// <summary>
            /// 着点ボタンの状態
            /// </summary>
            public EnumData.RaiseDrop IsRaised { get; set; } = EnumData.RaiseDrop.Raise;
            /// <summary>
            /// 着点ボタンの操作時間
            /// </summary>
            public DateTime OperatedAt { get; set; } = DateTime.MinValue;
        }
    }
}
