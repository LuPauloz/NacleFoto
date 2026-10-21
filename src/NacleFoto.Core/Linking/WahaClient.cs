using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NacleFoto.Core.Linking
{
    /// <summary>
    /// WAHA (WhatsApp HTTP API) client. The app sends photos directly to the
    /// WAHA server, which handles the WhatsApp session (QR code once, stays logged
    /// in thereafter). Without touching the WhatsApp Web DOM: stable API.
    ///
    /// WAHA runs wherever you want (Docker or Node). The app only needs the address.
    /// </summary>
    public sealed class WahaClient
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _session;
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

        public WahaClient(string baseUrl, string apiKey, string session)
        {
            _baseUrl = (baseUrl ?? "").Trim().TrimEnd('/');
            _apiKey = apiKey ?? "";
            _session = string.IsNullOrWhiteSpace(session) ? "default" : session.Trim();
        }

        public string Session { get { return _session; } }
        public string BaseUrl { get { return _baseUrl; } }
        public bool IsConfigured { get { return _baseUrl.Length > 0; } }

        private string Url(string path) { return _baseUrl + path; }

        private void Prepare(HttpRequestMessage req)
        {
            if (!string.IsNullOrEmpty(_apiKey))
                req.Headers.TryAddWithoutValidation("X-Api-Key", _apiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>Is the WAHA server up and running?</summary>
        public bool IsRunning()
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, Url("/api/sessions"));
                Prepare(req);
                var resp = Http.SendAsync(req).GetAwaiter().GetResult();
                return resp.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Does the server respond? (even with 401, i.e., it is running,
        /// but with another API key - the app cannot talk to it).
        /// </summary>
        public bool IsServerUp()
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, Url("/api/sessions"));
                Prepare(req);
                var resp = Http.SendAsync(req).GetAwaiter().GetResult();
                return resp.StatusCode != System.Net.HttpStatusCode.BadGateway &&
                       (int)resp.StatusCode < 500;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Enables session storage (NOWEB).
        ///
        /// Without this WAHA keeps everything in memory: pairing is lost when
        /// the server restarts and the app cannot list chats.
        ///
        /// Applying the configuration restarts the session, so it is only done
        /// when not yet enabled (and, with store enabled, pairing survives
        /// this restart).
        /// </summary>
        public bool EnsureStore()
        {
            try
            {
                // already enabled? do not touch the session
                if (IsStoreEnabled()) return true;

                var req = new HttpRequestMessage(HttpMethod.Put, Url("/api/sessions/" + _session));
                Prepare(req);
                req.Content = new StringContent(
                    "{\"config\":{\"noweb\":{\"store\":{\"enabled\":true,\"fullSync\":true}}}}",
                    Encoding.UTF8, "application/json");
                var resp = Http.SendAsync(req).GetAwaiter().GetResult();
                return resp.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Is session storage already enabled?</summary>
        public bool IsStoreEnabled()
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, Url("/api/sessions/" + _session));
                Prepare(req);
                var resp = Http.SendAsync(req).GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode) return false;

                var json = JObject.Parse(resp.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                var config = json["config"] as JObject;
                if (config == null) return false;
                var noweb = config["noweb"] as JObject;
                if (noweb == null) return false;
                var store = noweb["store"] as JObject;
                if (store == null) return false;
                return (bool?)store["enabled"] == true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Chats the account has on WhatsApp (as chat ids).
        ///
        /// Used by the app to find out if the destination number really exists:
        /// if the account never talked to the number, it is very likely
        /// wrong (and photos would be lost into the void).
        /// </summary>
        public List<string> GetChatIds()
        {
            var list = new List<string>();
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, Url("/api/" + _session + "/chats/overview?page=1&size=200"));
                Prepare(req);
                var resp = Http.SendAsync(req).GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode) return list;

                var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (string.IsNullOrEmpty(text) || text.TrimStart()[0] != '[') return list;

                var items = JArray.Parse(text);
                foreach (var item in items)
                {
                    var id = (string)item["id"];
                    if (!string.IsNullOrEmpty(id)) list.Add(id);
                }
            }
            catch (Exception) { }
            return list;
        }

        /// <summary>
        /// Has the account chatted with this number before?
        /// (null = unable to determine; NOWEB engine only responds after store)
        /// </summary>
        public bool? KnowsNumber(string chatId)
        {
            if (string.IsNullOrEmpty(chatId)) return null;

            var chats = GetChatIds();
            if (chats.Count == 0) return null; // without store unable to determine

            // WAHA returns id as "@c.us" or "@s.whatsapp.net"
            var digits = new string(chatId.Where(char.IsDigit).ToArray());
            foreach (var chat in chats)
            {
                var chatDigits = new string(chat.Where(char.IsDigit).ToArray());
                if (chatDigits == digits) return true;
            }
            return false;
        }

        /// <summary>Session status: WORKING, SCAN_QR_CODE, STARTING, FAILED, STOPPED...</summary>
        public string GetSessionStatus()
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, Url("/api/sessions/" + _session));
                Prepare(req);
                var resp = Http.SendAsync(req).GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode) return null;
                var json = JObject.Parse(resp.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                return (string)json["status"];
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Ensures the session exists AND is running.
        ///
        /// Without this WAHA returns 404 for QR code: session created via
        /// POST /api/sessions remains STOPPED, and only after POST /api/sessions/{name}/start
        /// does it transition to SCAN_QR_CODE (and generate the QR).
        /// </summary>
        public bool EnsureSession()
        {
            if (string.IsNullOrEmpty(_session)) return false;

            var status = GetSessionStatus();
            if (status == null)
            {
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, Url("/api/sessions"));
                    Prepare(req);
                    req.Content = new StringContent(
                        "{\"name\":\"" + _session + "\",\"config\":{\"client\":{\"deviceName\":\"Nacle Foto\",\"browserName\":\"Chrome\"}}}",
                        Encoding.UTF8, "application/json");
                    var resp = Http.SendAsync(req).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode) return false;
                }
                catch (Exception)
                {
                    return false;
                }
                status = "STOPPED";
            }

            // already paired (or pairing)? do not touch
            if (status == "WORKING" || status == "SCAN_QR_CODE" || status == "STARTING") return true;

            // hung? restart is the way to recover (start does not help)
            if (status == "FAILED") return RestartSession();

            // stopped (STOPPED): needs to be started
            return StartSession();
        }

        /// <summary>Starts the session (required for QR code to appear).</summary>
        public bool StartSession()
        {
            return CallSessionAction("start");
        }

        /// <summary>Restarts the session (required when it gets stuck/FAILED).</summary>
        public bool RestartSession()
        {
            return CallSessionAction("restart");
        }

        private bool CallSessionAction(string action)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, Url("/api/sessions/" + _session + "/" + action));
                Prepare(req);
                var resp = Http.SendAsync(req).GetAwaiter().GetResult();
                return resp.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>PNG of the QR code for the app to display (session must be paired).</summary>
        public byte[] GetQrCodePng()
        {
            // session must exist and be running beforehand (and WAHA requests a new QR on each refresh)
            EnsureSession();

            // actual WAHA route: GET /api/{session}/auth/qr
            // with Accept: application/json it returns { mimetype, data(base64) }
            string[] paths = {
                "/api/" + _session + "/auth/qr",
                "/api/sessions/" + _session + "/auth/qr"
            };

            // WhatsApp only provides ~3 QR codes before session drops
            // ("QR refs attempts ended"): retries multiple times and the app refreshes
            for (int attempt = 0; attempt < 5; attempt++)
            {
                foreach (var path in paths)
                {
                    var png = TryGetQr(path);
                    if (png != null) return png;
                }
                Thread.Sleep(3000);
            }
            return null;
        }

        private byte[] TryGetQr(string path)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, Url(path));
                Prepare(req);
                var resp = Http.SendAsync(req).GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode) return null;

                // JSON response: { mimetype: "image/png", data: "<base64>" }
                var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!text.TrimStart().StartsWith("{"))
                {
                    // raw response (binary PNG)
                    var bin = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    if (bin != null && bin.Length > 4 && bin[0] == 0x89 && bin[1] == 0x50) return bin;
                    return null;
                }

                var obj = JObject.Parse(text);
                var data = (string)obj["data"];
                if (string.IsNullOrEmpty(data)) return null;
                return Convert.FromBase64String(data);
            }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// Sends a file (original photo, uncompressed: maximum quality).
        /// </summary>
        public void SendFile(string chatId, string filePath, string caption = null)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);

            var bytes = File.ReadAllBytes(filePath);
            var fileName = Path.GetFileName(filePath);
            var mime = Mime(filePath);

            var payload = new JObject
            {
                ["session"] = _session,
                ["chatId"] = chatId,
                ["file"] = new JObject
                {
                    ["mimetype"] = mime,
                    ["filename"] = fileName,
                    ["data"] = Convert.ToBase64String(bytes)
                }
            };
            if (!string.IsNullOrEmpty(caption)) payload["caption"] = caption;

            // actual WAHA route: POST /api/sendFile (session goes in the body).
            // the /api/sessions/{session}/sendFile variant exists in other versions.
            PostPayload("/api/sendFile", payload, "/api/sessions/" + _session + "/sendFile");
        }

        public void SendText(string chatId, string text)
        {
            var payload = new JObject
            {
                ["session"] = _session,
                ["chatId"] = chatId,
                ["text"] = text ?? ""
            };
            PostPayload("/api/sendText", payload, "/api/sessions/" + _session + "/sendText");
        }

        private void PostPayload(string newPath, JObject payload, string legacyPath)
        {
            Exception lastEx = null;
            foreach (var path in new[] { newPath, legacyPath })
            {
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, Url(path));
                    Prepare(req);
                    req.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    var resp = Http.SendAsync(req).GetAwaiter().GetResult();
                    var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (resp.IsSuccessStatusCode) return;
                    lastEx = new IOException("WAHA respondeu " + (int)resp.StatusCode + ": " + body);
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
            }
            throw lastEx ?? new IOException("Nao foi possivel falar com o servidor WAHA.");
        }

        /// <summary>WAHA chatId for a phone number: 5511999999999@c.us</summary>
        public static string BuildChatId(string phone)
        {
            var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return null;
            if (digits.StartsWith("00")) digits = digits.Substring(2);
            return digits + "@c.us";
        }

        private static string Mime(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".heic": return "image/heic";
                case ".webp": return "image/webp";
                case ".pdf": return "application/pdf";
                case ".zip": return "application/zip";
                default: return "application/octet-stream";
            }
        }
    }
}
