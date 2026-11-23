using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using AWL.Core.Setup;
using AWL.Desktop.Resources;

namespace AWL.Desktop.Views;

public partial class SetupWizard : UserControl
{
    private readonly Dictionary<string, TextBlock> _statuses = [];
    private TaskCompletionSource<bool>? _choice;

    public SetupWizard()
    {
        InitializeComponent();
        Heading.Text = Strings.SetupTitle;
        Description.Text = Strings.SetupDescription;
        Reopen.Content = Strings.ReopenTerminal;
    }

    internal void Reset(IEnumerable<string> ids)
    {
        _statuses.Clear();
        Rows.Children.Clear();
        foreach (string id in ids)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            var state = new TextBlock { Text = Strings.StepWaiting };
            DockPanel.SetDock(state, Dock.Right);
            row.Children.Add(state);
            string key = "Step_" + id.Replace('-', '_');
            row.Children.Add(new TextBlock { Text = Strings.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? id });
            Rows.Children.Add(row);
            _statuses.Add(id, state);
        }
        Progress.Maximum = _statuses.Count;
        Progress.Value = 0;
        Message.Text = "";
        Details.Visibility = Copy.Visibility = Reopen.Visibility = ContinueButton.Visibility = Visibility.Collapsed;
        Copy.Content = Strings.CopySummary;
    }

    internal void Update(string id, SetupProgress status)
    {
        _statuses[id].Text = status switch
        {
            SetupProgress.Completed => Strings.StepCompleted,
            SetupProgress.Failed => Strings.StepFailed,
            _ => Strings.StepRunning,
        };
        if (status == SetupProgress.Completed)
        {
            Progress.Value++;
        }
    }

    internal Task<bool> WaitForLoginAsync(CancellationToken token)
    {
        Message.Text = Strings.LoginInstructions;
        Reopen.Visibility = Visibility.Visible;
        return WaitForChoiceAsync(Strings.ConfirmLogin, token);
    }

    internal Task<bool> WaitForRetryAsync(string message, string details, CancellationToken token)
    {
        Message.Text = message;
        Details.Text = details;
        Details.Visibility = Copy.Visibility = Visibility.Visible;
        Reopen.Visibility = Visibility.Collapsed;
        return WaitForChoiceAsync(Strings.RetryButton, token);
    }

    private async Task<bool> WaitForChoiceAsync(string label, CancellationToken token)
    {
        _choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ContinueButton.Content = label;
        ContinueButton.Visibility = Visibility.Visible;
        try
        {
            return await _choice.Task.WaitAsync(token);
        }
        finally
        {
            ContinueButton.Visibility = Reopen.Visibility = Visibility.Collapsed;
            _choice = null;
        }
    }

    private void ContinueClick(object sender, RoutedEventArgs e) => _choice?.TrySetResult(true);
    private void ReopenClick(object sender, RoutedEventArgs e) => _choice?.TrySetResult(false);

    private void CopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(Details.Text);
            Copy.Content = Strings.Copied;
        }
        catch (ExternalException)
        {
            Message.Text = Strings.CopyFailed;
        }
    }
}
