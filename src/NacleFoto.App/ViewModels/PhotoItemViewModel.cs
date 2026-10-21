using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using NacleFoto.Core.Models;

namespace NacleFoto.App.ViewModels
{
    /// <summary>A photo in the grid. Represents the same file on disk.</summary>
    public sealed class PhotoItemViewModel : ObservableBase
    {
        private BitmapSource _thumb;
        private bool _isSelected;
        private int _rating;
        private string _color;
        private PhotoMetadata _meta;

        public PhotoItemViewModel(string relativePath, string fullPath, long size, long mtimeTicks)
        {
            RelativePath = relativePath;
            FullPath = fullPath;
            Size = size;
            ModifiedUtcTicks = mtimeTicks;
        }

        public string RelativePath { get; private set; }
        public string FullPath { get; set; }
        public long Size { get; set; }
        public long ModifiedUtcTicks { get; set; }

        public string Name
        {
            get { try { return Path.GetFileName(FullPath); } catch { return RelativePath; } }
        }

        public BitmapSource Thumb
        {
            get { return _thumb; }
            set
            {
                _thumb = value;
                Raise("Thumb");
                Raise("HasThumbnail");
                Raise("LoadingThumbnail");
                Raise("Badge");
            }
        }

        public bool HasThumbnail
        {
            get { return _thumb != null; }
        }

        public bool LoadingThumbnail
        {
            get { return _thumb == null; }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { if (_isSelected == value) return; _isSelected = value; Raise("IsSelected"); }
        }

        public int Rating
        {
            get { return _rating; }
            set { if (_rating == value) return; _rating = value; Raise("Rating"); }
        }

        public string Color
        {
            get { return _color; }
            set { if (_color == value) return; _color = value; Raise("Color"); }
        }

        public PhotoMetadata Meta
        {
            get { return _meta; }
            set
            {
                _meta = value;
                Raise("Meta");
                Raise("Badge");
                Raise("HasBadge");
                Raise("TakenText");
                Raise("SizeText");
            }
        }

        public string Badge
        {
            get { return _meta != null ? _meta.ShortBadge : null; }
        }

        public bool HasBadge
        {
            get { return !string.IsNullOrEmpty(Badge); }
        }

        public string TakenText
        {
            get
            {
                if (_meta == null || !_meta.TakenUtc.HasValue) return null;
                return _meta.TakenUtc.Value.ToString("dd/MM/yyyy HH:mm:ss");
            }
        }

        public string SizeText
        {
            get { return FormatSize(Size); }
        }

        public string ToolTipText
        {
            get
            {
                var parts = new List<string>();
                parts.Add(Name);
                if (_meta != null)
                {
                    if (!string.IsNullOrEmpty(_meta.Camera)) parts.Add(_meta.Camera);
                    var tech = _meta.ShortBadge;
                    if (!string.IsNullOrEmpty(tech)) parts.Add(tech);
                    if (_meta.TakenUtc.HasValue) parts.Add(_meta.TakenUtc.Value.ToString("dd/MM/yyyy HH:mm"));
                }
                parts.Add(FormatSize(Size));
                return string.Join("\n", parts.ToArray());
            }
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("0.#") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("0.##") + " GB";
        }
    }
}
