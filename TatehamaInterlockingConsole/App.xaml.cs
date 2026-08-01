using System;
using System.IO;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;
using TatehamaInterlockingConsole.Config;
using TatehamaInterlockingConsole.Manager;
using TatehamaInterlockingConsole.Services;
using TatehamaInterlockingConsole.ViewModels;
using TatehamaInterlockingConsole.Views;

namespace TatehamaInterlockingConsole
{
    public partial class App : Application
    {
        private IHost _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 環境選択(コマンドライン引数 or ダイアログ)
            EnvironmentType selectedEnvironment;
            string customLocalUrl = null;

            if (e.Args.Length > 0 && Enum.TryParse<EnvironmentType>(e.Args[0], true, out var envFromArgs))
            {
                selectedEnvironment = envFromArgs;
                // Local環境でURLが指定されている場合
                if (selectedEnvironment == EnvironmentType.Local && e.Args.Length > 1)
                {
                    customLocalUrl = e.Args[1];
                }
            }
            else
            {
                // MainWindowがまだ存在しないため、ダイアログを閉じただけで
                // アプリが終了しないようにシャットダウンモードを一時的に変更する
                var previousShutdownMode = ShutdownMode;
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var selectWindow = new EnvironmentSelectWindow();
                var result = selectWindow.ShowDialog();

                ShutdownMode = previousShutdownMode;

                if (result != true)
                {
                    Shutdown();
                    return;
                }

                selectedEnvironment = selectWindow.SelectedEnvironment;
                customLocalUrl = selectWindow.CustomLocalUrl;
            }

            // 2. ServerAddressクラスを初期化
            EnvironmentDefinition.Initialize(selectedEnvironment, customLocalUrl);

            // 3. 環境別のトークンDBファイル名を生成(環境間でトークンが混在しないようにする)
            var envName = selectedEnvironment.ToString().ToLower();
            var dbFileName = $"trancrew-multiats-console-{envName}.sqlite3";

            // IHostの初期化
            _host = new HostBuilder()
                .ConfigureLogging(options => options.AddDebug())
                .ConfigureServices(services =>
                {
                    // DbContextの設定
                    services.AddDbContext<DbContext>(options =>
                    {
                        options.UseSqlite(
                            $"Filename={Path.Combine(Path.GetTempPath(), dbFileName)}");
                        options.UseOpenIddict();
                    });

                    // OpenIddictの設定
                    services.AddOpenIddict()

                        .AddCore(options =>
                        {
                            options.UseEntityFrameworkCore()
                                .UseDbContext<DbContext>();
                        })

                        .AddClient(options =>
                        {
                            options.AllowAuthorizationCodeFlow()
                                .AllowRefreshTokenFlow();

                            options.AddDevelopmentEncryptionCertificate()
                                .AddDevelopmentSigningCertificate();

                            options.UseSystemIntegration();

                            options.UseSystemNetHttp()
                                .SetProductInformation(typeof(App).Assembly);

                            options.AddRegistration(new OpenIddictClientRegistration
                            {
                                Issuer = new Uri(ServerAddress.SignalAddress, UriKind.Absolute),
                                ClientId = "MultiATS_Client",
                                RedirectUri = new Uri("/", UriKind.Relative),
                            });
                        });

                    // 必要なサービスの登録
                    services.AddSingleton(TimeService.Instance);
                    services.AddSingleton(DataManager.Instance);
                    // ViewModelの登録
                    services.AddTransient<MainViewModel>();
                    // ウィンドウの登録
                    services.AddTransient<MainWindow>();
                    // Workerサービスを登録
                    services.AddHostedService<Worker>();
                })
                .Build();

            // MainWindowのインスタンスを取得して表示
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // ホストの実行
            await _host.RunAsync();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host is not null)
                await _host.StopAsync();

            base.OnExit(e);
        }
    }
}
