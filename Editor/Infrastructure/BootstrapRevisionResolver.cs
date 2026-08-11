using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class BootstrapRevisionResult
    {
        private BootstrapRevisionResult(bool success, string revision, string errorMessage)
        {
            Success = success;
            Revision = revision ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool Success { get; }

        public string Revision { get; }

        public string ErrorMessage { get; }

        public static BootstrapRevisionResult CreateSuccess(string revision)
        {
            return new BootstrapRevisionResult(true, revision, string.Empty);
        }

        public static BootstrapRevisionResult CreateFailure(string errorMessage)
        {
            return new BootstrapRevisionResult(false, string.Empty, errorMessage);
        }
    }

    internal interface IBootstrapRevisionRequest : IDisposable
    {
        bool IsCompleted { get; }

        BootstrapRevisionResult Result { get; }
    }

    internal interface IBootstrapRevisionResolver
    {
        IBootstrapRevisionRequest Resolve(string gitUrl, string branch);
    }

    internal sealed class GitBootstrapRevisionResolver : IBootstrapRevisionResolver
    {
        public IBootstrapRevisionRequest Resolve(string gitUrl, string branch)
        {
            return new TaskBootstrapRevisionRequest(Task.Run(() => ResolveSynchronously(gitUrl, branch)));
        }

        internal static BootstrapRevisionResult ResolveSynchronously(string gitUrl, string branch)
        {
            if (string.IsNullOrWhiteSpace(gitUrl) || string.IsNullOrWhiteSpace(branch))
            {
                return BootstrapRevisionResult.CreateFailure("The target Git URL or branch is empty.");
            }

            string remoteUrl = NormalizeRemoteUrl(gitUrl);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-remote --heads " + Quote(remoteUrl) + " " + Quote("refs/heads/" + branch),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return BootstrapRevisionResult.CreateFailure("Git could not be started.");
                    }

                    if (!process.WaitForExit(15000))
                    {
                        process.Kill();
                        return BootstrapRevisionResult.CreateFailure("Git revision lookup timed out.");
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    if (process.ExitCode != 0)
                    {
                        return BootstrapRevisionResult.CreateFailure(
                            string.IsNullOrWhiteSpace(error) ? "Git revision lookup failed." : error.Trim());
                    }

                    string revision = (output ?? string.Empty)
                        .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault();
                    return IsFullRevision(revision)
                        ? BootstrapRevisionResult.CreateSuccess(revision)
                        : BootstrapRevisionResult.CreateFailure("Git did not return a full branch revision.");
                }
            }
            catch (Exception exception)
            {
                return BootstrapRevisionResult.CreateFailure(exception.GetBaseException().Message);
            }
        }

        private static string NormalizeRemoteUrl(string gitUrl)
        {
            string value = (gitUrl ?? string.Empty).Trim();
            if (value.StartsWith("git+", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring("git+".Length);
            }

            int fragment = value.IndexOf('#');
            return fragment >= 0 ? value.Substring(0, fragment) : value;
        }

        private static bool IsFullRevision(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Length == 40 &&
                   value.All(character =>
                       (character >= '0' && character <= '9') ||
                       (character >= 'a' && character <= 'f') ||
                       (character >= 'A' && character <= 'F'));
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }

    internal sealed class TaskBootstrapRevisionRequest : IBootstrapRevisionRequest
    {
        private Task<BootstrapRevisionResult> _task;

        public TaskBootstrapRevisionRequest(Task<BootstrapRevisionResult> task)
        {
            _task = task ?? throw new ArgumentNullException(nameof(task));
        }

        public bool IsCompleted => _task == null || _task.IsCompleted;

        public BootstrapRevisionResult Result
        {
            get
            {
                if (_task == null)
                {
                    return BootstrapRevisionResult.CreateFailure("Revision request was discarded during reload.");
                }

                if (!_task.IsCompleted)
                {
                    return BootstrapRevisionResult.CreateFailure("Revision request is still running.");
                }

                try
                {
                    return _task.GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    return BootstrapRevisionResult.CreateFailure(exception.GetBaseException().Message);
                }
            }
        }

        public void Dispose()
        {
            _task = null;
        }
    }
}
