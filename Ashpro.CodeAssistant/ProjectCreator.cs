using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Ashpro.CodeAssistant
{
    internal static class ProjectCreator
    {
        public static async Task CreateWpfProjectAsync(AsyncPackage package, string solutionName, string projectName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AshproSolutions",
                solutionName);

            Directory.CreateDirectory(basePath);

            // Create solution
            RunDotnet($"new sln -n {solutionName}", basePath);

            // Create WPF project
            RunDotnet($"new wpf -n {projectName}", basePath);

            // Add project to solution
            RunDotnet($"sln add {projectName}/{projectName}.csproj", basePath);
        }

        private static void RunDotnet(string args, string workingDir)
        {
            var psi = new ProcessStartInfo("dotnet", args)
            {
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var p = Process.Start(psi);
            p.WaitForExit();
        }
    }
}
