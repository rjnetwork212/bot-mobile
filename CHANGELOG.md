# Changelog — BotMobile

Format berdasarkan [Keep a Changelog](https://keepachangelog.com/id/1.1.0/).

## [0.5.0] - 2026-08-31

### Added — GUI redesign (Stitch design system) + metode Selector/GraphQL
- **GUI baru berdasarkan design system Stitch "RJ TRACK v3"** (dark terminal): deep-space #0A0E17, surface #101827, cyan #00E5FF, 4px corners, sidebar 220px dengan cyan left-edge marker nav, top bar + tombol "Jalankan Bot", feature cards, log console JetBrains Mono. Dikode ulang sebagai 4 page UserControl terpisah (`Views/Pages/`) — Fitur Bot, Data Akun, UID, Link.
- **Metode per fitur: GraphQL vs Selector** — bisa dipilih dari dropdown di popup konfigurasi fitur (dan badge metode di card). Arsip:
  - `Features/FeatureModes.cs` — konstanta metode.
  - `Features/Selector/UiSelector.cs` — klik elemen UI by label multi-bahasa (reliable click chain pointerdown→…→click, port add_friend button-mode Bot_Ngekeng).
  - Jalur Selector implementasi nyata: **Add Friend** (buka profil m.facebook/uid → klik "Tambahkan Teman" → verifikasi "Batalkan"), **Confirm Friend** (/friends/requests → klik "Konfirmasi"), **Add Suggestions** (/friends/suggestions → scroll + klik Add).
  - Fitur browser (Buka Beranda, Scroll Feed, Buka Profil) ditandai metode SELECTOR; fitur pure-GraphQL tetap GRAPHQL.
- Pool UID/Link terhubung dari tab UID & Link (tombol "Import ke Pool" + stats di status bar).

### Changed
- Feature cards menampilkan nomor urut mono, badge metode berwarna (cyan=GRAPHQL, amber=SELECTOR), toggle switch, gear config, tombol ▲▼.
- `IBotFeature.Modes` (default interface member) — fitur baru otomatis satu metode; override untuk dukung dua.

## [0.4.0] - 2026-08-31

### Added — Port fitur dari Bot_Ngekeng (subagent research 3 area: konten, sosial, infra)
- **fbhelper.js** (`Resources/`, embedded): port inti `window.__bot` dari Bot_Ngekeng — token scraper, graphql fetch wrapper, addFriend (3 varian payload), confirmFriendRequest, fetchPendingRequests, fetchSuggestions, createGroup, postStatus, setBioText, activateProMode, uploadPhotoGeneric, createStory, setStoryPrivacy, setProfilePic.
- **FbHelper.cs**: jembatan C# → window.__mfb (inject + call + parse JSON toleran tipe bool/number/string).
- **PoolDb.cs**: pool target UID & link (port `take_targets`/`take_one_link`) — atomic claim (guard `used_at IS NULL`), mark result, rollback, sweep orphan.
- **10 fitur bot** (file terpisah di `Features/`): Sambung GraphQL, Add Friend (pool target), Add Suggestions (PYMK), Confirm Friend, Buat Group, Post Status, Upload Story, Foto Profil, Set Bio, Mode Profesional — semuanya daftar di FeatureRegistry + state DB + popup config otomatis.
- **Fingerprint validasi empiris**: UA-CH metadata (`UserAgentMetadata` mobile=true/Android/model) — tanpa ini Chrome 151 kirim `Sec-CH-UA-Mobile:?0` dan FB serve desktop (bug "tampilan bukan mobile").
- **Cookie flow benar**: goto origin dulu → CDP `Network.setCookie` → refresh → cek login (page-level `SetCookieAsync` sebelum navigasi pertama tidak masuk jar).
- **CLI research**: `--probe-cookie`, `--probe-token`, `--probe-traffic` (listen endpoint asli FB).

### Validated (probe & run nyata, bukan asumsi)
- Login cookies fresh akun_2 → `CookieOk` (UA desktop & mobile dua-duanya dapat session)
- Buat group → thread_id nyata dibuat (`MessengerGroupCreateMutation` doc 577041672419534)
- Set bio → `bio_set` (`ProfileCometSetBioMutation` doc 26634540449575467)
- GraphQL query (FriendingCometRootQuery) → respons JSON Comet valid

### Critical findings (riset, bukan tebakan)
- **m.facebook mobile web TIDAK punya fb_dtsg** (DTSG stack tidak ada) — pakai `lsd` + `jazoest` dari form input.
- **Doc_id Comet DITOLAK dari konteks m.facebook** (`error 1357004`) — fitur GraphQL harus jalan di konteks desktop `www/web.facebook.com` (pola sama dengan Bot_Ngekeng yang memang desktop). Fase 1 login = mobile (bentuk APK), fase 2 fitur = tab www (UA desktop).
- Page-level `SetCookieAsync` sebelum navigasi pertama: cookie tidak masuk jar (dicek via `GetCookiesAsync` pada halaman FB).

### Fixed
- "Failed to open a new tab" → reuse tab about:blank bawaan Chrome.
- Login password dihalangi cookie mati yang menempel (halaman render varian saved-login tanpa form) → clear cookies sebelum login password.
- Parser JSON helper: `{"ok":true}` (boolean) membuat `Dictionary<string,string>` gagal → outcome salah dilaporkan "empty_response" padahal mutasi sukses.
- `WaitLoginResultAsync` crash "Execution context destroyed" saat login sukses (navigasi di tengah poll) → poll tahan-exception.
- Tab `about:blank` ekstra.

### Notes
- Seluruh doc_id rentan di-rotate FB — kini terpusat di `fbhelper.js` (satu file, mudah di-update).
- `post_status` legacy (UI composer) masih disabled; pakai `Post Status` GraphQL.

## [0.3.0] - 2026-08-31
- Menu Fitur Bot (reorder + toggle + config popup, state SQLite), Data Akun (bulk import), UID, Link.
- Parser akun auto-detect kolom by pola (cookie/email/2FA).
- 4 fitur dasar + OpenGraphQlFeature; engine skip fitur setelah session expired.
- Probe CLI: `--probe`, `--probe-login`, `--run`.

## [0.2.0] - 2026-08-31
- Fingerprint mobile FB IAB + viewport Android touch (pool 4 device, hash UID).
- stealth.js embedded; paralel max 3 Chrome.

## [0.1.0] - 2026-08-31
- App GUI .NET 8 murni (Avalonia + PuppeteerSharp + SQLite), login cookies → password, import/export akun, selftest.
