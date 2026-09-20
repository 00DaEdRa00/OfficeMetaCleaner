using System.IO;
using System.Windows;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.App;

public partial class MainWindow : Window
{
    private async void Clean_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || Items.Count == 0)
            return;

        var inPlace = InPlaceCheck.IsChecked == true;
        var all = Items.ToList();
        var items = all.Where(i => !string.Equals(i.Status, L10n.StatusDone, StringComparison.Ordinal)).ToList();
        var alreadyDone = all.Count - items.Count;

        if (items.Count == 0)
        {
            MessageBox.Show(this,
                L10n.Gui_AllDone,
                "OfficeMetaCleaner", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var useFolder = !inPlace && OutputFolderRadio.IsChecked == true;
        var outFolder = useFolder ? OutputFolderBox.Text?.Trim() : null;

        if (useFolder && string.IsNullOrWhiteSpace(outFolder))
        {
            MessageBox.Show(this, L10n.Gui_NeedFolder, "OfficeMetaCleaner",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var options = new ScrubOptions
        {
            InPlace = inPlace,
            StripImageMetadata = StripImagesCheck.IsChecked == true,
            RemoveSignatures = RemoveSignaturesCheck.IsChecked == true,
            SetPrivacyFlags = PrivacyFlagsCheck.IsChecked == true
        };

        SetBusy(true);

        Progress.Maximum = items.Count;
        Progress.Value = 0;

        var succeeded = 0;
        var skipped = 0;
        var waitBusy = WaitBusyCheck.IsChecked == true;
        var cts = new CancellationTokenSource();
        _runCts = cts;
        var token = cts.Token;

        try
        {
        await Task.Run(() =>
        {
            var processed = 0;

            // с ожиданием сначала свободные файлы, занятые — после (по мере закрытия)
            static bool IsFree(ScrubItem i) =>
                FileBusy.FindOwnerFile(i.FilePath) is null && !FileBusy.IsBusy(i.FilePath);

            var ordered = waitBusy
                ? items.Where(IsFree).Concat(items.Where(i => !IsFree(i))).ToList()
                : items;

            foreach (var item in ordered)
            {
                if (waitBusy && !IsFree(item))
                {
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = L10n.StatusWaiting;
                        item.Detail = L10n.Gui_WaitDetail;
                    });

                    if (!FileBusy.WaitUntilFree(item.FilePath, FileBusy.DefaultPollInterval, token))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            item.Status = L10n.StatusSkipped;
                            item.Detail = L10n.Gui_CancelledDetail;
                            item.Result = null;
                            skipped++;
                        });

                        processed++;
                        var cancelled = processed;
                        Dispatcher.Invoke(() =>
                        {
                            Progress.Value = cancelled;
                            SummaryText.Text = string.Format(L10n.Gui_ProcessedOf, cancelled, items.Count);
                        });
                        continue;
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    item.Status = L10n.StatusProcessing;
                    item.Detail = "…";
                });

                try
                {
                    var output = outFolder is null
                        ? null
                        : Path.Combine(outFolder, Path.GetFileName(item.FilePath));

                    var result = MetadataScrubber.Scrub(item.FilePath, output, options);

                    Dispatcher.Invoke(() =>
                    {
                        item.Container = result.Container;
                        item.OutputPath = result.OutputPath;
                        item.Result = result;

                        if (result.Success)
                        {
                            item.Status = L10n.StatusDone;
                            succeeded++;
                        }
                        else
                        {
                            item.Status = L10n.StatusSkipped;
                            skipped++;
                        }

                        item.Detail = BuildDetail(result, options);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        item.Status = L10n.StatusError;
                        skipped++;
                        item.Detail = ex.Message;
                    });
                }

                processed++;
                var current = processed;
                Dispatcher.Invoke(() =>
                {
                    Progress.Value = current;
                    SummaryText.Text = string.Format(L10n.Gui_ProcessedOf, current, items.Count);
                });
            }
        });
        }
        finally
        {
            _runCts = null;
            cts.Dispose();
        }

        string? resolvedDir = null;
        if (options.InPlace)
        {
            var first = items.Select(i => i.FilePath).FirstOrDefault();
            if (first is not null)
                resolvedDir = Path.GetDirectoryName(first);
        }
        else if (outFolder is not null)
        {
            resolvedDir = outFolder;
        }
        else
        {
            var firstOutput = items.Select(i => i.OutputPath).FirstOrDefault(p => !string.IsNullOrEmpty(p));
            if (firstOutput is not null)
                resolvedDir = Path.GetDirectoryName(firstOutput);
        }

        _lastOutputDir = resolvedDir;

        SetBusy(false);

        SummaryText.Text = string.Format(L10n.Gui_DoneSummary, succeeded, skipped, items.Count)
                           + (alreadyDone > 0 ? string.Format(L10n.Gui_AlreadyDone, alreadyDone) : string.Empty);

        UpdateUi();
    }

    private static string BuildDetail(ScrubResult result, ScrubOptions options)
    {
        var parts = new List<string>();

        if (result.DroppedParts > 0)
            parts.Add(string.Format(L10n.Gui_DroppedParts, result.DroppedParts));
        if (result.ScrubbedParts > 0)
            parts.Add(string.Format(L10n.Gui_ScrubbedParts, result.ScrubbedParts));

        var text = parts.Count > 0
            ? string.Join(", ", parts)
            : result.Actions.Count > 0
                ? string.Format(L10n.Gui_OperationsDone, result.Actions.Count)
                : L10n.Gui_NothingToClean;

        if (result.Warnings.Count > 0 && !result.Warnings.All(w => w.StartsWith(L10n.Core_InPlaceWarning, StringComparison.Ordinal)))
            text += " — " + result.Warnings[0];

        if (options.DryRun)
            text += L10n.Gui_DryRunTag;
        else if (result.ReplacedInPlace)
            text += L10n.Gui_ReplacedTag;
        else if (result.OutputPath is not null)
            text += $" → {Path.GetFileName(result.OutputPath)}";

        return text;
    }
}
