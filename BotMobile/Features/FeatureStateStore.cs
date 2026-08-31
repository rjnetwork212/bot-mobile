using BotMobile.Models;
using BotMobile.Services;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BotMobile.Features;

/// <summary>
/// State fitur bot (urutan, aktif/nonaktif, params) — tabel feature_state di DB yang sama.
/// Urutan bebas diatur user; run engine eksekusi sesuai kolom order.
/// </summary>
public static class FeatureStateStore
{
    static string DbPath => Path.Combine(AppContext.BaseDirectory, "data", "accounts.db");

    public static List<FeatureConfig> Load()
    {
        var list = new List<FeatureConfig>();
        using var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT feature_id, enabled, sort_order, params_json FROM feature_state ORDER BY sort_order";
        try
        {
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new FeatureConfig
                {
                    FeatureId = r.GetString(0),
                    Enabled = r.GetInt64(1) != 0,
                    Order = r.GetInt32(2),
                    Params = Deserialize(r.GetString(3)),
                });
            }
        }
        catch { /* tabel belum ada */ }
        return list;
    }

    public static void Save(IEnumerable<FeatureConfig> configs)
    {
        using var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS feature_state(
                feature_id  TEXT PRIMARY KEY,
                enabled     INTEGER NOT NULL DEFAULT 1,
                sort_order  INTEGER NOT NULL DEFAULT 0,
                params_json TEXT NOT NULL DEFAULT '{}'
            );
            """;
        cmd.ExecuteNonQuery();

        foreach (var cfg in configs)
        {
            cmd.CommandText = """
                INSERT INTO feature_state(feature_id, enabled, sort_order, params_json)
                VALUES($id, $en, $ord, $pj)
                ON CONFLICT(feature_id) DO UPDATE SET enabled=$en, sort_order=$ord, params_json=$pj
                """;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$id", cfg.FeatureId);
            cmd.Parameters.AddWithValue("$en", cfg.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$ord", cfg.Order);
            cmd.Parameters.AddWithValue("$pj", Serialize(cfg.Params));
            cmd.ExecuteNonQuery();
        }
    }

    static string Serialize(Dictionary<string, string> p) =>
        JsonSerializer.Serialize(p ?? new Dictionary<string, string>());

    static Dictionary<string, string> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch { return new(); }
    }
}
