using System;
using System.IO;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class BootstrapCatalogSelection
    {
        private BootstrapCatalogSelection(
            bool success,
            BootstrapPackageCatalog catalog,
            BootstrapInstallPlanResult plan,
            BootstrapCatalogOrigin origin,
            string source,
            string notice,
            string errorMessage)
        {
            Success = success;
            Catalog = catalog;
            Plan = plan ?? BootstrapInstallPlanResult.CreateFailure(errorMessage);
            Origin = origin;
            Source = source ?? string.Empty;
            Notice = notice ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool Success { get; }

        public BootstrapPackageCatalog Catalog { get; }

        public BootstrapInstallPlanResult Plan { get; }

        public BootstrapCatalogOrigin Origin { get; }

        public string Source { get; }

        public string Notice { get; }

        public string ErrorMessage { get; }

        public static BootstrapCatalogSelection CreateSuccess(
            BootstrapPackageCatalog catalog,
            BootstrapInstallPlanResult plan,
            BootstrapCatalogOrigin origin,
            string source,
            string notice)
        {
            return new BootstrapCatalogSelection(true, catalog, plan, origin, source, notice, string.Empty);
        }

        public static BootstrapCatalogSelection CreateFailure(string errorMessage)
        {
            return new BootstrapCatalogSelection(
                false,
                null,
                null,
                BootstrapCatalogOrigin.None,
                string.Empty,
                string.Empty,
                errorMessage);
        }
    }

    internal interface IBootstrapCatalogLoader : IDisposable
    {
        bool IsCompleted { get; }

        BootstrapCatalogSelection Selection { get; }

        void Begin(BootstrapChannel channel);

        void Tick();
    }

    internal interface IBootstrapFallbackCatalogSource
    {
        bool TryRead(out string json, out string errorMessage);
    }

    internal sealed class BootstrapPackageFallbackCatalogSource : IBootstrapFallbackCatalogSource
    {
        public bool TryRead(out string json, out string errorMessage)
        {
            json = string.Empty;
            errorMessage = string.Empty;

            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(BootstrapPackageFallbackCatalogSource).Assembly);
            string packageRoot = packageInfo != null ? packageInfo.resolvedPath : Application.dataPath;
            string relativePath = DeucarianBootstrapPackageConstants.FallbackCatalogRelativePath
                .Replace('/', Path.DirectorySeparatorChar);
            string path = Path.Combine(packageRoot, relativePath);

            try
            {
                if (!File.Exists(path))
                {
                    errorMessage = "Bundled fallback catalog was not found at " + path + ".";
                    return false;
                }

                json = File.ReadAllText(path);
                return true;
            }
            catch (IOException exception)
            {
                errorMessage = "Bundled fallback catalog could not be read: " + exception.Message;
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                errorMessage = "Bundled fallback catalog could not be read: " + exception.Message;
                return false;
            }
        }
    }

    internal sealed class BootstrapCatalogLoader : IBootstrapCatalogLoader
    {
        private const int RemoteTimeoutSeconds = 15;

        private readonly IBootstrapFallbackCatalogSource _fallbackSource;
        private readonly IBootstrapRemoteTextRequestFactory _remoteFactory;
        private BootstrapChannel _channel;
        private IBootstrapRemoteTextRequest _remoteRequest;
        private BootstrapCatalogSelection _fallbackSelection;

        public BootstrapCatalogLoader(
            IBootstrapFallbackCatalogSource fallbackSource,
            IBootstrapRemoteTextRequestFactory remoteFactory)
        {
            _fallbackSource = fallbackSource ?? throw new ArgumentNullException(nameof(fallbackSource));
            _remoteFactory = remoteFactory ?? throw new ArgumentNullException(nameof(remoteFactory));
            Selection = BootstrapCatalogSelection.CreateFailure("Catalog has not been loaded.");
        }

        public bool IsCompleted { get; private set; }

        public BootstrapCatalogSelection Selection { get; private set; }

        public void Begin(BootstrapChannel channel)
        {
            DisposeRemoteRequest();
            _channel = channel;
            IsCompleted = false;
            Selection = BootstrapCatalogSelection.CreateFailure("Catalog is loading.");
            _fallbackSelection = LoadFallback(channel);

            try
            {
                _remoteRequest = _remoteFactory.Start(
                    BootstrapChannelUtility.GetRegistryCatalogUrl(channel),
                    RemoteTimeoutSeconds);
                if (_remoteRequest == null)
                {
                    FinishWithFallback(
                        "Remote Package Registry request did not start.");
                }
            }
            catch (Exception exception)
            {
                FinishWithFallback("Remote Package Registry request could not start: " +
                    exception.GetBaseException().Message);
            }
        }

        public void Tick()
        {
            if (IsCompleted || _remoteRequest == null || !_remoteRequest.IsCompleted)
            {
                return;
            }

            IBootstrapRemoteTextRequest request = _remoteRequest;
            _remoteRequest = null;

            if (request.Succeeded)
            {
                BootstrapCatalogSelection remote = TryCreateSelection(
                    request.Text,
                    _channel,
                    BootstrapCatalogOrigin.Remote,
                    "Remote Package Registry #" + BootstrapChannelUtility.GetGitBranch(_channel),
                    string.Empty);
                request.Dispose();

                if (remote.Success)
                {
                    Selection = remote;
                    IsCompleted = true;
                    return;
                }

                FinishWithFallback("Remote Package Registry was invalid: " + remote.ErrorMessage);
                return;
            }

            string error = request.ErrorMessage;
            request.Dispose();
            FinishWithFallback("Remote Package Registry is unavailable: " + error);
        }

        public void Dispose()
        {
            DisposeRemoteRequest();
        }

        private BootstrapCatalogSelection LoadFallback(BootstrapChannel channel)
        {
            if (!_fallbackSource.TryRead(out string json, out string readError))
            {
                return BootstrapCatalogSelection.CreateFailure(readError);
            }

            return TryCreateSelection(
                json,
                channel,
                BootstrapCatalogOrigin.BundledFallback,
                "Bundled setup fallback",
                string.Empty);
        }

        private void FinishWithFallback(string remoteProblem)
        {
            if (_fallbackSelection != null && _fallbackSelection.Success)
            {
                Selection = BootstrapCatalogSelection.CreateSuccess(
                    _fallbackSelection.Catalog,
                    _fallbackSelection.Plan,
                    BootstrapCatalogOrigin.BundledFallback,
                    _fallbackSelection.Source,
                    remoteProblem + " Using the validated bundled setup fallback.");
            }
            else
            {
                string fallbackError = _fallbackSelection != null
                    ? _fallbackSelection.ErrorMessage
                    : "Bundled fallback was not loaded.";
                Selection = BootstrapCatalogSelection.CreateFailure(
                    remoteProblem + " Bundled fallback is unavailable or invalid: " + fallbackError);
            }

            IsCompleted = true;
        }

        private static BootstrapCatalogSelection TryCreateSelection(
            string json,
            BootstrapChannel channel,
            BootstrapCatalogOrigin origin,
            string source,
            string notice)
        {
            if (!BootstrapCatalogParser.TryParse(json, out BootstrapPackageCatalog catalog, out string parseError))
            {
                return BootstrapCatalogSelection.CreateFailure(parseError);
            }

            BootstrapInstallPlanResult plan = BootstrapSetupPlanner.Build(catalog, channel);
            return plan.Success
                ? BootstrapCatalogSelection.CreateSuccess(catalog, plan, origin, source, notice)
                : BootstrapCatalogSelection.CreateFailure(plan.ErrorMessage);
        }

        private void DisposeRemoteRequest()
        {
            if (_remoteRequest == null)
            {
                return;
            }

            _remoteRequest.Dispose();
            _remoteRequest = null;
        }
    }
}
