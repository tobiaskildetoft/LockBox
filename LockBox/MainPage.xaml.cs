using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using LockBox.Core.Models;
using LockBox.Core.Services;
using LockBox.Services;
using LockBox.Subviews;
using LockBox.ViewModels;
using Microsoft.Maui.Storage;

/// TODOs:
/// Change all calls here to be to a local service or multiple, and make those call Core
/// Fix toasts so info can be communicated
/// DONE - Disable restore button when key missing
/// Move to proper viewmodel binding. Consider https://github.com/matt-goldman/Plugin.Maui.SmartNavigation
/// add option to point to key when not found usual place or with usual name
/// Add option for mass restore by picking folder
/// Add option to attach created lockbox to email from app
/// Add option to create lockbox from existing key - both with adding copy in local storage and without
/// various checks (checksum for files?)
/// cleanup code
/// add unit tests
/// Add option to remove files (also without key)

namespace LockBox
{
    public partial class MainPage : ContentPage
    {
        private PrivateKeyFileService _fileService;
        private LockBoxService _lockBoxService;
        private LockBoxDocument? _loadedDocument;
        private string? _loadedBoxFilePath;
        private readonly ObservableCollection<FileEntryViewModel> _displayFiles = new();

        public MainPage()
        {
            InitializeComponent();
            FilesCollectionView.ItemsSource = _displayFiles;
            _fileService = Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<PrivateKeyFileService>() ?? new PrivateKeyFileService();
            _lockBoxService = Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<LockBoxService>() ?? new LockBoxService();
        }

        private async void OnOpenLockBoxClicked(object? sender, EventArgs e)
        {
            var pickOptions = new PickOptions
            {
                PickerTitle = "Open lockbox file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.WinUI] = new[] { ".box" },
                    [DevicePlatform.Android] = new[] { "application/octet-stream" },
                    [DevicePlatform.iOS] = new[] { "public.data" },
                    [DevicePlatform.MacCatalyst] = new[] { "public.data" }
                })
            };
            FileResult? result = await FilePicker.Default.PickAsync(pickOptions);
            if (result == null)
                return;

            string path = result.FullPath ?? string.Empty;
            string fileName = result.FileName ?? path;
            if (!fileName.EndsWith(".box", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".box", StringComparison.OrdinalIgnoreCase))
            {
                // TODO
                return;
            }

            LockBoxDocument doc;
            try
            {
                doc = await _lockBoxService.LoadDocumentFromFileAsync(path);
            }
            catch
            {
                using var stream = await result.OpenReadAsync();
                doc = await _lockBoxService.LoadDocumentFromStreamAsync(stream);
            }
            _loadedDocument = doc;
            _loadedBoxFilePath = path;

            string? privateKeyPem = await _fileService.TryGetPrivateKeyAsync(_loadedDocument.PublicKeyDigest);
            var hasPrivateKey = privateKeyPem != null;

            _displayFiles.Clear();
            foreach (var f in doc.Files)
                _displayFiles.Add(new FileEntryViewModel(f, hasPrivateKey));

            LoadedBoxPathLabel.Text = path;
            BoxLoadedComponent.IsVisible = true;
        }

        private void OnCloseLockBoxClicked(object? sender, EventArgs e)
        {
            _loadedDocument = null;
            _loadedBoxFilePath = null;
            _displayFiles.Clear();
            BoxLoadedComponent.IsVisible = false;
        }

        private async void OnCreateLockBoxClicked(object? sender, EventArgs e)
        {
            OpenLockBoxBtn.IsEnabled = false;
            CreateLockBoxBtn.IsEnabled = false;
            CreateLockBoxResult result = await _lockBoxService.CreateNewLockBoxAsync();

            var stream = new MemoryStream(result.BoxContent);
            var fileSaverResult = await FileSaver.Default.SaveAsync("LockBox.box", stream, CancellationToken.None);

            if (!fileSaverResult.IsSuccessful)
            {
                string message = fileSaverResult.Exception?.Message ?? "Save was cancelled or failed.";
                // TODO
                return;
            }

            await _fileService.SavePrivateKeyAsync(result.PrivateKeyPem, result.PublicKeyDigestHex);

            OpenLockBoxBtn.IsEnabled = true;
            CreateLockBoxBtn.IsEnabled = true;
        }

        private async void OnAddFileClicked(object? sender, EventArgs e)
        {
            if (_loadedDocument == null || _loadedBoxFilePath == null)
            {
                return;
            }

            FileResult? result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a file to add" });
            if (result == null)
                return;

            string path = result.FullPath ?? throw new InvalidOperationException("No file path.");
            AddFileBtn.IsEnabled = false;
            FileEntry entry = await _lockBoxService.AddFileToLockBoxAsync(_loadedDocument, path);
            await _lockBoxService.SaveDocumentAsync(_loadedDocument, _loadedBoxFilePath);

            string? privateKeyPem = await _fileService.TryGetPrivateKeyAsync(_loadedDocument.PublicKeyDigest);
            var hasPrivateKey = privateKeyPem != null;

            _displayFiles.Add(new FileEntryViewModel(entry, hasPrivateKey));

            AddFileBtn.IsEnabled = true;
        }

        private void OnFileSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Optional: track selected file for a single "Restore selected" button if desired
        }

        private async void OnRestoreFileClicked(object? sender, EventArgs e)
        {
            if (_loadedDocument == null || sender is not BindableObject bindable)
            {
                return;
            }
            if (bindable.BindingContext is not FileEntryViewModel fileEntryViewModel)
            {
                return;
            }
            var fileEntry = fileEntryViewModel.AsFileEntry();

            string? privateKeyPem = await _fileService.TryGetPrivateKeyAsync(_loadedDocument.PublicKeyDigest);
            if (string.IsNullOrEmpty(privateKeyPem))
            {
                // TODO: Just fail silently? (button should be disabled)
                return;
            }

            try
            {
                byte[] content = await _lockBoxService.DecryptFileContentAsync(fileEntry, privateKeyPem);
                using var stream = new MemoryStream(content);
                var fileSaverResult = await FileSaver.Default.SaveAsync(fileEntry.Filename, stream, CancellationToken.None);
                // TODO: Fix potentially nonsuccessful filesave
            }
            catch (Exception ex)
            {
                // TODO
            }
        }

        private async void OnRemoveFileClicked(object? sender, EventArgs e)
        {
            if (_loadedDocument == null || sender is not BindableObject bindable)
            {
                return;
            }
            if (bindable.BindingContext is not FileEntryViewModel fileEntryViewModel)
            {
                return;
            }

            var fileEntry = fileEntryViewModel.AsFileEntry();

            _displayFiles.Remove(fileEntryViewModel);
            _lockBoxService.RemoveFileFromLockBox(_loadedDocument, fileEntry);
            await _lockBoxService.SaveDocumentAsync(_loadedDocument, _loadedBoxFilePath);

            // TODO: Remove file from saved file
            // TODO: Consider warning or similar since file cannot be recovered. Or maybe make it reversible until saving?
        }
    }
}
