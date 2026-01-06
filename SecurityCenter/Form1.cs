using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using Common;

namespace SecurityCenter
{
    public partial class Form1 : Form
    {
        private TcpClient? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        // SEVERITY + ROOM -> MAP
        private readonly Dictionary<(string Sev, string Room), string> _mapBySevRoom =
           new()

    {
        // INFO (sarý)
        [("INFO", "KITCHEN")] = @"Assets\map_info_kitchen.png",
                [("INFO", "BEDROOM")] = @"Assets\map_info_bedroom.jpg",
                [("INFO", "LIVING_ROOM")] = @"Assets\map_info_living.jpg",
                [("INFO", "BATHROOM")] = @"Assets\map_info_bathroom.jpg",

                // WARNING (kýrmýzý oda)
                [("WARNING", "KITCHEN")] = @"Assets\map_warn_kitchen.jpg",
                [("WARNING", "BEDROOM")] = @"Assets\map_warn_bedroom.png",
                [("WARNING", "LIVING_ROOM")] = @"Assets\map_warn_living.jpg",
                [("WARNING", "BATHROOM")] = @"Assets\map_warn_bathroom.jpg",

                // CRITICAL (tüm harita)
                [("CRITICAL", "*")] = @"Assets\map_critical_all.jpg",
            };

        public Form1()
        {
            InitializeComponent();

            lstEvents.Dock = DockStyle.Right;
            pbMap.Dock = DockStyle.Fill;
            pbMap.SizeMode = PictureBoxSizeMode.Zoom;

            Load += async (_, __) =>
            {
                // Default açýlýþ
                ShowRoomMap("INFO", "KITCHEN", logEvent: false);
                await ConnectToHubAndListenAsync();
            };
        }

        // ----------------- CORE -----------------

        private async Task ConnectToHubAndListenAsync()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(NetworkConfig.HubIp, NetworkConfig.TcpPort);

                var ns = _client.GetStream();
                _reader = new StreamReader(ns);
                _writer = new StreamWriter(ns) { AutoFlush = true };

                await _writer.WriteLineAsync($"{Protocol.RegisterPrefix}SecurityCenter");
                var resp = await _reader.ReadLineAsync();
                Text = $"SecurityCenter (Connected) - {resp}";

                while (true)
                {
                    var line = await _reader.ReadLineAsync();
                    if (line == null) break;

                    if (!line.StartsWith(Protocol.AlertPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var payload = line.Substring(Protocol.AlertPrefix.Length).Trim();
                    // ALERT:LEVEL:ROOM

                    string level = Protocol.AlertInfo;
                    string room = payload;

                    var parts = payload.Split(':', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 2)
                    {
                        level = parts[0].ToUpperInvariant();
                        room = parts[1];
                    }

                    BeginInvoke(new Action(() =>
                    {
                        ShowRoomMap(level, room, logEvent: true);

                        // Sesler
                        if (level == Protocol.AlertCritical)
                        {
                            try { System.Media.SystemSounds.Hand.Play(); } catch { }
                            try { Activate(); } catch { }
                        }
                        else if (level == Protocol.AlertWarning)
                        {
                            try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Text = $"SecurityCenter (Offline) - {ex.Message}";
            }
        }

        // ----------------- MAP -----------------

        private void ShowRoomMap(string sevRaw, string roomRaw, bool logEvent = true)
        {
            var sev = (sevRaw ?? "INFO").Trim().ToUpperInvariant();
            var room = NormalizeRoom(roomRaw);

            string relPath;

            if (sev == "CRITICAL" &&
                _mapBySevRoom.TryGetValue(("CRITICAL", "*"), out var criticalPath))
            {
                relPath = criticalPath;
            }
            else if (_mapBySevRoom.TryGetValue((sev, room), out var p))
            {
                relPath = p;
            }
            else
            {
                relPath = @"Assets\map.jpeg";
            }

            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relPath);

            if (File.Exists(fullPath))
            {
                using var bmpTemp = new Bitmap(fullPath);
                pbMap.Image?.Dispose();
                pbMap.Image = new Bitmap(bmpTemp);
            }

            if (logEvent)
            {
                lstEvents.Items.Add($"{DateTime.Now:HH:mm:ss} ALERT:{sev}: {room}");
                lstEvents.TopIndex = lstEvents.Items.Count - 1;
            }
        }

        // ----------------- UTIL -----------------

        private string NormalizeRoom(string room)
        {
            room = (room ?? "").Trim().ToUpperInvariant();
            room = room.Replace(" ", "_").Replace("-", "_");
            return room;
        }
        private void pbMap_Click(object sender, EventArgs e)
        {
            // bilerek boþ
        }

        private void lstEvents_SelectedIndexChanged(object sender, EventArgs e)
        {
            // bilerek boþ
        }

    }
}
