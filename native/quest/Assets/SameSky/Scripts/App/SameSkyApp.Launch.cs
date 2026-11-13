// Launch: branded loading, seated calibration, sound and captions, then the sample or an account.

using System;
using System.Threading.Tasks;
using SameSky.Api;
using SameSky.Interaction;
using SameSky.UI;
using UnityEngine;

namespace SameSky.App
{
    public sealed partial class SameSkyApp
    {
        public void ShowLaunch()
        {
            SetDock();
            ClearRoom();
            ClearScreenFor(null);
            // no room is open: the lounge shows its usual furniture
            _decorPreview = null;
            _decorSaved = null;
            if (RoomDecor.Instance != null && FurnitureCatalog.Current.Slots.Count > 0) RoomDecor.Instance.Apply(FurnitureCatalog.Current.Defaults());
            // visual reference 10: a small, calm welcome with one clear first step, and the room visible around it
            var l = NewPanel("launch", 0.37f);
            l.Muted("<b>SameSky</b>");
            l.Title("A little closer, wherever you are");
            l.Underline();
            l.Button("launch:sample", Icons.With(Icons.Play, "Try a sample day"), () => _ = EnterSample(), PressStyle.Primary);
            if (AppConfig.SampleOnly)
            {
                // a build with no server (the one given to judges): no button that could only fail, and why
                l.Muted("Fictional people, stock photos and footage, and a synthesized voice, all labelled.");
                l.Muted("This build runs the sample day only. Accounts and live sessions with another person need the SameSky service.");
            }
            else
            {
                l.Muted("Works offline. Fictional people, stock photos and footage, and a synthesized voice, all labelled.");
                var stored = StoredSession.Load();
                if (!string.IsNullOrEmpty(stored.token) && stored.apiUrl == AppConfig.ApiUrl)
                    l.Button("launch:continue", Icons.With(Icons.SignIn, $"Continue as {stored.email}"), () => _ = ResumeLive(stored), PressStyle.Secondary);
                l.Row(("launch:signin", "Sign in", ShowSignIn, PressStyle.Secondary), ("launch:signup", "Create account", ShowSignUp, PressStyle.Secondary));
            }
            // captions and sound: two chips that say their state (green when on)
            Pressable captions = null, sound = null;
            static string CaptionsLabel() => Icons.With(Icons.Captions, Prefs.Current.captionsOn ? "Captions on" : "Captions off");
            static string SoundLabel() => Icons.With(Icons.Sound, Prefs.Current.soundOn ? "Sound on" : "Sound off");
            var toggles = l.Row(
                ("launch:captions", CaptionsLabel(), () =>
                {
                    Prefs.Current.captionsOn = !Prefs.Current.captionsOn;
                    Prefs.Save();
                    captions.SetToggled(Prefs.Current.captionsOn);
                    captions.SetText(CaptionsLabel());
                }, PressStyle.Chip),
                ("launch:sound", SoundLabel(), () =>
                {
                    Prefs.Current.soundOn = !Prefs.Current.soundOn;
                    Prefs.Save();
                    AudioListener.volume = Prefs.Current.soundOn ? Prefs.Current.volume : 0f;
                    sound.SetToggled(Prefs.Current.soundOn);
                    sound.SetText(SoundLabel());
                }, PressStyle.Chip));
            captions = toggles[0];
            sound = toggles[1];
            captions.SetToggled(Prefs.Current.captionsOn);
            sound.SetToggled(Prefs.Current.soundOn);
            foreach (var t in toggles) t.Label.fontSize *= 0.88f;
            FinishPanel(l,
                ("launch:recenter", Icons.With(Icons.Recenter, "Recenter"), () => Recenter(), PressStyle.Dark),
                ("launch:exit", Icons.With(Icons.Leave, "Leave"), ConfirmExit, PressStyle.Dark));
        }

        // ------------------------------------------------------------ sample

