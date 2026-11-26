# AWL v1 saha kabulü

**Durum: çalıştırılmadı.** Temiz bir Windows 10 x64 sanal makinesinde standart kullanıcı hesabıyla çalışın. Başlamadan önce anlık görüntü alın. #36 kapsamında oluşan taslağın EXE ve SHA256 dosyalarını kullanın. Sürüm etiketi, commit, ölçülen SHA256, Windows derlemesi, Claude sürümü, terminal türü ve test tarihini sonuç kaydına yazın. Gerçek müvekkil belgeleri yerine örnek dosyalar kullanın.

## Baştan sona oturum

| Adım | İşlem | Geçiş ölçütü |
|---|---|---|
| 1 | İndirilen EXE için `Get-FileHash -Algorithm SHA256` sonucunu özet dosyasıyla karşılaştırıp kurun. AWL'yi Başlat menüsünden açın ve Claude'a giriş yapın. | Özet eşleşir. UAC istemi çıkmaz. Sihirbaz biter, oturum listesi klavye girişine yanıt verir. |
| 2 | `İş Dosyaları; Deneme` klasörüne, klasörün boş alanına ve içindeki bir belgeye ayrı ayrı sağ tıklayıp **AWL ile aç** seçin. | Tek AWL penceresi kullanılır. Yeni oturum klasörü doğrudur. Dosyadan açılışta tırnaklı tam yol panodadır. |
| 3 | A oturumunda yalnızca teste özgü bir sözcük yazın. DOCX, XLSX, PDF ve UDF örneklerinin başlangıç SHA256 değerlerini kaydedin. Her biçimde küçük bir değişiklik isteyin. | Her orijinalin özeti aynı kalır. “- düzenlendi” kopyası beklenen değişiklikle ilgili okuyucuda açılır. UDF kopyasını UYAP Editör'de açın. Dönüşümü ayrıca deneyip çıktı ve orijinali kontrol edin. |
| 4 | B oturumu oluşturun. A ve B arasında geçin. Açık A'yı tekrar açın. AWL'yi kapatıp yeniden başlatın. Ardından A'nın terminalini kapatıp AWL'den A'ya devam edin. | İkinci bir A süreci açılmaz. İki oturumun durumları doğru görünür. Devam edilen A'da önceki test sözcüğü ve konuşma görünür. |
| 5 | Yalnızca test oturumu A için silmeyi önce iptal edin, sonra onaylayın. B'yi açıp belgeleri ve kopyaları yeniden kontrol edin. | İptalde kayıt ve geçmiş kalır. Onaydan sonra A'nın AWL kaydı, dökümü ve varsa debug günlüğü silinir. B, belgeler ve çıktılar korunur. |
| 6 | Ayarlardan onarımı, ardından kaldırmayı deneyin. Claude ve geçmişi silme sorularında korumayı seçin. | Onarım eksik AWL bileşenlerini geri getirir. Kaldırma AWL sağ tık menülerini temizler. Belgeler, çıktılar ve korunan geçmiş erişilebilirdir. |

## Ek denemeler

- **Ön kontroller.** Ayrı VM anlık görüntülerinde ağ kesintisi, yazılamayan hedef ve kısıtlı PowerShell durumlarını deneyin. Türkçe hata ve önerilen işlemi, tanılama ZIP'indeki sonucu ve koşul düzeltildikten sonra yeniden denemeyi kaydedin. Belge eklentisinin yüklenemediği koşulda kurulumun tamamlandığını kontrol edin.
- **Terminal eşdeğerliği.** Aynı kullanıcı, klasör, Claude sürümü ve terminal türünde doğrudan başlatılan Claude ile AWL oturumunu karşılaştırın. Yardım ve eğik çizgi komutları, giriş düzenleme, izin istemi, kesme, Türkçe karakterler ve panodaki örnek görseli Alt+V ile yapıştırma sonuçlarını iki sütunda kaydedin. Windows Terminal ve desteklenen konsol yolunu ayrı çalıştırın.
- **Python ve belgeler.** AWL oturumunda kullanılan Python yürütülebilir dosyasını kaydedin. Test paketini bu ortamda kurup içe aktarın. Sistem Python'unun ve PATH ayarının başlangıç durumunu koruduğunu kontrol edin. Birden çok örnek belgeyi AWL'ye bırakıp panodaki tırnaklı yolları karşılaştırın.
- **Eski geçmiş.** Saklama süresi içinde kalan, 30 günden eski bir test konuşmasıyla devam etmeyi deneyin. Gerçek yaşta örnek yoksa bu satırı beklemede bırakın ve kullanılan benzetimi ayrıca belirtin.
- **Erişilebilirlik.** Yalnız klavyeyle oluşturma, açma, ayarlar ve silme onayından vazgeçme akışlarını tamamlayın. Her adımda odağın görünür olduğunu, %100, %150 ve %200 ölçeklemede düğmelere ve hata metinlerine erişilebildiğini denetleyin. Metin ve arka plan renk çiftleri için ölçülen kontrast oranını ve WCAG 2.1 AA sonucunu kaydedin.
- **Performans.** SSD kullanılan makinede kurulumu ve girişi önceden tamamlayın. Yeniden başlatma sonrası AWL açılışından liste kullanılabilir olana kadar geçen süreyi ölçün. AWL'yi kapatıp yeniden açarak sıcak ölçümü alın. Ayrı olarak Aç ve Devam et komutlarından terminal penceresinin görünmesine kadar ölçün. Claude'un yanıtını beklemeyin. Soğuk, sıcak ve terminal sürelerini ayrı kaydedin. SRS hedefleri sırasıyla ≤3 s, ≤1,5 s ve ≤2 s.

