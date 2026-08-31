# Changelog — BotMobile

Format berdasarkan [Keep a Changelog](https://keepachangelog.com/id/1.1.0/).

## [0.3.0] - 2026-08-31

### Added
- **Menu Fitur Bot**: daftar fitur dengan urutan bebas (tombol ▲▼), toggle aktif/nonaktif (checkbox), dan popup konfigurasi per fitur (field otomatis dari definisi fitur). State (urutan/aktif/params) tersimpan di SQLite.
- **Menu Data Akun**: tabel akun + **Import Bulk** langsung dari textarea (1 akun per baris, tanpa file). Tambah kolom Email & 2FA.
- **Menu UID**: daftar UID target (1 per baris), tombol "Isi dari Data Akun". Dipakai fitur seperti Buka Profil.
- **Menu Link**: daftar link target + tombol buka link terpilih.
- 4 fitur bot built-in (file terpisah di `Features/`): `Buka Beranda`, `Scroll Feed`, `Buka Profil`, `Post Status` (belum aktif — selector composer belum di-probe).
- Parser akun **auto-detect kolom by pola** (cookie/email/secret-2FA dideteksi dari isi, bukan posisi) — mendukung `uid|pass`, `uid|pass|cookies`, `uid|pass|cookies|secret2fa`, `uid|pass|email|cookies`, `uid|pass|cookies|email`, dan campuran bebas.
- Probe runner CLI: `--probe <uid>` (dump elemen login/feed) dan `--probe-login <uid>` (uji login password nyata).
- CLI `--run [uid]`: jalankan engine penuh (login + fitur berurutan).
- Status akun baru: `Blocked` (rate-limit FB), `NoCookies`.

### Changed
- Login password dipindah ke `FacebookLogin` dengan klasifikasi hasil tervalidasi probe nyata: `ok` / `checkpoint` / `wrongpass` / `blocked` / `unknown`.
- Validasi login sukses kini mewajibkan cookie `c_user`+`xs` **dan** URL bukan halaman login (cookie `c_user` ternyata bisa menempel walau session mati — hasil probe).
- Semua navigation di fitur diberi timeout 30 dtk + fallback agar engine tidak hang.
- Kode dipisah per fitur/kepentingan: `Features/IBotFeature.cs`, `Fingerprint.cs`, `StealthLoader`, `LoginSelectors.cs`, `FacebookLogin.cs`, `BotEngine.cs`, `ProbeRunner.cs`.

### Validated (probe nyata, bukan asumsi)
- Selector login m.facebook: `input[name='email']`, `input[name='pass']`, `button[name='login']` ✓
- Teks rate-limit FB: "You've tried to log in too many times..." → status `Blocked` ✓
- Akun pertama login via cookies: `CookieOk` tersimpan + cookies diperbarui ✓
- Akun kedua: password login → `Blocked` (rate-limit FB) ✓

## [0.2.0] - 2026-08-31

### Added
- Fingerprint mobile "seperti APK FB": UA `[FB_IAB/FB4A;FBAV/...]` + viewport Android touch (pool 4 device, hash dari UID).
- Stealth injection (`stealth.js` embedded): `navigator.webdriver`, chrome object, plugins, WebGL, canvas noise.
- Paralel login max 3 Chrome instance.

## [0.1.0] - 2026-08-31

### Added
- App GUI .NET 8 murni (tanpa Python): Avalonia 11 (Windows + Linux) + PuppeteerSharp + SQLite.
- Tabel akun: import/export, tambah/edit/hapus, login cookies → password fallback.
- Import dari file `akun.txt` format `uid|pass|cookie`.
- Selftest (`--selftest`): parser + DB roundtrip.
