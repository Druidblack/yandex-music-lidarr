using System;
using System.Net.Http;
using YandexMusicSharp;
using YandexMusicSharp.Auth;

namespace NzbDrone.Core.Plugins.YandexMusic
{
    /// <summary>
    /// Process-wide cache of the YandexMusicClient keyed by OAuth token.
    /// Reuses a single HttpClient across all calls so connection pooling
    /// and HTTP/2 multiplexing kick in.
    ///
    /// Each plugin lives in its own AssemblyLoadContext so the static here
    /// is scoped to the plugin assembly, not shared across plugins.
    /// </summary>
    public static class YandexMusicApi
    {
        private static readonly object SyncRoot = new();
        private static readonly HttpClient SharedHttpClient = new();
        private static YandexMusicClient? _cachedClient;
        private static string? _cachedToken;

        public static YandexMusicClient GetClient(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("Access token must not be empty.", nameof(accessToken));
            }

            lock (SyncRoot)
            {
                if (_cachedClient is not null && string.Equals(_cachedToken, accessToken, StringComparison.Ordinal))
                {
                    return _cachedClient;
                }

                _cachedClient?.Dispose();
                _cachedClient = new YandexMusicClient(new YandexMusicCredentials(accessToken), SharedHttpClient);
                _cachedToken = accessToken;
                return _cachedClient;
            }
        }

        internal static void Reset()
        {
            lock (SyncRoot)
            {
                _cachedClient?.Dispose();
                _cachedClient = null;
                _cachedToken = null;
            }
        }
    }
}
