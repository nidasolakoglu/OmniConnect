using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// OmniConnect Akıllı Ev Sistemi - Akıllı Lamba İstemcisi.
/// Hub'dan gelen TCP komutlarını (ON/OFF) dinler ve anlık durumunu bildirir.
/// </summary>
class Program
{
    [SupportedOSPlatform("windows")]
    static async Task Main()
    {
        Console.Title = "SmartLamp";

        try
        {
            #region --- Connection & Setup (Bağlantı Kurulumu) ---

            using var client = new TcpClient();
            await client.ConnectAsync(NetworkConfig.HubIp, NetworkConfig.TcpPort);

            using var ns = client.GetStream();
            using var reader = new StreamReader(ns);
            using var writer = new StreamWriter(ns) { AutoFlush = true };

            // Birden fazla thread'in (Main ve Heartbeat) aynı anda yazmasını engellemek için kilit nesnesi
            object writeLock = new();

            // 1) Hub’a "ben SmartLamp’im" diye kendini tanıt (REGISTER)
            await writer.WriteLineAsync(Protocol.RegisterPrefix + "SmartLamp");

            // 2) Hub’ın cevabını oku ve ekrana yazdır
            var resp = await reader.ReadLineAsync();
            Console.WriteLine($"[HUB] {resp}");

            #endregion

            #region --- Heartbeat Task (Yaşam Sinyali Görevi) ---

            // Arkaplanda belirli aralıklarla PING göndererek bağlantıyı canlı tutar
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(DemoConfig.HeartbeatIntervalMs);

                    try
                    {
                        lock (writeLock)
                        {
                            writer.WriteLine("PING");
                        }
                    }
                    catch
                    {
                        // Bağlantı koptuğunda döngüden çık
                        break;
                    }
                }
            });

            #endregion

            #region --- Command Loop (Komut Dinleme Döngüsü) ---

            string state = "OFF";

            // 3) Sürekli Hub'dan gelecek komutları bekle
            // ... döngünün içi ...
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;

                if (line == "PONG" || line.StartsWith("OK"))
                    continue;

                Console.WriteLine($"[CMD RECEIVED] {line}");

                if (line == "CMD:LAMP:ON")
                {
                    state = "ON"; // Değişken atanıyor
                    Console.WriteLine($"Lamba Durumu: {state} ✅"); // Değişken burada KULLANILDI, uyarı silindi.

                    lock (writeLock)
                        writer.WriteLine("STATE:LAMP:ON");
                }
                else if (line == "CMD:LAMP:OFF")
                {
                    state = "OFF"; // Değişken atanıyor
                    Console.WriteLine($"Lamba Durumu: {state} ✅"); // Değişken burada KULLANILDI, uyarı silindi.

                    lock (writeLock)
                        writer.WriteLine("STATE:LAMP:OFF");
                }
                // ... geri kalanlar ...
            }

            #endregion
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n[!] Kritik Bir Hata Oluştu:");
            Console.WriteLine(ex.Message);
        }

        Console.WriteLine("\nBağlantı kesildi. Çıkmak için Enter tuşuna basın...");
        Console.ReadLine();
    }
}