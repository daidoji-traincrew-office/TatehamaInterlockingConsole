using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using TatehamaInterlockingConsole.Manager;
using TatehamaInterlockingConsole.Services;
using TatehamaInterlockingConsole.ViewModels;

namespace TatehamaInterlockingConsole.Models
{
    /// <summary>
    /// サーバー通信クラス
    /// </summary>
    public class ServerCommunication : IAsyncDisposable
    {
        private readonly TimeSpan _renewMargin = TimeSpan.FromMinutes(1);
        private readonly OpenIddictClientService _openIddictClientService;
        private readonly DataManager _dataManager;
        private readonly DataUpdateViewModel _dataUpdateViewModel;
        private static HubConnection _connection;
        private static bool _isUpdateLoopRunning = false;
        private const string HubConnectionName = "interlocking";
        
        private string _token = "";
        private string _refreshToken = "";
        private DateTimeOffset _tokenExpiration = DateTimeOffset.MinValue;
        private bool _eventHandlersSet = false;
        private const int ReconnectIntervalMs = 500;

        /// <summary>
        /// サーバー接続状態変更イベント
        /// </summary>
        public event Action<bool> ConnectionStatusChanged;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ServerCommunication(OpenIddictClientService openIddictClientService)
        {
            _openIddictClientService = openIddictClientService;
            _dataManager = DataManager.Instance;
            _dataUpdateViewModel = DataUpdateViewModel.Instance;

            if (!_isUpdateLoopRunning)
            {
                _isUpdateLoopRunning = true;

                // ループ処理開始
                Task.Run(() => UpdateLoop());
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
                var timer = Task.Delay(500);

                // サーバー接続状態変更イベント発火
                ConnectionStatusChanged?.Invoke(_dataManager.ServerConnected);
                await timer;
            }
        }
        
        /// <summary>
        /// インタラクティブ認証を行い、SignalR接続を試みる
        /// </summary>
        /// <returns>ユーザーのアクションが必要かどうか</returns>
        public async Task Authorize()
        {
            // 認証を行う
            var isAuthenticated = await InteractiveAuthenticateAsync();
            if (!isAuthenticated)
            {
                return;
            }

            await DisposeAndStopConnectionAsync(CancellationToken.None); // 古いクライアントを破棄
            InitializeConnection(); // 新しいクライアントを初期化
            // 接続を試みる
            var isActionNeeded = await ConnectAsync();
            if (isActionNeeded)
            {
                return;
            }

            SetEventHandlers(); // イベントハンドラを設定
        }

        /// <summary>
        /// ユーザー認証（インタラクティブ認証のみ）
        /// </summary>
        /// <returns>認証に成功した場合true、失敗した場合false</returns>
        private async Task<bool> InteractiveAuthenticateAsync()
        {
            return await InteractiveAuthenticateAsync(CancellationToken.None);
        }

