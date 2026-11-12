// SameSky native client: data contracts.
// Ported from web/src/shared/contracts.ts and api/samesky/serializers.py (the server is the source of truth).
// Responses are camelCase JSON; Newtonsoft matches these PascalCase properties case-insensitively.
// Request bodies use the API's snake_case field names and are built explicitly in ApiClient.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SameSky.Api
{
    public sealed class AuthResult
    {
        public string Token;
        public Me User;
    }

    public class AvatarView
    {
        /// <summary>"preset_demo" (labelled demo avatar) or "photo_derived" (owner-approved model).</summary>
        public string Kind;
        public JObject Definition;
        public int Version;
        public string AssetId;
        public int? AssetVersion;
        /// <summary>Short-lived, viewer-bound signed URL; re-authorized by the server on every download.</summary>
        public string ModelUrl;
        public JObject Rig;
        public string Provider;
        public string ApprovedAt;
        public string Visibility;

        [JsonIgnore] public bool IsPhotoDerived => Kind == "photo_derived" && !string.IsNullOrEmpty(ModelUrl);
        /// <summary>Identity of what should be on screen; a change means "load again".</summary>
        [JsonIgnore] public string Key => IsPhotoDerived ? $"{AssetId}@{AssetVersion}" : $"preset@{Version}";
    }

    public class UserPublic
    {
        public string Id;
        public string DisplayName;
        public string TimeZone;
        public AvatarView Avatar;
    }

    public sealed class Me : UserPublic
    {
        public string Email;
        public string QuietStart;
        public string QuietEnd;
        public int PreferredCallMinutes;
        public bool ShareAvailabilityHints;
        public Dictionary<string, bool> NotificationPrefs;
        public string CallLink;
        public bool LikenessConsent;
    }

    public sealed class Member : UserPublic
    {
        public string Role;
        public string Label;
        public string JoinedAt;
    }

    public sealed class Room
    {
        public string Id;
        public string Kind; // direct | circle
        public string Name;
        public int Capacity;
        public string ReportingZone;
        public string DailyDraftTime;
        public bool IsOwner;
        public bool Muted;
        public List<Member> Members = new();

        [JsonIgnore] public bool IsDirect => Kind == "direct";
    }

    public sealed class InviteResult
    {
        public string Id;
        public string Token;
        /// <summary>Short hand-typed code, e.g. "K7PQ-4XMR" (single use, same expiry as the link).</summary>
        public string Code;
        public string ExpiresAt;
        public string Path;
    }

    public sealed class MediaInfo
    {
        public string Id;
        public string Kind; // image | video | audio
        public string ProcessingState; // pending | processing | ready | failed
        public string SafeError;
        public string SourceDevice;
        /// <summary>Sample media only: simulated | synthesized | stock. Never set by the API.</summary>
        public string Origin;
        public string Projection;
        public int? DurationMs;
        public int? Width;
        public int? Height;
        public string Url;
        public string ThumbUrl;
        public string PosterUrl;
        public string CaptionsUrl;
        /// <summary>Ogg Vorbis twin of a voice note (native clients can't decode AAC clips).</summary>
        public string OggUrl;
        /// <summary>Sample media only: caption cues embedded from the companion's WebVTT.</summary>
        public List<CaptionCue> Captions;
    }

    public sealed class CaptionCue
    {
        public int StartMs;
        public int EndMs;
        public string Speaker;
        public string Text;
    }

    public sealed class CaptureLocal
    {
        public string Time;
        public string Date;
        public string Zone;
        public string Abbreviation;
    }

    public sealed class NameRef
    {
        public string UserId;
        public string DisplayName;
    }

    public sealed class ReactionRef
    {
        public string UserId;
        public string Kind;
    }

    public sealed class Moment
    {
        public string Id;
        public string RoomId;
        public string OwnerId;
        public string OwnerName;
        public string Kind; // photo | clip | voice | text
        public string Caption;
        public string CaptureAt;
        public CaptureLocal CaptureLocal;
        public string RoomDate;
        public string SharingState; // private | shared | withdrawn
        public int SourceVersion;
        public int SharingVersion;
        public List<NameRef> Audience = new();
        public bool IsMine;
        public bool? AiAllowed;
        public string Transcript;
        public List<ReactionRef> Reactions = new();
        public MediaInfo Media;
    }

    public sealed class Reply
    {
        public string Id;
        public string MomentId;
        public string AuthorId;
        public string AuthorName;
        public string Text;
        public string CreatedAt;
        public string Label;
    }

    public sealed class RoomObjectTransform
    {
        public float X, Y, Z, Yaw;
    }

    public sealed class RoomObject
    {
        public string Id;
        public string ObjectType; // frame | cup | postcard | audioStone | note
        public string MomentId;
        public string ReplyId;
        public string OwnerId;
        public RoomObjectTransform Transform = new();
        public int Version;
    }

    public sealed class SourceRef
    {
        public string MomentId;
        public int SourceVersion;
        public int SharingVersion;
    }

    public sealed class RecapSegment
    {
        public string Text;
        public string Kind; // stated | visual_interpretation | quote
        public List<SourceRef> Sources = new();
    }

    public sealed class RecapChapter
    {
        public string ContributorId;
        public List<RecapSegment> Segments = new();
        public List<string> PlaybackMomentIds = new();
        public string ObjectType;
        public float DurationSeconds;
    }

    public sealed class RecapPlan
    {
        public int SchemaVersion;
        public string RecapId;
        public string RoomId;
        public string RoomDate;
        public string RoomTimeZone;
        public string Title;
        public string ClosingLine;
        public int? OmittedCount;
        public string Fallback;
        public List<RecapChapter> Chapters = new();
    }

    public sealed class Contributor
    {
        public string UserId;
        public string DisplayName;
        public bool Approved;
    }

    public sealed class Recap
    {
        public string Id;
        public string RoomId;
        public string RoomDate;
        public int Version;
        /// <summary>queued | generating | private_preview | awaiting_approval | shared | invalidated | failed</summary>
        public string State;
        public string Generator; // ai | chronological | pending | sample
        public string GeneratorLabel;
        public int OmittedCount;
        public string InvalidationReason;
        public List<Contributor> Contributors = new();
        public string PublishedAt;
        public RecapPlan Plan;
        public List<Moment> Playback = new();
        public List<NameRef> Audience;
    }

    public sealed class Award
    {
        public string Milestone;
        public string Title;
        public string Trigger;
        public string Keepsake;
        public bool Earned;
        public string EarnedAt;
        public int Progress;
        public int Target;
        public string SourceRecapId;
    }

    public sealed class WeeklyRule
    {
        public int Weekday; // 0 = Monday
        public string Start;
        public string End;
    }

    public sealed class AvailabilityException
    {
        public string Id;
        public string Kind;
        public string Start;
        public string End;
        public string RoomId;
    }

    public sealed class MyAvailability
    {
        public string TimeZone;
        public List<WeeklyRule> Rules = new();
        public List<AvailabilityException> Exceptions = new();
        public string FreeNowUntil;
    }

    public sealed class LocalLabel
    {
        public string UserId;
        public string DisplayName;
        public string Zone;
        public string Local;
        public string Date;
        public string Time;
        public string EndTime;
        public string Weekday;
        public string Abbreviation;
    }

    public sealed class Participant
    {
        public string UserId;
        public string DisplayName;
        public string TimeZone;
        public bool FreeNow;
    }

    public sealed class Candidate
    {
        public string StartUtc;
        public string EndUtc;
        public int Minutes;
        public string Message;
        public List<LocalLabel> Local = new();
    }

    public sealed class OpportunityResult
    {
        public List<Participant> Participants = new();
        public string ParticipantLabel;
        public bool AllMarkedFreeNow;
        public string StatusMessage;
        public bool QuietHoursRespected;
        public List<Candidate> Candidates = new();
        public bool Simulated;
    }

    public sealed class InboxItem
    {
        public string Id;
        public string Kind; // recap_ready | reply_received | call_opportunity
        public string RoomId;
        public string ResourceType;
        public string ResourceId;
        public string Text;
        public string CreatedAt;
        public bool Read;
    }

    public sealed class AudienceResolution
    {
        public List<NameRef> Recipients = new();
        public string Label;
        public string ConfirmationToken;
    }

    /// <summary>Server-authoritative playback state of a live viewing session (realtime "state" message).</summary>
    public sealed class PlaybackState
    {
        public string SessionId;
        public string MomentId;
        public int Version;
        public double PlayheadMs;
        public bool Paused;
        public double StateAt;    // server epoch ms when PlayheadMs was true
        public double ServerTime; // server epoch ms when this message was sent
    }

    public sealed class LiveSessionInfo
    {
        public string Id;
        public string RoomId;
        public string MomentId;
        public string HostId;
        public List<string> ParticipantIds = new();
        public PlaybackState State;
    }

    public sealed class AvatarAssetInfo
    {
        public string Id;
        /// <summary>awaiting_owner_approval | active | superseded | revoked | deleted</summary>
        public string State;
        public int Version;
        public string Provider;
        public string SourceKind; // import | generated
        public string Visibility; // rooms | direct | private
        public string JobId;
        public string ApprovedAt;
        public JObject Rig;
        public long SizeBytes;
        public string ModelUrl;
        public string CreatedAt;
    }

    public sealed class AvatarStudio
    {
        public JObject Provider;
        public string ConsentTextVersion;
        public AvatarView Active;
        public string ActiveAssetId;
        public List<AvatarAssetInfo> Assets = new();
        public List<JObject> Jobs = new();
        public List<JObject> Sources = new();
        public List<JObject> Consents = new();
    }

    public sealed class ApiErrorBody
    {
        public ApiErrorDetail Error;
    }

    public sealed class ApiErrorDetail
    {
        public string Code;
        public string Message;
        public bool Retryable;
    }

    [Serializable]
    public sealed class ApiException : Exception
    {
        public readonly int Status;
        public readonly string Code;
        public readonly bool Retryable;

        public ApiException(int status, string code, string message, bool retryable = false) : base(message)
        {
            Status = status;
            Code = code;
            Retryable = retryable;
        }

        /// <summary>No HTTP answer at all (server down, Wi-Fi off, wrong address).</summary>
        public bool IsNetwork => Status == 0;
        public bool IsAuth => Status == 401;

        public override string ToString() => $"{Status} {Code}: {Message}";
    }
}
