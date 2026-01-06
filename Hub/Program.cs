using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Common;
using System.Text;
using System.Globalization;

/// <summary>
/// OMNICONNECT SMART HOME HUB - Merkezi Yönetim Sunucusu
/// Tüm TCP/UDP trafiğini yönetir, kural motorunu çalıştırır ve dashboard'u günceller.
/// </summary>
class Program
{
    #region --- Data Structures & State Management ---

    /// <summary>
    /// Bağlı olan her bir istemcinin (client) oturum bilgilerini tutar.
    /// </summary>
    class ClientSession
    {
        public DeviceType Type { get; init; }
        public StreamWriter Writer { get; init; } = default!;
        public object WriteLock { get; } = new(); // Aynı anda tek yazma işlemi için
    }

    // Thread-safe bağlı cihazlar listesi
    static ConcurrentDictionary<TcpClient, ClientSession> Clients = new();

    // --- Merkezi Sistem Durumu (State) ---
    static double LastTemp = double.NaN;
    static bool MotionDetected = false;
    static string LampState = "OFF";
    static string LockState = "UNLOCK";
    static string Mode = "AWAY";
    static string LastEvent = "-";
    static DateTime LastUdpTempTime = DateTime.MinValue;
    static long StateSeq = 0; // UDP paket takip numarası
    static string LastRoom = "-";

    // Kural motoru (rules.txt dosyasından yüklenir)
    static RuleEngine Rules = new RuleEngine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rules.txt"));

    #endregion

    #region --- Entry Point & Core Loops ---

    static async Task Main()
    {
        Console.Title = "OmniConnect HUB";

        // 1) TCP Server Başlatma
        var listener = new TcpListener(IPAddress.Parse(NetworkConfig.HubIp), NetworkConfig.TcpPort);
        listener.Start();
        Console.WriteLine($"[HUB] TCP listening on {NetworkConfig.HubIp}:{NetworkConfig.TcpPort}");
        HubLogger.Log($"TCP listening on {NetworkConfig.HubIp}:{NetworkConfig.TcpPort}");

        // 2) Arkaplan Görevlerini Başlat (UDP Dinleme ve Dashboard)
        _ = Task.Run(UdpListenLoop);
        _ = Task.Run(DashboardLoop);

        // 3) Ana TCP Kabul Döngüsü
        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    #endregion

    #region --- Client Handling (TCP) ---

    /// <summary>
    /// Yeni bağlanan her TCP istemcisini ayrı bir thread'de yönetir.
    /// </summary>
    static async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using var ns = client.GetStream();
            using var reader = new StreamReader(ns);
            using var writer = new StreamWriter(ns) { AutoFlush = true };

            // AŞAMA 1: Kimlik Doğrulama (REGISTER)
            string? firstLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(firstLine) || !firstLine.StartsWith(Protocol.RegisterPrefix))
            {
                await writer.WriteLineAsync("ERR:Expected REGISTER");
                client.Close();
                return;
            }

            var name = firstLine.Substring(Protocol.RegisterPrefix.Length).Trim();
            var dtype = ParseDeviceType(name);

            // Oturumu kaydet
            var session = new ClientSession { Type = dtype, Writer = writer };
            Clients[client] = session;

            LastEvent = $"Connected: {dtype}";
            HubLogger.Log($"Connected: {dtype}");

            await writer.WriteLineAsync("OK:REGISTERED");

            // Eğer bağlanan bir ControlPanel ise güncel durumu hemen gönder
            if (dtype == DeviceType.ControlPanel)
            {
                await writer.WriteLineAsync(BuildStateLine());
            }

