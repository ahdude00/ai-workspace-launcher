# Gömülü terminal kabulü (TRM-07, D17)

**Karar.** Windows Terminal varsayılan olarak kalır. Gömülü terminal Ayarlar'dan seçilebilir. Aşağıdaki saha denemeleri tüm geliştirme görevlerinden sonra yapılacak. Varsayılanı değiştirme kararı ancak tüm satırların sonucu kaydedildikten ve geçtiği görüldükten sonra tekrar değerlendirilecek.

Aynı Windows hesabı, Claude Code sürümü, klavye düzeni ve örnek çalışma klasörüyle her denemeyi önce doğrudan Windows Terminal'de, sonra AWL'nin gömülü terminalinde yapın. Sürüm etiketini veya commit'i, Windows derlemesini, WebView2 ve Claude sürümlerini sonuç kaydına ekleyin.

| Deneme | Geçiş ölçütü | Doğrudan | Gömülü |
|---|---|---|---|
| Slash komutları ve geçmiş | Yardım açılır. `/resume` seçicisinde oklar ve Enter doğru örnek konuşmayı açar. | Bekliyor | Bekliyor |
| İzin ekranı | Örnek dosyaya yazma isteğinde izin seçenekleri görünür. Reddetme dosyayı korur, ikinci denemede verilen izin uygulanır. | Bekliyor | Bekliyor |
| Esc ve çift Esc | Tek Esc çalışan yanıtı durdurur. Çift Esc ile geri alma seçicisi kullanılabilir. | Bekliyor | Bekliyor |
| Shift+Tab ve Ctrl+C | Mod göstergesi değişir. Kesme tuşu aynı durumda doğrudan terminalle aynı sonucu verir. | Bekliyor | Bekliyor |
| Shift+Enter | Girdi ikinci satıra geçer ve gönderilmeden düzenlenebilir. | Bekliyor | Bekliyor |
| Alt+V | Panodaki örnek ekran görüntüsü girişe görsel olarak eklenir. | Bekliyor | Bekliyor |
| Kaydırma, renk ve boyut | Uzun çıktının önceki satırlarına dönülebilir. Renkler ve istem görünür. Pencere daraltılıp genişletildiğinde yeni satırlar alana sığar. | Bekliyor | Bekliyor |
| Türkçe girdi ve çıktı | `İş, çığ, öğüt, şüphe, ılık` yazılıp gönderilir. İstemde ve yanıtta karakterler korunur. | Bekliyor | Bekliyor |
| Oturum değişimi | A ve B'yi açın. A çıktı üretirken B'ye geçip A'ya dönün. A aynı süreç kimliğiyle sürer, ara çıktı görünür ve B kendi geçmişini korur. | Bekliyor | Bekliyor |

Her hücreye sonuç ve gözlem girin. Fark varsa kullanılan giriş dizisini, beklenen ve görülen davranışı kaydedin. Açık kalan her satır için Windows Terminal varsayılanı geçerlidir.
