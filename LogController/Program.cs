using System.IO.Pipes;
using System.Text;
using Common;
using System.Runtime.Versioning;

/// <summary>
/// OmniConnect LogCollector - Merkezi Günlük Toplama Servisi.
/// Named Pipe üzerinden Hub'dan gelen logları dinler ve dosyaya kaydeder.
/// </summary>
class Program
{
    [SupportedOSPlatform("windows")]
    static async Task Main()
    {
        Console.Title = "OmniConnect LogCollector";

        #region --- Dosya ve Dizin Yapılandırması ---

        // Log klasörünü oluştur (Common içindeki IpcConfig'den alır)
        Directory.CreateDirectory(IpcConfig.LogFolder);

        // Zaman damgalı log dosyası ismini belirle
        string logPath = Path.Combine(
            IpcConfig.LogFolder,
            $"{IpcConfig.LogFilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}.log"
        );

        Console.WriteLine($"[LOG] File: {logPath}");
        Console.WriteLine($"[LOG] Pipe: \\\\.\\pipe\\{IpcConfig.LogPipeName}");
        Console.WriteLine("[LOG] Waiting for Hub...");

        #endregion

        #region --- Ana Sunucu Döngüsü (IPC Server Loop) ---

        while (true)
        {
            try
            {
                // Named Pipe sunucusunu başlat
                // In: Sadece giriş (Hub'dan buraya)
                // 1: Maksimum sunucu örneği sayısı
                using var server = new NamedPipeServerStream(
                    IpcConfig.LogPipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                // Hub'ın bağlanmasını bekle
                await server.WaitForConnectionAsync();
                Console.WriteLine("[LOG] Hub connected.");

                using var reader = new StreamReader(server, Encoding.UTF8);

                // Hub bağlı olduğu sürece satır satır oku
                while (server.IsConnected)
                {
                    var line = await reader.ReadLineAsync();

                    // Bağlantı koparsa veya boş satır gelirse döngüden çık
                    if (line == null) break;

                    // 1. Dosyaya yaz (Async append)
                    await File.AppendAllTextAsync(logPath, line + Environment.NewLine, Encoding.UTF8);

                    // 2. Ekrana yaz (Anlık izleme)
                    Console.WriteLine(line);
                }

                Console.WriteLine("[LOG] Hub disconnected.");
            }
            #endregion

            #region --- Hata Yönetimi & Yeniden Bağlanma ---
            catch (Exception ex)
            {
                // Hata durumunda konsola yazdır ve kısa bir süre bekle (CPU'yu yormamak için)
                Console.WriteLine("[LOG] Error: " + ex.Message);
                await Task.Delay(500);
            }
            #endregion
        }
    }
}