using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;

// Uses the OS .NET Framework, so bootstrap does not depend on the ASP.NET runtime it installs.
internal static class Program
{
    private static int Main(string[] args)
    {
        string temporary = null;
        try
        {
            if (args.Length == 0) throw new ArgumentException("A setup phase is required.");
            if (!Environment.Is64BitOperatingSystem) throw new InvalidOperationException("This setup package requires 64-bit Windows.");
            temporary = Path.Combine(Path.GetTempPath(), "AppSouls-Setup-" + Guid.NewGuid().ToString("N"));
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(temporary).Create(security);
            var assembly = Assembly.GetExecutingAssembly();
            foreach (string name in assembly.GetManifestResourceNames())
                using (var input = assembly.GetManifestResourceStream(name))
                using (var output = File.Create(Path.Combine(temporary, name))) input.CopyTo(output);
            string script, arguments;
            if (args[0] == "dependencies")
            {
                script = Path.Combine(temporary, "install-server.ps1");
                arguments = "-Source " + Quote(temporary) + " -DependenciesOnly";
            }
            else if (args[0] == "configure" && args.Length == 2)
            {
                string source = Path.GetFullPath(args[1]);
                script = Path.Combine(source, "install", "install-server.ps1");
                arguments = "-Source " + Quote(source) + " -HostPrepared -PackageManaged -Upgrade";
            }
            else if ((args[0] == "prepare" || args[0] == "remove" || args[0] == "stop") && args.Length == 2)
            {
                script = Path.Combine(temporary, "msi-hosting.ps1");
                arguments = "-Mode " + args[0] + " -Root " + Quote(Path.GetFullPath(args[1]));
            }
            else throw new ArgumentException("Unknown setup phase.");
            var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.Is64BitProcess ? "System32" : "Sysnative", "WindowsPowerShell", "v1.0", "powershell.exe");
            using (var child = Process.Start(new ProcessStartInfo(powershell,
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script) + " " + arguments) { UseShellExecute = false }))
            {
                child.WaitForExit();
                return child.ExitCode;
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("App Souls setup failed: " + error.GetType().Name + ". See the setup log.");
            return 1;
        }
        finally
        {
            if (temporary != null && Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }
    // Windows command-line quoting: protect embedded quotes and trailing directory separators.
    private static string Quote(string value)
    {
        if (value.IndexOf('"') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0) throw new ArgumentException("Invalid setup path.");
        return "\"" + value.TrimEnd('\\') + "\"";
    }
}
