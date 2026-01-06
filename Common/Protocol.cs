using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common;

/// <summary>
/// Hub ve Clientlar arasındaki iletişim protokolü standartlarını tanımlayan sınıf.
/// Tüm mesajlaşma formatları buradaki sabitlere göre yapılandırılır.
/// </summary>
public static class Protocol
{
    // --- Kayıt ve Temel İletişim ---

    /// <summary>
    /// Hub’a ilk bağlanınca istemcinin gönderdiği kayıt paketi ön eki.
    /// Örn: REGISTER:SmartLamp
    /// </summary>
    public const string RegisterPrefix = "REGISTER:";

    /// <summary>
    /// Kontrol komutları için kullanılan ön ek.
    /// Örn: CMD:LAMP:ON veya CMD:LOCK:UNLOCK
    /// </summary>
    public const string CmdPrefix = "CMD:";

    /// <summary>
    /// Cihazlarda meydana gelen olayları bildirmek için kullanılır.
    /// Örn: EVENT:MOTION
    /// </summary>
    public const string EventPrefix = "EVENT:";

    // --- Sensör ve Veri Akışı (UDP) ---

    /// <summary>
    /// UDP üzerinden gönderilen sıcaklık verisi ön eki.
    /// Örn: TEMP:24.7
    /// </summary>
    public const string TempPrefix = "TEMP:";

    /// <summary>
    /// Hareket veya güvenlik ihlali gibi anlık uyarı ön eki.
    /// </summary>
    public const string AlertPrefix = "ALERT:";

    /// <summary>
    /// UDP üzerinden yayınlanan durum (State) paketlerinin başlığı.
    /// </summary>
    public const string UdpStatePrefix = "STATE;";

    // --- Senkronizasyon ve Durum Yönetimi ---

    /// <summary>
    /// Veri kaybı durumunda ControlPanel'in TCP üzerinden talep ettiği yeniden senkronizasyon isteği.
    /// </summary>
    public const string ResyncRequest = "RESYNC_REQUEST";

    /// <summary>
    /// Hub'ın TCP üzerinden döneceği anlık durum özeti (snapshot) ön eki.
    /// </summary>
    public const string StateSnapshotPrefix = "STATE_SNAPSHOT:";

    // --- Uyarı Seviyeleri ---

    /// <summary>
    /// Bilgi amaçlı düşük seviye uyarı.
    /// </summary>
    public const string AlertInfo = "INFO";

    /// <summary>
    /// Dikkat edilmesi gereken orta seviye uyarı.
    /// </summary>
    public const string AlertWarning = "WARNING";

    /// <summary>
    /// Acil müdahale gerektiren kritik seviye uyarı.
    /// </summary>
    public const string AlertCritical = "CRITICAL";
}