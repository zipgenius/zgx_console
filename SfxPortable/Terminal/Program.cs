using Zgx.Sfx;
using static Zgx.Sfx.Session;

Console.OutputEncoding = new System.Text.UTF8Encoding(false);
Console.InputEncoding = new System.Text.UTF8Encoding(false);
try
{
    if (args.Contains("--help"))
    {
        Console.WriteLine("ZipGenius X SFX: [--destination=folder] [--password=value] [--overwrite] [--yes] [--list] [--no-open]");
        return 0;
    }
    using var session = Package.OpenSelf();
    session.CheckSafety(Console.Error.WriteLine);
    string destination = session.DefaultDestination();
    string? password = null;
    bool overwrite = false, yes = false, list = false, noOpen = false;
    foreach (string argument in args)
    {
        if (argument.StartsWith("--destination="))
        {
            if (session.Options.Destination != "editable") throw new ArgumentException("The destination is fixed by the package author.");
            destination = Path.GetFullPath(argument[14..]);
        }
        else if (argument.StartsWith("--password=")) password = argument[11..];
        else if (argument == "--overwrite") overwrite = true;
        else if (argument == "--yes") yes = true;
        else if (argument == "--list") list = true;
        else if (argument == "--no-open") noOpen = true;
        else throw new ArgumentException("Unknown option: " + argument);
    }
    Console.WriteLine(session.Options.Message);
    if (list || session.Options.FileList == "expanded")
    {
        foreach (string name in session.List(password)) Console.WriteLine(name);
        if (list) return 0;
    }
    if (!yes && !session.Options.AutoStart)
    {
        if (Console.IsInputRedirected) throw new InvalidOperationException("Use --yes to extract without interactive input.");
        if (session.Options.FileList == "collapsed")
        {
            Console.Write(L("Mostrare l’elenco dei file? [s/N] ", "Show file list? [y/N] "));
            if ((Console.ReadLine() ?? "").ToLowerInvariant() is "s" or "y")
                foreach (string name in session.List(password)) Console.WriteLine(name);
        }
        if (session.Options.Destination == "editable")
        {
            Console.Write(L("Cartella di destinazione", "Destination folder") + $" [{destination}]: ");
            string? chosen = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(chosen)) destination = Path.GetFullPath(chosen);
        }
        Console.Write(L("Estrarre i file? [s/N] ", "Extract files? [y/N] "));
        if ((Console.ReadLine() ?? "").ToLowerInvariant() is not ("s" or "y")) return 20;
    }
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
    int attempts = 0;
    while (true)
    {
        try
        {
            session.Extract(destination, password, overwrite,
                new Progress<(int Percent, string File)>(p => { if (!Console.IsOutputRedirected) Console.Write($"\r{p.Percent,3}% {p.File}     "); }), cancellation.Token);
            break;
        }
        catch (Exception ex) when (ex is not SafetyException && !Console.IsInputRedirected && attempts++ < 3 &&
            (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("encrypt", StringComparison.OrdinalIgnoreCase)))
        {
            Console.Write(L("Password: ", "Password: "));
            var secret = new System.Text.StringBuilder();
            ConsoleKeyInfo key;
            while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Escape) return 20;
                if (key.Key == ConsoleKey.Backspace) { if (secret.Length > 0) secret.Length--; }
                else if (!char.IsControl(key.KeyChar)) secret.Append(key.KeyChar);
            }
            Console.WriteLine();
            password = secret.ToString();
        }
    }
    Console.WriteLine(L("\nFile estratti in: ", "\nFiles extracted to: ") + destination);
    if (!noOpen) session.OpenAfterExtraction(destination);
    if (!session.Options.CloseOnSuccess && !Console.IsInputRedirected)
    {
        Console.WriteLine(L("Premi Invio per chiudere.", "Press Enter to close."));
        Console.ReadLine();
    }
    return 0;
}
catch (SafetyException ex) { Console.Error.WriteLine(ex.Message); return ex.ExitCode; }
catch (OperationCanceledException) { return 20; }
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }
