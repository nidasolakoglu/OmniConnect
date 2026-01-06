using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common;

public static class DemoConfig
{
    // ThermoSensor: UDP sıcaklık gönderme aralığı
    public const int ThermoIntervalMs = 3000;

    // MotionSensor: motion üretme aralığı
    public const int MotionIntervalMs = 15000;

    // Heartbeat: TCP PING aralığı
    public const int HeartbeatIntervalMs = 10000;

    // Hub dashboard yenileme aralığı
    public const int DashboardIntervalMs = 2000;

    // UDP sensor offline sayma eşiği (kaç ms paket gelmezse OFFLINE)
    public const int UdpOfflineThresholdMs = 7000;
}