        async Task EnterSample()
        {
            var l = NewPanel("loading");
            l.Title("Opening the sample…");
            l.Progress(0.2f);
            l.Muted("Everything here is fictional and stays on this device.");
            FinishPanel(l);
            await Task.Yield();
            try
            {
                var data = new SampleData();
                await data.Load();
                Data = data;
                Prefs.Current.mode = "sample";
                Prefs.Save();
                CurrentRoom = data.Rooms.Find(r => r.Id == SampleData.DirectRoom) ?? data.Rooms[0];
                TestProbe.Event("mode", "sample");
                EnterRoom();
            }
            catch (Exception e)
            {
                ShowError(e, () => _ = EnterSample(), "The sample couldn't open");
            }
        }

        // ------------------------------------------------------------ accounts

        string _email = "", _password = "", _name = "";

        /// <summary>A build with no server has nothing to sign in to: back to the welcome panel, with the reason.</summary>
        bool AccountsUnavailable()
        {
            if (!AppConfig.SampleOnly) return false;
            ShowLaunch();
            Toast("This build runs the sample day only.");
            return true;
        }

        public void ShowSignIn()
        {
            if (AccountsUnavailable()) return;
            SetDock();
            var l = NewPanel("signin");
            l.Title("Sign in");
            l.Body($"Email: {(_email == "" ? "—" : _email)}");
            l.Button("signin:email", "Type email", () => OpenKeyboard("Email", SpatialKeyboard.Kind.Email, _email, v => { _email = v.Trim(); CloseKeyboard(); ShowSignIn(); }, ShowSignIn), PressStyle.Secondary);
            l.Body($"Password: {(_password == "" ? "—" : new string('•', _password.Length))}");
            l.Button("signin:password", "Type password", () => OpenKeyboard("Password", SpatialKeyboard.Kind.Password, "", v => { _password = v; CloseKeyboard(); ShowSignIn(); }, ShowSignIn), PressStyle.Secondary);
            var go = l.Row(("signin:back", "Back", ShowLaunch, PressStyle.Quiet), ("signin:go", "Sign in", () => _ = SignIn(), PressStyle.Primary));
            go[1].Interactable = _email.Contains("@") && _password.Length > 0;
            l.Muted($"Server: {AppConfig.ApiUrl}");
            FinishPanel(l);
        }

        public void ShowSignUp()
        {
            if (AccountsUnavailable()) return;
            SetDock();
            var l = NewPanel("signup");
            l.Title("Create an account");
            l.Body($"Name people will see: {(_name == "" ? "—" : _name)}");
            l.Button("signup:name", "Type your name", () => OpenKeyboard("What should people call you?", SpatialKeyboard.Kind.Text, _name, v => { _name = v.Trim(); CloseKeyboard(); ShowSignUp(); }, ShowSignUp, max: 60), PressStyle.Secondary);
            l.Body($"Email: {(_email == "" ? "—" : _email)}");
            l.Button("signup:email", "Type email", () => OpenKeyboard("Email", SpatialKeyboard.Kind.Email, _email, v => { _email = v.Trim(); CloseKeyboard(); ShowSignUp(); }, ShowSignUp), PressStyle.Secondary);
            l.Body($"Password: {(_password == "" ? "—" : new string('•', _password.Length))}");
            l.Button("signup:password", "Choose a password (8+ characters)", () => OpenKeyboard("Password (at least 8 characters)", SpatialKeyboard.Kind.Password, "", v => { _password = v; CloseKeyboard(); ShowSignUp(); }, ShowSignUp), PressStyle.Secondary);
            l.Muted($"Time zone: {TimeZones.Detect()} (you can change it in setup)");
            var go = l.Row(("signup:back", "Back", ShowLaunch, PressStyle.Quiet), ("signup:go", "Create account", () => _ = SignUp(), PressStyle.Primary));
            go[1].Interactable = _name.Length > 0 && _email.Contains("@") && _password.Length >= 8;
            FinishPanel(l);
        }

        async Task SignIn()
        {
            var l = NewPanel("signin-wait");
            l.Title("Signing in…");
            FinishPanel(l);
            try
            {
                Api.SetBaseUrl(AppConfig.ApiUrl);
                var res = await Api.Login(_email, _password, ScreenToken);
                await StartLive(res);
            }
            catch (ApiException e) when (e.Code == "bad_credentials" || e.Status == 422)
            {
                _password = "";
                ShowSignIn();
                Toast(e.Message);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ShowError(e, () => _ = SignIn(), "Couldn't sign in");
            }
        }

