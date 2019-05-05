using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TaskRunnerExplorer;

namespace Nuke.VisualStudio.TaskRunner
{
    internal class TaskRunnerConfig : ITaskRunnerConfig
    {
        private readonly FileSystemWatcher _watcher;

        public TaskRunnerConfig(
            ITaskRunnerCommandContext context,
            IEnumerable<ITaskRunnerNode> nodes,
            ImageSource icon,
            string temporaryDirectory)
        {
            Context = context;
            Icon = icon;
            TaskHierarchy = new TaskRunnerNode("NUKE_ROOT");
            TaskHierarchy.Children.AddRange(nodes);

            if (Directory.Exists(temporaryDirectory))
            {
                _watcher = new FileSystemWatcher(temporaryDirectory, "visual-studio-debug.log")
                {
                    EnableRaisingEvents = true
                };
                _watcher.Changed += (s, e) =>
                {
                    ThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        AttachToBuild(e.FullPath);
                    });
                };
            }
        }

        public ITaskRunnerCommandContext Context { get; }
        public ITaskRunnerNode TaskHierarchy { get; }
        public ImageSource Icon { get; }

        public void Dispose()
        {
            _watcher.Dispose();
        }

        private void AttachToBuild(string debugFile)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            
            var processes = NukePackage.Dte.Debugger.LocalProcesses.Cast<Process>();
            var processId = int.Parse(File.ReadAllText(debugFile));
            var process = processes.FirstOrDefault(x => x.ProcessID == processId);
            try
            {
                process.Attach();
            }
            catch (Exception exception)
            {
                NukePackage.Dte.StatusBar.Text = exception.Message;
            }
            finally
            {
                File.Delete(debugFile);
            }
        }

        public string LoadBindings(string configPath)
        {
            return "<binding />";
        }

        public bool SaveBindings(string configPath, string bindingsXml)
        {
            return false;
        }
    }
}