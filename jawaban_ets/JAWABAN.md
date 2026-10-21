# Jawaban

## 1. Deskripsi dan ruang lingkup fitur aplikasi desktop

**PORTA (Portfolio & Multi-Asset Investment Journal System)** adalah aplikasi desktop untuk mengelola, memantau, dan mengevaluasi portofolio investasi pribadi dalam satu tempat. Aplikasi mencakup saham, kripto, reksa dana/obligasi, emas, dan kas, serta dikembangkan menggunakan C#, .NET 8, Avalonia UI, dan SQL Server.

Ruang lingkup fiturnya meliputi:
- **Login dan hak akses:** Investor dapat mengelola data, sedangkan Reviewer hanya dapat melihat data.
- **Dashboard:** menampilkan total nilai portofolio, modal investasi, keuntungan/kerugian, grafik alokasi aset, dan transaksi terbaru.
- **Manajemen aset:** menambah, melihat, mengubah, menghapus, mencari, dan memfilter aset serta memperbarui harga pasar secara manual.
- **Pencatatan transaksi:** mencatat pembelian, penjualan, dan dividen dengan perhitungan otomatis jumlah unit, harga beli rata-rata, serta keuntungan/kerugian.
- **Jurnal investasi:** mencatat alasan transaksi, target harga, batas kerugian (*stop loss*), strategi, dan kondisi emosi untuk evaluasi.
- **Kategori dan alokasi:** mengatur target persentase tiap kategori serta menampilkan indikator kelebihan atau kekurangan alokasi sebagai dasar penyeimbangan portofolio (*rebalancing*).
- **Pengaturan:** menyediakan tema gelap/terang, informasi profil, status koneksi database, dan logout.

## 2. Latar belakang dan manfaat aplikasi

**Latar belakang:** Kepemilikan investasi sering tersebar di berbagai platform sehingga investor kesulitan mengetahui total nilai aset dan komposisi portofolionya. Rekapitulasi manual membutuhkan waktu dan rentan kesalahan, terutama saat menghitung harga beli rata-rata serta keuntungan setelah biaya transaksi. Selain itu, keputusan akibat FOMO atau kepanikan sulit dievaluasi tanpa jurnal investasi.

**Manfaat:** PORTA memusatkan pencatatan investasi, mempercepat pemantauan kinerja, dan mengurangi kesalahan perhitungan manual. Grafik serta indikator alokasi membantu pengguna menjaga komposisi aset sesuai target, sedangkan jurnal membantu mengevaluasi strategi dan membangun kedisiplinan dalam mengambil keputusan investasi.

*Sumber: README.md dan PRD PORTA, bagian 1, 2, 4, dan 5.1.*