            // AŞAMA 2: Sürekli Mesaj Dinleme Döngüsü
            while (true)
            {
                string? line = await reader.ReadLineAsync();
                if (line == null) break; // Bağlantı koptu

                await ProcessTcpLine(dtype, line, writer, ns);
            }
        }
        catch (Exception ex)
        {
            LastEvent = $"Client error: {ex.Message}";
        }
        finally
        {
            // AŞAMA 3: Temizlik (Cleanup)
            Clients.TryRemove(client, out var removedSession);
            var removedType = removedSession?.Type ?? DeviceType.Unknown;

            try { client.Close(); } catch { }

            LastEvent = $"Disconnected: {removedType}";
            HubLogger.Log($"Disconnected: {removedType}");
        }
    }

    /// <summary>
    /// Gelen TCP mesajlarını analiz eder ve ilgili eylemi tetikler.
    /// </summary>
    static async Task ProcessTcpLine(DeviceType from, string line, StreamWriter writer, NetworkStream ns)
    {
        // --- Heartbeat (Yaşam Sinyali) ---
        if (line == "PING")
        {
            await writer.WriteLineAsync("PONG");
            return;
        }

        // --- Senkronizasyon Talebi ---
        if (line == Protocol.ResyncRequest)
        {
            await writer.WriteLineAsync(Protocol.StateSnapshotPrefix + BuildStateLine());
            HubLogger.Log("RESYNC_REQUEST received -> STATE_SNAPSHOT sent");
            return;
        }

        // --- Mod Değişimi (AWAY/HOME) ---
        if (line.StartsWith("MODE:"))
        {
            Mode = line.Substring("MODE:".Length).Trim();
            LastEvent = $"Mode -> {Mode}";
            await writer.WriteLineAsync("OK:MODE");

            BroadcastTcp(DeviceType.ControlPanel, BuildStateLine());
            BroadcastStateUdp();
            ApplyRulesAndBroadcast("MODE_CHANGE");
            return;
        }

        // --- Komut İşleme (CMD) ---
        if (line.StartsWith(Protocol.CmdPrefix))
        {
            var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                await writer.WriteLineAsync("ERR:Bad CMD format");
                return;
            }

            string targetName = parts[1].Trim();
            string action = parts[2].Trim();
            var targetType = ParseDeviceType(targetName);

            if (targetType == DeviceType.Unknown)
            {
                await writer.WriteLineAsync("ERR:Unknown target");
                return;
            }

            // Lamba Kontrolü
            if (targetType == DeviceType.SmartLamp)
            {
                if (action.Equals("ON", StringComparison.OrdinalIgnoreCase))
                {
                    LampState = "ON";
                    BroadcastTcp(DeviceType.SmartLamp, "CMD:LAMP:ON");
                    LastEvent = "CP -> LAMP ON";
                    HubLogger.Log("CMD from ControlPanel -> LAMP ON");
                    BroadcastStateUdp();
                    await writer.WriteLineAsync("OK:LAMP:ON");
                    return;
                }
                if (action.Equals("OFF", StringComparison.OrdinalIgnoreCase))
                {
                    LampState = "OFF";
                    BroadcastTcp(DeviceType.SmartLamp, "CMD:LAMP:OFF");
                    LastEvent = "CP -> LAMP OFF";
                    HubLogger.Log("CMD from ControlPanel -> LAMP OFF");
                    BroadcastStateUdp();
                    await writer.WriteLineAsync("OK:LAMP:OFF");
                    return;
                }
            }

            // Kilit Kontrolü
            if (targetType == DeviceType.SmartLock)
            {
                if (action.Equals("LOCK", StringComparison.OrdinalIgnoreCase))
                {
                    LockState = "LOCK";
                    BroadcastTcp(DeviceType.SmartLock, "CMD:LOCK:LOCK");
                    LastEvent = "CP -> LOCK";
                    HubLogger.Log("CMD from ControlPanel -> LOCK");
                    BroadcastStateUdp();
                    await writer.WriteLineAsync("OK:LOCK");
                    return;
                }
                if (action.Equals("UNLOCK", StringComparison.OrdinalIgnoreCase))
                {
                    LockState = "UNLOCK";
                    BroadcastTcp(DeviceType.SmartLock, "CMD:LOCK:UNLOCK");
                    LastEvent = "CP -> UNLOCK";
                    HubLogger.Log("CMD from ControlPanel -> UNLOCK");
                    BroadcastStateUdp();
                    await writer.WriteLineAsync("OK:UNLOCK");
                    return;
                }
            }
        }

        // --- Olay Bildirimi (EVENT:MOTION) ---
        if (line.StartsWith("EVENT:MOTION"))
        {
            MotionDetected = true;
            string room = "UNKNOWN";
            var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 3)
                room = parts[2].Trim();

            LastRoom = room.Trim().ToUpperInvariant();
            LastEvent = $"Motion detected @ {room}";
            HubLogger.Log($"EVENT:MOTION received @ {room}");

            Rules.NotifyMotion();
            ApplyRulesAndBroadcast("MOTION_EVENT");
            MotionDetected = false;

            await writer.WriteLineAsync("OK:MOTION");
            return;
        }

        // --- Cihaz Geri Bildirimleri (Acknowledge) ---
        if (line.StartsWith("STATE:LAMP:"))
        {
            LampState = line.Substring("STATE:LAMP:".Length).Trim();
            LastEvent = $"Lamp state -> {LampState}";
            await writer.WriteLineAsync("OK");
            return;
        }

        if (line.StartsWith("STATE:LOCK:"))
        {
            LockState = line.Substring("STATE:LOCK:".Length).Trim();
            LastEvent = $"Lock state -> {LockState}";
            await writer.WriteLineAsync("OK");
            return;
        }

        // --- Snapshot Dosya Transferi ---
        if (line.StartsWith("FILESIZE:"))
        {
            var sizeStr = line.Substring("FILESIZE:".Length).Trim();
            if (!int.TryParse(sizeStr, out int size) || size <= 0)
            {
                await writer.WriteLineAsync("ERR:Bad FILESIZE");
                return;
            }

            Directory.CreateDirectory("Snapshots");
            string filePath = Path.Combine("Snapshots", $"snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.bin");

            byte[] buffer = new byte[size];
            int readTotal = 0;
            while (readTotal < size)
            {
                int n = await ns.ReadAsync(buffer, readTotal, size - readTotal);
                if (n == 0) break;
                readTotal += n;
            }

            await File.WriteAllBytesAsync(filePath, buffer.Take(readTotal).ToArray());
            LastEvent = $"Snapshot saved -> {filePath} ({readTotal} bytes)";
            HubLogger.Log($"Snapshot saved -> {filePath} ({readTotal} bytes)");

            await writer.WriteLineAsync("OK:FILE");
            return;
        }

        await writer.WriteLineAsync("ERR:Unknown command");
    }

    #endregion

    #region --- Network Helpers (UDP & Broadcast) ---

    /// <summary>
    /// UDP üzerinden gelen sıcaklık verilerini dinler.
    /// </summary>
    static async Task UdpListenLoop()
    {
        using var udp = new UdpClient(NetworkConfig.UdpPort);
        while (true)
        {
            try
            {
                var result = await udp.ReceiveAsync();
                var text = Encoding.UTF8.GetString(result.Buffer);

                if (text.StartsWith(Protocol.TempPrefix))
                {
                    var valStr = text.Substring(Protocol.TempPrefix.Length).Trim();
                    if (double.TryParse(valStr, CultureInfo.InvariantCulture, out var t))
                    {
                        LastUdpTempTime = DateTime.Now;
                        LastTemp = t;

                        // Kural Motoru Değerlendirmesi
                        var acts = Rules.Evaluate(new HubContext(Mode, false, "UNKNOWN", true, t));

                        foreach (var act in acts)
                        {
                            if (act.Kind == "LAMP")
                            {
                                if (act.Value == "ON" && LampState != "ON")
                                {
                                    LampState = "ON";
                                    BroadcastTcp(DeviceType.SmartLamp, "CMD:LAMP:ON");
                                }
                                else if (act.Value == "OFF" && LampState != "OFF")
                                {
                                    LampState = "OFF";
                                    BroadcastTcp(DeviceType.SmartLamp, "CMD:LAMP:OFF");
                                }
                            }
                        }

                        LastEvent = $"Temp -> {t:0.0}";
                        HubLogger.Log($"TEMP received: {t:0.0}");
                        ApplyRulesAndBroadcast("TEMP_UPDATE");
                    }
                }
            }
            catch (Exception ex)
            {
                LastEvent = $"UDP error: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Belirli tipteki tüm cihazlara TCP mesajı gönderir.
    /// </summary>
    static void BroadcastTcp(DeviceType target, string msg)
    {
        foreach (var kv in Clients)
        {
            var session = kv.Value;
            if (session.Type != target) continue;

            try
            {
                lock (session.WriteLock)
                {
                    session.Writer.WriteLine(msg);
                }
            }
            catch { /* Hub'ın devamlılığı için hata yutulur */ }
        }
    }

    /// <summary>
    /// Sistemin son durumunu UDP üzerinden ağa yayınlar.
    /// </summary>
    static void BroadcastStateUdp()
    {
        try
        {
            StateSeq++;
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;
            var data = Encoding.UTF8.GetBytes(BuildUdpStatePacket());

            // 1) Genel Yayın (Broadcast)
            udp.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, NetworkConfig.UdpStatePort));

            // 2) Yerel Garanti (Windows bazen broadcast paketlerini döngüye almaz)
            udp.Send(data, data.Length, new IPEndPoint(IPAddress.Parse("127.0.0.1"), NetworkConfig.UdpStatePort));
        }
        catch { }
    }

    #endregion

    #region --- UI & Formatting ---

    /// <summary>
    /// Hub üzerindeki konsol göstergesini düzenli aralıklarla yeniler.
    /// </summary>
    static async Task DashboardLoop()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("==================================================");
            Console.WriteLine("         OMNICONNECT SMART HOME HUB");
            Console.WriteLine("==================================================");
            Console.WriteLine($"MOD: [{Mode}]    BAGLI CIHAZ: {Clients.Count}");
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine($"[UDP] Sicaklik : {(double.IsNaN(LastTemp) ? "-" : LastTemp.ToString("0.0"))} C");

            bool udpAlive = (DateTime.Now - LastUdpTempTime).TotalMilliseconds <= DemoConfig.UdpOfflineThresholdMs;
            Console.WriteLine($"[UDP] Sensor   : {(udpAlive ? "AKTIF" : "YOK")}");
            Console.WriteLine($"[TCP] Hareket  : {(MotionDetected ? "ALGILANDI" : "YOK")}");
            Console.WriteLine($"[TCP] Lamba    : {LampState}");
            Console.WriteLine($"[TCP] Kilit    : {LockState}");
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine($"Son Olay: {LastEvent}");
            Console.WriteLine("==================================================");

            MotionDetected = false;
            await Task.Delay(DemoConfig.DashboardIntervalMs);
        }
    }

    /// <summary>
    /// TCP için durum satırı oluşturur.
    /// </summary>
    static string BuildStateLine()
    {
        var temp = double.IsNaN(LastTemp) ? "-" : LastTemp.ToString("0.0");
        var motion = MotionDetected ? "ON" : "OFF";
        return $"STATE:MODE={Mode};TEMP={temp};MOTION={motion};LAMP={LampState};LOCK={LockState};LAST={LastEvent}";
    }

    /// <summary>
    /// UDP için nokta ayrımlı ve SEQ içeren durum paketi oluşturur.
    /// </summary>
    static string BuildUdpStatePacket()
    {
        var tempStr = double.IsNaN(LastTemp) ? "-" : LastTemp.ToString("0.0", CultureInfo.InvariantCulture);
        var motionStr = MotionDetected ? "ON" : "OFF";

        return $"{Protocol.UdpStatePrefix}" +
               $"SEQ={StateSeq};" +
               $"MODE={Mode};" +
               $"TEMP={tempStr};" +
               $"MOTION={motionStr};" +
               $"ROOM={LastRoom};" +
               $"LAMP={LampState};" +
               $"LOCK={LockState}";
    }

    #endregion

    #region --- Rule Logic (Kural Uygulama) ---

    /// <summary>
    /// Kural motorunu tetikler ve sonuçları ilgili cihazlara dağıtır.
    /// </summary>
    static void ApplyRulesAndBroadcast(string reason)
    {
        var ctx = new HubContext(Mode, MotionDetected, LastRoom, !double.IsNaN(LastTemp), LastTemp);

        foreach (var act in Rules.Evaluate(ctx))
        {
            if (act.Kind == "LAMP")
            {
                if (act.Value == "ON" && LampState != "ON")
                {
                    LampState = "ON";
                    BroadcastTcp(DeviceType.SmartLamp, "CMD:LAMP:ON");
                    HubLogger.Log($"RULE -> LAMP ON ({reason})");
                }
                else if (act.Value == "OFF" && LampState != "OFF")
                {
                    LampState = "OFF";
                    BroadcastTcp(DeviceType.SmartLamp, "CMD:LAMP:OFF");
                    HubLogger.Log($"RULE -> LAMP OFF ({reason})");
                }
            }
            else if (act.Kind == "LOCK")
            {
                if (act.Value == "LOCK" && LockState != "LOCK")
                {
                    LockState = "LOCK";
                    BroadcastTcp(DeviceType.SmartLock, "CMD:LOCK:LOCK");
                    HubLogger.Log($"RULE -> LOCK ({reason})");
                }
                else if (act.Value == "UNLOCK" && LockState != "UNLOCK")
                {
                    LockState = "UNLOCK";
                    BroadcastTcp(DeviceType.SmartLock, "CMD:LOCK:UNLOCK");
                    HubLogger.Log($"RULE -> UNLOCK ({reason})");
                }
            }
            else if (act.Kind == "ALERT")
            {
                var parts = act.Value.Split(':', StringSplitOptions.RemoveEmptyEntries);
                string severity = parts.Length >= 1 ? parts[0] : "INFO";
                string room = (parts.Length >= 2 && parts[1] != "ROOM") ? parts[1] : LastRoom;

                BroadcastTcp(DeviceType.SecurityCenter, $"{Protocol.AlertPrefix}{severity}:{room}");
                HubLogger.Log($"RULE -> ALERT {severity}:{room} ({reason})");
            }
        }

        BroadcastTcp(DeviceType.ControlPanel, BuildStateLine());
        BroadcastStateUdp();
    }

    #endregion

    #region --- Helper Parsers ---

    /// <summary>
    /// String isimden cihaz tipini belirler.
    /// </summary>
    static DeviceType ParseDeviceType(string s)
    {
        return s switch
        {
            "ThermoSensor" => DeviceType.ThermoSensor,
            "MotionSensor" => DeviceType.MotionSensor,
            "SmartLamp" => DeviceType.SmartLamp,
            "SmartLock" => DeviceType.SmartLock,
            "ControlPanel" => DeviceType.ControlPanel,
            "SecurityCenter" => DeviceType.SecurityCenter,
            _ => DeviceType.Unknown
        };
    }

    #endregion
}