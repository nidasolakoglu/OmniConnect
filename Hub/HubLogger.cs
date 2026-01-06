using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Pipes;
using Common;

/// <summary>
/// Hub sistemi için merkezi günlükleme (logging) sınıfı.
/// Logları hem konsola hem de Named Pipe üzerinden LogCollector'a iletir.
/// </summary>
public static class HubLogger
{
    #region --- Private Fields & Sync Objects ---

    private static StreamWriter? _pipeWriter;
    private static readonly object _lock = new();

    #endregion

    #region --- Public Methods ---

    /// <summary>
    /// Belirtilen mesajı zaman damgasıyla birlikte günlüğe kaydeder.
    /// </summary>
    /// <param name="msg">Kaydedilecek mesaj içeriği.</param>
    public static void Log(string msg)
    {
        // Mesajı formatla: [SS:DD:SS] Mesaj
        string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";

        // 1. Konsol Çıktısı (Anlık takip için)
        Console.WriteLine(line);

        // 2. Named Pipe Çıktısı (LogCollector için)
        try
        {
            EnsureConnected();

            lock (_lock)
            {
                if (_pipeWriter != null)
                {
                    _pipeWriter.WriteLine(line);
                    _pipeWriter.Flush();
                }
            }
        }
        catch
        {
            // LogCollector o anda bağlı olmayabilir veya pipe kopmuş olabilir.
            // Hub'ın çalışmasını engellememek için hata yutulur ve kaynaklar temizlenir.
            SafeClose();
        }
    }

    #endregion

    #region --- Connection Management ---

    /// <summary>
    /// LogCollector'a giden Named Pipe bağlantısının açık olduğundan emin olur.
    /// </summary>
    private static void EnsureConnected()
    {
        lock (_lock)
        {
            // Zaten bir yazıcı (writer) varsa işlemi sürdür
            if (_pipeWriter != null) return;

            // Yeni bir Named Pipe istemcisi oluştur
            var client = new NamedPipeClientStream(
                ".",
                IpcConfig.LogPipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            // KRİTİK: LogCollector açık değilse Hub'ın bloklanmaması için 
            // çok kısa bir timeout (150ms) ile bağlanmayı dener.
            client.Connect(timeout: 150);

            _pipeWriter = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
        }
    }

    /// <summary>
    /// Pipe akışını güvenli bir şekilde kapatır ve belleği temizler.
    /// </summary>
    private static void SafeClose()
    {
        lock (_lock)
        {
            try
            {
                _pipeWriter?.Dispose();
            }
            catch
            {
                /* Kapatma hatası görmezden gelinir */
            }

            _pipeWriter = null;
        }
    }

    #endregion
}