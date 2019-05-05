using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TaskRunnerExplorer;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Nuke.VisualStudio.TaskRunner
{
    [TaskRunnerExport(".nuke")]
    internal class TaskRunner : ITaskRunner
    {
        private readonly BitmapImage _icon;

        public TaskRunner()
        {
            var extensionDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var uriString = Path.Combine(extensionDirectory, "Resources", "icon.png");
            _icon = new BitmapImage(new Uri(uriString));

            Options = new List<ITaskRunnerOption> {
                new TaskRunnerOption(
                    "Attach to build process",
                    PackageIds.cmdAttach,
                    PackageGuids.guidNukePackageCmdSet,
                    false,
                    "--visual-studio-debug"),
                new TaskRunnerOption(
                    "Skip dependencies",
                    PackageIds.cmdSkip,
                    PackageGuids.guidNukePackageCmdSet,
                    false,
                    "--skip")
            };
        }

        public List<ITaskRunnerOption> Options { get; }

        public async Task<ITaskRunnerConfig> ParseConfig(ITaskRunnerCommandContext context, string configPath)
        {
            var rootDirectory = Path.GetDirectoryName(configPath);
            var temporaryDirectory = Path.Combine(rootDirectory, ".tmp");
            return await Task.Run(() =>
            {
                while (NukePackage.Dte == null)
                    System.Threading.Thread.Sleep(50);
                
                return new TaskRunnerConfig(
                    context,
                    CreateNodes(rootDirectory, temporaryDirectory),
                    _icon,
                    temporaryDirectory);
            });
        }

        private IEnumerable<ITaskRunnerNode> CreateNodes(string rootDirectory, string temporaryDirectory)
        {
            var process = Process.Start(new ProcessStartInfo {
                FileName = "powershell",
                WorkingDirectory = rootDirectory,
                Arguments = "-ExecutionPolicy ByPass -NoProfile ./build.ps1 --help",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process.WaitForExit();
            if (process.ExitCode != 0)
                return new[] {CreateNode("Build project must not contain errors", rootDirectory)};

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(new CamelCaseNamingConvention())
                .Build();

            var completionFileName = "shell-completion.yml";
            var persistentCompletionFile = Path.Combine(rootDirectory, completionFileName);
            var temporaryCompletionFile = Path.Combine(temporaryDirectory, completionFileName);
            var completionFile = File.Exists(persistentCompletionFile)
                ? persistentCompletionFile
                : temporaryCompletionFile;
            var completionFileContent = File.ReadAllText(completionFile);
            var completionItems = deserializer.Deserialize<Dictionary<string, string[]>>(completionFileContent);

            var targets = completionItems["Target"];
            return targets.Select(x => CreateNode(x, rootDirectory, x));
        }

        private ITaskRunnerNode CreateNode(string description, string rootDirectory, string target = null) =>
            new TaskRunnerNode(description, true) {
                Description = description,
                Command = new TaskRunnerCommand(
                    rootDirectory,
                    "powershell",
                    $"-ExecutionPolicy ByPass -NoProfile ./build.ps1 {target}")
            };
    }
}