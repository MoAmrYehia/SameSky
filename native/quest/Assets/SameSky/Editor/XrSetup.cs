// XR configuration: OpenXR as the only loader (Android for Quest, Windows for Meta XR Simulator), the Meta
// OpenXR features, hand tracking and interaction profiles, single-pass instanced (multiview) rendering,
// and the Meta project config (hands and controllers, floor-level origin). Meta-specific editor APIs are
// reached by reflection so a renamed member fails with a clear log line instead of breaking compilation.

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace SameSky.EditorTools
{
    public static class XrSetup
    {
        const string OpenXrLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";

        // Feature type names enabled per platform (matched by simple name; logged when missing).
        static readonly string[] AndroidFeatures =
        {
            "MetaQuestFeature", "MetaXRFeature", "HandTracking", "MetaHandTrackingAim",
            "OculusTouchControllerProfile", "HandInteractionProfile",
        };

        static readonly string[] WindowsFeatures =
        {
            "MetaXRFeature", "HandTracking", "MetaHandTrackingAim", "OculusTouchControllerProfile", "HandInteractionProfile",
        };

        public static void Configure()
        {
            var perTarget = PerBuildTarget();
            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
                if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
                var general = perTarget.SettingsForBuildTarget(group);
                general.InitManagerOnStart = true;
                foreach (var loader in general.Manager.activeLoaders.ToList())
                    if (loader.GetType().FullName != OpenXrLoader)
                        XRPackageMetadataStore.RemoveLoader(general.Manager, loader.GetType().FullName, group);
                if (!XRPackageMetadataStore.IsLoaderAssigned(OpenXrLoader, group))
                    XRPackageMetadataStore.AssignLoader(general.Manager, OpenXrLoader, group);

                // A fresh project has no feature instances until the OpenXR settings are refreshed once.
                UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(group);
                var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (openxr == null) { Debug.LogWarning($"[SameSky:setup] no OpenXR settings for {group}"); continue; }
                openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                var wanted = group == BuildTargetGroup.Android ? AndroidFeatures : WindowsFeatures;
                var all = openxr.GetFeatures();
                foreach (var name in wanted)
                {
                    var f = all.FirstOrDefault(x => x.GetType().Name == name);
                    if (f == null) { Debug.LogWarning($"[SameSky:setup] OpenXR feature {name} not found for {group}"); continue; }
                    f.enabled = true;
                    EditorUtility.SetDirty(f);
                }
                if (group == BuildTargetGroup.Android) TargetQuest3(all);
                EditorUtility.SetDirty(openxr);
                Debug.Log($"[SameSky:setup] XR {group}: OpenXR + {string.Join(", ", all.Where(x => x.enabled).Select(x => x.GetType().Name))}");
            }
            ConfigureMetaProject();
            AssetDatabase.SaveAssets();
        }

        static XRGeneralSettingsPerBuildTarget PerBuildTarget()
        {
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget existing) && existing != null)
                return existing;
            System.IO.Directory.CreateDirectory("Assets/XR");
            var created = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(created, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, created, true);
            return created;
        }

        /// <summary>Meta Quest support: target Quest 3 and 3S (device list is a serialized field; set by reflection).</summary>
        static void TargetQuest3(OpenXRFeature[] features)
        {
            var quest = features.FirstOrDefault(x => x.GetType().Name == "MetaQuestFeature");
            if (quest == null) return;
            try
            {
                var so = new SerializedObject(quest);
                var devices = so.FindProperty("targetDevices");
                if (devices != null && devices.isArray)
                {
                    for (var i = 0; i < devices.arraySize; i++)
                    {
                        var el = devices.GetArrayElementAtIndex(i);
                        var visible = el.FindPropertyRelative("visibleName")?.stringValue ?? "";
                        var enabled = el.FindPropertyRelative("enabled");
                        if (enabled != null) enabled.boolValue = visible.Contains("Quest 3");
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            catch (Exception e) { Debug.LogWarning("[SameSky:setup] couldn't set Quest target devices: " + e.Message); }
        }

        /// <summary>OVRProjectConfig: hands and controllers, high-frequency hand tracking, microphone allowed.</summary>
        static void ConfigureMetaProject()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("OVRProjectConfig")).FirstOrDefault(t => t != null);
            if (type == null) { Debug.LogWarning("[SameSky:setup] OVRProjectConfig not found"); return; }
            var get = type.GetMethod("GetProjectConfig", BindingFlags.Public | BindingFlags.Static);
            var commit = type.GetMethod("CommitProjectConfig", BindingFlags.Public | BindingFlags.Static);
            var cfg = (get != null ? get.Invoke(null, get.GetParameters().Length == 0 ? null : new object[] { true }) : null)
                      ?? type.GetProperty("CachedProjectConfig", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (cfg == null) { Debug.LogWarning("[SameSky:setup] OVRProjectConfig instance not available"); return; }
            SetEnum(cfg, "handTrackingSupport", "ControllersAndHands");
            SetEnum(cfg, "handTrackingFrequency", "HIGH");
            commit?.Invoke(null, new[] { cfg });
            EditorUtility.SetDirty((UnityEngine.Object)cfg);
            Debug.Log("[SameSky:setup] Meta project config: hands and controllers");
        }

        static void SetEnum(object target, string field, string value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f == null || !f.FieldType.IsEnum) { Debug.LogWarning($"[SameSky:setup] OVRProjectConfig.{field} not found"); return; }
            if (Enum.GetNames(f.FieldType).Contains(value)) f.SetValue(target, Enum.Parse(f.FieldType, value));
        }
    }

    public static class InteractionRigSetup
    {
        /// <summary>Adds Meta's comprehensive interaction rig (OVRCameraRig + hand and controller interactors) under <paramref name="parent"/>.</summary>
        public static void AddRig(Transform parent)
        {
            var api = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Oculus.Interaction.OVR.Editor.QuickActions.OVRQuickActionsAPI")).FirstOrDefault(t => t != null);
            var add = api?.GetMethod("AddOVRInteractionRig", BindingFlags.Public | BindingFlags.Static);
            if (add == null) throw new InvalidOperationException("Meta Interaction SDK OVRQuickActionsAPI.AddOVRInteractionRig not found");
            add.Invoke(null, new object[] { false });
            var rig = UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
            if (rig == null) throw new InvalidOperationException("The interaction rig did not create an OVRCameraRig");
            rig.transform.SetParent(parent, true);
            var mgr = rig.TryGetComponent<OVRManager>(out var existing) ? existing : rig.gameObject.AddComponent<OVRManager>();
            mgr.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            Debug.Log("[SameSky:setup] interaction rig added");
        }
    }
}
