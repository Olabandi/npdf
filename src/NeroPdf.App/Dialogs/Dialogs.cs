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

    /// <summary>Picks a file of one of the given types (extensions without dot) for converting.</summary>
    Task<string?> PickFileToConvertAsync(IReadOnlyList<string> extensions);

    /// <summary>Scanner, paper source and colour; null if cancelled.</summary>
    Task<ScanRequest?> AskScanSettingsAsync(IReadOnlyList<ScannerDevice> devices);

    /// <summary>After a flatbed page: true to scan another page, false when done.</summary>
    Task<bool> AskScanAnotherPageAsync(int pagesSoFar);
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

    public async Task<string?> PickFileToConvertAsync(IReadOnlyList<string> extensions)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Texts.Get("FileDialog.PrintableFiles"))
            {
                Patterns = extensions.Select(e => "*." + e).ToList(),
            }],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public Task<ScanRequest?> AskScanSettingsAsync(IReadOnlyList<ScannerDevice> devices) =>
        new ScanDialog(devices).ShowDialog<ScanRequest?>(owner);

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
