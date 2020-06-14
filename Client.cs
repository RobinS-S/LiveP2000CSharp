using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using System.Threading.Tasks;
using WatsonWebsocket;

namespace LiveP2000CSharp
{
    public class Client
    {
        private WatsonWsClient _client;
        private DateTime _lastServerMessage = DateTime.MinValue;
        private Timer _pingTimer;
        private bool _unlocked;

        public delegate void P2000PagesReceivedHandler(P2000Alert[] alerts);
        public event P2000PagesReceivedHandler OnReceived;
        public event Action OnConnect;
        public event Action OnDisconnect;

        public Client()
        {
        }

        public void Connect()
        {
            _client = new WatsonWsClient(new Uri("wss://www.livep2000.nl/LSM/websocket")); // open connection to websocket
            _client.MessageReceived += MessageReceived;
            _client.ServerConnected += ServerConnected;
            _client.ServerDisconnected += ServerDisconnected;
            if (!_client.Connected) _client.Start();
            else Console.WriteLine("wtf");
        }

        private void ServerDisconnected(object sender, EventArgs e)
        {
            if (_client != null)
            {
                _client.MessageReceived -= MessageReceived;
                _client.ServerConnected -= ServerConnected;
                _client.ServerDisconnected -= ServerDisconnected;
            }
            if (_pingTimer != null && _pingTimer.Enabled)
            {
                _pingTimer.Stop();
                _pingTimer.Dispose();
            }
            _lastServerMessage = DateTime.MinValue;
            _unlocked = false;
            _client.Dispose();
            OnDisconnect?.Invoke();
        }

        private async void ServerConnected(object sender, EventArgs e)
        {
            _pingTimer = new Timer(1000);
            _pingTimer.Elapsed += Ping;
            await Send(LiveP2000PacketType.AUTq, new JObject { ["TYP"] = "ANN", ["UID"] = "0", ["COO"] = await GetToken() }); // send server hello
            OnConnect?.Invoke();
        }

        private async void Ping(object sender, ElapsedEventArgs e)
        {
            if (_lastServerMessage != DateTime.MinValue && _lastServerMessage < DateTime.Now.Subtract(new TimeSpan(0, 5, 0)))
            {
                if (_client.Connected) await Send(LiveP2000PacketType.PINq, new JObject());
            }
        }

