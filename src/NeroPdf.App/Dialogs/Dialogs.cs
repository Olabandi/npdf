using Avalonia.Controls;
using Avalonia.Platform.Storage;
using NeroPdf.App.Infrastructure;
using NeroPdf.App.ViewModels;
using NeroPdf.Core.Platform;

namespace NeroPdf.App.Dialogs;

public enum UnsavedChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>Everything the view model needs to ask the user.</summary>
public interface IDialogs
{
    /// <summary>Asks for the password of a protected file; null if cancelled.</summary>
    Task<string?> AskPasswordAsync(string fileName, bool previousWasWrong);

    Task<UnsavedChoice> AskUnsavedPrintAsync();

    Task<UnsavedChoice> AskUnsavedChangesAsync(string fileName);

    Task ShowSaveErrorAsync(string fileName);

    Task<string?> PickFileToOpenAsync();

    Task<string?> PickSaveTargetAsync(string suggestedName, string? folder);

    /// <summary>A message with an OK button.</summary>
    Task ShowMessageAsync(string title, string text);

    /// <summary>
    /// Picks a file to convert. Filters: all supported files, images, documents; starts with the filter
    /// and folder used last.
    /// </summary>
    Task<ConvertPick?> PickFileToConvertAsync(ConvertibleTypes types, string? lastFilter, string? lastFolder);

    /// <summary>Scanner, paper source and colour; null if cancelled.</summary>
    Task<ScanRequest?> AskScanSettingsAsync(IReadOnlyList<ScannerDevice> devices);

    /// <summary>A problem the user may retry: true for "Try again", false for "OK".</summary>
    Task<bool> AskRetryAsync(string title, string text);

    /// <summary>Converting an image (with preview): page format and size; null if cancelled.</summary>
    Task<ImagePageChoice?> AskImagePageAsync(string filePath, ImagePageChoice last);

    /// <summary>After a flatbed page: true to scan another page, false when done.</summary>
    Task<bool> AskScanAnotherPageAsync(int pagesSoFar);
}

/// <summary>The file chosen in the Convert dialog and the file type filter that was selected.</summary>
public sealed record ConvertPick(string Path, string Filter)
{
    public const string All = "All";
    public const string Images = "Images";
    public const string Documents = "Documents";
}

/// <summary>Dialogs shown over the main window.</summary>
public sealed class Dialogs(Window owner) : IDialogs
{
    private const string Ok = "ok";
    private const string Cancel = "cancel";
    private const string Save = "save";
    private const string Discard = "discard";

    public async Task<string?> AskPasswordAsync(string fileName, bool previousWasWrong)
    {
        var dialog = DialogWindow.Create(
            Texts.Get("Password.Title"),
            Texts.Format("Password.Prompt", fileName),
            [(Ok, Texts.Get("Password.Open")), (Cancel, Texts.Get("Common.Cancel"))],
            Cancel,
            withPassword: true,
            error: previousWasWrong ? Texts.Get("Password.Wrong") : null);
        var result = await dialog.ShowDialog<string?>(owner);
        var password = dialog.EnteredPassword;
        dialog.EnteredPassword = null;
        return result == Ok ? password ?? string.Empty : null;
    }

    public Task<UnsavedChoice> AskUnsavedPrintAsync() =>
        AskUnsavedAsync(Texts.Get("Unsaved.Title"), Texts.Get("Unsaved.Text"));

    public Task<UnsavedChoice> AskUnsavedChangesAsync(string fileName) =>
        AskUnsavedAsync(Texts.Get("Changes.Title"), Texts.Format("Changes.Text", fileName));

    private async Task<UnsavedChoice> AskUnsavedAsync(string title, string text)
    {
        var dialog = DialogWindow.Create(
            title,
            text,
            [(Save, Texts.Get("Unsaved.Save")), (Discard, Texts.Get("Unsaved.Discard")), (Cancel, Texts.Get("Common.Cancel"))],
            Cancel);
        return await dialog.ShowDialog<string?>(owner) switch
        {
            Save => UnsavedChoice.Save,
            Discard => UnsavedChoice.Discard,
            _ => UnsavedChoice.Cancel,
        };
    }