        async Task SignUp()
        {
            var l = NewPanel("signup-wait");
            l.Title("Creating your account…");
            FinishPanel(l);
            try
            {
                Api.SetBaseUrl(AppConfig.ApiUrl);
                var res = await Api.Signup(_email, _password, _name, TimeZones.Detect(), ScreenToken);
                Prefs.Current.setupStep = "about";
                Prefs.Save();
                await StartLive(res);
            }
            catch (ApiException e) when (e.Code == "email_taken" || e.Status == 422)
            {
                ShowSignUp();
                Toast(e.Code == "email_taken" ? e.Message : "Check the email address and use a password of at least 8 characters.");
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ShowError(e, () => _ = SignUp(), "Couldn't create the account");
            }
        }

        async Task StartLive(AuthResult res)
        {
            _password = "";
            Api.Token = res.Token;
            StoredSession.Store(new StoredSession
            {
                token = res.Token, userId = res.User.Id, email = res.User.Email, apiUrl = AppConfig.ApiUrl, savedAt = DateTimeOffset.UtcNow.ToString("o"),
            });
            await LoadLive();
        }

        async Task ResumeLive(StoredSession stored)
        {
            var l = NewPanel("loading");
            l.Title("Opening your room…");
            l.Progress(0.35f);
            FinishPanel(l);
            Api.SetBaseUrl(AppConfig.ApiUrl);
            Api.Token = stored.token;
            try { await LoadLive(); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                if (e is ApiException { IsAuth: true }) return; // OnUnauthorized already moved to sign-in
                ShowError(e, () => _ = ResumeLive(stored), "Couldn't open your room");
            }
        }

        async Task LoadLive()
        {
            var data = new LiveData(Api);
            await data.Load(ScreenToken);
            Data = data;
            Prefs.Current.mode = "live";
            Prefs.Save();
            TestProbe.Event("mode", "live");
            CurrentRoom = data.Rooms.Find(r => r.Id == Prefs.Current.lastRoomId) ?? (data.Rooms.Count > 0 ? data.Rooms[0] : null);
            if (CurrentRoom == null || !string.IsNullOrEmpty(Prefs.Current.setupStep)) ShowSetup();
            else EnterRoom();
        }

        public void SignOut()
        {
            _ = LeaveLive();
            StoredSession.Clear();
            Media.ClearCache();
            Api.Token = null;
            Data = null;
            CurrentRoom = null;
            Prefs.Current.mode = null;
            Prefs.Current.lastRoomId = null;
            Prefs.Save();
            TestProbe.Event("signout", "");
            ShowLaunch();
        }
    }

    public static class TimeZones
    {
        /// <summary>The device's IANA time zone (Quest reports IANA ids; Windows ids are mapped for common zones).</summary>
        public static string Detect()
        {
            var id = TimeZoneInfo.Local.Id;
            if (id.Contains("/")) return id;
            return id switch
            {
                "Arabian Standard Time" => "Asia/Dubai",
                "China Standard Time" => "Asia/Shanghai",
                "GMT Standard Time" => "Europe/London",
                "W. Europe Standard Time" => "Europe/Berlin",
                "Romance Standard Time" => "Europe/Paris",
                "Eastern Standard Time" => "America/New_York",
                "Central Standard Time" => "America/Chicago",
                "Pacific Standard Time" => "America/Los_Angeles",
                "India Standard Time" => "Asia/Kolkata",
                "Tokyo Standard Time" => "Asia/Tokyo",
                "Singapore Standard Time" => "Asia/Singapore",
                "AUS Eastern Standard Time" => "Australia/Sydney",
                "Arab Standard Time" => "Asia/Riyadh",
                "Egypt Standard Time" => "Africa/Cairo",
                _ => "UTC",
            };
        }

        public static readonly string[] Common =
        {
            "Asia/Dubai", "Asia/Riyadh", "Africa/Cairo", "Europe/London", "Europe/Berlin", "Asia/Kolkata", "Asia/Singapore",
            "Asia/Shanghai", "Asia/Tokyo", "Australia/Sydney", "America/New_York", "America/Chicago", "America/Los_Angeles", "UTC",
        };
    }
}
