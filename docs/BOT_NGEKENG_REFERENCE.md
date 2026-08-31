# BOT_NGEKENG — Referensi Port ke BotMobile (C#)

Hasil bedah menyeluruh project referensi `/home/rj-network/Project/Bot_Ngekeng` (Python/nodriver,
~43K baris) oleh 5 sesi riset. Sumber kebenaran untuk port fitur; perubahan perilaku FB
dicatat di sini, bukan ditebak.

## Arsitektur Bot_Ngekeng
- Chrome asli + persistent profile pool (10 slot) + CDP via nodriver. Tanpa stealth lib —
  kepercayaan dari profile "aged" + VPN extension + timezone override.
- Semua fitur = inject `_HELPER_JS` (`window.__bot`, ~2300 baris JS di `core/fb_client.py`)
  → fetch same-origin dari halaman login → `/api/graphql/` & `/messaging/send/`.
- Runner: asyncio task per akun, concurrency gate (hot-reload), retry fitur max 5
  (recoverable: suspicious/checkpoint/no_tokens/... vs terminal: disabled/2fa/email/captcha).
- Semua di **www.facebook.com desktop**.

## Konstanta doc_id (fb_client.py:29-167 — rentan rotate, override via config)
| Fungsi | doc_id | Catatan |
|---|---|---|
| AddFriend send | 9012643805460802 | FriendingCometFriendRequestSendMutation, 3 varian payload |
| PYMK suggestions | 24534454102821334 | FriendingCometPYMKPanelPaginationQuery; butuh navigate /friends/suggestions dulu (error 1357031 tanpa itu) |
| Pending requests | 4499082396829105 | FriendingCometRootQuery, variables {requests_initial:1000} |
| Confirm request | 27260433676892385 / fallback 4379690545439556 | friending_channel FRIENDS_HOME_REQUESTS |
| Friends list | 29498081956473146 (+3 rotasi: 5587634024588688, 5300653556637337, 6385412561502450) | FriendingCometFriendsListPaginationQuery |
| Create group | 577041672419534 | MessengerGroupCreateMutation |
| Fetch groups threadlist | 1349387578499440 | via /api/graphqlbatch/ (NOT FOUND utk context akun baru 2026-09); legacy q multipart masih jalan |
| Add group member | Mercury /messaging/send/ + 5300653556637337 / 6822238384462615 | log:subscribe |
| Send message | Mercury + Lightspeed 9944623912245126 (version 24039080412369523, task label '46') | |
| Post timeline | 26200680759550052 | ComposerStoryCreateMutation; link preview dulu 31695001416753529 |
| Set bio | 26634540449575467 | ProfileCometSetBioMutation |
| City/hometown/website | 8728297980596938 / 9302920536386241 / 27881789778101663 + typeahead 24825162803742896 | |
| Story create / privacy | 26770527039211553 / 26547817461576340 | audiences: [{stories:{self:{target_id}}}] |
| Profile pic set | 9015637238455590 | upload dulu /profile/picture/upload/?photo_source=57 |
| Pro mode | 10032435873458768 | category_id 2347428775505624, surface PERMANENT_ENTRY |
| Change locale | 29960775910235124 | useCometLocaleSelectorLanguageChangeMutation {locale, referrer, fallback_locale:null} |
| Reels create (API) | 27822979900685146 | ComposerStoryCreateMutation; UI flow = jalur utama |

## Token scraping (getTokensFromPage)
fb_dtsg: DTSGInitialData → require('DTSGInitData') → input[name=fb_dtsg] → regex HTML.
lsd: window.LSD.token → input → regex `"LSD",[],{"token":"..."`.
jazoest: `"2" + sum(charCodes(fb_dtsg))` (desktop); mobile web: ambil dari input[name=jazoest].
userId: CurrentUserInitialData.USER_ID → cookie c_user → regex `"USER_ID":"(\d+)"`.
**Mobile web m.facebook: TIDAK ADA fb_dtsg/DTSG** — lsd+jazoest saja; doc_id Comet ditolak (1357004).

## Relogin state machine (relogin.py, urutan deteksi)
1. login_identify / device_based_login → terminal login_failed
2. /auth_platform/limbo → tunggu redirect 30s
3. /checkpoint + form 2FA → needs_2fa
4. /checkpoint klasifikasi: actionable tier identity→suspicious, onboarding→onboarding_gate,
   numeric-only path tanpa tombol → checkpoint_disabled (terminal)
