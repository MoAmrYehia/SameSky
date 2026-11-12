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
            // glossy things reflect the room as it is lit now: drawn once (static geometry only)
            foreach (var probe in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
                if (probe.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime) probe.RenderProbe();
            ShowLaunch();
        }

        void Update()
        {
            // the whole toast (text and its pill) goes: hiding only the text left an empty pill behind for good
            if (_toast != null && Time.unscaledTime > _toastUntil && _toast.transform.parent.gameObject.activeSelf) _toast.transform.parent.gameObject.SetActive(false);
            KeepPanelInView();
            UpdateRoom();
            UpdateStory();
            UpdateTogether();
            PollForNewMembers();
        }

        // Why the app is paused; sound and stories stop while any reason holds:
        //   "system" the OS backgrounded the app        "focus" the app lost focus (system menu, overlay)
        //   "input"  OpenXR input focus went elsewhere   "menu"  the person opened SameSky's pause menu
        readonly System.Collections.Generic.HashSet<string> _pauseReasons = new();

        void OnEnable()
        {
            OVRManager.InputFocusLost += OnInputFocusLost;
            OVRManager.InputFocusAcquired += OnInputFocusAcquired;
        }

        void OnDisable()
        {
            OVRManager.InputFocusLost -= OnInputFocusLost;
            OVRManager.InputFocusAcquired -= OnInputFocusAcquired;
        }

        void OnInputFocusLost() => SetPaused(true, "input");
        void OnInputFocusAcquired() => SetPaused(false, "input");
        void OnApplicationPause(bool paused) => SetPaused(paused, "system");

        void OnApplicationFocus(bool focus)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            // On a PC (Play mode, or a Windows build in Meta XR Simulator) window focus says nothing about the headset:
            // the simulator's window has the keyboard while the app runs. OpenXR input focus is the signal there.
            if (UnityEngine.XR.XRSettings.isDeviceActive) return;
