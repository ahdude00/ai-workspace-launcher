# AWL — AI Workspace Launcher

[![CI](https://github.com/ahdude00/ai-workspace-launcher/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ahdude00/ai-workspace-launcher/actions/workflows/ci.yml)

Windows 10'da teknik bilgisi sınırlı bir kullanıcının **Claude Code**'u kurmadan uğraşmadan kullanmasını, belgelerle çalışmasını, birden çok oturumu düzenli yönetmesini ve ileride yeni araçlar ekleyebilmesini sağlayan sade masaüstü uygulaması.

- **Kullanıcı hiçbir şeyi elle kurmaz.** Claude Code, Python ve gerekli kütüphaneler ilk açılışta AWL tarafından otomatik olarak, kullanıcı klasörüne kurulur.
- Arayüz Türkçedir; sohbetin kendisi Claude Code'un kendi terminal arayüzünde gerçekleşir.

> Durum: **Tasarım tamamlandı, geliştirme başlıyor.** Henüz çalışan bir sürüm yok.

## Belgeler

| Belge | İçerik |
|---|---|
| [Proje Kararları ve Mimari](docs/AWL_Proje_Kararlari_ve_Mimari.html) | Vizyon, karar günlüğü (D01–D23), yol haritası |
| [Teknik Tasarım ve SRS](docs/AWL_Teknik_Tasarim_ve_SRS.html) | Teknolojiler, mimari, kod standartları, gereksinimler (KUR-01 …) |
| [Geliştirme Süreci](docs/GELISTIRME_SURECI.md) | Görevler, dallar, PR, CI/CD |

HTML belgeleri indirip tarayıcıda açın (GitHub önizlemesi HTML'i çalıştırmaz).

## Teknoloji

C# / .NET 10 · WPF + MVVM · SQLite · Claude Code CLI · uv + Python · Inno Setup (kullanıcı bazlı kurulum) · GitHub Actions

## Derleme

Gerekenler: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build AWL.sln
dotnet test AWL.sln
dotnet publish src/AWL.Desktop -p:PublishProfile=win-x64   # → artifacts/publish/win-x64/AWL.exe
```

## Geliştirme akışı (kısa)

1. Her görev bir GitHub issue'sudur (`[F1-08] …`); issue'lar milestone'larla fazlara ayrılır.
2. Her görev için `main`'den bir dal açılır: `task/f1-08-kisa-ad`.
3. İş bitince PR açılır (`Closes #<issue>`); CI yeşil olmadan birleştirilmez.
4. PR `main`'e **squash merge** ile alınır, dal silinir.

Ayrıntılar: [docs/GELISTIRME_SURECI.md](docs/GELISTIRME_SURECI.md)
