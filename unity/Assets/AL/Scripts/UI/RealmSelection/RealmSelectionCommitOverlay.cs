using System;
using AL.Core;
using AL.UI.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.RealmSelection
{
    public sealed class RealmSelectionCommitOverlay : MonoBehaviour
    {
        public const string ConfirmButtonName = "BindRealmAction";
        public const string WithdrawButtonName = "WithdrawRealmAction";

        private Image _backdrop;
        private Image _emblem;
        private Text _title;
        private Text _realm;
        private Text _people;
        private Text _lockWarning;
        private Button _confirm;
        private Button _withdraw;
        private Action _onConfirm;
        private Action _onWithdraw;
        private bool _buttonListenersBound;

        public RealmId PendingRealmId { get; private set; }

        public bool IsVisible => gameObject.activeSelf && PendingRealmId != RealmId.None;

        public static RealmSelectionCommitOverlay Create(Transform parent, Font font)
        {
            var root = new GameObject("RealmSelectionCommitOverlay", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var overlay = root.AddComponent<RealmSelectionCommitOverlay>();
            overlay.Build(font);
            overlay.Hide();
            return overlay;
        }

        public void Bind(Action onConfirm, Action onWithdraw)
        {
            ResolveAuthoredReferences();
            BindButtonListeners();
            _onConfirm = onConfirm;
            _onWithdraw = onWithdraw;
        }

        public void Present(RealmIdentityPresentation identity, Sprite emblem)
        {
            PendingRealmId = identity.RuntimeId;
            if (_emblem != null)
            {
                _emblem.sprite = emblem;
                _emblem.enabled = emblem != null;
                _emblem.preserveAspect = true;
                _emblem.color = Color.white;
            }

            if (_title != null)
            {
                _title.text = "LOCK THIS REALM";
            }

            if (_realm != null)
            {
                _realm.text = identity.RealmName.ToUpperInvariant();
            }

            if (_people != null)
            {
                _people.text = identity.PeopleName + "  ·  " + identity.MarkName;
            }

            if (_lockWarning != null)
            {
                _lockWarning.text = RealmSelectionIdentity.LockWarningFallback;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            PendingRealmId = RealmId.None;
            gameObject.SetActive(false);
        }

        public bool TryConfirm()
        {
            if (!IsVisible)
            {
                return false;
            }

            _onConfirm?.Invoke();
            return true;
        }

        public void Withdraw()
        {
            _onWithdraw?.Invoke();
            Hide();
        }

        private void Build(Font font)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _backdrop = PresentationChrome.CreatePlate(
                transform,
                "CommitVeil",
                PresentationChrome.Veil,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                raycastTarget: true);

            Image plate = PresentationChrome.CreatePlate(
                transform,
                "CommitPlate",
                PresentationChrome.StonePlate,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(840f, 520f));
            var outline = plate.gameObject.AddComponent<Outline>();
            outline.effectColor = PresentationChrome.MetalEdge;
            outline.effectDistance = new Vector2(2f, -2f);

            PresentationChrome.CreatePlate(
                plate.transform,
                "CommitMetalRail",
                PresentationChrome.MetalEdge,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 0f),
                new Vector2(-48f, 3f));

            _emblem = PresentationChrome.CreatePlate(
                plate.transform,
                "CommitEmblem",
                Color.white,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -36f),
                new Vector2(128f, 128f));
            _emblem.preserveAspect = true;

            _title = PresentationChrome.CreateLabel(
                plate.transform,
                "CommitTitle",
                font,
                "LOCK THIS REALM",
                PresentationChrome.CaptionSize,
                PresentationChrome.MetalEdge,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -176f),
                new Vector2(720f, 22f));

            _realm = PresentationChrome.CreateLabel(
                plate.transform,
                "CommitRealm",
                font,
                string.Empty,
                34,
                PresentationChrome.Ink,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -206f),
                new Vector2(720f, 44f));

            _people = PresentationChrome.CreateLabel(
                plate.transform,
                "CommitPeople",
                font,
                string.Empty,
                PresentationChrome.PeopleSize,
                PresentationChrome.InkMuted,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -256f),
                new Vector2(720f, 24f));

            _lockWarning = PresentationChrome.CreateLabel(
                plate.transform,
                "CommitLock",
                font,
                RealmSelectionIdentity.LockWarningFallback,
                PresentationChrome.BodySize,
                PresentationChrome.InkFaint,
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -292f),
                new Vector2(720f, 56f));

            _withdraw = PresentationChrome.CreateHit(
                plate.transform,
                WithdrawButtonName,
                PresentationChrome.StoneInset,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-16f, 36f),
                new Vector2(280f, PresentationChrome.MinHit));
            PresentationChrome.CreateLabel(
                _withdraw.transform,
                "Label",
                font,
                "WITHDRAW",
                PresentationChrome.ActionSize,
                PresentationChrome.InkMuted,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);


            _confirm = PresentationChrome.CreateHit(
                plate.transform,
                ConfirmButtonName,
                new Color(0.16f, 0.15f, 0.13f, 1f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 0f),
                new Vector2(16f, 36f),
                new Vector2(280f, PresentationChrome.MinHit));
            var confirmEdge = _confirm.gameObject.AddComponent<Outline>();
            confirmEdge.effectColor = PresentationChrome.MetalEdge;
            confirmEdge.effectDistance = new Vector2(1.4f, -1.4f);
            PresentationChrome.CreateLabel(
                _confirm.transform,
                "Label",
                font,
                "BIND THIS REALM",
                PresentationChrome.ActionSize,
                PresentationChrome.Ink,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            BindButtonListeners();
        }

        private void ResolveAuthoredReferences()
        {
            _backdrop ??= FindComponent<Image>("CommitVeil");
            _emblem ??= FindComponent<Image>("CommitEmblem");
            _title ??= FindComponent<Text>("CommitTitle");
            _realm ??= FindComponent<Text>("CommitRealm");
            _people ??= FindComponent<Text>("CommitPeople");
            _lockWarning ??= FindComponent<Text>("CommitLock");
            _confirm ??= FindComponent<Button>(ConfirmButtonName);
            _withdraw ??= FindComponent<Button>(WithdrawButtonName);
        }

        private void BindButtonListeners()
        {
            if (_buttonListenersBound || _confirm == null || _withdraw == null)
            {
                return;
            }

            _confirm.onClick.AddListener(() => TryConfirm());
            _withdraw.onClick.AddListener(Withdraw);
            _buttonListenersBound = true;
        }

        private T FindComponent<T>(string objectName) where T : Component
        {
            Transform found = FindNamed(transform, objectName);
            return found != null ? found.GetComponent<T>() : null;
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamed(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
