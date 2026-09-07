using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AL.QA.RealmSlice
{
    [DefaultExecutionOrder(-31900)]
    [DisallowMultipleComponent]
    public sealed class RealmSliceEvidenceRunner : MonoBehaviour
    {
        public const string HostObjectName = "AL Realm Slice Evidence Runner";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isEditor) return;
            string[] arguments = Environment.GetCommandLineArgs();
            if (!RealmSliceEvidenceRequestParser.IsRequested(arguments)) return;
            Application.runInBackground = true;
            var host = new GameObject(HostObjectName);
            DontDestroyOnLoad(host);
            host.AddComponent<RealmSliceEvidenceRunner>();
        }

        private IEnumerator Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (!RealmSliceEvidenceRequestParser.TryParse(
                    arguments,
                    out RealmSliceEvidenceRequest request,
                    out string parseDiagnostic))
            {
                Debug.LogError("[AL-RSQ] " + parseDiagnostic);
                Exit(2);
                yield break;
            }

            string gameData = ResolveGameDataDirectory();
            string policyPath = Path.Combine(gameData, RealmSliceEvidenceRequestParser.PolicyFileName);
            string catalogPath = Path.Combine(gameData, RealmSliceEvidenceRequestParser.ScenarioCatalogFileName);
            string envelopePath = Path.Combine(request.EvidenceOutputRoot, "run-envelope.json");

            string policyJson = TryReadText(policyPath);
            byte[] catalogBytes = TryReadBytes(catalogPath);
            string catalogJson = catalogBytes != null
                ? Encoding.UTF8.GetString(catalogBytes)
                : null;
            if (!string.IsNullOrEmpty(catalogJson) && catalogJson[0] == '\uFEFF')
                catalogJson = catalogJson.Substring(1);
            string envelopeJson = File.Exists(envelopePath) ? TryReadText(envelopePath) : null;

            var capture = new RealmSliceCameraCapture();
            bool completed = RealmSliceEvidenceSession.TryExecute(
                request,
                policyJson,
                catalogJson,
                catalogBytes,
                envelopeJson,
                capture,
                out RealmSliceEvidenceResult result,
                out RealmSliceEvidenceLayout layout,
                out string diagnostic);
            string resultPath = layout != null ? layout.ResultPath : Path.Combine(request.EvidenceOutputRoot, "result.json");
            Debug.Log(
                "[AL-RSQ] diagnostic=" + diagnostic +
                " result=" + (result != null ? result.TechnicalResult : "none") +
                " path=" + resultPath);
            Exit(completed || (result != null && File.Exists(resultPath)) ? 0 : 2);
            yield break;
        }

        internal static string ResolveGameDataDirectory()
        {
            if (Application.isEditor)
            {
                return Path.Combine(Application.dataPath, "AL", "StreamingAssets", "GameData");
            }

            return Path.Combine(Application.streamingAssetsPath, "GameData");
        }

        private static string TryReadText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path, new UTF8Encoding(false)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static byte[] TryReadBytes(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Exit(int exitCode)
        {
            if (exitCode != 0)
                Debug.LogError("[AL-RSQ-EXIT] " + exitCode);
#if UNITY_EDITOR
            EditorApplication.Exit(exitCode);
#else
            Application.Quit(exitCode);
#endif
        }
    }

    internal sealed class RealmSliceCameraCapture : IRealmSliceEvidenceCapture
    {
        public bool TryCaptureStill(string outputPath, out string diagnostic)
        {
            return TryCaptureImage(outputPath, jpeg: false, out diagnostic);
        }

        public bool TryCaptureVideo(string outputPath, out string diagnostic)
        {
            string tempJpeg = outputPath + ".jpg";
            try
            {
                if (!TryCaptureImage(tempJpeg, jpeg: true, out diagnostic))
                    return false;
                byte[] jpeg = File.ReadAllBytes(tempJpeg);
                WriteMjpegAvi(outputPath, jpeg, 1280, 720);
                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length <= 0)
                {
                    diagnostic = "AVI mux produced no artifact";
                    return false;
                }

                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempJpeg)) File.Delete(tempJpeg);
                }
                catch (Exception)
                {
                }
            }
        }

        public bool TryCapturePerformance(
            string telemetryPath,
            string profilerPath,
            double warmupSeconds,
            double measuredSeconds,
            out string diagnostic)
        {
            if (warmupSeconds < RealmSliceEvidenceSession.MinimumWarmupSeconds ||
                measuredSeconds < RealmSliceEvidenceSession.MinimumMeasuredSeconds)
            {
                diagnostic = "AL-RSQ-PERF-DURATION-INVALID";
                return false;
            }

            diagnostic = "AL-RSQ-PERF-CAPTURE-UNAVAILABLE";
            return false;
        }

        private static bool TryCaptureImage(string outputPath, bool jpeg, out string diagnostic)
        {
            diagnostic = string.Empty;
            Camera camera = Camera.main;
            if (camera == null) camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            GameObject created = null;
            if (camera == null)
            {
                created = new GameObject("AL RSQ Capture Camera");
                camera = created.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
            }

            const int width = 1280;
            const int height = 720;
            RenderTexture target = new RenderTexture(width, height, 24);
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
                target.Create();
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                image.Apply(false, false);
                byte[] encoded = jpeg ? image.EncodeToJPG(85) : image.EncodeToPNG();
                if (encoded == null || encoded.Length == 0)
                {
                    diagnostic = "camera encode produced no bytes";
                    return false;
                }

                File.WriteAllBytes(outputPath, encoded);
                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length <= 0)
                {
                    diagnostic = "image capture produced no artifact";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                diagnostic = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.Destroy(image);
                UnityEngine.Object.Destroy(target);
                if (created != null) UnityEngine.Object.Destroy(created);
            }
        }

        private static void WriteMjpegAvi(string path, byte[] jpeg, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            int jpegSize = jpeg.Length;
            int moviSize = 4 + 4 + ((jpegSize + 1) & ~1);
            int hdrlSize = 4 + 8 + 56 + 8 + 4 + 8 + 48;
            int riffSize = 4 + (8 + hdrlSize) + (8 + moviSize);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream))
            {
                WriteFourCC(writer, "RIFF");
                writer.Write(riffSize);
                WriteFourCC(writer, "AVI ");
                WriteFourCC(writer, "LIST");
                writer.Write(hdrlSize);
                WriteFourCC(writer, "hdrl");
                WriteFourCC(writer, "avih");
                writer.Write(56);
                writer.Write(333333);
                writer.Write(jpegSize);
                writer.Write(0);
                writer.Write(0x110);
                writer.Write(1);
                writer.Write(0);
                writer.Write(1);
                writer.Write(width);
                writer.Write(height);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                WriteFourCC(writer, "LIST");
                writer.Write(4 + 8 + 48);
                WriteFourCC(writer, "strl");
                WriteFourCC(writer, "strh");
                writer.Write(48);
                WriteFourCC(writer, "vids");
                WriteFourCC(writer, "MJPG");
                writer.Write(0);
                writer.Write((short)0);
                writer.Write((short)0);
                writer.Write(0);
                writer.Write(1);
                writer.Write(3);
                writer.Write(0);
                writer.Write(1);
                writer.Write(jpegSize);
                writer.Write(0);
                writer.Write((short)0);
                writer.Write((short)0);
                WriteFourCC(writer, "LIST");
                writer.Write(moviSize);
                WriteFourCC(writer, "movi");
                WriteFourCC(writer, "00dc");
                writer.Write(jpegSize);
                writer.Write(jpeg);
                if ((jpegSize & 1) == 1) writer.Write((byte)0);
            }
        }

        private static void WriteFourCC(BinaryWriter writer, string code)
        {
            writer.Write(Encoding.ASCII.GetBytes(code));
        }
    }
}
