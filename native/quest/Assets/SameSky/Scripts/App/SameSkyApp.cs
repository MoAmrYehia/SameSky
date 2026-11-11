// SameSky native app: the root of the immersive experience.
// One seated lounge; one panel in front of the person at a time (comfortable reach, below eye level);
// a dock of hand-operable buttons at the table edge; spatial objects (cup, frames, screen, shelf) that carry
// the content. Everything here works with hands alone; controllers are optional.
//
// Flow: Launch (loading, seated calibration, sound/captions, sample or account)
//       -> Sample  (bundled fictional day, offline)          or
//       -> Live    (sign in / create account -> first-run setup -> the room)
// Partial files: Launch, Setup, Room, Story, Share, Together, Keepsakes, Settings.

using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using SameSky.Api;
using SameSky.Interaction;
using SameSky.UI;
using TMPro;
using UnityEngine;

namespace SameSky.App
{
    public sealed partial class SameSkyApp : MonoBehaviour
    {
        public static SameSkyApp Instance { get; private set; }

        // ------------------------------------------------------------ scene references (found by name)
        Transform _world;       // room content root ("Room")
        Transform _anchors;     // named anchors created by the project setup ("Room/Anchors")
        Transform _playerRoot;  // parent of the camera rig, moved by Recenter
        Transform _head;        // centre eye
        Transform _uiRoot;

        // ------------------------------------------------------------ state
        public IRoomData Data { get; private set; }
        public ApiClient Api { get; private set; }
        public Room CurrentRoom { get; private set; }
        public string Screen { get; private set; } = "boot";
        public bool Paused { get; private set; }
        public float SeatedEyeHeight = 1.18f;

        CancellationTokenSource _screenCts = new();
        Transform _panel;
        Renderer _panelBg;
        Transform _dock;
        TextMeshPro _toast;
        float _toastUntil;
        SpatialKeyboard _keyboard;
        TextMeshPro _status;

        public event Action<string> ScreenChanged;

        // ------------------------------------------------------------ lifecycle

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 72;
            Theme.TextScale = Prefs.Current.textScale;
            AudioListener.volume = Prefs.Current.soundOn ? Prefs.Current.volume : 0f;
            _world = GameObject.Find("Room")?.transform ?? new GameObject("Room").transform;
            _anchors = _world.Find("Anchors") ?? new GameObject("Anchors").transform;
            _uiRoot = new GameObject("SameSky UI").transform;
            Api = new ApiClient(AppConfig.ApiUrl);
            Api.Unauthorized += OnUnauthorized;
            if (TestProbe.Enabled) gameObject.AddComponent<TestProbe>();
        }

        IEnumerator Start()
        {
            var rig = FindAnyObjectByType<OVRCameraRig>();
            if (rig != null)
            {
                _head = rig.centerEyeAnchor;
                _playerRoot = rig.transform.parent != null ? rig.transform.parent : rig.transform;
            }
            else
            {
                // Desktop preview without the rig (never the judged path): a plain camera at the seat.
                var cam = Camera.main != null ? Camera.main.transform : new GameObject("Preview Camera", typeof(Camera)).transform;
                cam.position = new Vector3(0, SeatedEyeHeight, 0);
                _head = cam;
                _playerRoot = cam;
            }
            BuildDock();
            BuildToast();
            yield return null; // let the XR session start and tracking settle for a frame
            yield return new WaitForSeconds(0.4f);
            Recenter(silent: true);
            ShowLaunch();
        }

