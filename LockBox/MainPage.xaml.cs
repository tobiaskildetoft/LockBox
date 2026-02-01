using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using LockBox.Models;
using LockBox.Services;
using LockBox.ViewModels;
using Microsoft.Maui.Storage;

/// TODOs:
/// Fix toasts so info can be communicated
/// DONE - Disable restore button when key missing
/// Move to proper viewmodel binding
/// add option to point to key when not found usual place or with usual name
/// Add option for mass restore by picking folder
/// Add option to attach created lockbox to email from app
/// Add option to create lockbox from existing key - both with adding copy in local storage and without
/// various checks (checksum for files?)
/// cleanup code
/// add unit tests

namespace LockBox
{
    public partial class MainPage : ContentPage
    {
        private LockBoxService? _service;
        private LockBoxDocument? _loadedDocument;
        private string? _loadedBoxFilePath;
        private readonly ObservableCollection<FileEntryViewModel> _displayFiles = new();

        public MainPage()
        {
            InitializeComponent();
            FilesCollectionView.ItemsSource = _displayFiles;
        }

        private LockBoxService? GetService()
        {
            _service ??= Application.Current?.Handler?.MauiContext?.Services.GetService<LockBoxService>();
            return _service;
        }

        private async void OnOpenLockBoxClicked(object? sender, EventArgs e)
        {
            var service = GetService();
            if (service == null)
            {
                // await Toast.Make("LockBox service is not available.").Show();
                return;
            }

            try
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
                    // await Toast.Make("Please select a .box file.").Show();
                    return;
                }

                LockBoxDocument doc;
                try
                {
                    doc = await service.LoadDocumentAsync(path);
                }
                catch
                {
                    using var stream = await result.OpenReadAsync();
                    doc = await service.LoadDocumentAsync(stream);
                }
                _loadedDocument = doc;
                _loadedBoxFilePath = path;

                string? privateKeyPem = await service.TryGetPrivateKeyAsync(_loadedDocument.PublicKeyDigest);
                var hasPrivateKey = privateKeyPem != null;

                _displayFiles.Clear();
                foreach (var f in doc.Files)
                    _displayFiles.Add(FileEntryViewModel.FromFileEntry(f, hasPrivateKey));

                LoadedBoxPathLabel.Text = path;
                NoBoxLoadedLayout.IsVisible = false;
                BoxLoadedLayout.IsVisible = true;
                // await Toast.Make("Lockbox opened.").Show();
            }
            catch (Exception ex)
            {
                // await Toast.Make($"Error: {ex.Message}").Show();
            }
        }

        private void OnCloseLockBoxClicked(object? sender, EventArgs e)
        {
            _loadedDocument = null;
            _loadedBoxFilePath = null;
            _displayFiles.Clear();
            NoBoxLoadedLayout.IsVisible = true;
            BoxLoadedLayout.IsVisible = false;
        }

        private async void OnCreateLockBoxClicked(object? sender, EventArgs e)
        {
            var service = GetService();
            if (service == null)
            {
                // await Toast.Make("LockBox service is not available.").Show();
                return;
            }

            OpenLockBoxBtn.IsEnabled = false;
            CreateLockBoxBtn.IsEnabled = false;
            try
            {
                CreateLockBoxResult result = await service.CreateNewLockBoxAsync();

                var stream = new MemoryStream(result.BoxContent);
                var fileSaverResult = await FileSaver.Default.SaveAsync("LockBox.box", stream, CancellationToken.None);

                if (!fileSaverResult.IsSuccessful)
                {
                    string message = fileSaverResult.Exception?.Message ?? "Save was cancelled or failed.";
                    // await Toast.Make($"Lockbox was not saved: {message}").Show();
                    return;
                }

                await service.SavePrivateKeyAsync(result.PrivateKeyPem, result.PublicKeyDigestHex);

                // await Toast.Make($"Lockbox created at {fileSaverResult.FilePath}. Key saved for this lockbox.").Show();
            }
            catch (Exception ex)
            {
                // await Toast.Make($"Error: {ex.Message}").Show();
            }
            finally
            {
                OpenLockBoxBtn.IsEnabled = true;
                CreateLockBoxBtn.IsEnabled = true;
            }
        }

        private async void OnAddFileClicked(object? sender, EventArgs e)
        {
            if (_loadedDocument == null || _loadedBoxFilePath == null)
                return;
            var service = GetService();
            if (service == null)
            {
                await Toast.Make("LockBox service is not available.").Show();
                return;
            }

            try
            {
                FileResult? result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a file to add" });
                if (result == null)
                    return;

                string path = result.FullPath ?? throw new InvalidOperationException("No file path.");
                AddFileBtn.IsEnabled = false;
                FileEntry entry = await service.AddFileToLockBoxAsync(_loadedDocument, path);
                await service.SaveDocumentAsync(_loadedDocument, _loadedBoxFilePath);

                string? privateKeyPem = await service.TryGetPrivateKeyAsync(_loadedDocument.PublicKeyDigest);
                var hasPrivateKey = privateKeyPem != null;

                _displayFiles.Add(FileEntryViewModel.FromFileEntry(entry, hasPrivateKey));
                // await Toast.Make($"Added: {entry.Filename}").Show();
            }
            catch (Exception ex)
            {
                // await Toast.Make($"Error: {ex.Message}").Show();
            }
            finally
            {
                AddFileBtn.IsEnabled = true;
            }
        }

        private void OnFileSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Optional: track selected file for a single "Restore selected" button if desired
        }

        private async void OnRestoreFileClicked(object? sender, EventArgs e)
        {
            if (_loadedDocument == null || sender is not BindableObject bindable)
                return;
            var entry = bindable.BindingContext as FileEntry;
            if (entry == null)
                return;
            var service = GetService();
            if (service == null)
            {
                // await Toast.Make("LockBox service is not available.").Show();
                return;
            }

            string? privateKeyPem = await service.TryGetPrivateKeyAsync(_loadedDocument.PublicKeyDigest);
            if (string.IsNullOrEmpty(privateKeyPem))
            {
                // await Toast.Make("No private key found for this lockbox. You need the key that was created with this lockbox to restore files.").Show();
                return;
            }

            try
            {
                byte[] content = await service.DecryptFileContentAsync(entry, privateKeyPem);
                using var stream = new MemoryStream(content);
                var fileSaverResult = await FileSaver.Default.SaveAsync(entry.Filename, stream, CancellationToken.None);
                // if (fileSaverResult.IsSuccessful)
                    // await Toast.Make($"Restored to {fileSaverResult.FilePath}").Show();
                // else
                    // await Toast.Make($"Save failed: {fileSaverResult.Exception?.Message ?? "Unknown"}").Show();
            }
            catch (Exception ex)
            {
                // await Toast.Make($"Decrypt failed: {ex.Message}").Show();
            }
        }
    }
}
