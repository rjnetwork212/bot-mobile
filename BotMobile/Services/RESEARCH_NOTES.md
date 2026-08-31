// Research update 2026-08-31 (hasil TrafficProbe): m.facebook mobile web modern
// TIDAK memanggil /api/graphql (doc_id Comet ditolak: error 1357004 "closing browser").
// Mobile web pakai endpoint klasik (form ajax) — harus direcon per fitur.
//
// Keputusan arsitektur (valid, tidak asumsi):
// - LOGIN: m.facebook (sudah tervalidasi, UI mobile + fingerprint mobile OK)
// - FITUR (GraphQL Comet doc_id): jalan di www.facebook.com desktop context —
//   sama seperti Bot_Ngekeng (semua fiturnya di www.facebook.com desktop).
//   Ini dua fase: fase 1 login mobile (bentuk APK), fase 2 buka tab/halaman www
//   untuk aksi GraphQL.
