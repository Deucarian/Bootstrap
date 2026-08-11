using System;
using UnityEngine.Networking;

namespace Deucarian.Bootstrap.Editor
{
    internal interface IBootstrapRemoteTextRequest : IDisposable
    {
        bool IsCompleted { get; }

        bool Succeeded { get; }

        string Text { get; }

        string ErrorMessage { get; }
    }

    internal interface IBootstrapRemoteTextRequestFactory
    {
        IBootstrapRemoteTextRequest Start(string url, int timeoutSeconds);
    }

    internal sealed class UnityBootstrapRemoteTextRequestFactory : IBootstrapRemoteTextRequestFactory
    {
        public IBootstrapRemoteTextRequest Start(string url, int timeoutSeconds)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = Math.Max(1, timeoutSeconds);
            request.SendWebRequest();
            return new UnityBootstrapRemoteTextRequest(request);
        }
    }

    internal sealed class UnityBootstrapRemoteTextRequest : IBootstrapRemoteTextRequest
    {
        private UnityWebRequest _request;

        public UnityBootstrapRemoteTextRequest(UnityWebRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public bool IsCompleted => _request == null || _request.isDone;

        public bool Succeeded =>
            _request != null &&
            _request.isDone &&
            _request.result == UnityWebRequest.Result.Success;

        public string Text =>
            Succeeded && _request.downloadHandler != null
                ? _request.downloadHandler.text ?? string.Empty
                : string.Empty;

        public string ErrorMessage
        {
            get
            {
                if (_request == null)
                {
                    return "The remote request is no longer available.";
                }

                return string.IsNullOrWhiteSpace(_request.error)
                    ? "The remote request failed."
                    : _request.error;
            }
        }

        public void Dispose()
        {
            if (_request == null)
            {
                return;
            }

            _request.Dispose();
            _request = null;
        }
    }
}