        private async void MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            var data = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(e.Data));
            _lastServerMessage = DateTime.Now;
            if (data.GetType() == typeof(JObject)) // Protocol command
            {
                switch ((LiveP2000PacketType)((JObject)data)["COM"].Value<int>())
                {
                    case LiveP2000PacketType.AUTr: // handle auth response
                        await Send(LiveP2000PacketType.DATq, new JObject() { ["FRQ"] = true });
                        break;
                    case LiveP2000PacketType.STAc: // unlock command
                        //Console.WriteLine("Starting to receive live P2000 reports.");
                        _unlocked = true;
                        break;
                    case LiveP2000PacketType.PINr:
                        //Console.WriteLine("PING REPLY");
                        break;
                    default:
                        //Console.WriteLine("[" + (LiveP2000PacketType)((JObject)data)["COM"].Value<int>() + "] Message from server: " + Encoding.UTF8.GetString(e.Data));
                        break;
                }
            }
            else // Else we've got reports
            {
                List<P2000Alert> alerts = new List<P2000Alert>();
                foreach (JObject obj in (JArray)data) alerts.Add(ConvertAlert(obj));
                if (alerts.Count > 0 && _unlocked) OnReceived?.Invoke(alerts.ToArray());
            }
        }

        private async Task Send(LiveP2000PacketType cmd, JObject data)
        {
            data["COM"] = (int)cmd;
            await _client.SendAsync(JsonConvert.SerializeObject(data));
        }

        private P2000Alert ConvertAlert(JObject obj)
        {
            P2000Alert alert = new P2000Alert();
            alert.Latitude = obj["LAT"].Type != JTokenType.Null ? obj.Value<double>("LAT") : 0.0;
            alert.Longitude = obj["LON"].Type != JTokenType.Null ? obj.Value<double>("LON") : 0.0;
            alert.Service = (P2000ServiceType)obj.Value<int>("DII");
            string spi = obj["SPI"].Value<string>();
            alert.Time = new DateTime(2000 + int.Parse(spi.Substring(0, 2)), int.Parse(spi.Substring(2, 2)), int.Parse(spi.Substring(4, 2)), int.Parse(spi.Substring(6, 2)), int.Parse(spi.Substring(8, 2)), int.Parse(spi.Substring(10, 2)), DateTimeKind.Local); // it's noted in the digits, decimal style
            int dii = int.Parse(spi.Substring(12, 2)); // origin code
            int rii = int.Parse(spi.Substring(14, 2)); // region code
            if (dii == 28) alert.Service = P2000ServiceType.AmbulanceHelicopter; // trauma helicopter
            if (obj.ContainsKey("capcodes"))
            {
                List<P2000Capcode> capcodes = new List<P2000Capcode>();
                HtmlDocument capdoc = new HtmlDocument();
                foreach (var cap in (JArray)obj["capcodes"])
                {
                    capdoc.LoadHtml(cap.Value<string>("CTT"));
                    capcodes.Add(new P2000Capcode() { Capcode = int.Parse(cap.Value<string>("CPI")), UnitName = string.Join(" ", capdoc.DocumentNode.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Text || (n.NodeType == HtmlNodeType.Element && n.HasClass("wb"))).Select(n => n.InnerText)) });
                }
                alert.Capcodes = capcodes.ToArray();
            }
            // is priority?
            if (obj.ContainsKey("TXT") && !string.IsNullOrWhiteSpace(obj.Value<string>("TXT")))
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(obj.Value<string>("TXT"));
                Dictionary<string, string> pairs = new Dictionary<string, string>();

                foreach (var el in doc.DocumentNode.Descendants())
                {
                    if (el.GetClasses().Count() > 0)
                    {
                        string className = el.GetClasses().First();
                        if (pairs.ContainsKey(className)) continue;
                        pairs.Add(className, el.InnerText);
                    }
                }
                if (pairs.ContainsKey("c")) alert.City = pairs["c"];
                if (pairs.ContainsKey("s")) alert.Street = pairs["s"];

                alert.Message = string.Join(" ", doc.DocumentNode.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Text || (n.NodeType == HtmlNodeType.Element && n.HasClass("wb"))).Select(n => n.InnerText)); // do not include unnecessary text that's described in other properties
                try
                {
                    Match postcode = Regex.Match(obj.Value<string>("TXT"), @"[1-9][0-9]{3}(?!SA|SD|SS)[A-Z]{2}"); // regex for dutch postal codes
                    if (postcode.Success) alert.PostalCode = postcode.Value;
                }
                catch (Exception) { }

                // extra clean up message
                if (alert.City != null && alert.Message.Contains(alert.City)) alert.Message = alert.Message.Replace(alert.City, "");
                if (alert.Street != null && alert.Message.Contains(alert.Street)) alert.Message = alert.Message.Replace(alert.Street, "");
                if (alert.PostalCode != null && alert.Message.Contains(alert.PostalCode)) alert.Message = alert.Message.Replace(alert.PostalCode, "");
                alert.Message = Regex.Replace(alert.Message, @"(\s)\s+", "$1");
                if (!string.IsNullOrWhiteSpace(alert.Message))
                {
                    if (Regex.Matches(alert.Message, @"/\bgrip\b|\bvos\b|\bopschaling\b/gi").Count > 0) alert.IsPriority = true;
                    else if (Regex.Matches(alert.Message, @"/middel|grote|groot|peleton|zeer grote/gi").Count > 0 && Regex.Matches(alert.Message, "/brand|waterongeval|duik/gi").Count > 0) alert.IsPriority = true;
                }
            }
            return alert;
        }

        private async Task<string> GetToken()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://www.livep2000.nl/monitor/");
            request.UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 13_4_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/13.1 Mobile/15E148 Safari/604.1";
            request.Method = WebRequestMethods.Http.Get;
            request.Timeout = 10000;
            request.Proxy = null;
            WebResponse response = await request.GetResponseAsync();
            string cookie = response.Headers[HttpResponseHeader.SetCookie];
            return WebUtility.UrlDecode(cookie.Substring(13, cookie.IndexOf(';') - 13)).Split(':')[1]; // Get the value after the cookie name, split that and get the required token
        }
    }
}