#endif
            SetPaused(!focus, "focus");
        }

        /// <summary>Headset removed, system menu opened, app backgrounded or the pause menu open: audio and video
        /// stop; resume picks up where it was.</summary>
        void SetPaused(bool paused, string reason)
        {
            var changed = paused ? _pauseReasons.Add(reason) : _pauseReasons.Remove(reason);
            var now = _pauseReasons.Count > 0;
            if (!changed || now == Paused) return;
            Paused = now;
            AudioListener.pause = now;
            PauseStory(now);
            TestProbe.Event("pause", now ? reason : "resume");
            if (!now && Screen == "room") Toast("Welcome back.");
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
            if (_panel == null || !_panel.gameObject.activeSelf || _panelAtTable) return;
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
            // the pause menu's pause lasts while it (or its "Leave SameSky?" question) is open
            if (screen != "pause" && screen != "exit") SetPaused(false, "menu");
            if (_panel != null) Destroy(_panel.gameObject);
            StopVoice();   // a screen takes over: the cup's message and its card step aside
            _panel = new GameObject("Panel " + screen).transform;
            _panel.SetParent(_uiRoot, false);
            _panel.position = PanelPose(out var rot);
            _panel.rotation = rot;
            _panelAtTable = false;
            _panelBg = Ui.Panel(_panel, "Background", new Vector2(width, 0.3f), Theme.Card, 0.024f, new Vector3(0, 0, 0.003f), order: Ui.OrderBackground);
            TestProbe.Event("screen", screen);
            ScreenChanged?.Invoke(screen);
            var content = Ui.Node(_panel, "Content").transform;
            return new Layout(content, width);
        }

        /// <summary>Sizes the panel background to its content and anchors the content's top at the panel top.</summary>
        public void FinishPanel(Layout l, params (string id, string label, Action action, PressStyle style)[] footers)
        {
            var h = Mathf.Max(0.12f, l.Height);
            l.Root.localPosition = new Vector3(0, h / 2, 0);
            Ui.SetPanel(_panelBg, new Vector2(l.Width, h), Theme.Card, 0.03f, new Color(1, 1, 1, 0.6f), 0.0012f);
            _panelBg.transform.localScale = new Vector3(l.Width, h, 1);
            // pills under the panel (Recenter and Leave on the launch screen), as in the references
            var below = 0f;
            if (footers is { Length: > 0 })
            {
                const float fh = 0.046f, fw = 0.15f, gap = 0.014f;
                var x = -(footers.Length * fw + (footers.Length - 1) * gap) / 2f + fw / 2f;
                foreach (var f in footers)
                {
                    Pressable.Create(_panel, f.id, f.label, new Vector2(fw, fh), f.action, f.style, new Vector3(x, -h / 2 - 0.018f - fh / 2, 0));
                    x += fw + gap;
                }
                below = 0.018f + fh;
            }
            var tableTop = Anchor("TableTop") != null ? Anchor("TableTop").position.y : 0.74f;
            if (_roomContent != null)
            {
                // In the room a panel lies low at the table's near edge, tilted up like a tablet, so the person across
                // the table stays in view above it (visual references 02 and 05). It takes the dock's place; the dock
                // comes back when the panel closes.
                var dock = Anchor("Dock");
                var near = dock != null ? dock.position : new Vector3(0, 0.865f, 0.3f);
                // taller panels lie flatter, so their top edge stays below the other person's face
                var tilt = Mathf.Lerp(40f, 54f, Mathf.InverseLerp(0.4f, 0.6f, h));
                var rotation = (dock != null && dock.parent != null ? dock.parent.rotation : Quaternion.identity) * Quaternion.Euler(tilt, 0, 0);
                var edge = new Vector3(near.x, tableTop + 0.035f, near.z) + rotation * Vector3.back * 0.02f;
                _panel.rotation = rotation;
                _panel.position = edge + rotation * Vector3.up * (h / 2 + below);
                _panelAtTable = true;
                if (_dock != null && _dock.gameObject.activeSelf) { _dock.gameObject.SetActive(false); _dockHiddenForPanel = true; }
                return;
            }
            // before there is a room (launch, sign-in, setup) it stands upright in front of the seat, above the table top
            var bottom = _panel.position.y - h / 2 - below;
            if (bottom < tableTop + 0.1f) _panel.position += Vector3.up * (tableTop + 0.1f - bottom);
        }

        bool _panelAtTable, _dockHiddenForPanel;

        public void HidePanel()
        {
            // the hidden screen's pending work stops; what starts next (the room, a story) gets a fresh token
            _screenCts.Cancel();
            _screenCts = new CancellationTokenSource();
            CloseKeyboard();
            if (_panel != null) Destroy(_panel.gameObject);
            _panel = null;
            _panelAtTable = false;
            if (_dockHiddenForPanel && _dock != null && _dock.childCount > 0) _dock.gameObject.SetActive(true);
            _dockHiddenForPanel = false;
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
            // the keyboard sits where the dock is: hide the dock so a reach for a key can't press a dock button
            if (_dock != null && _dock.gameObject.activeSelf) { _dock.gameObject.SetActive(false); _dockHiddenForKeyboard = true; }
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
            if (_dockHiddenForKeyboard && _dock != null) _dock.gameObject.SetActive(true);
            _dockHiddenForKeyboard = false;
        }

        bool _dockHiddenForKeyboard;

        // ------------------------------------------------------------ toast and status

        void BuildToast()
        {
            var go = Ui.Node(_uiRoot, "Toast");
            _toast = Ui.Text(go.transform, "Text", "", Theme.Body, Theme.OnDark, 0.5f, TextAlignmentOptions.Center);
            Ui.Panel(go.transform, "Bg", new Vector2(0.52f, 0.05f), Theme.CardDark, 0.025f, new Vector3(0, 0, 0.002f), order: Ui.OrderBackground);
            go.SetActive(false);
        }

        public void Toast(string message, float seconds = 3.5f)
        {
            if (_toast == null) return;
            var go = _toast.transform.parent.gameObject;
            go.SetActive(true);
            if (!_toast.gameObject.activeSelf) _toast.gameObject.SetActive(true);
            _toast.text = message;
            var head = HeadPosition;
            var fwd = _head != null ? Vector3.ProjectOnPlane(_head.forward, Vector3.up).normalized : Vector3.forward;
            go.transform.position = head + fwd * 0.75f + Vector3.up * 0.3f;   // above the partner's presence label, not over it
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

        // dock icons; Pause and Recenter are compact round icon buttons at the ends of the bar
        static readonly System.Collections.Generic.Dictionary<string, string> DockIcons = new()
        {
            ["day"] = Icons.Calendar, ["share"] = Icons.Share, ["together"] = Icons.Users, ["keepsakes"] = Icons.Layers,
            ["settings"] = Icons.Settings, ["pause"] = Icons.CirclePause, ["recenter"] = Icons.Recenter,
        };

        void SetDock(params (string id, string label, Action action)[] items)
        {
            Ui.Clear(_dock);
            _dockButtons.Clear();
            _dockHiddenForPanel = false;
            const float w = 0.084f, round = 0.046f, h = 0.046f, gap = 0.004f;
            bool Compact(string id) => id is "pause" or "recenter";
            var total = 0f;
            foreach (var it in items) total += (Compact(it.id) ? round : w) + gap;
            total -= gap;
            // one dark-green bar, a pill like the references' dock
            Ui.Panel(_dock, "DockBg", new Vector2(total + 0.018f, h + 0.014f), Theme.GreenDark, (h + 0.014f) / 2f, new Vector3(0, 0, 0.004f),
                border: new Color(1, 1, 1, 0.18f), order: Ui.OrderBackground);
            var x = -total / 2f;
            for (var i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var bw = Compact(it.id) ? round : w;
                var icon = DockIcons.TryGetValue(it.id, out var ic) ? ic : null;
                var text = Compact(it.id) ? icon ?? it.label : icon != null ? Icons.With(icon, it.label) : it.label;
                var p = Pressable.Create(_dock, "dock:" + it.id, text, new Vector2(bw, h), it.action, PressStyle.DockItem,
                    new Vector3(x + bw / 2f, 0, 0));
                p.Label.fontSize *= Compact(it.id) ? 1.15f : 0.78f;
                p.Label.enableAutoSizing = !Compact(it.id);   // long labels (Keepsakes) shrink rather than wrap
                p.Label.fontSizeMax = p.Label.fontSize;
                p.Label.fontSizeMin = p.Label.fontSize * 0.75f;
                _dockButtons[it.id] = p;
                if (i < items.Length - 1)   // a thin divider between items
                    Ui.Panel(_dock, "Divider", new Vector2(0.0012f, h * 0.45f), new Color(1, 1, 1, 0.22f), 0.0006f, new Vector3(x + bw + gap / 2f, 0, 0.001f), new Color(1, 1, 1, 0), 0);
                x += bw + gap;
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