## Sonuç kaydı

Her koşu için bu dosyanın bir kopyasını kullanın. Aşağıdaki her zorunlu gereksinime gözlem, test adı veya kanıt referansı ekleyin. Başarısızlıkta yeniden üretim adımlarını, engelde eksik koşulu belirtin. Tüm zorunlu satırlar temiz standart kullanıcı ortamında doğrulanınca Faz 1 kabulünü kaydedin.

| SRS | Beklenen davranış | Sonuç | Gözlem / kanıt |
|---|---|---|---|
| KUR-01 | AWL, UAC istemi çıkarmadan, kullanıcı bazlı olarak %LOCALAPPDATA%\Programs\AWL altına kurulmalıdır. | Bekliyor | |
| KUR-02 | Kurulum, Başlat menüsü kısayolu ve Windows “Uygulamalar” listesinde görünen bir kaldırma kaydı oluşturmalıdır. | Bekliyor | |
| KUR-03 | İlk açılışta kurulum sihirbazı açılmalı ve adımları sırayla, durumlarıyla birlikte göstermelidir: ön kontroller, Claude Code, Python ortamı, yapılandırma, Windows entegrasyonu, giriş. | Bekliyor | |
| KUR-20 | Son kullanıcı hiçbir bileşeni elle kurmamalı ve hiçbir komut çalıştırmamalıdır; Claude Code, Python ve gerekli kütüphaneler ilk açılışta AWL tarafından otomatik kurulmalıdır. | Bekliyor | |
| KUR-04 | Ön kontroller şunları denetlemeli ve sonucu Türkçe açıklamalıdır: Windows sürümü ve mimarisi, kullanıcı klasörlerine yazma izni, Claude ve indirme adreslerine erişim, PowerShell dil modu. | Bekliyor | |
| KUR-05 | Claude Code zaten kuruluysa sürümü doğrulanmalı ve yeniden kurulmamalıdır. | Bekliyor | |
| KUR-06 | Claude Code kurulu değilse ilk açılışta, kullanıcıdan hiçbir işlem istenmeden resmi kurulum betiğiyle kullanıcı klasörüne kurulmalı ve claude --version ile doğrulanmalıdır. | Bekliyor | |
| KUR-07 | AWL Git for Windows kurmamalıdır. | Bekliyor | |
| KUR-08 | AWL'ye özel Python çalışma ortamı ve sanal ortam, uv ile AWL_HOME altında oluşturulmalı; sistem Python'u ve kullanıcı PATH'i değiştirilmemelidir. | Bekliyor | |
| KUR-09 | Temel belge paketleri hash'li kilit dosyasındaki sabit sürümlerle sanal ortama kurulmalıdır. | Bekliyor | |
| KUR-10 | claude-settings.json (izin modu, saklama süresi, ortam değişkenleri) ve awl-context.md oluşturulmalı; ayarlar değiştiğinde yeniden yazılmalıdır. | Bekliyor | |
| KUR-11 | udf_tool.py, AWL_HOME\tools altına kurulmalıdır. | Bekliyor | |
| KUR-14 | Her kurulum adımı tekrar çalıştırılabilir olmalı; tamamlanmış adım atlanmalı, başarısız adım “Tekrar dene” ile yeniden çalıştırılabilmelidir. | Bekliyor | |
| KUR-15 | Başarısız bir adımda kullanıcıya ne olduğu ve ne yapması gerektiği Türkçe gösterilmeli, teknik ayrıntı günlüğe yazılmalıdır. | Bekliyor | |
| KUR-16 | Claude girişi Claude'un kendi akışıyla yapılmalı; AWL parola, token veya kimlik bilgisi istememeli, saklamamalı, okumamalıdır. | Bekliyor | |
| KUR-18 | Kaldırma yalnızca AWL'nin oluşturduğu dosya ve kayıtları silmelidir. Kullanıcı belgeleri, düzenlenmiş kopyalar ve AWL_Ciktilar silinmemeli; Claude Code'un ve oturum geçmişinin kaldırılması ayrıca sorulmalıdır. | Bekliyor | |
| KUR-19 | Bir sistem kısıtlaması (AppLocker, Constrained Language Mode, proxy, antivirüs) bir adımı engellerse AWL bunu aşmaya çalışmamalı; durumu açıklamalı ve destek için paylaşılabilecek teknik bir özet sunmalıdır. | Bekliyor | |
| OTR-01 | Kullanıcı başlık, çalışma klasörü ve isteğe bağlı kategoriyle yeni oturum oluşturabilmelidir. | Bekliyor | |
| OTR-02 | Yeni oturumda AWL bir UUID üretmeli ve Claude'u --session-id &lt;uuid&gt; ile başlatmalıdır. | Bekliyor | |
| OTR-03 | Kayıtlı bir oturum, kendi çalışma klasöründe --resume &lt;uuid&gt; ile devam ettirilmelidir. | Bekliyor | |
| OTR-04 | Her başlatmada --settings ve --append-system-prompt-file parametreleri AWL'nin dosyalarıyla verilmelidir. | Bekliyor | |
| OTR-05 | Oturumlar sol panelde kategoriye göre gruplanarak listelenmelidir; kategorisiz oturumlar ayrı bir grupta görünmelidir. | Bekliyor | |
| OTR-06 | Her oturumun durumu görünmelidir: Açık, Kapalı, Arşivde, Geçmiş bulunamadı, Hata. | Bekliyor | |
| OTR-07 | “Açık” durumu, oturum kimliğini taşıyan claude.exe süreci izlenerek belirlenmeli ve en geç 3 saniye içinde güncellenmelidir. | Bekliyor | |
| OTR-08 | Açık bir oturum yeniden açılmak istendiğinde ikinci bir süreç başlatılmamalı; kullanıcı bilgilendirilmeli ve terminal penceresi öne getirilmeye çalışılmalıdır. | Bekliyor | |
| OTR-09 | Birden fazla oturum aynı anda açık olabilmeli; aynı klasörde birden fazla oturum desteklenmelidir. | Bekliyor | |
| OTR-10 | Kullanıcı bir oturumu yeniden adlandırabilmeli, kategorisini değiştirebilmeli, arşivleyebilmeli ve arşivden çıkarabilmelidir. | Bekliyor | |
| OTR-11 | Silinen oturum için onay sonrası AWL kaydı, Claude dökümü, debug günlüğü ve Faz 0'da belirlenen oturuma ait diğer Claude dosyaları silinmelidir. Kullanıcı belgeleri, düzenlenmiş kopyalar ve çıktılar silinmemelidir. | Bekliyor | |
| OTR-12 | Silme onay ekranı, silinecekleri ve işlemin geri alınamayacağını açıkça belirtmelidir. | Bekliyor | |
| OTR-13 | Açık bir oturum silinmek istendiğinde önce kapatılması gerektiği bildirilmeli; kullanıcı onaylarsa süreç sonlandırılmalıdır. | Bekliyor | |
| OTR-14 | Silmenin Claude tarafı kısmen başarısız olursa AWL kaydı silinmemeli; oturum “Hata” durumuna geçmeli ve silme tekrar denenebilmelidir. | Bekliyor | |
| OTR-15 | Uygulama açılışında her oturum için döküm varlığı ve süreç durumu yeniden hesaplanmalı; son etkinlik zamanı döküm dosyasının değişiklik zamanından alınmalıdır. | Bekliyor | |
| OTR-16 | Saklama süresini aşmış ve açık olmayan oturumlar açılışta AWL'den ve Claude'dan birlikte silinmelidir. | Bekliyor | |
| OTR-17 | “Geçmiş bulunamadı” durumundaki oturum için aynı klasörde yeni oturum başlatma ve kaydı silme seçenekleri sunulmalıdır. | Bekliyor | |
| OTR-20 | Çalışma klasörü artık yoksa oturum başlatılmamalı; kullanıcıya yeni klasör seçme veya kaydı silme seçeneği sunulmalıdır. | Bekliyor | |
| KAT-01 | Kullanıcı kategori oluşturabilmeli, yeniden adlandırabilmeli, sıralayabilmeli ve silebilmelidir. | Bekliyor | |
| KAT-02 | Bir kategori silindiğinde içindeki oturumlar silinmemeli, “Kategorisiz” grubuna taşınmalıdır. | Bekliyor | |
| TRM-01 | Claude Code gerçek bir terminal penceresinde açılmalı; AWL Claude'un giriş ve çıkışına aracılık etmemelidir. | Bekliyor | |
| TRM-02 | Taşınabilir Windows Terminal varsa o, yoksa Windows konsolu kullanılmalı; tercih Ayarlar'dan değiştirilebilmelidir. | Bekliyor | |
| TRM-04 | AWL'den açılan bir oturumda Claude Code'un tüm komut, kısayol ve özellikleri, Claude'un doğrudan çalıştırıldığı durumla aynı davranmalıdır. | Bekliyor | |
| TRM-05 | Python ortamının yolu ve AWL_HOME, terminal türünden bağımsız olarak Claude'a ulaştırılmalıdır (env ayarı ve bağlam dosyası). | Bekliyor | |
| BLG-01 | Bağlam dosyası, orijinal belgelerin değiştirilmemesini, değişikliğin “ad - düzenlendi” kopyasında yapılmasını ve doğrudan değiştirilen dosyanın adına “- düzenlendi” eklenmesini söyleyen kopya kuralını içermelidir. | Bekliyor | |
| BLG-02 | Bağlam dosyası, belge işlerinde önce skill'lerin, sonuç alınamazsa Python kütüphanelerinin kullanılacağını belirtmelidir. | Bekliyor | |
| BLG-03 | Bağlam dosyası Python'un tam yolunu, paket kurma komutunu, udf_tool.py kullanımını ve çıktıların AWL_Ciktilar klasörüne yazılacağını içermelidir. | Bekliyor | |
| BLG-04 | udf_tool.py; info, text, unpack, pack komutlarını sağlamalı ve pack değiştirilmeyen ZIP girdilerini bayt düzeyinde korumalıdır. | Bekliyor | |
| BLG-05 | udf_tool.py pack kaynak UDF dosyasının üzerine yazmayı reddetmelidir. | Bekliyor | |
| BLG-06 | AWL'nin kendisi kullanıcı belgelerini değiştirmemeli, taşımamalı ve silmemelidir. | Bekliyor | |
| SAG-01 | Bir klasöre sağ tıklandığında “AWL ile aç” seçeneği bulunmalı ve klasör dolu bir “Yeni oturum” penceresi açmalıdır. | Bekliyor | |
| SAG-02 | Klasörün boş alanına sağ tıklandığında aynı seçenek o klasör için bulunmalıdır. | Bekliyor | |
| SAG-04 | Komut satırı argümanları doğrulanmalıdır: yalnızca bilinen bayraklar, mutlak ve var olan yollar; aygıt yolları reddedilmelidir. | Bekliyor | |
| SAG-05 | AWL zaten açıksa yeni istek çalışan örneğe iletilmeli; ikinci bir AWL penceresi açılmamalıdır. | Bekliyor | |
| SAG-06 | Boşluk, Türkçe karakter ve noktalı virgül içeren yollar tüm akışlarda doğru işlenmelidir. | Bekliyor | |
| SAG-07 | Kaldırma sırasında yalnızca AWL'nin oluşturduğu HKCU anahtarları silinmelidir. | Bekliyor | |
| AYR-01 | Saklama süresi gün olarak ayarlanabilmeli (varsayılan 3650); değişince claude-settings.json güncellenmeli ve aynı süre AWL kayıtlarına uygulanmalıdır. | Bekliyor | |
| AYR-02 | Terminal tercihi (Windows Terminal / Windows konsolu) ayarlanabilmelidir. | Bekliyor | |
| AYR-03 | Arayüzdeki tüm metinler Türkçe olmalı ve kaynak dosyalarından gelmelidir. | Bekliyor | |
| TAN-01 | Günlükler AWL_HOME\logs altında boyut ve sayı sınırlı dönen dosyalara yazılmalıdır. | Bekliyor | |
| TAN-02 | Günlükler belge içeriği, sohbet içeriği, oturum başlığı veya kimlik bilgisi içermemelidir. | Bekliyor | |
| TAN-03 | “Tanılama paketini kaydet” komutu günlükleri, sürüm bilgilerini ve ön kontrol sonuçlarını tek bir ZIP dosyası olarak kullanıcının seçtiği yere kaydetmelidir. | Bekliyor | |
| TAN-04 | Her açılışta Claude Code sürümü günlüğe yazılmalıdır. | Bekliyor | |
| TAN-05 | Beklenmeyen hatalar yakalanmalı; kullanıcıya sade bir mesaj gösterilmeli, ayrıntı günlüğe yazılmalı ve veri tutarlılığı korunmalıdır. | Bekliyor | |
| GUV-01 | Hiçbir işlem yükseltilmiş yetki gerektirmemeli veya UAC istemi tetiklememelidir. | Bekliyor | |
| GUV-02 | AWL; AppLocker, WDAC, Constrained Language Mode, yürütme ilkesi, proxy veya antivirüs kısıtlarını aşmaya ya da devre dışı bırakmaya çalışmamalıdır. | Bekliyor | |
| GUV-03 | AWL, Claude Code'un kimlik bilgisi dosyalarını okumamalıdır. | Bekliyor | |
| GUV-04 | AWL telemetri göndermemeli; ağa yalnızca kurulum/onarım indirmeleri ve kullanıcının açtığı araç bağlantıları için çıkmalıdır. | Bekliyor | |
| GUV-05 | Dış süreçler yalnızca argüman listesiyle başlatılmalı; komut dizesi birleştirilmemelidir. | Bekliyor | |
| GUV-06 | Kayıt defterine yalnızca HKCU altına yazılmalı ve komutlardaki yollar tırnak içinde olmalıdır. | Bekliyor | |
| GUV-07 | Claude Code yalnızca resmi kaynaktan kurulmalı; uv ve Windows Terminal, sürümü sabit olarak AWL paketinde gelmelidir. | Bekliyor | |
| GUV-08 | Python paketleri hash doğrulamalı kilit dosyasıyla kurulmalıdır. | Bekliyor | |
| GUV-09 | Tek örnek iletişim kanalı yalnızca mevcut kullanıcıya açık olmalı ve gelen her veri doğrulanmalıdır. | Bekliyor | |
| GVL-01 | Veritabanı WAL modunda çalışmalı ve tüm yazmalar işlem içinde yapılmalıdır; uygulama çökse bile veritabanı tutarlı kalmalıdır. | Bekliyor | |
| GVL-02 | AWL kapanınca açık Claude oturumları kapanmamalı; AWL yeniden açıldığında bu oturumları “Açık” olarak tanımalıdır. | Bekliyor | |
| KUL-01 | AWL'nin kendi işlevleri terminal bilgisi gerektirmemeli; metinler teknik terim içermeyen sade Türkçe olmalıdır. | Bekliyor | |
| KUL-02 | Her hata mesajı ne olduğunu ve kullanıcının ne yapması gerektiğini içermelidir. | Bekliyor | |
| KUL-04 | Arayüz %100–%200 ekran ölçeklemesinde bozulmadan görünmelidir. | Bekliyor | |
| KUL-05 | Geri alınamaz işlemler açık bir onay gerektirmelidir. | Bekliyor | |
| UYM-01 | AWL, Windows 10 1809+ ve Windows 11 x64 üzerinde çalışmalıdır. | Bekliyor | |
| UYM-02 | AWL, makinede .NET kurulu olmasını gerektirmemelidir. | Bekliyor | |
| BAK-01 | Kod, §02'deki üç projeli yapıya ve bağımlılık yönüne uymalı; bu kurallar otomatik testle denetlenmelidir. | Bekliyor | |
| BAK-02 | Derleme uyarısız olmalı; nullable referans türleri ve analizörler açık olmalıdır. | Bekliyor | |
| BAK-04 | Claude CLI bayrak adları ve Claude dosya konumları kodda tek bir yerde tanımlanmalıdır. | Bekliyor | |
| TAS-01 | AWL tek bir kurulum dosyasıyla dağıtılmalıdır. | Bekliyor | |
| TAS-02 | Otomatik güncelleme olmamalı; yeni sürüm eskisinin üzerine kurulduğunda kullanıcı verisi ve ayarlar korunmalıdır. | Bekliyor | |
| LIS-01 | Pakete giren üçüncü taraf bileşenlerin lisansları dağıtıma uygun olmalı; Anthropic belge skill'leri pakete gömülmemelidir. | Bekliyor | |
