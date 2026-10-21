using System;
using System.Collections.Generic;

namespace NacleFoto.Core.Models
{
    public enum WeddingState
    {
        /// <summary>Selection in progress: can select, deselect, add photos.</summary>
        Open = 0,
        /// <summary>Selection frozen. The send package has been generated.</summary>
        Closed = 1,
        /// <summary>Sent to the print lab.</summary>
        Sent = 2
    }

    /// <summary>
    /// A selected photo within a wedding.
    /// Identity is (relative path, size, modification date) to survive
    /// drive letter changes (USB flash drive E: -> F:) and detect modified files.
    /// </summary>
    public sealed class SelectionItem
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public long ModifiedUtcTicks { get; set; }
        /// <summary>0 = unrated, 1..5 stars.</summary>
        public int Rating { get; set; }
        /// <summary>Color label: green, yellow, red, blue, purple.</summary>
        public string Color { get; set; }

        public DateTime ModifiedUtc
        {
            get { return new DateTime(ModifiedUtcTicks, DateTimeKind.Utc); }
            set { ModifiedUtcTicks = value.Ticks; }
        }
    }

    /// <summary>Part of a send package (a ZIP within the size limit).</summary>
    public sealed class PackagePart
    {
        public string FilePath { get; set; }
        public long Bytes { get; set; }
        public int Part { get; set; }
        public int TotalParts { get; set; }
        public int FileCount { get; set; }
    }

    /// <summary>
    /// The central unit of the app: a job (in practice, a wedding).
    /// Can contain multiple folders and subfolders from the same drive or different drives.
    /// </summary>
    public sealed class Wedding
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Client { get; set; }
        public string Type { get; set; }
        public DateTime? EventDate { get; set; }
        public string Notes { get; set; }

        /// <summary>Absolute reference root folder (drive letter may change; informational only).</summary>
        public string RootFolder { get; set; }
        /// <summary>Additional folders included (absolute) when the selection spans multiple drives.</summary>
        public List<string> ExtraFolders { get; set; } = new List<string>();

        /// <summary>Subfolders excluded from indexing (path relative to RootFolder).</summary>
        public List<string> ExcludedFolders { get; set; } = new List<string>();

        public List<SelectionItem> Selection { get; set; } = new List<SelectionItem>();

        public WeddingState State { get; set; } = WeddingState.Open;
        public DateTime? ClosedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public string SendChannel { get; set; }
        public string SentLink { get; set; }

        public List<PackagePart> Packages { get; set; } = new List<PackagePart>();
        public string PackageFolder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public bool IsClosed
        {
            get { return State != WeddingState.Open; }
        }

        public int TotalSelected
        {
            get { return Selection == null ? 0 : Selection.Count; }
        }

        public long SelectedSize
        {
            get
            {
                long total = 0;
                if (Selection != null)
                    foreach (var s in Selection) total += s.Size;
                return total;
            }
        }

        public Wedding Clone()
        {
            return new Wedding
            {
                Id = Id,
                Name = Name,
                Client = Client,
                Type = Type,
                EventDate = EventDate,
                Notes = Notes,
                RootFolder = RootFolder,
                ExtraFolders = new List<string>(ExtraFolders ?? new List<string>()),
                ExcludedFolders = new List<string>(ExcludedFolders ?? new List<string>()),
                Selection = new List<SelectionItem>(Selection ?? new List<SelectionItem>()),
                State = State,
                ClosedAt = ClosedAt,
                SentAt = SentAt,
                SendChannel = SendChannel,
                SentLink = SentLink,
                Packages = new List<PackagePart>(Packages ?? new List<PackagePart>()),
                PackageFolder = PackageFolder,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
        }
    }
}
