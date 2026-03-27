using SharpDX.XAudio2;
using SharpDX.Multimedia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TatehamaInterlockingConsole.Manager;

namespace TatehamaInterlockingConsole.Services
{
    /// <summary>
    /// Soundクラス
    /// </summary>
    public class Sound
    {
        private static readonly Sound _instance = new();
        public static Sound Instance => _instance;

        private XAudio2 xAudio2;
        private MasteringVoice masteringVoice;
        public Dictionary<string, SourceVoice> SoundSource = new();
        public Dictionary<string, AudioBuffer> SoundBuffer = new();
        public Dictionary<string, float> SoundVolumeDic = new();
        public float fMasterVolume = 1.0f;

        // クラスフィールドに音声データのキャッシュを追加
        private Dictionary<string, byte[]> SoundDataCache = new();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public Sound()
        {

        }

        /// <summary>
        /// サウンド初期化メソッド　
        /// </summary>
        public void SoundInit()
        {
            try
            {
                // XAudio2とMasteringVoiceを初期化
                xAudio2 = new();
                masteringVoice = new(xAudio2);
                LoadSoundFiles();
                LoopSoundAllPlay();

                // ループ処理開始
                Task.Run(() => UpdateLoop());
            }
            catch
            {
                CustomMessage.Show("サウンドデバイスの生成に失敗しました。", "エラー");
            }
        }

