const Shield = () => (
  <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth="1.8">
    <path d="M12 3 5 6v5c0 4.7 2.8 8.1 7 10 4.2-1.9 7-5.3 7-10V6l-7-3Z" />
    <path d="m9 12 2 2 4-5" />
  </svg>
);

const features = [
  ["Tersimpan sebagai ciphertext", "File dienkripsi sebelum masuk object storage. Link publik tidak pernah membuka bucket secara langsung."],
  ["Akses yang dapat berakhir", "Atur waktu kedaluwarsa, password, batas unduhan, atau jadikan tautan sekali pakai."],
  ["Jejak aktivitas yang aman", "Pantau akses dan perubahan penting tanpa merekam password, token rahasia, atau isi file."],
];

export default function Home() {
  return (
    <div className="min-h-screen bg-[var(--canvas)] text-[var(--ink)]">
      <a className="skip-link" href="#content">Lewati ke konten utama</a>
      <header className="mx-auto flex max-w-7xl items-center justify-between px-5 py-5 md:px-10">
        <Link href="/" className="flex min-h-11 items-center gap-2 rounded-lg font-bold tracking-[-0.02em] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-blue-700">
          <span className="grid size-9 place-items-center rounded-xl bg-blue-700 text-white"><Shield /></span>
          VaultShare
        </Link>
        <nav aria-label="Navigasi utama" className="flex items-center gap-2">
          <Link href="/login" className="button-quiet">Masuk</Link>
          <Link href="/register" className="button-primary hidden sm:inline-flex">Buat akun</Link>
        </nav>
      </header>

      <main id="content">
        <section className="mx-auto grid max-w-7xl gap-12 px-5 pb-20 pt-14 md:px-10 md:pb-28 md:pt-24 lg:grid-cols-[1.06fr_.94fr] lg:items-center">
          <div>
            <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-blue-200 bg-blue-50 px-3 py-2 text-sm font-semibold text-blue-900">
              <Shield /> Privasi dimulai sebelum file disimpan
            </div>
            <h1 className="max-w-3xl text-balance text-5xl font-bold leading-[1.02] tracking-[-0.055em] sm:text-6xl lg:text-7xl">
              Bagikan file. <span className="text-blue-700">Bukan kendalinya.</span>
            </h1>
            <p className="mt-7 max-w-2xl text-pretty text-lg leading-8 text-slate-600 md:text-xl">
              VaultShare membantu individu dan tim mengirim file dengan penyimpanan terenkripsi, akses terbatas, pemindaian malware, dan audit yang dapat ditelusuri.
            </p>
            <div className="mt-9 flex flex-col gap-3 sm:flex-row">
              <Link href="/register" className="button-primary">Mulai berbagi aman</Link>
              <a href="#cara-kerja" className="button-secondary">Lihat cara kerjanya</a>
            </div>
            <p className="mt-5 text-sm text-slate-500">Tidak ada iklan atau pelacak pihak ketiga pada halaman share publik.</p>
          </div>

          <div className="transfer-card" aria-label="Ilustrasi alur pemrosesan file aman">
            <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
              <div>
                <p className="text-sm font-bold">proposal-q3.pdf</p>
                <p className="mt-1 text-xs text-slate-500">18,4 MB · Workspace Produk</p>
              </div>
              <span className="rounded-full bg-emerald-50 px-3 py-1.5 text-xs font-bold text-emerald-800">Tersedia</span>
            </div>
            <ol className="space-y-1 p-3">
              {[
                ["01", "Upload diterima", "Streaming · tidak dimuat penuh ke memori"],
                ["02", "Pemindaian malware", "Clean · ClamAV"],
                ["03", "Enkripsi file", "AES-256-GCM · kunci unik per file"],
                ["04", "Penyimpanan privat", "Ciphertext · object key acak"],
              ].map(([index, title, detail]) => (
                <li key={index} className="group grid grid-cols-[2.5rem_1fr_auto] items-center gap-3 rounded-xl px-3 py-4 hover:bg-slate-50">
                  <span className="font-mono text-xs font-bold text-slate-400">{index}</span>
                  <span><strong className="block text-sm">{title}</strong><span className="mt-1 block text-xs leading-5 text-slate-500">{detail}</span></span>
                  <span className="grid size-7 place-items-center rounded-full bg-blue-50 text-blue-700" aria-label="Selesai">✓</span>
                </li>
              ))}
            </ol>
            <div className="mx-5 mb-5 rounded-xl bg-slate-950 p-4 text-slate-100">
              <div className="flex items-center justify-between text-xs"><span>TAUTAN AKSES</span><span className="text-amber-300">Berakhir 6 hari lagi</span></div>
              <div className="mt-3 h-2 overflow-hidden rounded-full bg-slate-700"><div className="h-full w-2/3 rounded-full bg-blue-400" /></div>
              <p className="mt-3 font-mono text-xs text-slate-400">2 dari 3 unduhan telah digunakan</p>
            </div>
          </div>
        </section>

        <section id="cara-kerja" className="border-y border-slate-200 bg-white/70">
          <div className="mx-auto max-w-7xl px-5 py-20 md:px-10 md:py-24">
            <p className="section-label">KONTROL YANG DAPAT DIPAHAMI</p>
            <h2 className="mt-3 max-w-2xl text-3xl font-bold tracking-[-0.035em] md:text-4xl">Keamanan yang bekerja tanpa teatrikal.</h2>
            <div className="mt-12 grid gap-px overflow-hidden rounded-2xl border border-slate-200 bg-slate-200 md:grid-cols-3">
              {features.map(([title, copy]) => (
                <article key={title} className="bg-[var(--surface)] p-7 md:p-8">
                  <div className="mb-6 grid size-10 place-items-center rounded-xl border border-blue-200 bg-blue-50 text-blue-700"><Shield /></div>
                  <h3 className="text-lg font-bold">{title}</h3>
                  <p className="mt-3 leading-7 text-slate-600">{copy}</p>
                </article>
              ))}
            </div>
          </div>
        </section>
      </main>

      <footer className="mx-auto flex max-w-7xl flex-col gap-3 px-5 py-8 text-sm text-slate-500 sm:flex-row sm:items-center sm:justify-between md:px-10">
        <p>© 2026 VaultShare · Application-level encryption at rest</p>
        <div className="flex gap-5"><Link href="/security">Keamanan</Link><Link href="/privacy">Privasi</Link></div>
      </footer>
    </div>
  );
}
import Link from "next/link";
