using LockBox.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LockBox.ViewModels
{
    internal class FileEntryViewModel
    {
        public string Filename { get; set; } = string.Empty;

        public bool HasPrivateKey { get; set; } = false;

        internal static FileEntryViewModel FromFileEntry(FileEntry fileEntry, bool hasPrivateKey)
        {
            return new FileEntryViewModel
            {
                Filename = fileEntry.Filename,
                HasPrivateKey = hasPrivateKey,
            };
        }
    }
}
