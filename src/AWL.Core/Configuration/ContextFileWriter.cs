using System.Text;

namespace AWL.Core.Configuration;

/// <summary>Writes the Turkish workspace instructions appended to the Claude system prompt.</summary>
public static class ContextFileWriter
{
    public static void Write(Stream destination, AwlPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string python = PowerShellLiteral(paths.PythonExecutable);
        string udfTool = PowerShellLiteral(paths.UdfToolFile);
        using var writer = new StreamWriter(destination, new UTF8Encoding(false), leaveOpen: true);
        writer.WriteLine($"""
            # AWL çalışma ortamı

            - Yanıtlarını Türkçe ver.
            - Python için her zaman şu tam yolu kullan: {paths.PythonExecutable}
              PowerShell ile paket kurma: & {python} -m pip install <paket>
            - Orijinal belgeleri koru. Düzenleme istendiğinde önce aynı klasöre
              "<ad> - düzenlendi.<uzantı>" adıyla kopyala ve değişiklikleri kopyada yap.
              Ad kullanımdaysa "<ad> - düzenlendi (2).<uzantı>" ile başlayarak boş bir ad seç.
              Orijinali doğrudan değiştirdiysen adına "- düzenlendi" ekle ve kullanıcıya bildir.
            - DOCX/PDF/XLSX işlerinde önce belge skill'lerini kullan. Sonuç alamazsan Python kütüphanelerini kullan.
            - UDF dosyalarını işlerken aracın kullanımına bak: & {python} {udfTool} --help
              E-imzalı bir UDF düzenlenirse oluşturulan kopyanın imzasız olduğunu kullanıcıya bildir.
            - Yeni üretilen dosyaları çalışma klasörü altındaki "AWL_Ciktilar" klasörüne yaz.
            """);
    }

    private static string PowerShellLiteral(string path) =>
        "'" + path.Replace("'", "''", StringComparison.Ordinal) + "'";
}
