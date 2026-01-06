using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Common;

namespace ControlPanel
{
    /// <summary>
    /// OmniConnect Akýllý Ev Sistemi - Kontrol Paneli Ana Formu.
    /// Hub'dan gelen verileri görselleþtirir ve að trafiðini yönetir.
    /// </summary>
    public partial class Form1 : Form
    {
        #region --- Fields (Deðiþkenler) ---

        // Að Baðlantý Nesneleri
        private TcpClient? _tcp;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;

        // UDP Durum Yönetimi
        private UdpClient? _udp;
        private long _lastSeq = -1; // Paket kaybý kontrolü için son sýra numarasý

        // UI Bileþenleri
        private TextBox txtDashboard = null!;
        private ListBox lstLog = null!;

        #endregion

        #region --- Constructor & Initialization ---

        public Form1()
        {
            // Arayüzü oluþtur
            BuildUi();

            // Form gösterildiðinde Hub'a baðlan
            Shown += async (_, __) => await ConnectToHub();

            // Form kapanýrken kaynaklarý temizle
            FormClosing += (_, __) => Cleanup();
        }

        /// <summary>
        /// Arayüz bileþenlerini programatik olarak yapýlandýrýr.
        /// </summary>
        private void BuildUi()
        {
            Text = "OmniConnect ControlPanel";
            Width = 900;
            Height = 550;
            StartPosition = FormStartPosition.CenterScreen;

            // Terminal benzeri gösterge paneli
            txtDashboard = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.None,
                Left = 10,
                Top = 10,
                Width = ClientSize.Width - 20,
                Height = 260,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new System.Drawing.Font("Consolas", 11),
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.White
            };

            // Olay kayýtlarý listesi
            lstLog = new ListBox
            {
                Left = 10,
                Top = 280,
                Width = ClientSize.Width - 20,
                Height = ClientSize.Height - 290,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            Controls.Add(txtDashboard);
            Controls.Add(lstLog);

            SetDashboard("-", "-", "-", "-", "-", "-", "DISCONNECTED");
        }

        #endregion

        #region --- Network Management (Að Yönetimi) ---

        /// <summary>
        /// Hub sunucusuna TCP baðlantýsý kurar ve dinleme thread'lerini baþlatýr.
        /// </summary>
        private async Task ConnectToHub()
        {
            try
            {
                SetDashboard("-", "-", "-", "-", "-", "-", "CONNECTING...");
                Log("[ME] Connecting...");

                _tcp = new TcpClient();
                await _tcp.ConnectAsync(NetworkConfig.HubIp, NetworkConfig.TcpPort);

                var ns = _tcp.GetStream();
                _reader = new StreamReader(ns, Encoding.UTF8);
                _writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };

                // Hub'a kendini tanýt
                await _writer.WriteLineAsync(Protocol.RegisterPrefix + "ControlPanel");
                Log("[ME] REGISTER:ControlPanel");

                _cts = new CancellationTokenSource();

                // UDP Dinleme Döngüsü (State Broadcastlarý)
                _udp = new UdpClient(NetworkConfig.UdpStatePort);
                _udp.EnableBroadcast = true;
                _ = Task.Run(() => UdpStateListenLoop(_cts.Token));

                // TCP Dinleme Döngüsü (Snapshot ve Resync yanýtlarý)
                _ = Task.Run(() => ListenLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                SetDashboard("-", "-", "-", "-", "-", "-", "ERROR");
                Log("Connect error: " + ex.Message);
            }
        }

