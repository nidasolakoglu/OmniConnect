using System.Net.Sockets;
using Common;

/// <summary>
/// OmniConnect Akıllı Ev Sistemi - Akıllı Kilit İstemcisi.
/// Hub üzerinden gelen güvenlik komutlarını (LOCK/UNLOCK) yürütür ve durum raporlar.
/// </summary>
class Program
{
    static async Task Main()
    {
        Console.Title = "SmartLock";

        try
        {
            #region --- Connection & Registration (Bağlantı ve Kayıt) ---

            // 1) Hub'a TCP Bağlantısı Kurma
            using var client = new TcpClient();
            await client.ConnectAsync(NetworkConfig.HubIp, NetworkConfig.TcpPort);

            using var ns = client.GetStream();
            using var reader = new StreamReader(ns);
            using var writer = new StreamWriter(ns) { AutoFlush = true };

            // Hub’a kendini "SmartLock" olarak tanıt
            await writer.WriteLineAsync(Protocol.RegisterPrefix + "SmartLock");

            // Hub’dan gelen onay cevabını oku
            var resp = await reader.ReadLineAsync();
            Console.WriteLine($"[HUB CONNECTION] {resp}");

            #endregion

            #region --- Command Processing Loop (Komut İşleme Döngüsü) ---

            Console.WriteLine("[INFO] Komutlar bekleniyor...");

            // 3) Sürekli Hub'dan gelecek komutları dinle
            while (true)
            {
                var line = await reader.ReadLineAsync();

                // Bağlantı kesildiyse döngüden çık
                if (line == null) break;

                // Sistem mesajlarını (Heartbeat vb.) ayıkla
                if (line == "PONG" || line.StartsWith("OK"))
                    continue;

                Console.WriteLine($"[INCOMING CMD] {line}");

                // Kilitleme Komutu
                if (line == "CMD:LOCK:LOCK")
                {
                    Console.WriteLine("Kilit KİLİTLENDİ 🔒");

                    // Hub'a işlemin başarıyla tamamlandığını raporla
                    await writer.WriteLineAsync("STATE:LOCK:LOCK");
                }
                // Kilit Açma Komutu
                else if (line == "CMD:LOCK:UNLOCK")
                {
                    Console.WriteLine("Kilit AÇILDI 🔓");

                    // Hub'a işlemin başarıyla tamamlandığını raporla
                    await writer.WriteLineAsync("STATE:LOCK:UNLOCK");
                }
                else
                {
                    // Bilinmeyen komutlar güvenlik gereği loglanabilir ancak sessiz geçilir
                    // Console.WriteLine($"[WARN] Bilinmeyen komut: {line}");
                }
            }

            #endregion
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n[!] Bağlantı Hatası:");
            Console.WriteLine(ex.Message);
        }

        #region --- Termination (Sonlandırma) ---

        Console.WriteLine("\nSistem durduruldu. Çıkmak için Enter tuşuna basın...");
        Console.ReadLine();

        #endregion
    }
}