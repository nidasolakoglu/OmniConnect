using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Globalization;
using Common;

/// <summary>
/// OmniConnect Akıllı Ev Sistemi - Termal Sensör İstemcisi.
/// UDP protokolü kullanarak Hub'a periyodik sıcaklık verisi gönderir.
/// </summary>
class Program
{
    static async Task Main()
    {
        Console.Title = "ThermoSensor (UDP)";

        #region --- Configuration & Initial State (Yapılandırma) ---

        // UDP Gönderici (Sender) yapılandırması
        using var udp = new UdpClient();

        // Hub'ın hedef ağ adresi ve portu
        var hubEndpoint = new IPEndPoint(IPAddress.Parse(NetworkConfig.HubIp), NetworkConfig.UdpPort);

        // Başlangıç sıcaklık değeri (Simülasyon için)
        double temp = 25.0;

        Console.WriteLine("==================================================");
        Console.WriteLine("       OMNICONNECT THERMO-SENSOR ACTIVE           ");
        Console.WriteLine("==================================================");
        Console.WriteLine($"[TARGET] Hub IP  : {NetworkConfig.HubIp}");
        Console.WriteLine($"[TARGET] UDP Port: {NetworkConfig.UdpPort}");
        Console.WriteLine("--------------------------------------------------");

        #endregion

        #region --- Transmission Loop (Veri Gönderim Döngüsü) ---

        while (true)
        {
            // 1) SICAKLIK SİMÜLASYONU
            // Mevcut mantık: 24 ile 32 derece arasında rastgele dalgalanma sağlar.
            temp += (Random.Shared.NextDouble() - 0.5) * 1.5;

            // Sınır koruması (24°C - 32°C aralığında tutar)
            if (temp < 24) temp = 24;
            if (temp > 32) temp = 32;

            // 2) MESAJ HAZIRLAMA
            // Örn format: "TEMP:29.2" (Nokta ayracı için InvariantCulture korunmuştur)
            string msg = Protocol.TempPrefix + temp.ToString("0.0", CultureInfo.InvariantCulture);

            // 3) UDP PAKET GÖNDERİMİ
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                await udp.SendAsync(data, data.Length, hubEndpoint);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Veri Gönderildi -> {msg}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Gönderim Hatası: {ex.Message}");
            }

            // Yapılandırmada belirtilen süre kadar bekle (DemoConfig)
            await Task.Delay(DemoConfig.ThermoIntervalMs);
        }

        #endregion
    }
}