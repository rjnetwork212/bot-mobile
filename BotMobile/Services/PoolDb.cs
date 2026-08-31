using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace BotMobile.Services;

/// <summary>
/// Pool target UID & link (port Bot_Ngekeng db.py take_targets/take_one_link):
/// atomic claim dengan guard used_at IS NULL (anti double-claim antar akun paralel),
/// rollback untuk abort pre-attempt, sweep orphan.
/// </summary>
public class PoolDb : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly object _lock = new();

    static string DbPath => Path.Combine(AppContext.BaseDirectory, "data", "accounts.db");

    public PoolDb() : this(DbPath) { }

    public PoolDb(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS targets(
                uid TEXT PRIMARY KEY,
                used_at TEXT,
                used_by TEXT,
                success INTEGER,
                error TEXT,
                note TEXT,
                category TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_targets_used ON targets(used_at);
            CREATE TABLE IF NOT EXISTS links(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                url TEXT UNIQUE NOT NULL,
                used_at TEXT,
                used_by TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_links_used ON links(used_at);
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Klaim n UID yang belum dipakai (atomic). consumer = "uid#fitur" untuk audit.</summary>
    public List<string> TakeTargets(int n, string consumer, string category = "")
    {
        lock (_lock)
        {
            var outList = new List<string>();
            using var tx = _conn.BeginTransaction();
            using (var select = _conn.CreateCommand())
            {
                select.Transaction = tx;
                select.CommandText = category.Length > 0
                    ? "SELECT uid FROM targets WHERE used_at IS NULL AND category=$cat LIMIT $n"
                    : "SELECT uid FROM targets WHERE used_at IS NULL LIMIT $n";
                select.Parameters.AddWithValue("$n", n);
                select.Parameters.AddWithValue("$cat", category);
                using var r = select.ExecuteReader();
                while (r.Read()) outList.Add(r.GetString(0));
            }
            foreach (var uid in outList)
            {
                using var upd = _conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE targets SET used_at=datetime('now'), used_by=$c WHERE uid=$u AND used_at IS NULL";
                upd.Parameters.AddWithValue("$c", consumer);
                upd.Parameters.AddWithValue("$u", uid);
                upd.ExecuteNonQuery();
            }
            tx.Commit();
            return outList;
        }
    }

    public void MarkTargetResult(string uid, bool success, string? error = null)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE targets SET success=$s, error=$e WHERE uid=$u";
            cmd.Parameters.AddWithValue("$s", success ? 1 : 0);
            cmd.Parameters.AddWithValue("$e", (object?)error ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$u", uid);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Rolback klaim (hanya untuk abort pre-attempt — UID belum dicoba).</summary>
    public void RollbackTargets(IEnumerable<string> uids)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE targets SET used_at=NULL, used_by=NULL WHERE uid=$u AND success IS NULL";
            cmd.Parameters.Add("$u", SqliteType.Text);
            foreach (var uid in uids)
            {
                cmd.Parameters["$u"].Value = uid;
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>Klaim 1 link belum dipakai. null = habis.</summary>
    public string? TakeOneLink(string consumer)
    {
        lock (_lock)
        {
            using var tx = _conn.BeginTransaction();
            string? url = null;
            using (var select = _conn.CreateCommand())
            {
                select.Transaction = tx;
                select.CommandText = "SELECT url FROM links WHERE used_at IS NULL LIMIT 1";
                using var r = select.ExecuteReader();
                if (r.Read()) url = r.GetString(0);
            }
            if (url != null)
            {
                using var upd = _conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE links SET used_at=datetime('now'), used_by=$c WHERE url=$u AND used_at IS NULL";
                upd.Parameters.AddWithValue("$c", consumer);
                upd.Parameters.AddWithValue("$u", url);
                upd.ExecuteNonQuery();
            }
            tx.Commit();
            return url;
        }
    }

    public void RollbackLink(string url)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE links SET used_at=NULL, used_by=NULL WHERE url=$u";
            cmd.Parameters.AddWithValue("$u", url);
            cmd.ExecuteNonQuery();
        }
    }

    public void AddTargets(IEnumerable<string> uids, string category = "")
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO targets(uid, category) VALUES($u, $c)";
            cmd.Parameters.Add("$u", SqliteType.Text);
            cmd.Parameters.Add("$c", SqliteType.Text);
            foreach (var uid in uids)
            {
                cmd.Parameters["$u"].Value = uid.Trim();
                cmd.Parameters["$c"].Value = category;
                cmd.ExecuteNonQuery();
            }
        }
    }

    public void AddLinks(IEnumerable<string> urls)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO links(url) VALUES($u)";
            cmd.Parameters.Add("$u", SqliteType.Text);
            foreach (var url in urls)
            {
                var t = url.Trim();
                if (t.Length == 0) continue;
                cmd.Parameters["$u"].Value = t;
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>Reset klaim stuck (used_at terisi tapi success NULL lebih dari minAgeHours) — port sweep_orphan_targets.</summary>
    public int SweepOrphans(double minAgeHours = 6.0)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"""
                UPDATE targets SET used_at=NULL, used_by=NULL
                WHERE used_at IS NOT NULL AND success IS NULL
                  AND used_at < datetime('now', '-{minAgeHours:0.#} hours')
                """;
            return cmd.ExecuteNonQuery();
        }
    }

    public (int TargetsFree, int TargetsUsed, int LinksFree) Stats()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT
                  (SELECT COUNT(*) FROM targets WHERE used_at IS NULL),
                  (SELECT COUNT(*) FROM targets WHERE used_at IS NOT NULL),
                  (SELECT COUNT(*) FROM links WHERE used_at IS NULL)
                """;
            using var r = cmd.ExecuteReader();
            r.Read();
            return (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2));
        }
    }

    public void Dispose() => _conn.Dispose();
}
