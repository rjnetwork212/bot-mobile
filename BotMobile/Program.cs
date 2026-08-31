using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;
using Avalonia.Threading;
using BotMobile;
using BotMobile.Features;
using BotMobile.Services;
using System;
using System.Linq;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--screenshot"))
        {
            ScreenshotRunner.Run();
            return;
        }
        if (args.Contains("--selftest"))
        {
            SelfTest.Run();
            return;
        }
        if (args.Length > 0 && (args[0] == "--import" || args[0] == "--login" || args[0] == "--probe" || args[0] == "--probe-login" || args[0] == "--probe-graphql" || args[0] == "--probe-cookie" || args[0] == "--probe-token" || args[0] == "--probe-traffic" || args[0] == "--run"))
        {
            Cli.Run(args).GetAwaiter().GetResult();
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}

public static class ScreenshotRunner
{
    // ponytail: headless render 1 frame utk QA visual (Wayland blok screen capture)
    public static void Run()
    {
        var app = AppBuilder.Configure<App>().UseHeadless(new Avalonia.Headless.AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();
        var lifetime = new Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime();
        app.SetupWithLifetime(lifetime);
        var win = new BotMobile.Views.MainWindow();
        win.Show();
        win.Width = 1180; win.Height = 760;

        // pump dispatcher sampai layout+render jalan
        foreach (var page in new[] { "fitur", "akun", "uid", "link" })
        {
            win.Navigate(page);
            Dispatcher.UIThread.RunJobs();
            System.Threading.Thread.Sleep(300);
            var frame = Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(win);
            if (frame == null) { Console.WriteLine($"frame {page} null"); continue; }
            using var fs = System.IO.File.Create($"/tmp/gui_{page}.png");
            frame.Save(fs);
            Console.WriteLine($"screenshot: /tmp/gui_{page}.png");
        }
    }
}

public static class Cli
{
    // ponytail: CLI minimal untuk testing headless; upgrade = arg parsing penuh kalau perlu
    public static async System.Threading.Tasks.Task Run(string[] args)
    {
        var dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "accounts.db");
        using var db = new AccountDb(dbPath);
        var bot = new BotService();
        bot.Log += l => Console.WriteLine(l);

        switch (args[0])
        {
            case "--import":
            {
                var file = args.Length > 1 ? args[1] : "Data_Testing/akun.txt";
                var n = db.ImportLines(System.IO.File.ReadAllLines(file));
                Console.WriteLine($"import {n} akun → {dbPath}");
                foreach (var a in db.GetAll())
                    Console.WriteLine($"  {a.Uid} | pw:{(a.Password.Length > 0 ? "ada" : "KOSONG")} | cookie:{(a.Cookies.Length > 0 ? $"{a.Cookies.Split(';').Length} item" : "kosong")}");
                break;
            }
            case "--login":
            {
                var targets = db.GetAll();
                if (args.Length > 1)
                    targets = targets.Where(a => a.Uid == args[1]).ToList();
                if (targets.Count == 0) { Console.WriteLine("tidak ada akun"); return; }
                await bot.LoginAccountsAsync(targets, acc => { db.Upsert(acc); return System.Threading.Tasks.Task.CompletedTask; });
                foreach (var a in db.GetAll())
                    Console.WriteLine($"HASIL {a.Uid}: {a.Status} | cookies: {(a.Cookies.Length > 0 ? "disimpan" : "kosong")}");
                break;
            }
            case "--probe":
            {
                await ProbeRunner.Run(args.Length > 1 ? args[1] : null);
                bot.Shutdown();
                return;
            }
            case "--probe-login":
            {
                await ProbeRunner.RunPasswordProbe(args.Length > 1 ? args[1] : "");
                bot.Shutdown();
                return;
            }
            case "--probe-graphql":
            {
                await GraphQLProbe.Run(args.Length > 1 ? args[1] : "");
                bot.Shutdown();
                return;
            }
            case "--probe-traffic":
            {
                await TrafficProbe.Run(args.Length > 1 ? args[1] : "");
                bot.Shutdown();
                return;
            }
            case "--probe-token":
            {
                await TokenProbe.Run(args.Length > 1 ? args[1] : "");
                bot.Shutdown();
                return;
            }
            case "--probe-cookie":
            {
                await CookieProbe.Run(args.Length > 1 ? args[1] : "");
                bot.Shutdown();
                return;
            }
            case "--run":
            {
                // run engine penuh (login + fitur) untuk 1 akun (default: akun pertama)
                var all = db.GetAll();
                var acc = args.Length > 1 ? all.FirstOrDefault(a => a.Uid == args[1]) : all.FirstOrDefault();
                if (acc == null) { Console.WriteLine("tidak ada akun"); return; }
                var features = FeatureStateStore.Load();
                if (features.Count == 0)
                {
                    features = FeatureRegistry.All.Select((f, i) => new FeatureConfig { FeatureId = f.Id, Enabled = f.DefaultEnabled, Order = i }).ToList();
                    FeatureStateStore.Save(features);
                }
                var engine = new BotEngine();
                engine.Log += l => Console.WriteLine(l);
                await engine.RunAsync(new[] { acc }, features, a => { db.Upsert(a); return System.Threading.Tasks.Task.CompletedTask; });
                Console.WriteLine($"HASIL {acc.Uid}: {acc.Status}");
                break;
            }
        }
        bot.Shutdown();
    }
}
