using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Themes.Fluent;
using Zgx.Sfx;
using static Zgx.Sfx.Session;

namespace Zgx.Sfx.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try { App.Session = Package.OpenSelf(); }
        catch (Exception ex) { App.StartupError = ex.Message; }
        return AppBuilder.Configure<App>().UsePlatformDetect()
            .With(new X11PlatformOptions { EnableSessionManagement = false })
            .StartWithClassicDesktopLifetime(args);
    }
}

public sealed class App : Application
{
    internal static Session? Session;
    internal static string? StartupError;
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Session is null
                ? new Window { Title = "ZipGenius X SFX", Width = 520, Height = 180, Content = new TextBlock { Text = StartupError, Margin = new Thickness(24), TextWrapping = Avalonia.Media.TextWrapping.Wrap } }
                : new ExtractWindow(Session);
            desktop.Exit += (_, _) => Session?.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class ExtractWindow : Window
{
    private readonly Session session;
    private readonly TextBox destination;
    private readonly TextBox password = new() { PasswordChar = '●' };
    private readonly CheckBox overwrite = new() { Content = L("Sostituisci i file già esistenti", "Replace existing files") };
    private readonly TextBlock status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ProgressBar progress = new() { Minimum = 0, Maximum = 100 };
    private readonly Button extract = new() { Content = L("Estrai", "Extract") };
    private readonly Button cancel = new() { Content = L("Chiudi", "Close") };
    private readonly Button browse = new() { Content = L("Sfoglia…", "Browse…") };
    private readonly TextBox files = new() { IsReadOnly = true, AcceptsReturn = true, Height = 160 };
    private CancellationTokenSource? cancellation;
    private bool busy;

    public ExtractWindow(Session session)
    {
        this.session = session;
        Title = "ZipGenius X — " + session.Options.ArchiveName;
        Width = 680; Height = 620; MinWidth = 480; MinHeight = 480;
        destination = new TextBox { Text = session.DefaultDestination(), IsReadOnly = session.Options.Destination != "editable" };
        browse.IsVisible = session.Options.Destination == "editable";
        if (session.Options.IconBase64.Length > 0)
            Icon = new WindowIcon(new MemoryStream(Convert.FromBase64String(session.Options.IconBase64)));
        var path = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        path.Children.Add(destination); Grid.SetColumn(browse, 1); path.Children.Add(browse);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(extract); buttons.Children.Add(cancel);
        var body = new StackPanel { Margin = new Thickness(24), Spacing = 12 };
        body.Children.Add(new TextBlock { Text = session.Options.Message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 18 });
        body.Children.Add(new TextBlock { Text = L("Cartella di destinazione", "Destination folder") });
        body.Children.Add(path);
        body.Children.Add(new TextBlock { Text = L("Password (solo per archivi protetti)", "Password (protected archives only)") });
        body.Children.Add(password);
        body.Children.Add(overwrite);
        if (session.Options.FileList != "hidden")
        {
            var refresh = new Button { Content = L("Mostra / aggiorna elenco", "Show / refresh file list") };
            refresh.Click += (_, _) => LoadList();
            var listing = new StackPanel { Spacing = 8 };
            listing.Children.Add(refresh); listing.Children.Add(files);
            body.Children.Add(new Expander { Header = L("File nell’archivio", "Files in archive"), IsExpanded = session.Options.FileList == "expanded", Content = listing });
        }
        body.Children.Add(progress); body.Children.Add(status); body.Children.Add(buttons);
        Content = new ScrollViewer { Content = body };
        browse.Click += async (_, _) =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = L("Scegli la cartella", "Choose a folder"), AllowMultiple = false });
            if (folders.Count > 0) destination.Text = folders[0].Path.LocalPath;
        };
        extract.Click += async (_, _) => await ExtractAsync();
        cancel.Click += (_, _) => { if (busy) cancellation?.Cancel(); else Close(); };
        Closing += (_, e) => { if (busy) { e.Cancel = true; cancellation?.Cancel(); } };
        Opened += async (_, _) =>
        {
            try
            {
                int safety = session.CheckSafety(message => status.Text = message);
                if (safety == 0) status.Text = L("Pronto per estrarre.", "Ready to extract.");
                if (session.Options.FileList == "expanded") LoadList();
                if (session.Options.AutoStart) await ExtractAsync();
            }
            catch (Exception ex) { status.Text = ex.Message; extract.IsEnabled = false; }
        };
    }

    private void LoadList()
    {
        try { files.Text = string.Join(Environment.NewLine, session.List(password.Text)); }
        catch (Exception ex) { status.Text = ex.Message; }
    }

    private async Task ExtractAsync()
    {
        if (busy) return;
        busy = true; extract.IsEnabled = false; browse.IsEnabled = false;
        destination.IsEnabled = false; password.IsEnabled = false; overwrite.IsEnabled = false;
        cancel.Content = L("Annulla", "Cancel");
        cancellation = new CancellationTokenSource();
        bool success = false;
        try
        {
            string path = Path.GetFullPath(destination.Text ?? "");
            string? secret = password.Text;
            bool replace = overwrite.IsChecked == true;
            var updates = new Progress<(int Percent, string File)>(p => { progress.Value = p.Percent; status.Text = p.File; });
            await Task.Run(() => session.Extract(path, secret, replace, updates, cancellation.Token));
            status.Text = L("Estrazione completata.", "Extraction completed.");
            session.OpenAfterExtraction(path);
            success = true;
        }
        catch (OperationCanceledException) { status.Text = L("Estrazione annullata.", "Extraction cancelled."); }
        catch (Exception ex) { status.Text = ex.Message; }
        finally
        {
            cancellation.Dispose(); cancellation = null; busy = false;
            extract.IsEnabled = true; browse.IsEnabled = true; destination.IsEnabled = true; password.IsEnabled = true; overwrite.IsEnabled = true;
            cancel.Content = L("Chiudi", "Close");
        }
        if (success && session.Options.CloseOnSuccess) Close();
    }
}
