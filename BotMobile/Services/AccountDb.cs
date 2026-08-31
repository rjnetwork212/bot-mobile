using BotMobile.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BotMobile.Services;

public class AccountDb : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly object _lock = new();

    public AccountDb(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS accounts(
                uid        TEXT PRIMARY KEY,
                password   TEXT NOT NULL DEFAULT '',
                cookies    TEXT NOT NULL DEFAULT '',
                email      TEXT NOT NULL DEFAULT '',
                secret2fa  TEXT NOT NULL DEFAULT '',
                status     TEXT NOT NULL DEFAULT 'NotLogged',
                last_login TEXT NOT NULL DEFAULT ''
            );
            """;
        cmd.ExecuteNonQuery();
        // migrasi ringan: tambah kolom email/secret2fa jika DB lama belum punya
        foreach (var col in new[] { "email", "secret2fa" })
        {
            using var chk = _conn.CreateCommand();
            chk.CommandText = "SELECT COUNT(*) FROM pragma_table_info('accounts') WHERE name=$c";
            chk.Parameters.AddWithValue("$c", col);
            if (Convert.ToInt64(chk.ExecuteScalar()!) == 0)
            {
                using var alt = _conn.CreateCommand();
                alt.CommandText = $"ALTER TABLE accounts ADD COLUMN {col} TEXT NOT NULL DEFAULT ''";
                alt.ExecuteNonQuery();
            }
        }
    }

    public List<Account> GetAll()
    {
        lock (_lock)
        {
            var list = new List<Account>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT uid, password, cookies, email, secret2fa, status, last_login FROM accounts ORDER BY rowid";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Account
                {
                    Uid = r.GetString(0),
                    Password = r.GetString(1),
                    Cookies = r.GetString(2),
                    Email = r.GetString(3),
                    Secret2Fa = r.GetString(4),
                    Status = r.GetString(5),
                    LastLogin = r.GetString(6),
                });
            }
            return list;
        }
    }

    public void Upsert(Account a)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO accounts(uid, password, cookies, email, secret2fa, status, last_login)
                VALUES($uid, $pw, $ck, $em, $tf, $st, $ll)
                ON CONFLICT(uid) DO UPDATE SET
                    password=$pw, cookies=$ck, email=$em, secret2fa=$tf, status=$st, last_login=$ll
                """;
            cmd.Parameters.AddWithValue("$uid", a.Uid);
            cmd.Parameters.AddWithValue("$pw", a.Password);
            cmd.Parameters.AddWithValue("$ck", a.Cookies);
            cmd.Parameters.AddWithValue("$em", a.Email);
            cmd.Parameters.AddWithValue("$tf", a.Secret2Fa);
            cmd.Parameters.AddWithValue("$st", a.Status);
            cmd.Parameters.AddWithValue("$ll", a.LastLogin);
            cmd.ExecuteNonQuery();
        }
    }

    public void Delete(string uid)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM accounts WHERE uid=$uid";
            cmd.Parameters.AddWithValue("$uid", uid);
            cmd.ExecuteNonQuery();
        }
    }

    public int ImportLines(IEnumerable<string> lines)
    {
        int n = 0;
        foreach (var line in lines)
        {
            var acc = AccountParser.ParseLine(line);
            if (acc == null) continue;
            Upsert(acc);
            n++;
        }
        return n;
    }

    public void Export(string file) =>
        File.WriteAllLines(file, GetAll().Select(AccountParser.ToLine));

    public void Dispose() => _conn.Dispose();
}
