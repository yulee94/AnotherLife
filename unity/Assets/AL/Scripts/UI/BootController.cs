using System.Collections;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.UI
{
    public class BootController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _minSplashScreenTime = 2f;
        [SerializeField] private string _realmSelectionScene = "RealmSelection";
        [SerializeField] private string _kingdomScene = "Kingdom";

        [Header("Presentation")]
        [SerializeField] private bool _buildRuntimeSplash = true;
        [SerializeField] private string _buildLabel = "PRE-ALPHA RUNTIME";

        private const float ProgressWidth = 760f;
        private const float ProgressHeight = 8f;

        private readonly List<BootParticle> _particles = new List<BootParticle>(36);
        private readonly List<Image> _pulseTargets = new List<Image>(8);
        private CanvasGroup _splashGroup;
        private Image _progressFill;
        private Text _statusText;
        private Text _percentText;
        private RectTransform _scanLine;
        private RectTransform _leftGate;
        private RectTransform _rightGate;

        private IEnumerator Start()
        {
            Debug.Log("AL Boot Sequence Started...");
            Bootloader.InitializeIfMissing();

            if (_buildRuntimeSplash)
            {
                BuildRuntimeSplash();
            }

            yield return RunSplashSequence();

            var realmService = ServiceLocator.Get<IRealmService>();

            if (realmService.CurrentRealmId == RealmId.None)
            {
                Debug.Log("No Realm Selected. Transitioning to Realm Selection...");
                SceneManager.LoadScene(_realmSelectionScene);
            }
            else
            {
                Debug.Log($"Realm {realmService.CurrentRealmId} detected. Loading Kingdom...");
                SceneManager.LoadScene(_kingdomScene);
            }
        }

        private IEnumerator RunSplashSequence()
        {
            float duration = Mathf.Max(0.8f, _minSplashScreenTime);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                UpdateSplash(normalizedTime, elapsed);
                yield return null;
            }

            UpdateSplash(1f, duration);
            yield return new WaitForSecondsRealtime(0.12f);
        }

        private void BuildRuntimeSplash()
        {
            _particles.Clear();
            _pulseTargets.Clear();

            var canvasObject = new GameObject("BootSplashCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            _splashGroup = canvasObject.AddComponent<CanvasGroup>();
            _splashGroup.alpha = 0f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                       Resources.GetBuiltinResource<Font>("Arial.ttf");

            CreateGradientPanel(canvasObject.transform, "AtmosphereGradient", new Color(0.035f, 0.052f, 0.069f, 1f), new Color(0.006f, 0.008f, 0.012f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            CreateGradientPanel(canvasObject.transform, "LowerCommandGlow", new Color(0.020f, 0.030f, 0.040f, 0.08f), new Color(0.82f, 0.48f, 0.19f, 0.22f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 440f));

            var leftGlow = CreatePanel(canvasObject.transform, "LeftSignalGlow", new Color(0.25f, 0.53f, 0.72f, 0.32f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(210f, -40f), new Vector2(620f, 620f));
            leftGlow.sprite = CreateRadialGlowSprite("BootBlueGlow", new Color(0.25f, 0.53f, 0.72f, 0.50f), new Color(0.25f, 0.53f, 0.72f, 0f));
            var rightGlow = CreatePanel(canvasObject.transform, "RightSignalGlow", new Color(0.92f, 0.55f, 0.22f, 0.28f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-160f, 100f), new Vector2(680f, 680f));
            rightGlow.sprite = CreateRadialGlowSprite("BootGoldGlow", new Color(0.92f, 0.55f, 0.22f, 0.46f), new Color(0.92f, 0.55f, 0.22f, 0f));

            BuildCitadelSilhouette(canvasObject.transform);
            BuildGateSigil(canvasObject.transform);
            BuildParticleField(canvasObject.transform);

            var title = CreateText(canvasObject.transform, "Title", font, "ANOTHER LIFE", 54, new Vector2(0f, -318f), new Vector2(980f, 74f), TextAnchor.MiddleCenter);
            title.color = new Color(1f, 0.88f, 0.62f, 1f);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 32;
            title.resizeTextMaxSize = 54;

            var subtitle = CreateText(canvasObject.transform, "Subtitle", font, "REALM COMMAND SYSTEM", 18, new Vector2(0f, -374f), new Vector2(660f, 30f), TextAnchor.MiddleCenter);
            subtitle.color = new Color(0.74f, 0.84f, 0.93f, 0.90f);

            var buildLabel = CreateText(canvasObject.transform, "BuildLabel", font, _buildLabel, 13, new Vector2(0f, -409f), new Vector2(520f, 22f), TextAnchor.MiddleCenter);
            buildLabel.color = new Color(0.96f, 0.72f, 0.40f, 0.78f);

            CreatePanel(canvasObject.transform, "UpperGoldRule", new Color(1f, 0.78f, 0.36f, 0.62f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -282f), new Vector2(780f, 3f));
            CreatePanel(canvasObject.transform, "UpperBlueRule", new Color(0.24f, 0.50f, 0.72f, 0.38f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -288f), new Vector2(560f, 2f));

            _statusText = CreateText(canvasObject.transform, "Status", font, "INITIALIZING RUNTIME SERVICES", 16, new Vector2(-270f, -948f), new Vector2(520f, 30f), TextAnchor.MiddleLeft);
            _statusText.color = new Color(0.80f, 0.88f, 0.94f, 0.92f);
            _percentText = CreateText(canvasObject.transform, "Percent", font, "0%", 16, new Vector2(346f, -948f), new Vector2(120f, 30f), TextAnchor.MiddleRight);
            _percentText.color = new Color(1f, 0.82f, 0.46f, 0.92f);

            BuildProgressBar(canvasObject.transform);
        }

        private void BuildGateSigil(Transform parent)
        {
            var outer = CreatePanel(parent, "GateOuter", new Color(0.92f, 0.70f, 0.38f, 0.30f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(212f, 212f));
            outer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _pulseTargets.Add(outer);

            var mid = CreatePanel(parent, "GateMiddle", new Color(0.20f, 0.46f, 0.70f, 0.24f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(158f, 158f));
            mid.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _pulseTargets.Add(mid);

            var core = CreatePanel(parent, "GateCore", new Color(0.012f, 0.018f, 0.026f, 0.92f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(102f, 102f));
            core.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            CreatePanel(parent, "GateVerticalTrace", new Color(1f, 0.82f, 0.46f, 0.74f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(5f, 154f));
            CreatePanel(parent, "GateHorizontalTrace", new Color(0.28f, 0.56f, 0.78f, 0.56f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(154f, 4f));

            var scan = CreatePanel(parent, "GateScanLine", new Color(0.74f, 0.92f, 1f, 0.56f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(132f, 2f));
            _scanLine = scan.rectTransform;

            var left = CreatePanel(parent, "GateLeftPlate", new Color(0.013f, 0.020f, 0.029f, 0.94f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 90f), new Vector2(150f, 112f));
            var right = CreatePanel(parent, "GateRightPlate", new Color(0.013f, 0.020f, 0.029f, 0.94f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 90f), new Vector2(150f, 112f));
            _leftGate = left.rectTransform;
            _rightGate = right.rectTransform;
        }

        private void BuildCitadelSilhouette(Transform parent)
        {
            CreatePanel(parent, "HorizonRidgeFar", new Color(0.020f, 0.025f, 0.031f, 0.92f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 126f), new Vector2(1560f, 78f));
            CreatePanel(parent, "HorizonRidgeNear", new Color(0.008f, 0.012f, 0.017f, 0.98f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 66f), new Vector2(1920f, 116f));

            for (int i = 0; i < 11; i++)
            {
                float x = -820f + i * 164f;
                float height = 34f + (i % 4) * 20f;
                float width = 42f + (i % 3) * 16f;
                var tower = CreatePanel(parent, "CitadelTower_" + i, new Color(0.006f, 0.009f, 0.013f, 0.96f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 124f), new Vector2(width, height));
                if (i % 3 == 0)
                {
                    _pulseTargets.Add(CreatePanel(tower.transform, "TowerWindow", new Color(1f, 0.62f, 0.26f, 0.36f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(5f, 18f)));
                }
            }
        }

        private void BuildProgressBar(Transform parent)
        {
            CreatePanel(parent, "ProgressOuter", new Color(0.006f, 0.009f, 0.014f, 0.94f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 112f), new Vector2(ProgressWidth + 36f, 26f));
            CreatePanel(parent, "ProgressTopTrace", new Color(1f, 0.78f, 0.38f, 0.46f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 126f), new Vector2(ProgressWidth + 20f, 2f));
            CreatePanel(parent, "ProgressTrack", new Color(0.035f, 0.046f, 0.058f, 0.95f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 112f), new Vector2(ProgressWidth, ProgressHeight));
            _progressFill = CreateGradientPanel(parent, "ProgressFill", new Color(1f, 0.82f, 0.44f, 0.98f), new Color(0.24f, 0.58f, 0.82f, 0.98f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(-ProgressWidth * 0.5f, 112f), new Vector2(1f, ProgressHeight));
            _pulseTargets.Add(_progressFill);
        }

        private void BuildParticleField(Transform parent)
        {
            for (int i = 0; i < 36; i++)
            {
                float x = -910f + (i * 151f) % 1820f;
                float y = -760f + (i * 89f) % 920f;
                float size = 2.5f + i % 5;
                float alpha = 0.10f + (i % 6) * 0.025f;
                var image = CreatePanel(parent, "BootAsh_" + i, new Color(1f, 0.66f, 0.30f, alpha), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
                _particles.Add(new BootParticle
                {
                    Image = image,
                    RectTransform = image.rectTransform,
                    Velocity = new Vector2(-6f + i % 7 * 2.5f, 14f + i % 9 * 4f),
                    BaseAlpha = alpha,
                    Phase = i * 0.47f,
                    PulseSpeed = 1.1f + i % 5 * 0.18f
                });
            }
        }

        private void UpdateSplash(float normalizedTime, float elapsed)
        {
            float easedProgress = 1f - Mathf.Pow(1f - normalizedTime, 2.65f);
            float entryFade = Mathf.Clamp01(normalizedTime / 0.16f);

            if (_splashGroup != null)
            {
                _splashGroup.alpha = entryFade;
            }

            if (_progressFill != null)
            {
                _progressFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, ProgressWidth * easedProgress), ProgressHeight);
            }

            if (_percentText != null)
            {
                _percentText.text = Mathf.RoundToInt(easedProgress * 100f) + "%";
            }

            if (_statusText != null)
            {
                _statusText.text = GetStatusLine(normalizedTime);
            }

            if (_scanLine != null)
            {
                float scanY = Mathf.Lerp(36f, 144f, Mathf.PingPong(elapsed * 0.48f, 1f));
                _scanLine.anchoredPosition = new Vector2(0f, scanY);
            }

            if (_leftGate != null && _rightGate != null)
            {
                float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((normalizedTime - 0.18f) / 0.64f));
                _leftGate.anchoredPosition = new Vector2(Mathf.Lerp(-18f, -96f, open), 90f);
                _rightGate.anchoredPosition = new Vector2(Mathf.Lerp(18f, 96f, open), 90f);
            }

            for (int i = 0; i < _pulseTargets.Count; i++)
            {
                var image = _pulseTargets[i];
                if (image == null)
                {
                    continue;
                }

                Color color = image.color;
                float pulse = 0.74f + Mathf.Sin(elapsed * 1.65f + i * 0.93f) * 0.18f;
                color.a = Mathf.Clamp01(color.a * 0.86f + pulse * 0.14f);
                image.color = color;
            }

            AnimateParticles(elapsed);
        }

        private void AnimateParticles(float elapsed)
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                BootParticle particle = _particles[i];
                if (particle.RectTransform == null || particle.Image == null)
                {
                    continue;
                }

                Vector2 position = particle.RectTransform.anchoredPosition;
                position += particle.Velocity * Time.unscaledDeltaTime;
                position.x += Mathf.Sin(elapsed * 0.8f + particle.Phase) * Time.unscaledDeltaTime * 10f;

                if (position.y > 560f)
                {
                    position.y = -540f;
                    position.x = -910f + (i * 151f + Mathf.FloorToInt(elapsed * 37f)) % 1820f;
                }

                if (position.x > 960f)
                {
                    position.x = -960f;
                }
                else if (position.x < -960f)
                {
                    position.x = 960f;
                }

                particle.RectTransform.anchoredPosition = position;
                Color color = particle.Image.color;
                color.a = particle.BaseAlpha * (0.62f + Mathf.Sin(elapsed * particle.PulseSpeed + particle.Phase) * 0.28f);
                particle.Image.color = color;
            }
        }

        private static string GetStatusLine(float normalizedTime)
        {
            if (normalizedTime < 0.34f)
            {
                return "INITIALIZING RUNTIME SERVICES";
            }

            if (normalizedTime < 0.66f)
            {
                return "SYNCING SHARED CATALOGS";
            }

            if (normalizedTime < 0.92f)
            {
                return "PREPARING COMMAND ENTRY";
            }

            return "ENTERING REALM";
        }

        private static Image CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return image;
        }

        private static Image CreateGradientPanel(Transform parent, string name, Color topColor, Color bottomColor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var image = CreatePanel(parent, name, Color.white, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            image.sprite = CreateVerticalGradientSprite(name + "_Sprite", topColor, bottomColor);
            return image;
        }

        private static Sprite CreateVerticalGradientSprite(string name, Color topColor, Color bottomColor)
        {
            const int width = 8;
            const int height = 96;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name + "_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f);
                Color color = Color.Lerp(bottomColor, topColor, t);
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateRadialGlowSprite(string name, Color centerColor, Color edgeColor)
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name + "_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float t = Mathf.Clamp01(distance);
                    float falloff = 1f - Mathf.SmoothStep(0f, 1f, t);
                    texture.SetPixel(x, y, Color.Lerp(edgeColor, centerColor, falloff));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Text CreateText(Transform parent, string name, Font font, string textValue, int fontSize, Vector2 anchoredPosition, Vector2 sizeDelta, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.text = textValue;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return text;
        }

        private sealed class BootParticle
        {
            public Image Image;
            public RectTransform RectTransform;
            public Vector2 Velocity;
            public float BaseAlpha;
            public float Phase;
            public float PulseSpeed;
        }
    }
}