5. two_step pre_auth → tunggu 15s
6. consent_flow (URL /privacy/consent|policies|cookie|gdpr + teks) → dismiss (radio "gratis dengan iklan" skip paid markers)
7. captcha iframe (iframe#captcha-recaptcha dll) → terminal captcha_blocked — cek SEBELUM body text
8. body text: disabled→checkpoint_disabled, suspicious→suspicious, captcha→captcha_blocked, 2fa→needs_2fa, email→needs_email_verify
9. saved_profile_resume: tombol Continue (multi-bahasa aria-label^) — MENANG atas login_form;
   klik → modal password → fill+submit atomic per-form
10. login_form: input[name=email] + input[name=pass] visible → fill → button[name=login]
11. needs_2fa: SEL_2FA_CODE (approvals_code) → TOTP pyotp (secret normalize: strip spasi+upper) timeout 60s
12. needs_email_verify: tempmail rjnetwork (tools.rjnetwork.site/api/tempmail, X-Session-Token,
    poll 5s, OTP regex \b\d{4,8}\b, timeout 120s)
13. unknown → diagnostic + retry; logged_in → dismiss cookie banner

Selector login: email/pass/login button multi-fallback (m_login_email, royal_login_button, loginbutton).
Post-login error "couldn't be processed" → suspicious. Max attempts 2.

## Label multi-bahasa (fb_lang.py — port subset ke Services/FbLang.cs)
Set terpenting: CONTINUE, LOGIN_BUTTON, ADD_FRIEND, CONFIRM, DECLINE, CANCEL_REQUEST,
MODAL_GATE (hanya "oke" exact — "ok" tidak, false-positive "Book/Cookie"), ADS_FREE_RADIO +
ADS_PAID_SKIP_MARKERS, COOKIE_ALLOW (ordered prioritas) / COOKIE_DECLINE, NEEDS_2FA,
SUSPICIOUS, DISABLED, CAPTCHA_BLOCKED, NEEDS_EMAIL_CONFIRM, USE_ANOTHER_PROFILE,
REELS_NEXT/SAVE/PUBLISH, AUDIENCE_PUBLIC/FRIENDS, PROFILE_REELS_TAB/CREATE_REEL,
REELS_UPLOAD_MEDIA, AUTOMATION_WARNING. Full list verbatim ada di fb_lang.py:23-1644.

## Upload Reels (UI flow — satu-satunya jalur, API 404)
www desktop + viewport force 1366x850. Urutan: profile.php?id= → tab Reels (scoring href
sk=reels +80) → baseline reel ids → Create reel (loop 20×) → upload media (file input dalam
dialog reels-surface, videoAccept, BUKAN composer foto) → CDP DOM.setFileInputFiles
(depth=-1 pierce, dispatch input+change) → poll Next enabled + ready-after-upload (max 45s)
→ caption (CDP insertText, keyword scoring) → Continue → Next → Next(edit) → audience gate
→ publish (exactOnly: post/publish/posting/kirim/...) → submitted probe 25s → visibility
proof 45s (diff reel id vs baseline; a[href*=/reel/]). Gagal tengah → pending_verification
dihitung sukses. Stage rollback-safe list di upload_reels.py.
Foto → video: ffmpeg filter verbatim (scale+pad+zoompan+format=yuv420p, anullsrc audio,
libx264 main 4.1 bt709, aac 128k, faststart) — lihat photo_to_video.py:222.

## Tag Friend
Pool link TERPISAH (tag_friend_links). get_all_friends (rotasi 4 doc_id, cursor, gender filter,
hanya id numeric). createTagPost: 2 langkah — link preview (31695001416753529 → share_scrape_data)
→ ComposerStoryCreateMutation attachments:[{link:{share_scrape_data}}] + with_tags_ids.
Rate-limited response: `__ar:1` tanpa data/errors.

## Caption builder (16 bahasa, 268 hook / 153 cta — Resources/caption_templates.json)
Pola: `{opening_emoji} {hook}~ {cta} {cta_emoji} {link}` + `\n\n{hashtags}`.
lang: random/specific/mixed/custom (+custom pool {link} placeholder). PRESET_CAPTIONS 35%
hanya jika use_preset (default OFF — moderation risk).

## Recovery pattern (common.py)
wait_user_loaded (poll token match 15s) → recover_gate_then_wait_user (dismiss actionable
checkpoint "ini saya"/... ) → verify_session_alive → recover_session_for_retry.
_AUTH_ERROR_MARKERS: no_tokens, userid=0, 1357001, 1357053, "log in to continue", multi-bahasa.
dismiss_modal_gate: 4 fase × max 8 iter (gate exact → ads radio (skip paid markers €/$/subscribe)
→ continue → no-match streak≥2 break), reliableClick = scrollIntoView + pointer/mouse chain.
post_feature_gate_maintenance antar fitur (refresh home + banner + gate + consent + ads done).

## Pools & DB
targets/links/tag_friend_links: atomic claim (used_at IS NULL guard), mark result, rollback
pre-attempt, sweep orphan >6 jam. consumer tag = "{uid}#{fitur}".

## Hasil khusus BotMobile (probe 2026-08/09)
- 2 fase: login mobile (FB_IAB) → fitur desktop (doc_id Comet). Lihat memory fb-graphql-architecture.
- graphqlbatch NOT FOUND; legacy q multipart jalan tapi nodes[] kosong utk akun/group baru →
  pakai runtime context (thread dari Buat Group).
- /messaging/send/ mercury dari web.facebook.com: HTML redirect — perlu endpoint desktop
  messenger yang benar (Lightspeed fallback 9944623912245126 = kandidat; perlu validasi).
