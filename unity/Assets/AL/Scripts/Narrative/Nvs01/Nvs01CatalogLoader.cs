using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AL.Narrative.Nvs01.Contracts;
using UnityEngine;
using UnityEngine.Networking;

namespace AL.Narrative.Nvs01
{
    public sealed class Nvs01CatalogLoader
    {
        private static readonly Nvs01CatalogLoader SharedInstance = new Nvs01CatalogLoader();

        private bool _isLoading;
        private Nvs01CatalogLoadResult _cachedResult;

        private Nvs01CatalogLoader()
        {
        }

        public static Nvs01CatalogLoader Shared => SharedInstance;
        public bool IsLoading => _isLoading;
        public bool HasResult => _cachedResult != null;
        public Nvs01CatalogLoadResult CachedResult => _cachedResult;

        public IEnumerator LoadOnce(Action<Nvs01CatalogLoadResult> completed)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));

            if (_cachedResult != null)
            {
                completed(_cachedResult);
                yield break;
            }

            if (_isLoading)
            {
                while (_isLoading)
                {
                    yield return null;
                }

                completed(_cachedResult);
                yield break;
            }

            _isLoading = true;
            try
            {
                byte[] bytes = null;
                Nvs01CatalogLoadResult transportFailure = null;
                string path = BuildStreamingAssetsPath();

                if (RequiresUnityWebRequest(path))
                {
                    UnityWebRequest request = null;
                    try
                    {
                        request = UnityWebRequest.Get(path);
                    }
                    catch (Exception)
                    {
                        transportFailure = Failure(
                            Nvs01CatalogLoadStatus.TransportFailed,
                            "CATALOG-MISSING",
                            "StreamingAssets catalog request could not be created.",
                            "readable packaged catalog",
                            "request creation failed");
                    }

                    if (request != null)
                    {
                        using (request)
                        {
                            UnityWebRequestAsyncOperation operation = null;
                            try
                            {
                                operation = request.SendWebRequest();
                            }
                            catch (Exception)
                            {
                                transportFailure = Failure(
                                    Nvs01CatalogLoadStatus.TransportFailed,
                                    "CATALOG-MISSING",
                                    "StreamingAssets catalog request could not be started.",
                                    "started package request",
                                    "request start failed");
                            }

                            if (operation != null)
                            {
                                yield return operation;
                                if (request.result != UnityWebRequest.Result.Success)
                                {
                                    bool notFound = request.responseCode == 404;
                                    transportFailure = Failure(
                                        notFound ? Nvs01CatalogLoadStatus.NotFound : Nvs01CatalogLoadStatus.TransportFailed,
                                        "CATALOG-MISSING",
                                        notFound
                                            ? "StreamingAssets catalog is missing."
                                            : "StreamingAssets catalog request failed.",
                                        "HTTP/package success",
                                        request.responseCode.ToString());
                                }
                                else
                                {
                                    bytes = request.downloadHandler.data;
                                }
                            }
                        }
                    }
                }
                else
                {
                    try
                    {
                        if (!File.Exists(path))
                        {
                            transportFailure = Failure(
                                Nvs01CatalogLoadStatus.NotFound,
                                "CATALOG-MISSING",
                                "StreamingAssets catalog is missing.",
                                Nvs01CatalogContract.StreamingAssetsRelativePath,
                                "missing");
                        }
                        else
                        {
                            bytes = File.ReadAllBytes(path);
                        }
                    }
                    catch (Exception)
                    {
                        transportFailure = Failure(
                            Nvs01CatalogLoadStatus.TransportFailed,
                            "CATALOG-MISSING",
                            "StreamingAssets catalog could not be read.",
                            "readable packaged catalog",
                            "read failed");
                    }
                }

                Publish(transportFailure ?? Validate(bytes));
            }
            finally
            {
                if (_isLoading)
                {
                    Publish(Failure(
                        Nvs01CatalogLoadStatus.TransportFailed,
                        "CATALOG-MISSING",
                        "StreamingAssets catalog load was interrupted.",
                        "completed one-time load",
                        "interrupted"));
                }
            }

            completed(_cachedResult);
        }

        internal Nvs01CatalogLoadResult LoadBytesOnceForTests(byte[] bytes)
        {
            if (_cachedResult == null)
            {
                Publish(Validate(bytes));
            }

            return _cachedResult;
        }

        private static Nvs01CatalogLoadResult Validate(byte[] bytes)
        {
            Nvs01CatalogValidationResult validation;
            try
            {
                validation = Nvs01CatalogValidator.ValidateCanonicalArtifact(bytes);
            }
            catch (Exception)
            {
                return Failure(
                    Nvs01CatalogLoadStatus.Rejected,
                    "CATALOG-MALFORMED",
                    "Catalog validation failed unexpectedly.",
                    "stable validation result",
                    "exception");
            }

            return new Nvs01CatalogLoadResult(
                validation.IsAccepted ? Nvs01CatalogLoadStatus.Succeeded : Nvs01CatalogLoadStatus.Rejected,
                validation.VerifiedCatalog,
                new List<Nvs01CatalogDiagnostic>(validation.Diagnostics));
        }

        private void Publish(Nvs01CatalogLoadResult result)
        {
            _cachedResult = result ?? throw new ArgumentNullException(nameof(result));
            _isLoading = false;
        }

        private static Nvs01CatalogLoadResult Failure(
            Nvs01CatalogLoadStatus status,
            string code,
            string message,
            string expected,
            string actual)
        {
            return new Nvs01CatalogLoadResult(
                status,
                null,
                new[]
                {
                    new Nvs01CatalogDiagnostic(
                        code,
                        Nvs01CatalogContract.StreamingAssetsRelativePath,
                        message,
                        expected,
                        actual)
                });
        }

        private static string BuildStreamingAssetsPath()
        {
            string root = Application.streamingAssetsPath;
            if (RequiresUnityWebRequest(root))
            {
                return root.TrimEnd('/', '\\') + "/" + Nvs01CatalogContract.StreamingAssetsRelativePath;
            }

            return Path.Combine(
                root,
                Nvs01CatalogContract.StreamingAssetsRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static bool RequiresUnityWebRequest(string path)
        {
            return path.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                   path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase);
        }
    }
}
