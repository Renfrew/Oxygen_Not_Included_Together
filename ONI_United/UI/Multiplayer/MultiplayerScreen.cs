using ONI_United.Networking.Sessions;
using ONI_United.UI.Multiplayer.Views;
using System;
using System.Collections;
using Shared.UI;
using UnityEngine;
using ONI_United.Core;

namespace ONI_United.UI.Multiplayer
{
    // One coordinator per native dialog. Only the base dialog owns Pause and scene lifetime.
    internal sealed class MultiplayerScreen
    {
        private static MultiplayerScreen instance;
        private readonly MultiplayerDialogScreen dialog;
        private readonly MultiplayerScreen parent;
        private MultiplayerScreen child;
        private MultiplayerJoinView join;
        private MultiplayerHostView host;
        private MultiplayerSessionView session;
        private KScreen content;
        private LocText version;
        private PauseScreen pauseOwner;
        private CameraController camera;
        private readonly bool cameraWasDisabled;
        private Coroutine polling;
        private float nextRefresh;
        private bool sessionShown, closed, disposed;

        private MultiplayerScreen(MultiplayerDialogScreen dialog, MultiplayerScreen parent = null)
        {
            this.dialog = dialog;
            this.parent = parent;
            camera = CameraController.Instance;
            cameraWasDisabled = camera != null && camera.DisableUserCameraControl;
            dialog.Initialize(ModAssets.UiTemplates);
            dialog.SetHeader("Multiplayer: ONI United");
            dialog.AddVersion(MultiplayerMod.ModVersion, out version);
            dialog.Dismissing += OnDeactivated;
            dialog.Subscribe((int)GameHashes.Close, OnClosed);
        }

        internal static void Open(KScreen origin)
        {
            if (instance != null) return;

            ModAssets.InitializeUiTemplates();

            var owner = origin as PauseScreen;
            if (owner != null && owner.childDialog != null)
                throw new InvalidOperationException("The Pause menu already owns a child screen.");

            var dialog = Util.KInstantiateUI<MultiplayerDialogScreen>(
                ModAssets.ModUI.Dialog.gameObject,
                origin.transform.parent.gameObject);

            try
            {
                instance = new MultiplayerScreen(dialog);

                // Create the cached Join action before the native Close option, including when
                // opening directly into Session and revealing Join later.
                instance.join = new MultiplayerJoinView(instance, dialog);
                instance.RefreshActiveView();

                dialog.AddOption("CLOSE", d => d.Deactivate());

                // pause screen handling, same as vanilla Options panel
                if (owner != null)
                {
                    instance.pauseOwner = owner;
                    owner.childDialog = dialog.gameObject;
                    owner.Hide();
                }

                App.OnPreLoadScene += instance.OnSceneChanging;
                dialog.Activate();
                instance.polling = Global.Instance.StartCoroutine(instance.Poll());
            }
            catch
            {
                dialog.gameObject.SetActive(false);
                instance?.Dispose();
                UnityEngine.Object.Destroy(dialog.gameObject);
                throw;
            }
        }

        internal void OpenHostSetup()
        {
            if (closed || child != null || Session.IsActive) return;
            join.StopEditing();
            var next = Util.KInstantiateUI<MultiplayerDialogScreen>(ModAssets.ModUI.Dialog.gameObject,
                dialog.transform.parent.gameObject, false);
            try
            {
                child = new MultiplayerScreen(next, this);
                child.RefreshActiveView();
                next.AddOption(global::STRINGS.UI.CONFIRMDIALOG.CANCEL, d => d.Deactivate());
                next.Activate();
            }
            catch
            {
                next.gameObject.SetActive(false);
                child?.Dispose();
                child = null;
                UnityEngine.Object.Destroy(next.gameObject);
                throw;
            }
        }

        internal void RefreshActiveView()
        {
            if (closed) return;
            sessionShown = Session.IsActive || Lifecycle.HasHostOperation;
            KScreen next;
            if (sessionShown)
            {
                session ??= new MultiplayerSessionView(this, dialog);
                next = session.Content;
            }
            else if (parent != null)
            {
                host ??= new MultiplayerHostView(this, dialog);
                next = host.Content;
            }
            else
            {
                join ??= new MultiplayerJoinView(this, dialog);
                next = join.Content;
            }
            if (content != next)
            {
                if (content != null)
                {
                    if (content == host?.Content) host.Show(false);
                    else if (content == join?.Content) join.Show(false);
                    else content.Show(false);
                }
                content = next;
                if (content == host?.Content) host.Show(true);
                else if (content == join?.Content) join.Show(true);
                else content.Show(true);
            }
            version.gameObject.SetActive(parent == null || sessionShown);
            if (sessionShown) session.Refresh();
            else if (parent != null) host.RefreshAvailability();
            else join.RefreshStatus();
            nextRefresh = Time.unscaledTime + 0.25f;
        }

        private IEnumerator Poll()
        {
            while (!closed && dialog != null)
            {
                var visible = child ?? this;
                if (visible.sessionShown != (Session.IsActive || Lifecycle.HasHostOperation) || Time.unscaledTime >= visible.nextRefresh)
                    visible.RefreshActiveView();
                yield return null;
            }
        }

        private void OnDeactivated()
        {
            closed = true;
            join?.Dispose(); // Reject late lobby-list callbacks; native destruction owns the UI objects.
            host?.Dispose();
            if (child != null) child.dialog.Deactivate();
            if (parent != null)
            {
                parent.child = null;
                // Reveal Session, rather than stale Join controls, if the top dialog closed while connected.
                parent.RefreshActiveView();
            }
        }

        private void OnClosed(object data) => Dispose();

        private void OnSceneChanging()
        {
            var top = child;
            try { dialog.Deactivate(); }
            finally
            {
                top?.Dispose();
                Dispose(); // Scene loading clears Klei Close events and suppresses OnCmpDisable.
            }
        }

        private void Dispose()
        {
            if (disposed) return;
            disposed = true;
            closed = true;
            var top = child;
            child = null;
            if (top != null)
            {
                top.dialog.gameObject.SetActive(false);
                top.dialog.Deactivate();
                top.Dispose();
            }
            if (parent == null)
            {
                App.OnPreLoadScene -= OnSceneChanging;
                if (polling != null && Global.Instance != null) Global.Instance.StopCoroutine(polling);
                instance = null;
            }
            if (dialog != null)
            {
                dialog.Dismissing -= OnDeactivated;
                dialog.Unsubscribe((int)GameHashes.Close, OnClosed);
            }
            join?.Dispose();
            host?.Dispose();
            if (camera != null && camera == CameraController.Instance)
                camera.DisableUserCameraControl = parent != null && parent.closed ? parent.cameraWasDisabled : cameraWasDisabled;
            camera = null;
            if (pauseOwner != null && pauseOwner.childDialog == dialog.gameObject)
                pauseOwner.Unhide();
            pauseOwner = null;
        }

    }
}