    public async Task ShowSaveErrorAsync(string fileName)
    {
        var dialog = DialogWindow.Create(
            Texts.Get("SaveError.Title"),
            Texts.Format("SaveError.Text", fileName),
            [(Ok, Texts.Get("Common.Ok"))],
            Ok);
        await dialog.ShowDialog<string?>(owner);
    }

    public async Task ShowMessageAsync(string title, string text)
    {
        var dialog = DialogWindow.Create(title, text, [(Ok, Texts.Get("Common.Ok"))], Ok);
        await dialog.ShowDialog<string?>(owner);
    }

    public async Task<ConvertPick?> PickFileToConvertAsync(ConvertibleTypes types, string? lastFilter, string? lastFolder)
    {
        var filters = new (string Key, FilePickerFileType Type)[]
        {
            (ConvertPick.All, FileType("FileDialog.AllSupported", types.All)),
            (ConvertPick.Images, FileType("FileDialog.Images", types.Images)),
            (ConvertPick.Documents, FileType("FileDialog.Documents", types.Documents)),
        };
        var options = new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = filters.Select(f => f.Type).ToList(),
            SuggestedFileType = filters.FirstOrDefault(f => f.Key == lastFilter).Type ?? filters[0].Type,
        };
        if (lastFolder is not null && Directory.Exists(lastFolder))
        {
            options.SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(lastFolder);
        }
        var result = await owner.StorageProvider.OpenFilePickerWithResultAsync(options);
        if (result.Files.Count == 0 || result.Files[0].TryGetLocalPath() is not { } path)
        {
            return null;
        }
        var filter = filters.FirstOrDefault(f => ReferenceEquals(f.Type, result.SelectedFileType)).Key ?? lastFilter ?? ConvertPick.All;
        return new ConvertPick(path, filter);
    }

    private static FilePickerFileType FileType(string textId, IEnumerable<string> extensions) => new(Texts.Get(textId))
    {
        Patterns = extensions.Select(e => "*." + e).ToList(),
    };

    public Task<ScanRequest?> AskScanSettingsAsync(IReadOnlyList<ScannerDevice> devices) =>
        new ScanDialog(devices).ShowDialog<ScanRequest?>(owner);

    public Task<ImagePageChoice?> AskImagePageAsync(string filePath, ImagePageChoice last) =>
        new ImagePageDialog(filePath, last).ShowDialog<ImagePageChoice?>(owner);

    public async Task<bool> AskRetryAsync(string title, string text)
    {
        const string retry = "retry";
        var dialog = DialogWindow.Create(title, text, [(retry, Texts.Get("Common.Retry")), (Ok, Texts.Get("Common.Ok"))], Ok);
        return await dialog.ShowDialog<string?>(owner) == retry;
    }

    public async Task<bool> AskScanAnotherPageAsync(int pagesSoFar)
    {
        const string more = "more";
        var dialog = DialogWindow.Create(
            Texts.Get("Scan.More.Title"),
            Texts.Format("Scan.More.Text", pagesSoFar),
            [(more, Texts.Get("Scan.More.Yes")), (Ok, Texts.Get("Scan.More.Done"))],
            Ok);
        return await dialog.ShowDialog<string?>(owner) == more;
    }

    public async Task<string?> PickFileToOpenAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [PdfFileType()],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveTargetAsync(string suggestedName, string? folder)
    {
        var options = new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = "pdf",
            FileTypeChoices = [PdfFileType()],
            ShowOverwritePrompt = true,
        };
        if (folder is not null)
        {
            options.SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(folder);
        }
        var file = await owner.StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    private static FilePickerFileType PdfFileType() => new(Texts.Get("FileDialog.PdfFiles"))
    {
        Patterns = ["*.pdf"],
        MimeTypes = ["application/pdf"],
        AppleUniformTypeIdentifiers = ["com.adobe.pdf"],
    };
}
