# MEŞRUİYET — Unity kurulumu (bir kerelik, ~45 dakika)

Bu klasör, oyunu Unity + C# ile yapmak ve **her adımı yapay zekânın yürütmesi** için gereken
iskeleti içerir. Senin yapacağın tek iş aşağıdaki kurulum. Ondan sonrası bende.

---

## 1. Unity kur (~20 dk, çoğu indirme beklemesi)

1. **Unity Hub** indir: <https://unity.com/download>
2. Hub'a Unity hesabınla giriş yap. Lisans: **Personal** (ücretsiz). Hub → Preferences → Licenses → Add → *Get a free personal license*.
3. Hub → Installs → Install Editor → **Unity 6 LTS** (6000.x). Modüllerden şunları işaretle:
   - **Windows Build Support (IL2CPP)** — zaten gelir
   - **WebGL Build Support** — tarayıcıdan co-op testi için
   - **Linux Build Support (Mono)** — ileride sunucuya build almak için

> Neden Unity 6: LTS, uzun destekli ve UI Toolkit'in oturmuş sürümü onda.

## 2. Claude Code kur (~10 dk)

Node 18+ gerekiyor. Yoksa önce <https://nodejs.org> üzerinden LTS kur, sonra PowerShell'de:

```powershell
npm install -g @anthropic-ai/claude-code
claude --version
```

## 3. Projeyi al

```powershell
cd $HOME\Documents
git clone <bu-repo-nun-adresi> mesruiyet
cd mesruiyet\unity
```

## 4. Unity projesini oluştur

Unity Hub → New Project → **Universal 3D (URP)** → konum olarak `mesruiyet\unity\Game` seç,
isim `Game`. Oluşunca Unity'yi **kapat**.

Sonra bu klasördeki `Assets` içeriğini yeni projeye kopyala:

```powershell
Copy-Item -Recurse -Force .\Assets\* .\Game\Assets\
```

## 5. Claude'u başlat

```powershell
cd $HOME\Documents\mesruiyet
claude
```

İlk mesaj olarak şunu yaz:

> unity/CLAUDE.md ve PROMPT-CITY.md dosyalarını oku, sonra MEŞRUİYET'in ilk oynanabilir
> dilimini kur ve ajan döngüsüyle kendin test et.

Bu noktadan sonra kod yazmıyorsun, Unity açmıyorsun, Play'e basmıyorsun. Ben derleyip
çalıştırıp oynayıp ekran görüntüsü alıp sana rapor veriyorum.

---

## Ajan döngüsü nasıl çalışıyor

`Assets/Scripts/Agent/AgentBridge.cs` oyunun içinde küçük bir TCP sunucusu açar
(`127.0.0.1:8787`, yalnızca debug/editor build'lerinde). Satır başına bir JSON komut alır,
JSON cevap döner:

| Komut | Ne yapar |
|---|---|
| `{"cmd":"ping"}` | ayakta mı |
| `{"cmd":"state"}` | tüm oyun durumunu JSON olarak döker (gerçek değerler) |
| `{"cmd":"press","key":"space"}` | tuşa basar |
| `{"cmd":"click","id":"btn_end_turn"}` | isimli UI öğesine tıklar |
| `{"cmd":"endturn","n":10}` | n tur ilerletir |
| `{"cmd":"shot","path":"agent/shots/x.png"}` | ekran görüntüsü kaydeder |
| `{"cmd":"quit"}` | oyunu kapatır |

`agent/loop.ps1` bunu tek komuta indirir: derle → çalıştır → komutları gönder → görüntüleri
topla → logu oku. Ben bu script'i çalıştırıp çıktısına bakıyorum.

```powershell
.\agent\loop.ps1 -Scenario smoke
```

## Sık çıkan iki sorun

**Unity yolu bulunamadı.** `agent/loop.ps1` içindeki `$UnityExe` satırını kendi sürümünle
güncelle: `C:\Program Files\Unity\Hub\Editor\6000.x.y\Editor\Unity.exe`

**Derleme hatası görünmüyor.** Loglar `agent/logs/` altına düşer; `build.log` derleme,
`player.log` çalışma zamanı hatalarını içerir. İkisini de ben okuyorum.

## Co-op sunucusu (sonraki aşama)

Oyunun kendisi senin makinende geliştirilir; co-op için gereken tek şey mesajları ileten
küçük bir sunucu. Cloudflare Workers üzerinde Durable Object olarak çalışır, ayda birkaç
dolar ya da ücretsiz kotada. Bunu ben yazarım, sen sadece Cloudflare hesabına deploy
komutunu çalıştırırsın. Ayrı bir VM kiralamana gerek yok.
