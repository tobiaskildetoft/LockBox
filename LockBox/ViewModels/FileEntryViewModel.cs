using LockBox.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LockBox.ViewModels
{
    internal class FileEntryViewModel
    {
        private readonly FileEntry _fileEntry;

        internal FileEntryViewModel(FileEntry fileEntry, bool hasPrivateKey)
        {
            _fileEntry = fileEntry;
            HasPrivateKey = hasPrivateKey;
        }
        public string Filename
        {
            get
            {
                return _fileEntry.Filename; 
            }
            set
            {
                _fileEntry.Filename = value; 
            }
        }

        public bool HasPrivateKey { get; private set; }

        internal FileEntry AsFileEntry()
        {
            return _fileEntry;
        }
    }
}
