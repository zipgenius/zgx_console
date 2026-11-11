using System.Diagnostics;
 
  public static class ShellHelper
  {
    public static Task<int> Bash(this string cmd)
    {
      var source = new TaskCompletionSource<int>();
      var escapedArgs = cmd.Replace("\"", "\\\"");
      var process = new Process
                      {
                        StartInfo = new ProcessStartInfo
                                      {
                                        FileName = "bash",
                                        Arguments = $"-c \"{escapedArgs}\"",
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = true
                                      },
                        EnableRaisingEvents = true
                      };
      process.Exited += (sender, args) =>
        {
          //logger.LogWarning(process.StandardError.ReadToEnd());
          //logger.LogInformation(process.StandardOutput.ReadToEnd());
          if (process.ExitCode == 0)
          {
            source.SetResult(0);
          }
          else
          {
            source.SetException(new Exception($"Command `{cmd}` failed with exit code `{process.ExitCode}`"));
          }

          process.Dispose();
        };

      try
      {
        process.Start();
      }
      catch (Exception e)
      {
        //logger.LogError(e, "Command {} failed", cmd);
        source.SetException(e);
      }

      return source.Task;
    }
  }