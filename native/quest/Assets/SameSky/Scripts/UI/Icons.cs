// Lucide line icons (lucide-static 0.460.0, ISC licence) by codepoint. The text font (SameSkySans SDF) carries the
// icon font as a fallback, so an icon is just a character in any label: Icons.With(Icons.Calendar, "Our day").

namespace SameSky.UI
{
    public static class Icons
    {
        public const string Calendar = "";    // calendar-days
        public const string Users = "";
        public const string Layers = "";
        public const string Settings = "";
        public const string Play = "";
        public const string Pause = "";
        public const string Mic = "";
        public const string Video = "";
        public const string Image = "";
        public const string Replay = "";      // rotate-ccw
        public const string Reply = "";
        public const string Cup = "";         // coffee
        public const string Recenter = "";    // locate-fixed
        public const string Hand = "";
        public const string Pointer = "";
        public const string ShieldCheck = "";
        public const string Folder = "";      // folder-open
        public const string Leave = "";       // log-out
        public const string Share = "";       // share-2
        public const string Send = "";
        public const string Refresh = "";     // refresh-cw
        public const string Check = "";
        public const string Close = "";       // x
        public const string Waveform = "";    // audio-lines
        public const string Captions = "";
        public const string Sound = "";       // volume-2
        public const string TextSize = "";    // type
        public const string SignIn = "";
        public const string UserPlus = "";
        public const string Sprout = "";
        public const string Clock = "";
        public const string MapPin = "";
        public const string Heart = "";
        public const string Sun = "";
        public const string Sparkles = "";
        public const string CirclePause = "";
        public const string BookOpen = "";
        public const string Armchair = "\uE2C0";
        public const string Gift = "";

        /// <summary>Every icon used, for prebuilding the icon atlas (no hitch the first time one shows).</summary>
        public const string All = Calendar + Users + Layers + Settings + Play + Pause + Mic + Video + Image + Replay + Reply + Cup
            + Recenter + Hand + Pointer + ShieldCheck + Folder + Leave + Share + Send + Refresh + Check + Close + Waveform + Captions
            + Sound + TextSize + SignIn + UserPlus + Sprout + Clock + MapPin + Heart + Sun + Sparkles + CirclePause + BookOpen + Gift + Armchair;

        /// <summary>An icon before a label, with a little air between them.</summary>
        public static string With(string icon, string label) => string.IsNullOrEmpty(label) ? icon : $"{icon}<space=0.35em>{label}";
    }
}