        /// <summary>
        /// TCP üzerinden gelen verileri sürekli dinleyen döngü.
        /// </summary>
        private async Task ListenLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _tcp != null && _tcp.Connected)
                {
                    var line = await _reader!.ReadLineAsync();
                    if (line == null) break;

                    // UI güncellemeleri ana thread üzerinden yapýlmalý
                    BeginInvoke(new Action(() => HandleHubLine(line)));
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => Log("TCP listen error: " + ex.Message)));
            }

            BeginInvoke(new Action(() => SetDashboard("-", "-", "-", "-", "-", "-", "DISCONNECTED")));
        }

        /// <summary>
        /// UDP üzerinden gelen STATE paketlerini dinler ve SEQ takibi yapar.
        /// </summary>
        private async Task UdpStateListenLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _udp != null)
                {
                    UdpReceiveResult result;
                    try
                    {
                        // Port kapatýldýðýnda veya iptal geldiðinde Exception fýrlatýr
                        result = await _udp.ReceiveAsync();
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (SocketException) { break; }

                    var msg = Encoding.UTF8.GetString(result.Buffer);

                    if (!msg.StartsWith(Protocol.UdpStatePrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Paket ayrýþtýrma: STATE;SEQ=124;MODE=...
                    long seq = 0;
                    var parts = msg.Split(';', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var p in parts)
                    {
                        var kv = p.Split('=', 2);
                        if (kv.Length != 2) continue;

                        var k = kv[0].Trim().ToUpperInvariant();
                        var v = kv[1].Trim();

                        if (k == "SEQ") long.TryParse(v, out seq);
                    }

                    // PAKET KAYBI KONTROLÜ (Gap Detection)
                    if (_lastSeq != -1 && seq != 0 && seq > _lastSeq + 1)
                    {
                        try
                        {
                            // Eksik veri var, TCP üzerinden tam snapshot iste
                            await _writer!.WriteLineAsync(Protocol.ResyncRequest);
                            BeginInvoke(new Action(() =>
                                Log($"[ME] UDP GAP {_lastSeq} -> {seq} (RESYNC_REQUEST sent)")
                            ));
                        }
                        catch { }
                    }

                    if (seq != 0) _lastSeq = Math.Max(_lastSeq, seq);

                    // UDP log kaydý
                    BeginInvoke(new Action(() => Log("[UDP] " + msg)));
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => Log("UDP listen error: " + ex.Message)));
            }
        }

        #endregion

        #region --- Data Processing (Veri Ýþleme) ---

        /// <summary>
        /// Hub'dan gelen satýrlarý analiz eder ve Dashboard'u günceller.
        /// </summary>
        private void HandleHubLine(string line)
        {
            Log("[HUB] " + line);

            // Snapshot yanýtý gelmiþse prefix'i ayýkla
            if (line.StartsWith(Protocol.StateSnapshotPrefix, StringComparison.OrdinalIgnoreCase))
            {
                line = line.Substring(Protocol.StateSnapshotPrefix.Length).Trim();
            }

            if (!line.StartsWith("STATE:", StringComparison.OrdinalIgnoreCase))
                return;

            string mode = "-", temp = "-", motion = "-", lamp = "-", lockv = "-", last = "-";

            // STATE: payload kýsmýný iþle
            var payload = line.Substring("STATE:".Length);
            foreach (var part in payload.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;

                var k = kv[0].Trim().ToUpperInvariant();
                var v = kv[1].Trim();

                if (k == "MODE") mode = v;
                else if (k == "TEMP") temp = v;
                else if (k == "MOTION") motion = v;
                else if (k == "LAMP") lamp = v;
                else if (k == "LOCK") lockv = v;
                else if (k == "LAST") last = v;
            }

            SetDashboard(mode, temp, motion, lamp, lockv, last, "CONNECTED");
        }

        #endregion

        #region --- UI & Cleanup (Arayüz ve Temizlik) ---

        /// <summary>
        /// Dashboard ekranýný verilerle günceller.
        /// </summary>
        private void SetDashboard(string mode, string temp, string motion, string lamp, string lockv, string last, string conn)
        {
            var t = temp == "-" ? "-" : $"{temp} C";

            txtDashboard.Text =
$@"==================================================
        OMNICONNECT CONTROL PANEL
==================================================
MODE      : {mode}
TEMP      : {t}
MOTION    : {motion}
LAMP      : {lamp}
LOCK      : {lockv}
--------------------------------------------------
LAST EVENT: {last}
CONN      : {conn}
==================================================";
        }

        /// <summary>
        /// Olay kayýtlarýný listeye ekler ve liste boyutunu sýnýrlar.
        /// </summary>
        private void Log(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";

            // Bellek yönetimi için eski loglarý temizle
            if (lstLog.Items.Count > 250)
                lstLog.Items.RemoveAt(lstLog.Items.Count - 1);

            lstLog.Items.Insert(0, line);
        }

        /// <summary>
        /// Tüm aktif baðlantýlarý ve thread'leri güvenli þekilde kapatýr.
        /// </summary>
        private void Cleanup()
        {
            try { _cts?.Cancel(); } catch { }
            try { _udp?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
        }

        #endregion
    }
}