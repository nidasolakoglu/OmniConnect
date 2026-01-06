using System.Net.Sockets;
using Common;

/// <summary>
/// OmniConnect Akıllı Ev Sistemi - Hareket Sensörü İstemcisi.
/// Hareket algıladığında Hub'a bildirim yapar ve bir görsel snapshot iletir.
/// </summary>
class Program
{
    #region --- Main Logic (Ana Mantık) ---

    static async Task Main()
    {
        Console.Title = "MotionSensor";

        try
        {
            // 1) Hub'a TCP Bağlantısı Kurma
            using var client = new TcpClient();
            await client.ConnectAsync(NetworkConfig.HubIp, NetworkConfig.TcpPort);

            using var ns = client.GetStream();
            using var reader = new StreamReader(ns);
            using var writer = new StreamWriter(ns) { AutoFlush = true };

            // 2) HUB KAYIT (REGISTER) AŞAMASI
            // Hub'a bu istemcinin bir "MotionSensor" olduğunu bildiriyoruz.
            await writer.WriteLineAsync(Protocol.RegisterPrefix + "MotionSensor");

            // Hub'dan gelen onay mesajını yazdır
            Console.WriteLine("[HUB] " + await reader.ReadLineAsync());

            // 3) SİMÜLASYON DÖNGÜSÜ
            // DemoConfig içinde belirlenen aralıklarla hareket üretilir.
            while (true)
            {
                await Task.Delay(DemoConfig.MotionIntervalMs);

                Console.WriteLine("\n>>> MOTION DETECTED! (Hareket Algılandı)");

                #region --- Olay Gönderimi (Event Notification) ---

                // Rastgele bir oda seçimi yaparak olayı yapılandır
                string[] rooms = { "KITCHEN", "BEDROOM", "LIVING_ROOM", "BATHROOM" };
                string room = rooms[Random.Shared.Next(rooms.Length)];

                // EVENT:MOTION:ODA_ADI formatında mesajı gönder
                await writer.WriteLineAsync($"{Protocol.EventPrefix}MOTION:{room}");

                // Hub yanıtını dinle (Heartbeat PONG veya OK:MOTION)
                var hubReply = await reader.ReadLineAsync();

                if (hubReply == null)
                {
                    Console.WriteLine("[HUB] Bağlantı kapandı.");
                    break;
                }

                if (!(hubReply == "PONG" || hubReply.StartsWith("OK")))
                {
                    Console.WriteLine("[HUB] Mesaj: " + hubReply);
                }

                #endregion

                #region --- Dosya Transferi (Snapshot Upload) ---

                // Gerçek bir görüntü yerine simüle edilmiş byte dizisi üret
                byte[] fakeJpg = GenerateFakeSnapshotBytes();

                // Hub'a dosya boyutunu önceden bildir
                await writer.WriteLineAsync($"FILESIZE:{fakeJpg.Length}");

                // Ham byte verisini network stream üzerinden gönder
                await ns.WriteAsync(fakeJpg, 0, fakeJpg.Length);
                await ns.FlushAsync();

                Console.WriteLine($"[INFO] Snapshot gönderildi: {fakeJpg.Length} bytes.");

                // KRİTİK: Hub'ın dosya alım onayı (OK:FILE) beklenerek senkronizasyon korunur.
                var fileAck = await reader.ReadLineAsync();
                if (fileAck != null && !(fileAck == "PONG" || fileAck.StartsWith("OK")))
                {
                    Console.WriteLine("[HUB RESPONSE] " + fileAck);
                }

                #endregion
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n[!] HATA OLUŞTU:");
            Console.WriteLine(ex.Message);
        }

        Console.WriteLine("\nÇıkmak için Enter tuşuna basın...");
        Console.ReadLine();
    }

    #endregion

    #region --- Helper Methods (Yardımcı Metotlar) ---

    /// <summary>
    /// Test ve sunum amaçlı, rastgele verilerden oluşan "sahte" bir görsel dosyası üretir.
    /// </summary>
    /// <returns>Rastgele veri içeren byte dizisi.</returns>
    static byte[] GenerateFakeSnapshotBytes()
    {
        // Yaklaşık 30 KB boyutunda rastgele byte dizisi üretilir
        byte[] data = new byte[30_000];
        Random.Shared.NextBytes(data);
        return data;
    }

    #endregion
}