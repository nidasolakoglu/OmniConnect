using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common;

/// <summary>
/// Merkezi ağ yapılandırma ayarlarını içeren statik sınıf.
/// Tüm istemci ve sunucu (Hub) bileşenleri bu ortak ayarları kullanır.
/// </summary>
public static class NetworkConfig
{
    // --- Bağlantı Adresleri ---

    /// <summary>
    /// Hub (Merkezi Sunucu) IP adresi. Varsayılan: Localhost
    /// </summary>
    public const string HubIp = "127.0.0.1";

    // --- Port Tanımlamaları ---

    /// <summary>
    /// Güvenilir veri iletimi (Komutlar, Kayıt vb.) için kullanılan TCP portu.
    /// </summary>
    public const int TcpPort = 5000;

    /// <summary>
    /// Hızlı veri iletimi için ayrılmış ana UDP portu.
    /// </summary>
    public const int UdpPort = 5001;

    /// <summary>
    /// Cihaz durumlarının (STATE) broadcast yayını için kullanılan özel port.
    /// </summary>
    public const int UdpStatePort = 5002;
}