using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

namespace NacleFoto.Core.Linking
{
    /// <summary>
    /// File upload to sendit.sh (free service, no login).
    /// The response comes in text, with the line "wget &lt;link&gt;".
    ///
    /// CAUTION: the service retains the file for 1 DAY and allows 5 downloads.
    /// Useful for quick link transfers, not as permanent storage.
    /// </summary>
    public sealed class SenditClient
    {
        private const string BaseUrl = "https://sendit.sh/";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromHours(2) };

        /// <summary>How many days the file remains available on the service.</summary>
        public const int RetentionDays = 1;

        /// <summary>
        /// Uploads the file and returns the download link.
        /// </summary>
        public string UploadFile(string localPath, string fileName,
                                IProgress<double> progress = null,
                                CancellationToken cancel = default(CancellationToken))
        {
            if (!File.Exists(localPath)) throw new FileNotFoundException(localPath);

            var url = BaseUrl + Uri.EscapeDataString(fileName ?? Path.GetFileName(localPath));

            using (var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan))
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                var content = new StreamContent(fs);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                request.Content = content;

                using (var response = Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).GetAwaiter().GetResult())
                {
                    // DO NOT use ReadAsStringAsync: the service responds with "charset=utf8" (without hyphen)
                    // and .NET Framework does not recognize this encoding name
                    var raw = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    var body = Encoding.UTF8.GetString(raw);
                    if (!response.IsSuccessStatusCode)
                        throw new IOException("Upload failed (" + (int)response.StatusCode + "): " + body);

                    var link = ExtractUrl(body);
                    if (string.IsNullOrEmpty(link))
                        throw new IOException("O servico nao devolveu o link: " + body);
                    return link;
                }
            }
        }

        /// <summary>
        /// Extracts the link from the service response. The response looks like:
        /// "Uploaded 1 file, 30 bytes\n\nwget https://sendit.sh/xxxx/nome.jpg\n"
        /// </summary>
        public static string ExtractUrl(string response)
        {
            if (string.IsNullOrEmpty(response)) return null;

            foreach (var line in response.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("wget ", StringComparison.OrdinalIgnoreCase))
                    return t.Substring(5).Trim();
                if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            return null;
        }
    }
}
