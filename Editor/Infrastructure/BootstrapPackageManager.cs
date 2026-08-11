using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace Deucarian.Bootstrap.Editor
{
    internal sealed class BootstrapPackageRecord
    {
        public BootstrapPackageRecord(string packageId, string version, string source, string packageReference)
        {
            PackageId = packageId ?? string.Empty;
            Version = version ?? string.Empty;
            Source = source ?? string.Empty;
            PackageReference = packageReference ?? string.Empty;
        }

        public string PackageId { get; }

        public string Version { get; }

        public string Source { get; }

        public string PackageReference { get; }
    }

    internal interface IBootstrapPackageManagerRequest : IDisposable
    {
        bool IsCompleted { get; }

        bool Succeeded { get; }

        string ErrorMessage { get; }

        IReadOnlyList<BootstrapPackageRecord> Packages { get; }
    }

    internal interface IBootstrapPackageManager
    {
        IBootstrapPackageManagerRequest List();

        IBootstrapPackageManagerRequest Add(string packageReference);

        IBootstrapPackageManagerRequest Remove(string packageId);
    }

    internal sealed class UnityBootstrapPackageManager : IBootstrapPackageManager
    {
        public IBootstrapPackageManagerRequest List()
        {
            return new UnityBootstrapListRequest(Client.List(true, true));
        }

        public IBootstrapPackageManagerRequest Add(string packageReference)
        {
            return new UnityBootstrapOperationRequest(Client.Add(packageReference));
        }

        public IBootstrapPackageManagerRequest Remove(string packageId)
        {
            return new UnityBootstrapOperationRequest(Client.Remove(packageId));
        }
    }

    internal sealed class UnityBootstrapListRequest : IBootstrapPackageManagerRequest
    {
        private readonly ListRequest _request;
        private IReadOnlyList<BootstrapPackageRecord> _packages;

        public UnityBootstrapListRequest(ListRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public bool IsCompleted => _request.IsCompleted;

        public bool Succeeded => IsCompleted && _request.Status == StatusCode.Success;

        public string ErrorMessage =>
            _request.Error != null && !string.IsNullOrWhiteSpace(_request.Error.message)
                ? _request.Error.message
                : "Unity Package Manager list request failed.";

        public IReadOnlyList<BootstrapPackageRecord> Packages
        {
            get
            {
                if (_packages != null)
                {
                    return _packages;
                }

                if (!Succeeded || _request.Result == null)
                {
                    return Array.Empty<BootstrapPackageRecord>();
                }

                _packages = _request.Result
                    .Where(package => package != null)
                    .Select(package => new BootstrapPackageRecord(
                        package.name,
                        package.version,
                        package.source.ToString(),
                        package.packageId))
                    .ToArray();
                return _packages;
            }
        }

        public void Dispose()
        {
        }
    }

    internal sealed class UnityBootstrapOperationRequest : IBootstrapPackageManagerRequest
    {
        private readonly Request _request;

        public UnityBootstrapOperationRequest(Request request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public bool IsCompleted => _request.IsCompleted;

        public bool Succeeded => IsCompleted && _request.Status == StatusCode.Success;

        public string ErrorMessage =>
            _request.Error != null && !string.IsNullOrWhiteSpace(_request.Error.message)
                ? _request.Error.message
                : "Unity Package Manager operation failed.";

        public IReadOnlyList<BootstrapPackageRecord> Packages =>
            Array.Empty<BootstrapPackageRecord>();

        public void Dispose()
        {
        }
    }
}
