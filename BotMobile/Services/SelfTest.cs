using BotMobile.Models;
using System;
using System.IO;
using System.Linq;

namespace BotMobile.Services;

public static class SelfTest
{
    public static void Run()
    {
        // --- parser multi-format ---
        var a = AccountParser.ParseLine("123|pw|c_user=1; xs=2") ?? throw new Exception("parser: uid|pass|cookie");
        if (a.Uid != "123" || a.Password != "pw" || a.Cookies != "c_user=1; xs=2") throw new Exception("parser: field salah");

        var b = AccountParser.ParseLine("u1|p1|JBSWY3DPEHPK3PXP|c_user=9; xs=8") ?? throw new Exception("parser: 2fa|cookie");
        if (b.Secret2Fa != "JBSWY3DPEHPK3PXP" || b.Cookies != "c_user=9; xs=8") throw new Exception("parser: 2fa|cookie salah");

        var c = AccountParser.ParseLine("u2|p2|a@mail.com|c_user=7; xs=6") ?? throw new Exception("parser: email|cookie");
        if (c.Email != "a@mail.com" || c.Cookies != "c_user=7; xs=6" || c.Secret2Fa != "") throw new Exception("parser: email|cookie salah");

        var d = AccountParser.ParseLine("u3|p3|c_user=5; xs=4|me@home.co") ?? throw new Exception("parser: cookie|email");
        if (d.Cookies != "c_user=5; xs=4" || d.Email != "me@home.co") throw new Exception("parser: cookie|email salah");

        var e = AccountParser.ParseLine("u4|p4") ?? throw new Exception("parser: uid|pass saja");
        if (e.Cookies != "" || e.Email != "" || e.Secret2Fa != "") throw new Exception("parser: kolom opsional harus kosong");

        if (AccountParser.ParseLine("badline") != null) throw new Exception("parser: line 1 kolom harus null");
        if (AccountParser.ParseLine("") != null) throw new Exception("parser: kosong harus null");
        if (AccountParser.ParseLine("# komentar") != null) throw new Exception("parser: komentar harus null");

        var f = AccountParser.ParseLine("u5|p5|c_user=1|xs=2") ?? throw new Exception("parser: pipe pisah cookie");
        if (f.Cookies != "c_user=1; xs=2") throw new Exception($"parser: pipe pisah cookie salah: {f.Cookies}");

        var g = AccountParser.ParseLine("1|p|c_user=1|JBSWY3DPEHPK3PXP|mail@x.com") ?? throw new Exception("parser: 3 kolom ekstra");
        if (g.Cookies != "c_user=1" || g.Secret2Fa != "JBSWY3DPEHPK3PXP" || g.Email != "mail@x.com") throw new Exception("parser: 3 kolom ekstra salah");

        var h = AccountParser.ParseLine("u|p|JBSWY3DP") ?? throw new Exception("parser: base32 pendek tetap akun valid");
        if (h.Secret2Fa != "") throw new Exception("parser: base32 pendek tak boleh masuk 2fa");

        // --- DB ---
        var tmp = Path.Combine(Path.GetTempPath(), "botmobile_selftest_" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var db = new AccountDb(Path.Combine(tmp, "accounts.db")))
            {
                if (db.ImportLines(new[] { "111|p1|", "222|p2|c_user=9; xs=8", "", "# cmt" }) != 2) throw new Exception("db: import count");
                var all = db.GetAll();
                if (all.Count != 2 || all[0].Uid != "111") throw new Exception("db: getall");
                db.Upsert(new Account { Uid = "111", Password = "p1x", Cookies = "c_user=7", Status = "CookieOk" });
                if (db.GetAll().Count != 2) throw new Exception("db: upsert duplikat");
                if (db.GetAll().First(x => x.Uid == "111").Password != "p1x") throw new Exception("db: upsert update");
                db.Upsert(new Account { Uid = "999", Password = "p9", Email = "e@x.co", Secret2Fa = "JBSWY3DPEHPK3PXP" });
                var r9 = db.GetAll().First(x => x.Uid == "999");
                if (r9.Email != "e@x.co" || r9.Secret2Fa != "JBSWY3DPEHPK3PXP") throw new Exception("db: kolom email/2fa");
                db.Delete("111");
                if (db.GetAll().Count != 2) throw new Exception("db: delete");
                var exp = Path.Combine(tmp, "export.txt");
                db.Export(exp);
                var lines = File.ReadAllLines(exp).ToList();
                if (!lines.Contains("222|p2|c_user=9; xs=8")) throw new Exception("db: export");
                if (!lines.Contains("999|p9|JBSWY3DPEHPK3PXP|e@x.co")) throw new Exception("db: export 2fa+email");
            }
            Console.WriteLine("selftest OK");
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }
}
