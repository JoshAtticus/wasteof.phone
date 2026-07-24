using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace wasteof.phone.Services
{
    public sealed class SocketService
    {
        private static readonly SocketService _instance = new SocketService();
        public static SocketService Instance => _instance;

        private MessageWebSocket _webSocket;
        private string _token;
        private bool _isConnecting = false;
        private bool _isConnected = false;
        private bool _shouldRun = false;
        private int _lastUnreadCount = -1;
        private string _lastNotificationId = null;

        public event Action<int> UnreadCountChanged;

        private SocketService()
        {
        }

        public void Connect()
        {
            if (!ApiService.Instance.IsLoggedIn) return;

            _shouldRun = true;
            _token = ApiService.Instance.Token;

            if (_isConnected || _isConnecting) return;

            var task = ConnectAsync();
        }

        public void Disconnect()
        {
            _shouldRun = false;
            CloseSocket();
            _lastUnreadCount = -1;
            _lastNotificationId = null;
        }

        private void CloseSocket()
        {
            _isConnected = false;
            _isConnecting = false;
            if (_webSocket != null)
            {
                try
                {
                    _webSocket.Closed -= WebSocket_Closed;
                    _webSocket.MessageReceived -= WebSocket_MessageReceived;
                    _webSocket.Dispose();
                }
                catch { }
                _webSocket = null;
            }
        }

        private async Task ConnectAsync()
        {
            if (_isConnecting) return;
            _isConnecting = true;

            try
            {
                CloseSocket();

                _webSocket = new MessageWebSocket();
                _webSocket.Control.MessageType = SocketMessageType.Utf8;
                _webSocket.MessageReceived += WebSocket_MessageReceived;
                _webSocket.Closed += WebSocket_Closed;

                // Set headers to pass Cloudflare and origin verification
                _webSocket.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 6.3; ARM; Trident/7.0; Touch; rv:11.0; WPDesktop; Lumia 930) like Gecko");
                _webSocket.SetRequestHeader("Origin", "https://wasteof.money");

                var uri = new Uri("wss://api.wasteof.money/socket.io/?transport=websocket&EIO=4&token=" + Uri.EscapeDataString(_token));
                await _webSocket.ConnectAsync(uri);

                _isConnected = true;
                _isConnecting = false;
            }
            catch (Exception)
            {
                _isConnecting = false;
                _isConnected = false;
                
                // Retry connection if we should still run
                if (_shouldRun)
                {
                    await Task.Delay(5000);
                    if (_shouldRun) Connect();
                }
            }
        }

        private async Task SendMessageAsync(string message)
        {
            if (_webSocket == null || !_isConnected) return;

            try
            {
                using (var dataWriter = new DataWriter(_webSocket.OutputStream))
                {
                    dataWriter.WriteString(message);
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                }
            }
            catch { }
        }

        private async void WebSocket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            _isConnected = false;
            _isConnecting = false;

            if (_shouldRun)
            {
                await Task.Delay(5000);
                if (_shouldRun) Connect();
            }
        }

        private async void WebSocket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (Windows.Storage.Streams.DataReader reader = args.GetDataReader())
                {
                    reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                    string message = reader.ReadString(reader.UnconsumedBufferLength);

                    if (string.IsNullOrEmpty(message)) return;

                    // Handle Engine.io open packet (starts with 0)
                    if (message.StartsWith("0"))
                    {
                        var authPayload = new JObject();
                        authPayload["token"] = _token;
                        string connectPacket = "40" + authPayload.ToString(Formatting.None);
                        var task = Task.Run(async () => await SendMessageAsync(connectPacket));
                        return;
                    }

                    // Handle Engine.io ping (2) -> pong (3)
                    if (message == "2")
                    {
                        var task = Task.Run(async () => await SendMessageAsync("3"));
                        return;
                    }

                    // Handle Socket.io Event packet (starts with 42)
                    if (message.StartsWith("42"))
                    {
                        string eventJson = message.Substring(2);
                        var array = JsonConvert.DeserializeObject<JArray>(eventJson);
                        if (array != null && array.Count >= 2)
                        {
                            string eventName = array[0].ToString();
                            if (eventName == "updateMessageCount")
                            {
                                int unreadCount = 0;
                                int.TryParse(array[1].ToString(), out unreadCount);

                                await Windows.ApplicationModel.Core.CoreApplication.MainView.Dispatcher.RunAsync(
                                    Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                    {
                                        HandleUnreadCountUpdate(unreadCount);
                                    });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private async void HandleUnreadCountUpdate(int unreadCount)
        {
            if (UnreadCountChanged != null)
            {
                UnreadCountChanged(unreadCount);
            }

            if (unreadCount > 0)
            {
                try
                {
                    var notifs = await ApiService.Instance.GetNotificationsAsync(true, 1);
                    if (notifs != null && notifs.Count > 0)
                    {
                        var latest = notifs[0];
                        if (_lastUnreadCount != -1 && unreadCount > _lastUnreadCount && latest.Id != _lastNotificationId)
                        {
                            _lastNotificationId = latest.Id;
                            string title = latest.NotificationText;
                            string content = !string.IsNullOrEmpty(latest.NotificationContent) 
                                ? latest.NotificationContent 
                                : "You have new unread messages.";
                            SendToastNotification(title, content);
                        }
                    }
                }
                catch { }
            }

            _lastUnreadCount = unreadCount;

            // Update live tile and badge in background
            var task = UpdateTileAndBadgeAsync(unreadCount);
        }

        private void SendToastNotification(string title, string content)
        {
            try
            {
                var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
                var textNodes = toastXml.GetElementsByTagName("text");
                textNodes[0].AppendChild(toastXml.CreateTextNode(title));
                textNodes[1].AppendChild(toastXml.CreateTextNode(content));

                var toast = new ToastNotification(toastXml);
                ToastNotificationManager.CreateToastNotifier().Show(toast);
            }
            catch { }
        }

        private async Task<List<string>> DownloadTileImagesAsync(List<string> remoteUrls)
        {
            var localPaths = new List<string>();
            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;

            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; ARM; Trident/7.0; Touch; rv:11.0; WPDesktop; Lumia 930) like Gecko");
                
                for (int i = 0; i < remoteUrls.Count; i++)
                {
                    if (i >= 5) break;

                    try
                    {
                        var bytes = await client.GetByteArrayAsync(remoteUrls[i]);
                        string filename = "tile_avatar_" + i + ".jpg";
                        var file = await folder.CreateFileAsync(filename, Windows.Storage.CreationCollisionOption.ReplaceExisting);
                        await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);
                        localPaths.Add("ms-appdata:///local/" + filename);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return localPaths;
        }

        public async Task UpdateTileAndBadgeAsync(int unreadCount)
        {
            try
            {
                // Update badge number natively on the tile
                var badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
                var badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
                badgeElement.SetAttribute("value", unreadCount.ToString());
                BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(new BadgeNotification(badgeXml));

                if (unreadCount > 0)
                {
                    // Fetch latest unread notifications to extract actors' profile pictures
                    var notifs = await ApiService.Instance.GetNotificationsAsync(true, 1);
                    var pics = new List<string>();
                    if (notifs != null)
                    {
                        foreach (var n in notifs)
                        {
                            if (n != null && n.Data != null && n.Data.Actor != null && !string.IsNullOrEmpty(n.Data.Actor.ProfilePictureUrl))
                            {
                                string url = n.Data.Actor.ProfilePictureUrl;
                                if (!pics.Contains(url))
                                {
                                    pics.Add(url);
                                }
                            }
                        }
                    }

                    if (pics.Count > 0)
                    {
                        var localPaths = await DownloadTileImagesAsync(pics);
                        if (localPaths.Count > 0)
                        {
                            // Build a peek tile showing sender avatar and unread count!
                            string wideXml = $@"
<tile>
  <visual version=""2"">
    <binding template=""TileSquare150x150PeekImageAndText04"" fallback=""TileSquarePeekImageAndText04"">
      <image id=""1"" src=""{localPaths[0]}"" />
      <text id=""1"">{unreadCount} unread</text>
    </binding>
    <binding template=""TileWide310x150PeekImageAndText01"" fallback=""TileWidePeekImageAndText01"">
      <image id=""1"" src=""{localPaths[0]}"" />
      <text id=""1"">{unreadCount} unread messages</text>
      <text id=""2"">wasteof.phone</text>
      <text id=""3"">tap to read</text>
    </binding>
  </visual>
</tile>";
                            var tileXml = new XmlDocument();
                            tileXml.LoadXml(wideXml);
                            TileUpdateManager.CreateTileUpdaterForApplication().Update(new TileNotification(tileXml));
                            return;
                        }
                    }
                }

                // Fallback (or if count is 0) to standard app logos
                string textXml = $@"
<tile>
  <visual version=""2"">
    <binding template=""TileSquare150x150Image"">
      <image id=""1"" src=""ms-appx:///Assets/Logo.png"" />
    </binding>
    <binding template=""TileWide310x150Image"">
      <image id=""1"" src=""ms-appx:///Assets/WideLogo.png"" />
    </binding>
  </visual>
</tile>";
                var fallbackXml = new XmlDocument();
                fallbackXml.LoadXml(textXml);
                TileUpdateManager.CreateTileUpdaterForApplication().Update(new TileNotification(fallbackXml));
            }
            catch { }
        }
    }
}