        /// <summary>
        /// ループ処理
        /// </summary>
        /// <returns></returns>
        private async Task UpdateLoop()
        {
            while (true)
            {
                var timer = Task.Delay(50);
                await timer;

                var stationSettingList = DataManager.Instance.StationSettingList;
                var directionStateList = DataManager.Instance.DirectionStateList;
                var activeStationList = DataManager.Instance.ActiveStationsList;

                // 接近警報鳴動処理
                var activeAlarmKeys = new HashSet<string>();
                var tmpActiveAlarmsList = DataManager.Instance.ActiveAlarmsList.ToList();

                foreach (var activeAlarm in tmpActiveAlarmsList)
                {
                    var stationSetting = stationSettingList
                        .FirstOrDefault(s => s.StationName == activeAlarm.StationName);

                    if (stationSetting == null) continue;

                    bool isPlay = activeStationList
                        .Any(s => s == stationSetting.StationNumber);

                    if (activeAlarm.IsUpSide)
                    {
                        var alarmName = stationSetting.UpSideAlarmName + "_loop";
                        if (isPlay) activeAlarmKeys.Add(alarmName);
                        SetAlarmVolumeBasedOnType(stationSetting.UpSideAlarmType, alarmName, isPlay);
                    }
                    else
                    {
                        var alarmName = stationSetting.DownSideAlarmName + "_loop";
                        if (isPlay) activeAlarmKeys.Add(alarmName);
                        SetAlarmVolumeBasedOnType(stationSetting.DownSideAlarmType, alarmName, isPlay);
                    }
                }
                // ActiveAlarmsListに存在しない警報音声を停止
                foreach (var alarmKey in SoundSource.Keys.Where(k => k.Contains("_loop") && k.Contains("TH")))
                {
                    if (!activeAlarmKeys.Contains(alarmKey))
                    {
                        SetVolume(alarmKey, 0.0f);
                    }
                }

                // 方向てこ警報鳴動処理
                foreach (var direction in directionStateList)
                {
                    var stationSetting = stationSettingList
                        .FirstOrDefault(s => s.StationNumber == direction.Name.Split('_')[0]);
                    bool isPlay = activeStationList
                        .Any(s => s == stationSetting.StationNumber);

                    if (stationSetting != null)
                    {
                        // 方向てこ状態が変化してから2秒以内なら処理
                        if ((DateTime.Now - direction.UpdateTime).TotalSeconds < 2.0d)
                        {
                            SetAlarmVolumeBasedOnType(stationSetting.DirectionAlarmType, stationSetting.DirectionAlarmName + "_loop", isPlay);
                            direction.IsAlarmPlayed = true;
                        }
                        // 再生中かつ2秒以上経過している場合は音声停止
                        else if (direction.IsAlarmPlayed)
                        {
                            SetAlarmVolumeBasedOnType(stationSetting.DirectionAlarmType, stationSetting.DirectionAlarmName + "_loop", false);
                            direction.IsAlarmPlayed = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 音声ファイル読み込みメソッド
        /// </summary>
        private void LoadSoundFiles()
        {
            try
            {
                // 既存のSourceVoiceとAudioBufferを解放してクリア
                ClearSoundData();

                // Soundフォルダ内の全てのwavファイルを取得
                var soundFiles = Directory.GetFiles($".\\Sound", "*.wav").ToList();
                var stationSettingList = DataManager.Instance.StationSettingList;

                // サウンド読み込み
                foreach (var filePath in soundFiles)
                {
                    if (!File.Exists(filePath))
                    {
                        CustomMessage.Show($"サウンドファイルが見つかりません: {filePath}", "エラー");
                        continue;
                    }

                    // サウンドファイルを読み込んでバイト配列として保持
                    WaveFormat waveFormat;
                    byte[] audioData;

                    using (var stream = new SoundStream(File.OpenRead(filePath)))
                    {
                        waveFormat = stream.Format;
                        using (var dataStream = stream.ToDataStream())
                        {
                            audioData = new byte[dataStream.Length];
                            dataStream.Read(audioData, 0, (int)dataStream.Length);
                        }
                    }

                    var fileName = Path.GetFileNameWithoutExtension(filePath);

                    if (!fileName.Contains("_loop") && !fileName.Contains("_end"))
                    {
                        CreateSourceVoice(fileName, waveFormat, audioData, 1.0f);
                    }
                    else
                    {
                        // キャッシュに保存
                        SoundDataCache[fileName] = audioData;

                        var baseFileName = fileName.Replace("_loop", "").Replace("_end", "");

                        foreach (var stationSetting in stationSettingList)
                        {
                            if (stationSetting.UpSideAlarmName.Contains(baseFileName)
                                || stationSetting.DownSideAlarmName.Contains(baseFileName)
                                || stationSetting.DirectionAlarmName.Contains(baseFileName))
                            {
                                if (fileName.Contains("_loop"))
                                {
                                    if (stationSetting.UpSideAlarmName.Contains(baseFileName))
                                    {
                                        var loopFileName = stationSetting.UpSideAlarmName + "_loop";
                                        CreateSourceVoice(loopFileName, waveFormat, audioData, 0.0f);
                                    }
                                    if (stationSetting.DownSideAlarmName.Contains(baseFileName))
                                    {
                                        var loopFileName = stationSetting.DownSideAlarmName + "_loop";
                                        CreateSourceVoice(loopFileName, waveFormat, audioData, 0.0f);
                                    }
                                    if (stationSetting.DirectionAlarmName.Contains(baseFileName))
                                    {
                                        var loopFileName = stationSetting.DirectionAlarmName + "_loop";
                                        CreateSourceVoice(loopFileName, waveFormat, audioData, 0.0f);
                                    }
                                }
                                else if (fileName.Contains("_end"))
                                {
                                    if (stationSetting.UpSideAlarmName.Contains(baseFileName))
                                    {
                                        var endFileName = stationSetting.UpSideAlarmName + "_end";
                                        CreateSourceVoice(endFileName, waveFormat, audioData, 1.0f);
                                    }
                                    if (stationSetting.DownSideAlarmName.Contains(baseFileName))
                                    {
                                        var endFileName = stationSetting.DownSideAlarmName + "_end";
                                        CreateSourceVoice(endFileName, waveFormat, audioData, 1.0f);
                                    }
                                    if (stationSetting.DirectionAlarmName.Contains(baseFileName))
                                    {
                                        var endFileName = stationSetting.DirectionAlarmName + "_end";
                                        CreateSourceVoice(endFileName, waveFormat, audioData, 1.0f);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessage.Show($"サウンドファイルの読み込みに失敗しました。\n{ex.Message}", "エラー");
            }
        }

        /// <summary>
        /// SourceVoiceとAudioBufferを作成して辞書に追加
        /// </summary>
        private void CreateSourceVoice(string key, WaveFormat waveFormat, byte[] audioData, float initialVolume)
        {
            var sourceVoice = new SourceVoice(xAudio2, waveFormat, VoiceFlags.None, maxFrequencyRatio: 4.0f);
            var buffer = new AudioBuffer
            {
                Stream = SharpDX.DataStream.Create(audioData, true, false),
                AudioBytes = audioData.Length,
                LoopCount = 0,
                LoopBegin = 0,
                LoopLength = 0,
                PlayBegin = 0,
                PlayLength = 0,
                Flags = BufferFlags.EndOfStream
            };

            SoundSource[key] = sourceVoice;
            SoundBuffer[key] = buffer;
            SoundVolumeDic[key] = initialVolume;
        }

        /// <summary>
        /// 既存の音声データをクリア
        /// </summary>
        private void ClearSoundData()
        {
            // すべてのSourceVoiceを停止して解放
            SoundAllStop();

            // AudioBufferのStreamを解放
            foreach (var buffer in SoundBuffer.Values)
            {
                buffer.Stream?.Dispose();
            }

            SoundSource.Clear();
            SoundBuffer.Clear();
            SoundDataCache.Clear();
        }

        /// <summary>
        /// ループ音声再生メソッド
        /// </summary>
        private void LoopSoundAllPlay()
        {
            foreach (var voice in SoundSource.Keys.Where(s => s.Contains("TH") && s.Contains("loop")))
            {
                if (voice != null)
                {
                    SoundPlay(voice, true, 0.0f);
                }
            }
        }

        /// <summary>
        /// ループ音声停止メソッド
        /// </summary>
        /// <param name="isUpSide"></param>
        public void LoopSoundAllStop(string stationName, bool isUpSide)
        {
            var stationSetting = DataManager.Instance.StationSettingList
                .FirstOrDefault(s => s.StationName == stationName);

            if (isUpSide)
            {
                SetVolume(stationSetting.UpSideAlarmName + "_loop", 0.0f);
                SoundPlay(stationSetting.UpSideAlarmName + "_loop", false);
            }
            else
            {
                SetVolume(stationSetting.DownSideAlarmName + "_loop", 0.0f);
                SoundPlay(stationSetting.UpSideAlarmName + "_loop", false);
            }
        }

        /// <summary>
        /// 音声再生メソッド
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="isLoop"></param>
        /// <param name="volume"></param>
        /// <param name="pitch"></param>
        public void SoundPlay(string fileName, bool isLoop, float volume = 1.0f, float pitch = 1.0f)
        {
            if (!SoundSource.TryGetValue(fileName, out SourceVoice sourceVoice) || sourceVoice == null) return;

            // 既に再生中の場合は処理しない
            if (sourceVoice.State.BuffersQueued > 0) return;

            // 音量とピッチを設定
            SetVolume(fileName, volume);
            SetPitch(fileName, pitch);

            // ループ再生の設定
            if (isLoop)
                SoundBuffer[fileName].LoopCount = 255;
            else
                SoundBuffer[fileName].LoopCount = 0;

            // 再生位置の設定
            SoundBuffer[fileName].PlayBegin = 0;
            SoundBuffer[fileName].PlayLength = 0;

            var buffer = SoundBuffer[fileName];

            // バッファをソースに渡して再生開始
            sourceVoice.SubmitSourceBuffer(buffer, null);
            sourceVoice.Start();
        }

        /// <summary>
        /// 全音声停止メソッド
        /// </summary>
        public void SoundAllStop()
        {
            // 全サウンドを停止
            foreach (var sourceVoice in SoundSource.Values)
            {
                if (sourceVoice != null)
                {
                    sourceVoice.Stop();
                    sourceVoice.FlushSourceBuffers();
                }
            }
        }

        /// <summary>
        /// 音声停止メソッド
        /// </summary>
        /// <param name="fileName"></param>
        public void SoundStop(string fileName)
        {
            if (!SoundSource.TryGetValue(fileName, out SourceVoice value) || value == null) return;

            // 既に停止している場合は処理しない
            if (value.State.BuffersQueued == 0) return;

            value.Stop(0);
            value.FlushSourceBuffers();
        }

        /// <summary>
        /// 音量設定メソッド
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="volume"></param>
        public void SetVolume(string fileName, float volume)
        {
            if (!SoundSource.ContainsKey(fileName) || SoundSource[fileName] == null) return;
            if (volume < 0) volume = 0;
            SoundSource[fileName].SetVolume(fMasterVolume * volume);
            SoundVolumeDic[fileName] = volume;
        }

        /// <summary>
        /// ピッチ設定メソッド
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="pitch"></param>
        public void SetPitch(string fileName, float pitch)
        {
            if (!SoundSource.ContainsKey(fileName) || SoundSource[fileName] == null) return;
            SoundSource[fileName].SetFrequencyRatio(pitch);
        }

        /// <summary>
        /// アラーム音量設定メソッド
        /// </summary>
        /// <param name="alarmType"></param>
        /// <param name="alarmName"></param>
        public void SetAlarmVolumeBasedOnType(string alarmType, string alarmName, bool isPlay)
        {
            if (!isPlay) { SetVolume(alarmName, 0.0f); return; }

            if (alarmType.Equals("SHORT", StringComparison.CurrentCultureIgnoreCase))
            {
                bool flagValue = DataManager.Instance.FlagValue;
                float volume = flagValue ? 1.0f : 0.0f;
                SetVolume(alarmName, volume);
            }
            else
            {
                SetVolume(alarmName, 1.0f);
            }
        }

        /// <summary>
        /// マスターボリューム設定メソッド
        /// </summary>
        /// <param name="volume"></param>
        public void SetMasterVolume(float volume)
        {
            if (volume < 0) volume = 0;
            fMasterVolume = volume;

            // 全ての音声にマスターボリュームを設定
            foreach (var sourceVoice in SoundSource.Keys)
            {
                if (SoundSource[sourceVoice] != null)
                {
                    SetVolume(sourceVoice, SoundVolumeDic[sourceVoice]);
                }
            }
        }

        /// <summary>
        /// リソース解放
        /// </summary>
        public void Dispose()
        {
            // 既存の音声データをクリア
            ClearSoundData();

            // リソースを解放
            masteringVoice.Dispose();
            xAudio2.Dispose();
        }
    }
}