using System;
using System.IO;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;
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

            // IHostの初期化
            _host = new HostBuilder()
                .ConfigureLogging(options => options.AddDebug())
                .ConfigureServices(services =>
                {
                    // DbContextの設定
                    services.AddDbContext<DbContext>(options =>
                    {
                        options.UseSqlite(
                            $"Filename={Path.Combine(Path.GetTempPath(), "trancrew-multiats-client.sqlite3")}");
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