        /// <summary>
        /// ユーザー認証（インタラクティブ認証のみ、キャンセラブル）
        /// </summary>
        /// <param name="cancellationToken">キャンセラレーショントークン</param>
        /// <returns>認証に成功した場合true、失敗した場合false</returns>
        private async Task<bool> InteractiveAuthenticateAsync(CancellationToken cancellationToken)
        {
            try
            {
                // ブラウザで認証要求
                var result = await _openIddictClientService.ChallengeInteractivelyAsync(new()
                {
                    CancellationToken = cancellationToken,
                    Scopes = [OpenIddictConstants.Scopes.OfflineAccess]
                });

                // 認証完了まで待機
                var resultAuth = await _openIddictClientService.AuthenticateInteractivelyAsync(new()
                {
                    CancellationToken = cancellationToken,
                    Nonce = result.Nonce
                });

                // 認証成功(トークン取得)
                _token = resultAuth.BackchannelAccessToken;
                _tokenExpiration = resultAuth.BackchannelAccessTokenExpirationDate ?? DateTimeOffset.MinValue;
                _refreshToken = resultAuth.RefreshToken;
                return true;
            }
            catch (OpenIddictExceptions.ProtocolException exception) when (exception.Error == OpenIddictConstants.Errors.AccessDenied)
            {
                // ログインしたユーザーがサーバーにいないか、入鋏ロールがついてない
                CustomMessage.Show("認証が拒否されました。\n司令主任に連絡してください。", "認証拒否", exception, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (OpenIddictExceptions.ProtocolException exception) when (exception.Error == OpenIddictConstants.Errors.ServerError)
            {
                // サーバーでトラブル発生
                CustomMessage.Show("認証時にサーバーでエラーが発生しました。", "サーバーエラー", exception, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception exception)
            {
                // その他別な理由で認証失敗
                CustomMessage.Show("認証に失敗しました。", "認証失敗", exception, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// HubConnection初期化
        /// </summary>
        private void InitializeConnection()
        {
            if (_connection != null)
            {
                throw new InvalidOperationException("_connection is already initialized.");
            }

            // HubConnectionの作成
            _connection = new HubConnectionBuilder()
                .WithUrl($"{ServerAddress.SignalAddress}/hub/{HubConnectionName}?access_token={_token}")
                .Build();
            _eventHandlersSet = false;
        }

        /// <summary>
        /// サーバー接続
        /// </summary>
        /// <returns>ユーザーのアクションが必要かどうか</returns>
        private async Task<bool> ConnectAsync()
        {
            // サーバー接続
            while (!_dataManager.ServerConnected)
            {
                try
                {
                    await _connection.StartAsync();
                    Debug.WriteLine("Connected");
                    _dataManager.ServerConnected = true;
                }
                catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
                {
                    // 該当Hubにアクセスするためのロールが無い
                    CustomMessage.Show("接続が拒否されました。\n付与されたロールを確認の上、司令主任に連絡してください。", "ロール不一致", exception,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return true;
                }
                // Disposeされた接続を使用しようとした場合のエラー
                catch (InvalidOperationException)
                {
                    Debug.WriteLine("Maybe using disposed connection");
                    _dataManager.ServerConnected = false;
                    ConnectionStatusChanged?.Invoke(false);
                    // 一旦接続を破棄して再初期化
                    await DisposeAndStopConnectionAsync(CancellationToken.None);
                    InitializeConnection();
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"Connection Error!! {exception.Message}");
                    _dataManager.ServerConnected = false;
                    ConnectionStatusChanged?.Invoke(false);

                    var result = CustomMessage.Show("接続に失敗しました。\n再接続しますか？", "接続失敗", exception, MessageBoxButton.YesNo,
                        MessageBoxImage.Error);
                    if (result == MessageBoxResult.Yes)
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// イベントハンドラ設定
        /// </summary>
        private void SetEventHandlers()
        {
            if (_connection == null)
            {
                throw new InvalidOperationException("_connection is not initialized.");
            }
            if (_eventHandlersSet)
            {
                return; // イベントハンドラは一度だけ設定する
            }

            _connection.On<DatabaseOperational.DataFromServer>("ReceiveData", OnReceiveDataFromServer);

            _connection.Reconnecting += async(exception) =>
            {
                Debug.WriteLine("Disconnected");
                // サーバー接続状態を更新
                _dataManager.ServerConnected = false;
                ConnectionStatusChanged?.Invoke(false);
                
                // 例外が発生していない場合(=正常終了時)は何もしない
                if (exception == null)
                {
                    return;
                }

                // 例外が発生した場合はログに出力し再接続
                Debug.WriteLine($"Exception: {exception.Message}\nStackTrace: {exception.StackTrace}");
                
                // 再接続処理を開始
                await TryReconnectAsync();
            };

            _eventHandlersSet = true;
        }

        /// <summary>
        /// 再接続処理
        /// </summary>
        /// <returns></returns>
        private async Task TryReconnectAsync()
        {
            while (!_dataManager.ServerConnected)
            {
                try
                {
                    var isActionNeeded = await TryReconnectOnceAsync();
                    if (isActionNeeded)
                    {
                        Debug.WriteLine("Action needed after reconnection.");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Reconnect failed: {ex.Message}");
                }

                if (_connection != null && _connection.State == HubConnectionState.Connected)
                {
                    Debug.WriteLine("Reconnected successfully.");
                    _dataManager.ServerConnected = true;
                    ConnectionStatusChanged?.Invoke(true);
                    break;
                }

                await Task.Delay(ReconnectIntervalMs);
            }
        }

        /// <summary>
        /// 1回の再接続試行
        /// </summary>
        /// <returns>ユーザーによるアクションが必要かどうか</returns>
        private async Task<bool> TryReconnectOnceAsync()
        {
            // トークンが切れていない場合 かつ 切れるまで余裕がある場合はそのまま再接続
            if (_tokenExpiration > DateTimeOffset.UtcNow + _renewMargin)
            {
                Debug.WriteLine("Try reconnect with current token...");
                var isActionNeeded = await ConnectAsync();
                if (isActionNeeded)
                {
                    Debug.WriteLine("Action needed after reconnect.");
                    return true;
                }
                SetEventHandlers();
                Debug.WriteLine("Reconnected with current token.");
                return false;
            }

            // トークンが切れていてリフレッシュトークンが有効な場合はリフレッシュ
            try
            {
                Debug.WriteLine("Try refresh token...");
                await RefreshTokenAsync(CancellationToken.None);
                await DisposeAndStopConnectionAsync(CancellationToken.None);
                InitializeConnection();
                var isActionNeeded = await ConnectAsync();
                if (isActionNeeded)
                {
                    Debug.WriteLine("Action needed after reconnect.");
                    return true;
                }
                SetEventHandlers();
                Debug.WriteLine("Reconnected with refreshed token.");
                return false;
            }
            catch (OpenIddictExceptions.ProtocolException ex)
                when (ex.Error is
                          OpenIddictConstants.Errors.InvalidToken
                          or OpenIddictConstants.Errors.InvalidGrant
                          or OpenIddictConstants.Errors.ExpiredToken)
            {
                // ignore: リフレッシュトークンが無効な場合
            }
            catch (InvalidOperationException)
            {
                // ignore: リフレッシュトークンが設定されていない場合
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during token refresh: {ex.Message}");
                throw;
            }

            // リフレッシュトークンが無効な場合、再認証が必要
            Debug.WriteLine("Refresh token is invalid or expired.");
            var result = CustomMessage.Show("トークンが切れました。\n再認証してください。", "認証失敗", 
                MessageBoxButton.YesNo, MessageBoxImage.Error);
            if (result == MessageBoxResult.Yes)
            {
                await Authorize();
            }

            return true;
        }

        /// <summary>
        /// リフレッシュトークンを使用してトークンを更新
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task RefreshTokenAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_refreshToken))
            {
                throw new InvalidOperationException("Refresh token is not set.");
            }

            var result = await _openIddictClientService.AuthenticateWithRefreshTokenAsync(new()
            {
                CancellationToken = cancellationToken,
                RefreshToken = _refreshToken
            });

            _token = result.AccessToken;
            _tokenExpiration = result.AccessTokenExpirationDate ?? DateTimeOffset.MinValue;
            _refreshToken = result.RefreshToken;
            Debug.WriteLine("Token refreshed successfully");
        }

        /// <summary>
        /// 接続の破棄と停止
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task DisposeAndStopConnectionAsync(CancellationToken cancellationToken)
        {
            if (_connection == null)
            {
                return;
            }

            try
            {
                await _connection.StopAsync(cancellationToken);
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing connection: {ex.Message}");
            }
            finally
            {
                _connection = null;
                _eventHandlersSet = false;
            }
        }

        /// <summary>
        /// サーバーからデータが来たときの処理
        /// </summary>
        /// <param name="data">サーバーから受信されたデータ</param>
        /// <returns></returns>
        private void OnReceiveDataFromServer(DatabaseOperational.DataFromServer data)
        {
            try
            {
                if (data == null)
                {
                    Debug.WriteLine("Failed to receive Data.");
                    return;
                }

                // 運用クラスに代入
                if (_dataManager.DataFromServer == null)
                {
                    _dataManager.DataFromServer = data;
                }
                else
                {
                    // 変更があれば更新
                    foreach (var property in data.GetType().GetProperties())
                    {
                        var newValue = property.GetValue(data);
                        var oldValue = property.GetValue(_dataManager.DataFromServer);
                        if (newValue != null && !newValue.Equals(oldValue))
                        {
                            property.SetValue(_dataManager.DataFromServer, newValue);
                        }
                    }
                }

                // 方向てこ情報を保存
                if (data.Directions != null)
                {
                    _dataManager.DirectionStateList = data.Directions.Select(d =>
                    {
                        var existingDirection =
                            _dataManager.DirectionStateList?.FirstOrDefault(ds => ds.Name == d.Name);
                        if (existingDirection != null)
                        {
                            // 値が変更されている場合のみ更新
                            if (existingDirection.State != d.State)
                            {
                                existingDirection.State = d.State;
                                existingDirection.UpdateTime = DateTime.Now;
                                existingDirection.IsAlarmPlayed = false;
                            }

                            return existingDirection;
                        }
                        else
                        {
                            // 新しいデータの場合は追加
                            return new DirectionStateList
                            {
                                Name = d.Name,
                                State = d.State,
                                UpdateTime = DateTime.Now,
                                IsAlarmPlayed = false
                            };
                        }
                    }).ToList();
                }

                // コントロール更新処理
                _dataUpdateViewModel.UpdateControl(_dataManager.DataFromServer);

                // 物理ボタン情報を前回の値として保存
                if (data.PhysicalButtons != null)
                {
                    _dataManager.PhysicalButtonOldList = data.PhysicalButtons.Select(d =>
                        new DatabaseOperational.DestinationButtonData
                        {
                            Name = d.Name,
                            IsRaised = d.IsRaised,
                            OperatedAt = d.OperatedAt
                        }).ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error server receiving: {ex.Message}{ex.StackTrace}");
            }
        }

        /// <summary>
        /// サーバーへ物理てこイベント送信用データをリクエスト
        /// </summary>
        /// <param name="leverData"></param>
        /// <returns></returns>
        public async Task SendLeverEventDataRequestToServerAsync(DatabaseOperational.LeverData leverData)
        {
            try
            {
                // サーバーメソッドの呼び出し
                var data = await _connection.InvokeAsync<DatabaseOperational.LeverData>(
                    "SetPhysicalLeverData", leverData);
                try
                {
                    if (data != null)
                    {
                        // 変更があれば更新
                        var lever = _dataManager.DataFromServer
                            .PhysicalLevers.FirstOrDefault(l => l.Name == data.Name);
                        foreach (var property in data.GetType().GetProperties())
                        {
                            var newValue = property.GetValue(data);
                            var oldValue = property.GetValue(lever);
                            if (newValue != null && !newValue.Equals(oldValue))
                            {
                                property.SetValue(lever, newValue);
                            }
                        }

                        // コントロール更新処理
                        _dataUpdateViewModel.UpdateControl(_dataManager.DataFromServer);
                    }
                    else
                    {
                        Debug.WriteLine("Failed to receive Data.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error server receiving: {ex.Message}{ex.StackTrace}");
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Failed to send event data to server: {exception.Message}");
            }
        }

        /// <summary>
        /// サーバーへ物理鍵てこイベント送信用データをリクエスト
        /// </summary>
        /// <param name="keyLeverData"></param>
        /// <returns></returns>
        public async Task<bool> SendKeyLeverEventDataRequestToServerAsync(DatabaseOperational.KeyLeverData keyLeverData)
        {
            bool isResult = true;
            try
            {
                // サーバーメソッドの呼び出し
                var data = await _connection.InvokeAsync<DatabaseOperational.KeyLeverData>(
                    "SetPhysicalKeyLeverData", keyLeverData);
                try
                {
                    if (data != null)
                    {
                        // 変更があれば更新
                        var keyLever = _dataManager.DataFromServer
                            .PhysicalKeyLevers.FirstOrDefault(l => l.Name == data.Name);

                        // 鍵挿入・非挿入操作の応答に変化が無ければ認証拒否として処理
                        if (data.IsKeyInserted == keyLever.IsKeyInserted)
                        {
                            isResult = false;
                        }

                        foreach (var property in data.GetType().GetProperties())
                        {
                            var newValue = property.GetValue(data);
                            var oldValue = property.GetValue(keyLever);
                            if (newValue != null && !newValue.Equals(oldValue))
                            {
                                property.SetValue(keyLever, newValue);
                            }
                        }

                        // コントロール更新処理
                        _dataUpdateViewModel.UpdateControl(_dataManager.DataFromServer);

                        
                    }
                    else
                    {
                        Debug.WriteLine("Failed to receive Data.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error server receiving: {ex.Message}{ex.StackTrace}");
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Failed to send event data to server: {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// サーバーへ着点ボタンイベント送信用データをリクエスト
        /// </summary>
        /// <param name="buttonData"></param>
        /// <returns></returns>
        public async Task SendButtonEventDataRequestToServerAsync(DatabaseOperational.DestinationButtonData buttonData)
        {
            try
            {
                // サーバーメソッドの呼び出し
                var data = await _connection.InvokeAsync<DatabaseOperational.DestinationButtonData>(
                    "SetDestinationButtonState", buttonData);
                try
                {
                    if (data != null)
                    {
                        // コントロール更新処理
                        _dataUpdateViewModel.UpdateControl(_dataManager.DataFromServer);

                        // 物理ボタン情報を前回の値として保存
                        if (_dataManager.PhysicalButtonOldList != null)
                        {
                            var existingButton = _dataManager.PhysicalButtonOldList
                                .FirstOrDefault(b => b.Name == data.Name);

                            if (existingButton != null)
                            {
                                existingButton.IsRaised = data.IsRaised;
                                existingButton.OperatedAt = data.OperatedAt;
                            }
                            else
                            {
                                _dataManager.PhysicalButtonOldList.Add(new DatabaseOperational.DestinationButtonData
                                {
                                    Name = data.Name,
                                    IsRaised = data.IsRaised,
                                    OperatedAt = data.OperatedAt
                                });
                            }
                        }
                        else
                        {
                            _dataManager.PhysicalButtonOldList = new List<DatabaseOperational.DestinationButtonData>
                            {
                                new DatabaseOperational.DestinationButtonData
                                {
                                    Name = data.Name,
                                    IsRaised = data.IsRaised,
                                    OperatedAt = data.OperatedAt
                                }
                            };
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Failed to receive Data.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error server receiving: {ex.Message}{ex.StackTrace}");
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Failed to send event data to server: {exception.Message}");
            }
        }

        /// <summary>
        /// サーバー切断
        /// </summary>
        /// <returns></returns>
        public async Task DisconnectAsync()
        {
            await DisposeAndStopConnectionAsync(CancellationToken.None);
            _dataManager.ServerConnected = false;
            ConnectionStatusChanged?.Invoke(false);
        }

        /// <summary>
        /// IAsyncDisposable実装
        /// </summary>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            await DisposeAndStopConnectionAsync(CancellationToken.None);
        }
    }
}