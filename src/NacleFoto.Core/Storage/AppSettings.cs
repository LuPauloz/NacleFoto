using System;
using System.IO;
using Newtonsoft.Json;
using NacleFoto.Core.Localization;

namespace NacleFoto.Core.Storage
{
    /// <summary>How delivery is performed.</summary>
    public enum SendFormat
    {
        /// <summary>Compresses into .zip (the print lab accepts zip).</summary>
        Zip = 0,
        /// <summary>Leaves photos loose to send directly via WhatsApp.</summary>
        Loose = 1,
        /// <summary>Prepares both.</summary>
        Both = 2
    }

    /// <summary>App preferences. Stored in %LOCALAPPDATA%\NacleFoto\settings.json</summary>
    public sealed class AppSettings
    {
        /// <summary>
        /// Language of the interface: "auto" (follow the operating system),
        /// "pt" or "en".
        /// </summary>
        public string Language { get; set; } = "auto";

        /// <summary>
        /// Print lab phone number, digits only with country and area code (e.g. 5511999999999).
        /// Empty by default: user fills in Settings and it stays saved.
        /// Used to open the WhatsApp conversation pre-filled with the message.
        /// </summary>
        public string WhatsAppPhone { get; set; } = "";

        /// <summary>Delivery format. Default: loose (as done previously).</summary>
        public string SendFormatName { get; set; } = "loose";

        /// <summary>WhatsApp Web limit: 100 MB per file. Safety margin.</summary>
        public int PartSizeMb { get; set; } = 95;

        /// <summary>Folder where delivery packages are generated.</summary>
        public string DestinationFolder { get; set; }

        /// <summary>
        /// Message template used in WhatsApp. Empty by default: parents just want to send
        /// the photos. {wedding} and {parts} are replaced.
        /// </summary>
        public string MessageTemplate { get; set; } = "";

        /// <summary>Use download link for delivery.</summary>
        public bool UseDownloadLink { get; set; } = false;

        /// <summary>
        /// Download link provider: none | sendit | folder.
        /// - none: no link (default). Delivery is via WhatsApp, as usual.
        /// - sendit: sendit.sh (free, no login; links expire in 1 day)
        /// - s3: any S3-compatible storage (R2, B2, MinIO)
        /// - folder: copies to a synced folder and the user shares it (zero credentials)
        /// </summary>
        public string LinkProvider { get; set; } = "sendit";

        /// <summary>Synced folder (Drive/Dropbox/OneDrive) for "folder" mode.</summary>
        public string SyncFolder { get; set; } = "";

        /// <summary>Validity of generated link, in hours.</summary>
        public int LinkExpiresHours { get; set; } = 72;

        /// <summary>
        /// How many photos the app sends per message in automated WhatsApp delivery.
        /// 99% of weddings have fewer than this.
        /// </summary>
        public int WhatsAppBatchSize { get; set; } = 50;

        // ---------- WAHA (WhatsApp HTTP API) ----------

        /// <summary>WAHA server address (e.g. http://localhost:3000).</summary>
        public string WahaUrl { get; set; } = "http://localhost:3000";

        /// <summary>WAHA API key (X-Api-Key).</summary>
        public string WahaApiKey { get; set; } = "";

        /// <summary>WhatsApp session name inside WAHA.</summary>
        public string WahaSession { get; set; } = "default";

        /// <summary>Start WAHA automatically when app opens.</summary>
        public bool WahaAutoStart { get; set; } = true;

        /// <summary>Command to start WAHA (full command line).</summary>
        public string WahaStartCommand { get; set; } = "";

        /// <summary>
        /// Include photos that are still "in the cloud" (OneDrive). When enabled, app waits
        /// for each file to download and slows down (and may freeze the machine).
        /// </summary>
        public bool IncludeCloudFiles { get; set; } = false;

        /// <summary>
        /// After how many days old selections are deleted from the list.
        /// 0 = never delete. Photos on disk are never touched.
        /// </summary>
        public int RetentionDays { get; set; } = 365;

        /// <summary>Maximum thumbnail edge in px.</summary>
        public int ThumbnailSize { get; set; } = 256;

        /// <summary>
        /// The language actually in use. With "auto" it follows the operating
        /// system: Portuguese on a Portuguese machine, English anywhere else.
        /// </summary>
        [JsonIgnore]
        public AppLanguage AppLanguage
        {
            get
            {
                if (Language == "en") return AppLanguage.English;
                if (Language == "pt") return AppLanguage.Portuguese;
                return NacleFoto.Core.Localization.Strings.DetectSystemLanguage();
            }
            set { Language = value == AppLanguage.English ? "en" : "pt"; }
        }

        public SendFormat Format
        {
            get
            {
                SendFormat f;
                // case-insensitive: saved value might be "loose" or "Loose"
                return Enum.TryParse(SendFormatName, true, out f) ? f : SendFormat.Loose;
            }
            set { SendFormatName = value.ToString(); }
        }

        public static AppSettings Load(string appDataDir)
        {
            var file = Path.Combine(appDataDir, "settings.json");
            if (!File.Exists(file)) return CreateDefault(appDataDir);
            try
            {
                var s = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(file));
                return s ?? CreateDefault(appDataDir);
            }
            catch (Exception)
            {
                return CreateDefault(appDataDir);
            }
        }

        public static AppSettings CreateDefault(string appDataDir)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            return new AppSettings
            {
                DestinationFolder = Path.Combine(desktop, "Envios")
            };
        }

        public void Save(string appDataDir)
        {
            ProjectStore.AtomicWrite(Path.Combine(appDataDir, "settings.json"),
                                     JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