        void Update()
        {
            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil) _toast.gameObject.SetActive(false);
            KeepPanelInView();
            UpdateRoom();
            UpdateStory();
            UpdateTogether();
            PollForNewMembers();
        }

        void OnApplicationPause(bool paused) => SetPaused(paused, "system");
        void OnApplicationFocus(bool focus) => SetPaused(!focus, "focus");

        /// <summary>Headset removed, system menu opened or app backgrounded: audio and video stop; resume picks up where it was.</summary>
        void SetPaused(bool paused, string reason)
        {
            if (Paused == paused) return;
            Paused = paused;
            AudioListener.pause = paused;
            PauseStory(paused);
            TestProbe.Event("pause", paused ? reason : "resume");
            if (!paused && Screen == "room") Toast("Welcome back.");
        }

        // ------------------------------------------------------------ positions (seated)

        public Vector3 HeadPosition => _head != null ? _head.position : new Vector3(0, SeatedEyeHeight, 0);
        public Transform Head => _head;
        public Transform Anchor(string name) => _anchors.Find(name);

        /// <summary>Where a panel sits: in front of the seat, a little below the eyes, within a fingertip's reach.</summary>
        Vector3 PanelPose(out Quaternion rotation, float distance = 0.5f, float drop = 0.2f)
        {
            var anchor = Anchor("Panel");
            var pos = anchor != null ? anchor.position : new Vector3(0, SeatedEyeHeight - drop, distance);
            rotation = Quaternion.LookRotation((pos - HeadPosition).normalized, Vector3.up);
            return pos;
        }

        void KeepPanelInView()
        {
            if (_panel == null || !_panel.gameObject.activeSelf) return;
            // Panels are placed once per screen; only re-face the person if they leaned far (comfort: no swimming).
            var to = _panel.position - HeadPosition;
            if (Vector3.Angle(_panel.forward, to) > 25f) _panel.rotation = Quaternion.Slerp(_panel.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), Time.unscaledDeltaTime * 2f);
        }

        /// <summary>Re-seats the person: their head goes to the chair's eye point, facing the table. Yaw only; no swimming.</summary>
        public void Recenter(bool silent = false)
        {
            if (_head == null || _playerRoot == null || _head == _playerRoot) return;
            var seat = Anchor("SeatEye");
            var target = seat != null ? seat.position : new Vector3(0, SeatedEyeHeight, 0);
            target.y += Prefs.Current.seatHeight;
            var fwd = _head.forward;
            fwd.y = 0;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            var yaw = Vector3.SignedAngle(fwd.normalized, Vector3.forward, Vector3.up);
            _playerRoot.RotateAround(_head.position, Vector3.up, yaw);
            _playerRoot.position += target - _head.position;
            TestProbe.Event("recenter", $"{target.y:F2}");
            if (!silent) Toast("Recentered. The table is in front of you.");
        }

        // ------------------------------------------------------------ panel host

        /// <summary>Starts a new screen: clears the panel and cancels the previous screen's pending work.</summary>
        public Layout NewPanel(string screen, float width = 0.46f)
        {
            _screenCts.Cancel();
            _screenCts = new CancellationTokenSource();
            CloseKeyboard();
            Screen = screen;
            if (_panel != null) Destroy(_panel.gameObject);
            _panel = new GameObject("Panel " + screen).transform;
            _panel.SetParent(_uiRoot, false);
            _panel.position = PanelPose(out var rot);
            _panel.rotation = rot;
            _panelBg = Ui.Panel(_panel, "Background", new Vector2(width, 0.3f), Theme.Card, 0.024f, new Vector3(0, 0, 0.003f));
            TestProbe.Event("screen", screen);
            ScreenChanged?.Invoke(screen);
            var content = Ui.Node(_panel, "Content").transform;
            return new Layout(content, width);
        }

        /// <summary>Sizes the panel background to its content and anchors the content's top at the panel top.</summary>
        public void FinishPanel(Layout l)
        {
            var h = Mathf.Max(0.12f, l.Height);
            l.Root.localPosition = new Vector3(0, h / 2, 0);
            Ui.SetPanel(_panelBg, new Vector2(l.Width, h), Theme.Card, 0.024f, Theme.Border, 0.0012f);
            _panelBg.transform.localScale = new Vector3(l.Width, h, 1);
            // keep the panel's bottom edge above the table top
            var tableTop = Anchor("TableTop") != null ? Anchor("TableTop").position.y : 0.74f;
            var bottom = _panel.position.y - h / 2;
            if (bottom < tableTop + 0.06f) _panel.position += Vector3.up * (tableTop + 0.06f - bottom);
        }

        public void HidePanel()
        {
            _screenCts.Cancel();
            CloseKeyboard();
            if (_panel != null) Destroy(_panel.gameObject);
            _panel = null;
        }

        public CancellationToken ScreenToken => _screenCts.Token;

        // ------------------------------------------------------------ keyboard

        public SpatialKeyboard OpenKeyboard(string label, SpatialKeyboard.Kind kind, string initial, Action<string> onDone, Action onCancel = null, string hint = null, int max = 120)
        {
            CloseKeyboard();
            _keyboard = SpatialKeyboard.Open(_uiRoot, label, kind, initial, max, hint);
            var kb = Anchor("Keyboard");
            var head = HeadPosition;
            var pos = kb != null ? kb.position : new Vector3(0, SeatedEyeHeight - 0.33f, 0.36f);
            _keyboard.transform.position = pos;
            // tilt it like a laptop keyboard toward the eyes
            _keyboard.transform.rotation = Quaternion.LookRotation((pos - head).normalized, Vector3.up);
            if (_panel != null) _panel.gameObject.SetActive(false);
            _keyboard.Submitted += v => onDone?.Invoke(v);
            _keyboard.Cancelled += () =>
            {
                CloseKeyboard();
                onCancel?.Invoke();
            };
            TestProbe.Event("keyboard", label);
            return _keyboard;
        }

        public void CloseKeyboard()
        {
            if (_keyboard != null) Destroy(_keyboard.gameObject);
            _keyboard = null;
            if (_panel != null) _panel.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------ toast and status

        void BuildToast()
        {
            var go = Ui.Node(_uiRoot, "Toast");
            _toast = Ui.Text(go.transform, "Text", "", Theme.Body, Theme.OnDark, 0.5f, TextAlignmentOptions.Center);
            Ui.Panel(go.transform, "Bg", new Vector2(0.52f, 0.05f), Theme.CardDark, 0.025f, new Vector3(0, 0, 0.002f));
            go.SetActive(false);
        }

        public void Toast(string message, float seconds = 3.5f)
        {
            if (_toast == null) return;
            var go = _toast.transform.parent.gameObject;
            go.SetActive(true);
            _toast.text = message;
            var head = HeadPosition;
            var fwd = _head != null ? Vector3.ProjectOnPlane(_head.forward, Vector3.up).normalized : Vector3.forward;
            go.transform.position = head + fwd * 0.75f + Vector3.up * 0.16f;
            go.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _toastUntil = Time.unscaledTime + seconds;
            TestProbe.Event("toast", message.Length > 60 ? message.Substring(0, 60) : message);
        }

        /// <summary>Shows a failure with what to do next. Never silently switches live mode to sample content.</summary>
        public void ShowError(Exception e, Action retry, string context)
        {
            var message = e is ApiException api ? api.Message : "Something went wrong.";
            var l = NewPanel("error");
            l.Title(context);
            l.Body(message);
            if (e is ApiException { IsNetwork: true }) l.Muted($"The app is looking for SameSky at {AppConfig.ApiUrl}. Is the local API running?");
            l.Row(("error:retry", "Try again", retry, PressStyle.Primary), ("error:back", "Back", ShowHome, PressStyle.Secondary));
            FinishPanel(l);
            TestProbe.Event("error", $"{context}: {message}");
        }

        void OnUnauthorized()
        {
            if (Data is LiveData)
            {
                StoredSession.Clear();
                Toast("Your session ended. Please sign in again.");
                ShowSignIn();
            }
        }

        /// <summary>The main room screen, whatever the mode.</summary>
        public void ShowHome()
        {
            if (Data == null) { ShowLaunch(); return; }
            EnterRoom();
        }

        // ------------------------------------------------------------ dock (always within reach at the table edge)

        readonly System.Collections.Generic.Dictionary<string, Pressable> _dockButtons = new();

        void BuildDock()
        {
            _dock = new GameObject("Dock").transform;
            _dock.SetParent(_uiRoot, false);
            var anchor = Anchor("Dock");
            _dock.position = anchor != null ? anchor.position : new Vector3(0, 0.9f, 0.34f);
            _dock.rotation = anchor != null ? anchor.rotation : Quaternion.Euler(35, 0, 0);
            _dock.gameObject.SetActive(false);
        }

        void SetDock(params (string id, string label, Action action)[] items)
        {
            Ui.Clear(_dock);
            _dockButtons.Clear();
            const float w = 0.078f, h = 0.044f, gap = 0.008f;
            var total = items.Length * w + (items.Length - 1) * gap;
            Ui.Panel(_dock, "DockBg", new Vector2(total + 0.024f, h + 0.02f), new Color(0.12f, 0.15f, 0.14f, 0.55f), 0.03f, new Vector3(0, 0, 0.004f));
            for (var i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var p = Pressable.Create(_dock, "dock:" + it.id, it.label, new Vector2(w, h), it.action, PressStyle.Secondary,
                    new Vector3(-total / 2 + w / 2 + i * (w + gap), 0, 0));
                p.Label.fontSize *= 0.82f;
                _dockButtons[it.id] = p;
            }
            _dock.gameObject.SetActive(items.Length > 0);
        }

        public Pressable DockButton(string id) => _dockButtons.TryGetValue(id, out var p) ? p : null;

        // ------------------------------------------------------------ leaving

        public void ConfirmExit()
        {
            var l = NewPanel("exit");
            l.Title("Leave SameSky?");
            l.Body(Data is LiveData ? "Your replies and placements are saved. You'll come back to the same room." : "The sample keeps what you did here for next time.");
            l.Row(("exit:stay", "Stay", ShowHome, PressStyle.Secondary), ("exit:quit", "Leave", Quit, PressStyle.Primary));
            FinishPanel(l);
        }

        void Quit()
        {
            TestProbe.Event("exit", "quit");
            _ = LeaveLive();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy()
        {
            _screenCts.Cancel();
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------ helpers

        /// <summary>Runs an async UI action: shows errors with a retry, ignores results of screens already left.</summary>
        public async void Run(Func<CancellationToken, Task> action, string context, Action retry = null)
        {
            var ct = ScreenToken;
            try { await action(ct); }
            catch (OperationCanceledException) { /* the person moved on */ }
            catch (Exception e)
            {
                if (ct.IsCancellationRequested) return;
                Debug.LogWarning($"[SameSky] {context}: {e}");
                ShowError(e, retry ?? ShowHome, context);
            }
        }
    }
}
