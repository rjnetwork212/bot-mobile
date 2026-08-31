using System;
using System.Collections.Generic;
using System.Linq;

namespace BotMobile.Services;

/// <summary>
/// Label set multi-bahasa FB (port fb_lang.py Bot_Ngekeng, subset terpakai).
/// Semua lowercase, match: text == label || startsWith(label + " ") || contains.
/// </summary>
public static class FbLang
{
    static bool Matches(string text, string label) =>
        text == label || text.StartsWith(label + " ") || text.StartsWith(label + "\n") || text.Contains(label);

    public static bool MatchAny(string? text, IEnumerable<string> keywords)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var t = text.Trim().ToLowerInvariant();
        return keywords.Any(k => Matches(t, k));
    }

    public static string? DetectStateText(string? body) // urutan sesuai fb_lang.detect_state_text
    {
        if (string.IsNullOrEmpty(body)) return null;
        var t = body.ToLowerInvariant();
        if (MatchAny(t, Disabled)) return "checkpoint_disabled";
        if (MatchAny(t, Suspicious)) return "suspicious";
        if (MatchAny(t, CaptchaBlocked)) return "captcha_blocked";
        if (MatchAny(t, Needs2Fa)) return "needs_2fa";
        if (MatchAny(t, NeedsEmailConfirm)) return "needs_email_verify";
        return null;
    }

    // ---- checkpoint/verification states ----
    public static readonly string[] Disabled =
    {
        "akun dinonaktifkan", "akun anda dinonaktifkan", "telah dinonaktifkan", "akun ini telah dikunci",
        "akun anda telah dikunci", "kami telah mengunci", "kami memblokir akun", "akun diblokir",
        "account is disabled", "account has been disabled", "account disabled", "we locked your account",
        "account has been locked", "we blocked your account", "bloqueamos sua conta",
        "compte désactivé", "cuenta inhabilitada", "cuenta deshabilitada", "アカウントが無効",
        "kont zostało wyłączone", "zablokowaliśmy twoje konto", "wir haben dein konto gesperrt",
    };

    public static readonly string[] Suspicious =
    {
        "akun mencurigakan", "aktivitas mencurigakan", "amankan akun", "ini saya", "bukan saya",
        "verifikasi identitas", "suspicious activity", "secure your account", "verify your identity",
        "was this you", "this wasn't me", "this was me", "we noticed unusual", "is this you",
        "ben jij dit", "was jij dit", "dit was jij", "aanmeldpoging", "inlogpoging",
        "activity information from ad partners", "pode ter sido hackeada", "cuenta comprometida",
        "アカウントを保護", "czy to byłeś ty",
    };

    public static readonly string[] CaptchaBlocked =
    {
        "recaptcha enterprise", "recaptcha", "i'm not a robot", "bukan robot",
        "arkose", "funcaptcha", "prove you're human", "prove you are human", "complete the puzzle",
    };

    public static readonly string[] Needs2Fa =
    {
        "buka aplikasi autentikasi", "kode 6 digit", "kode autentikasi", "verifikasi dua faktor",
        "autentikasi dua faktor", "open your authentication app", "two-factor authentication",
        "enter the 6-digit code", "authentication code", "approvals code", "two_step",
        "log in on another device", "approve this login", "masuk di perangkat lain",
    };

    public static readonly string[] NeedsEmailConfirm =
    {
        "konfirmasi email", "kode konfirmasi", "masukkan kode konfirmasi", "verifikasi email",
        "kode verifikasi yang kami kirim", "confirmation code", "enter the confirmation code",
        "verify your email", "we sent you a code", "enter the code we sent",
    };

    public static readonly string[] AutomationWarning =
    {
        "mencurigai perilaku otomatis", "perilaku otomatis di akun",
        "we suspect automated behavior", "automated behavior on your account",
        "suspect automated behavior", "geautomatiseerd gedrag op je account",
        "perilaku otomatis", "automated behavior",
    };

    public static readonly string[] NegativeButtons = // JANGAN diklik
    {
        "bukan saya", "this wasn", "das war ich nicht", "no fui yo", "não fui eu",
        "non sono io", "dit was ik niet", "いいえ", "不是我", "not now", "nanti saja",
        "lain kali", "sekarang tidak", "cancel", "tutup",
    };

    public static readonly string[] IdentityButtons = // "ini saya" tier
    {
        "yes, this was me", "this was me", "this is me", "ini saya", "ya, ini saya", "saya",
        "continue", "lanjutkan", "teruskan", "yes", "da", "oui", "sí", "sim", "はい、私です", "예, 접니다",
    };

    public static readonly string[] DismissButtons =
    {
        "ignore", "abaikan", "ignorar", "close", "tutup", "fechar", "cerrar", "schließen",
        "dismiss", "descartar", "not now", "not-now", "agora não", "ahora no", "nanti saja",
        "pas maintenant", "jetzt nicht", "skip", "lewati", "omitir", "sauter", "pular", "非表示",
    };

    public static readonly string[] ContinueLabels =
    {
        "continue", "proceed", "next", "lanjutkan", "teruskan", "seterusnya", "weiter",
        "fortfahren", "doorgaan", "ga door", "continuer", "poursuivre", "continuar", "seguir",
        "seguinte", "avançar", "avancar", "prosegui", "dalej", "kontynuuj", "devam", "devam et",
        "pokračovat", "продолжить", "次へ", "次", "進む", "继续", "계속", "متابعة", "jatka",
        "ดำเนินการต่อ", "magpatuloy", "आगे बढ़ें",
    };

    public static readonly string[] LoginButtonLabels =
    {
        "log in", "login", "sign in", "masuk", "log masuk", "anmelden", "einloggen", "inloggen",
        "se connecter", "connexion", "iniciar sesión", "entrar", "fazer login", "accedi",
        "zaloguj się", "giriş yap", "oturum aç", "autentificare", "войти", "ログイン", "登录",
        "登入", "로그인", "تسجيل الدخول", "đăng nhập", "เข้าสู่ระบบ", "mag-log in",
    };

    public static readonly string[] UseAnotherProfileLabels =
    {
        "use another profile", "use a different profile", "choose another profile",
        "switch profile", "gunakan profil lain", "pakai profil lain", "pilih profil lain",
        "anderes profil verwenden", "ander profiel gebruiken", "utilisateur un autre profil",
        "usar otro perfil", "escolher outro perfil", "別のプロフィールを使う", "使用其他个人主页",
        "다른 프로필 사용", "penting: profil lain",
    };

    public static readonly string[] CookieAllowLabels = // ORDERED prioritas
    {
        "allow all cookies", "allow all", "izinkan semua cookie", "izinkan semua", "accept all cookies",
        "accept all", "terima semua", "allow cookies", "allow", "izinkan", "accept cookies",
        "accepter tous les cookies", "permitir todas las cookies", "alle cookies erlauben",
        "accetta tutti i cookie", "zezwól na wszystkie pliki cookie", "tüm çerezlere izin ver",
        "alle cookies toestaan", "允许所有cookie", "すべてのcookieを許可", "모든 쿠키 허용",
    };

    public static readonly string[] CookieDeclineLabels =
    {
        "decline optional cookies", "decline optional", "tolak cookie opsional", "tolak opsional",
        "optionale cookies ablehnen", "refuser les cookies optionnels", "rechazar cookies opcionales",
        "rifiuta cookie opzionali", "odrzuć opcjonalne pliki cookie",
    };

    public static readonly string[] ModalGateLabels =
    {
        "get started", "start", "begin", "let's go", "lets go", "start now", "begin now",
        "mulai", "ayo mulai", "mulai sekarang", "mulai saja", "oke", "empezar", "comenzar",
        "vamos", "iniciar", "começar", "commencer", "démarrer", "inizia", "loslegen",
        "los geht's", "beginnen", "aan de slag", "начать", "始める", "开始", "시작", "ابدأ",
        "başla", "rozpocznij", "bắt đầu", "เริ่ม", "magsimula",
    };

    public static readonly string[] AdsFreeRadioLabels =
    {
        "gunakan secara gratis dengan iklan", "lanjutkan dengan iklan", "lanjutkan gratis dengan iklan",
        "gratis dengan iklan", "use for free with ads", "continue with ads", "use facebook for free",
        "use for free", "free with ads", "see ads", "view ads", "seguir gratis con anuncios",
        "continuar con anuncios", "continuar gratuitamente com anúncios", "kostenlos mit werbung",
        "gratis gebruiken met advertenties", "magpatuloy gamit ang mga ad", "libre na may mga ad",
    };

    public static readonly string[] AdsPaidSkipMarkers =
    {
        "berlangganan", "subscribe", "subscription", "tanpa iklan", "without ads", "no ads",
        "ad-free", "€", "eur", "usd", "$", "al mes", "monthly", "abonnement", "subskrybuj",
        "bez reklam", "subskrypcja", "suscribirte", "sin anuncios",
    };

    // ---- selector mode / reels / composer ----
    public static readonly string[] AddFriendLabels =
    {
        "tambahkan teman", "tambah teman", "add friend", "i-add", "agregar amigo",
        "ajouter comme ami", "als freund hinzufügen", "aggiungi amico", "친구 추가", "フレンド追加",
    };

    public static readonly string[] CancelRequestLabels =
    {
        "batalkan permintaan", "batalkan", "cancel request", "cancel", "pending",
        "menunggu", "requested", "permintaan terkirim",
    };

    public static readonly string[] ConfirmLabels = { "konfirmasi", "confirm", "terima", "accept" };

    public static readonly string[] DeclineLabels = { "hapus", "delete", "tolak", "decline" };

    public static readonly string[] ReelsNextLabels =
    {
        "next", "berikutnya", "berikut", "lanjut", "siguiente", "avançar", "suivant", "avanti",
        "weiter", "volgende", "次へ", "下一步", "다음", "التالي", "ileri", "tiếp",
    };

    public static readonly string[] ReelsSaveLabels =
    {
        "save", "simpan", "save changes", "done", "selesai", "guardar", "salvar", "enregistrer",
        "speichern", "opslaan", "保存", "儲存", "저장", "kaydet",
    };

    public static readonly string[] ReelsPublishLabels =
    {
        "publish", "publish now", "publish reel", "post", "post now", "post reel", "posting",
        "publikasikan", "kirim", "share now", "share reel", "publicar", "postar", "publier",
        "condividi", "veröffentlichen", "投稿", "公開", "シェア", "共有", "发布", "分享", "게시",
    };

    public static readonly string[] AudiencePublicLabels =
    {
        "public", "publik", "semua orang", "público", "pubblico", "öffentlich", "公開",
        "全員に公開", "公开", "전체 공개", "công khai",
    };

    public static readonly string[] AudienceFriendsLabels =
    {
        "friends", "teman", "kawan", "rakan", "amigos", "amis", "freunde", "vrienden",
        "友達", "好友", "朋友", "친구",
    };

    public static readonly string[] ProfileReelsTabLabels =
    {
        "reels", "reel", "ril", "rilis", "short videos", "リール", "短视频", "릴스",
    };

    public static readonly string[] ProfileCreateReelLabels =
    {
        "create reel", "create a reel", "buat reel", "buat reels", "buat ril", "criar reel",
        "crear reel", "créer un reel", "リールを作成", "릴스 만들기",
    };

    public static readonly string[] ReelsUploadMediaLabels =
    {
        "add video", "select video", "upload video", "upload reel", "choose video", "choose file",
        "select file", "tambahkan video", "pilih video", "unggah video", "pilih file",
        "agregar video", "añadir video", "subir video", "escolher vídeo", "ajouter une vidéo",
        "aggiungi video", "carica video", "video hinzufügen", "動画を追加", "添加视频",
        "동영상 추가", "pilih file", "tambahkan foto",
    };

    public static readonly string[] ReelsBackLabels = { "back", "kembali", "voltar", "atrás", "zurück", "戻る" };
}
