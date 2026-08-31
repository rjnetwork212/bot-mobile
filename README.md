# BotMobile

App GUI C# (.NET 8) untuk login & bot Facebook, cross-platform **Windows + Linux**, tanpa Python.

## Stack
- **GUI**: Avalonia 11 (TabControl: Fitur Bot / Data Akun / UID / Link)
- **Automation**: PuppeteerSharp (Chrome lokal, mode FB Android IAB "seperti APK" + stealth)
- **DB**: SQLite (`data/accounts.db`) — akun & state fitur bot

## Fitur
- Login per akun: **cookies dulu** → fallback **UID + password**
- Fingerprint mobile per-akun (UA `[FB_IAB/FB4A;...]`, viewport Android touch, device stabil dari hash UID)
- Status akun: `CookieOk`, `PasswordOk`, `Checkpoint`, `WrongPassword`, `Blocked`, `NoCookies`, `Failed`
- **Fitur Bot**: urutan bebas (▲▼), toggle aktif/nonaktif, popup config per fitur — state tersimpan DB
  - Buka Beranda, Scroll Feed, Buka Profil, Post Status (eksperimen)
- Import akun **bulk** (textarea, 1 per baris), auto-detect kolom:
  ```
  uid|pass
  uid|pass|cookies
  uid|pass|cookies|secret2fa
  uid|pass|email|cookies
  ```
  Kolom dideteksi dari isi (cookie ada `=;` + nama cookie FB, email ada `@.`, 2FA base32 16-64 char).

## Menjalankan
```bash
cd BotMobile
~/.dotnet/dotnet run            # atau dotnet run
```

CLI:
```bash
dotnet run -- --selftest                     # test parser + DB
dotnet run -- --import file.txt              # import dari file
dotnet run -- --run <uid>                    # login + jalankan fitur untuk 1 akun
dotnet run -- --probe <uid>                  # dump elemen halaman login/feed FB
dotnet run -- --probe-login <uid>            # uji alur password login (headless)
```

Publish:
```bash
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r win-x64 --self-contained
```

## Struktur
```
BotMobile/
  Program.cs              # bootstrap + CLI
  Models/Account.cs       # model akun (INPC thread-safe)
  Services/
    AccountDb.cs          # SQLite accounts
    AccountParser.cs      # parser multi-format
    BotEngine.cs          # engine run (login → fitur berurutan)
    FacebookLogin.cs      # alur login + klasifikasi hasil
    LoginSelectors.cs     # selector TERVERIFIKASI probe (jangan ubah asal-asalan)
    Fingerprint.cs        # device pool + UA FB IAB
    BotService.cs         # util Chrome
    ProbeRunner.cs        # probe elemen FB nyata
    SelfTest.cs
  Features/               # 1 file per fitur + registry + state store
  Resources/stealth.js    # anti-detect (embedded)
  Views/                  # MainWindow (4 tab), FeatureConfigWindow, EditAccountWindow
```

## Catatan
- Selector login diverifikasi dengan probe nyata (`--probe`). Jika login gagal, probe dulu, jangan menebak.
- Checkpoint/2FA tidak diotomasi — dilaporkan sebagai status.
- Data `Data_Testing/akun.txt` hanya untuk testing lokal, **jangan commit**.

## Repo
- GitHub: https://github.com/rjnetwork212/bot-mobile
