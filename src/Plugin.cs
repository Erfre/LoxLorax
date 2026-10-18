using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoxLorax
{
    [BepInPlugin(Id, "Lox Lorax", "1.0.8")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "com.loxlorax.mod";
        internal static Plugin Instance;
        internal ModelAsset Model, VileModel;
        internal ConfigEntry<float> Height, VileHeight, Yaw, VerticalOffset;
        internal ConfigEntry<bool> Motion, ReplaceVile;
        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<KeyboardShortcut> ToggleKey;
        private Harmony harmony;
        private string modelPath;
        private bool modelFailed, vileModelFailed;
        private bool replacementReported, vileReplacementReported;

        private void Awake()
        {
            Instance = this;
            Enabled = Config.Bind("General", "Enabled", true, "Replace living lox and Vile visuals. Can be changed while playing.");
            ToggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F8),
                "Enable or disable all Lorax replacements immediately. The choice is saved.");
            Height = Config.Bind("Appearance", "Height", 4.2f,
                new ConfigDescription("Model height in metres. Does not change the lox hitbox.", new AcceptableValueRange<float>(0.5f, 8f)));
            Yaw = Config.Bind("Appearance", "Yaw", 0f, "Rotation around the vertical axis, in degrees.");
            VerticalOffset = Config.Bind("Appearance", "VerticalOffset", 0f, "Vertical adjustment in metres.");
            Motion = Config.Bind("Appearance", "Motion", true, "Follow the original lox animations for legs, body and head.");
            ReplaceVile = Config.Bind("Vile", "Enabled", true, "Replace Vile (Unbjorn) with the undead Lorax. Restart after changing.");
            VileHeight = Config.Bind("Vile", "Height", 4.2f,
                new ConfigDescription("Undead Lorax height in metres. Does not change the Vile hitbox.", new AcceptableValueRange<float>(0.5f, 8f)));
            modelPath = Path.Combine(Path.GetDirectoryName(Info.Location), "lorax.llmesh");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            try
            {
                harmony = new Harmony(Id);
                harmony.PatchAll(typeof(Plugin).Assembly);
                if (Enabled.Value)
                    foreach (var character in Character.GetAllCharacters().ToArray()) Attach(character);
                Logger.LogInfo("Lox Lorax ready. Press " + ToggleKey.Value + " to enable or disable replacements.");
            }
            catch (Exception error)
            {
                if (harmony != null) { harmony.UnpatchSelf(); harmony = null; }
                if (Model != null) { Model.Dispose(); Model = null; }
                if (VileModel != null) { VileModel.Dispose(); VileModel = null; }
                Logger.LogError("Lox Lorax could not initialize " + modelPath + ". Vanilla visuals are kept. " + error.Message);
            }
        }

        private void Update()
        {
            if (ToggleKey != null && ToggleKey.Value.IsDown()) SetEnabled(!Enabled.Value, true);
        }

        private void SetEnabled(bool value, bool notify)
        {
            Enabled.Value = value;
            Config.Save();
            if (value)
            {
                foreach (var character in Character.GetAllCharacters().ToArray()) Attach(character);
            }
            else
            {
                foreach (var visual in LoraxVisual.Active.ToArray())
                    if (visual) { visual.Restore(); Destroy(visual); }
            }
            string message = "Lox Lorax " + (value ? "enabled" : "disabled");
            Logger.LogInfo(message + ".");
            if (notify && MessageHud.instance)
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, message);
        }

        internal static void Attach(Character character)
        {
            if (!Instance || !character || Instance.Enabled == null || !Instance.Enabled.Value) return;
            string prefab = character.gameObject.name;
            // Exact names avoid touching players, modded variants or unrelated prefabs.
            bool vile = prefab == "Unbjorn" || prefab == "Unbjorn(Clone)";
            if (!vile && prefab != "Lox" && prefab != "Lox(Clone)") return;
            if (vile && !Instance.ReplaceVile.Value) return;
            if (character.IsDead()) return;
            if (character.GetComponent<LoraxVisual>()) return;
            if (!Instance.EnsureModel(vile)) return;
            var visual = character.gameObject.AddComponent<LoraxVisual>();
            try
            {
                visual.Initialize(character, Instance, vile);
                if (vile && !Instance.vileReplacementReported)
                {
                    Instance.Logger.LogInfo("Undead Lorax replacement attached to Vile (Unbjorn); original body hidden.");
                    Instance.vileReplacementReported = true;
                }
                if (!vile && !Instance.replacementReported)
                {
                    Instance.Logger.LogInfo("Lorax replacement attached to a living lox; original body hidden.");
                    Instance.replacementReported = true;
                }
            }
            catch (Exception error)
            {
                visual.Restore();
                Destroy(visual);
                Instance.Logger.LogError("Could not replace creature visuals: " + error);
            }
        }

        private bool EnsureModel(bool vile = false)
        {
            if (vile ? vileModelFailed : modelFailed) return false;
            if ((vile ? VileModel : Model) != null) return true;
            try
            {
                if (vile) VileModel = ModelAsset.Load(Path.Combine(Path.GetDirectoryName(modelPath), "vile-lorax.llmesh"), true);
                else Model = ModelAsset.Load(modelPath);
                return true;
            }
            catch (Exception error)
            {
                if (vile) vileModelFailed = true; else modelFailed = true;
                Logger.LogError("Could not load " + (vile ? "undead Lorax" : "Lorax") + " model; keeping vanilla creature. " + error);
                return false;
            }
        }

        internal static void Death(Ragdoll corpse, Transform liveModel = null, bool vile = false)
        {
            if (!Instance || !corpse || corpse.GetComponent<LoraxDeathHandled>() || !Instance.EnsureModel(vile)) return;
            Transform temporary = null;
            try
            {
                if (!liveModel)
                {
                    var original = corpse.GetComponentInChildren<Renderer>();
                    temporary = (vile ? Instance.VileModel : Instance.Model).Instantiate(null, original ? original.gameObject.layer : 0, false);
                    temporary.SetPositionAndRotation(corpse.transform.position + Vector3.up * Instance.VerticalOffset.Value,
                        corpse.transform.rotation * Quaternion.Euler(0, Instance.Yaw.Value, 0));
                    temporary.localScale = Vector3.one * (vile ? Instance.VileHeight.Value : Instance.Height.Value);
                    liveModel = temporary;
                }
                LoraxDeath.Create(liveModel, corpse);
            }
            catch (Exception error) { Instance.Logger.LogError("Could not play Lorax death effect; vanilla corpse retained. " + error); }
            finally { if (temporary) { temporary.gameObject.SetActive(false); Destroy(temporary.gameObject); } }
        }

        private static bool IsReplacementCorpse(Ragdoll corpse, out bool vile)
        {
            vile = false;
            if (!Instance || Instance.Enabled == null || !Instance.Enabled.Value || !ZNetScene.instance) return false;
            foreach (string name in new[] { "Lox", "Unbjorn" })
            {
                bool undead = name == "Unbjorn";
                if (undead && (!Instance || !Instance.ReplaceVile.Value)) continue;
                var prefab = ZNetScene.instance.GetPrefab(name);
                var creature = prefab ? prefab.GetComponent<Character>() : null;
                if (!creature || creature.m_deathEffects == null || creature.m_deathEffects.m_effectPrefabs == null) continue;
                foreach (var effect in creature.m_deathEffects.m_effectPrefabs)
                {
                    if (effect.m_prefab && effect.m_prefab.GetComponent<Ragdoll>() &&
                        (corpse.name == effect.m_prefab.name || corpse.name == effect.m_prefab.name + "(Clone)"))
                    { vile = undead; return true; }
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
            foreach (var visual in LoraxVisual.Active.ToArray())
                if (visual) { visual.Restore(); Destroy(visual); }
            foreach (var death in LoraxDeath.Active.ToArray())
                if (death) Destroy(death.gameObject);
            if (Model != null) Model.Dispose();
            if (VileModel != null) VileModel.Dispose();
            if (Instance == this) Instance = null;
        }

        // Lox is a Humanoid. Its Start does not call Character.Start, so a
        // patch on the base implementation never runs for loaded/spawned lox.
        [HarmonyPatch(typeof(Humanoid), "Start")]
        private static class HumanoidStart
        {
            private static void Postfix(Character __instance) { Attach(__instance); }
        }

        [HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
        private static class LoxDeath
        {
            private static void Postfix(Humanoid __instance, Ragdoll __0)
            {
                var visual = __instance.GetComponent<LoraxVisual>();
                if (visual) visual.PlayDeath(__0);
            }
        }

        // Remote clients receive a ragdoll without running the owner's OnDeath.
        [HarmonyPatch(typeof(Ragdoll), "Start")]
        private static class RemoteLoxDeath
        {
            private static void Postfix(Ragdoll __instance)
            {
                bool vile;
                if (!__instance.GetComponent<LoraxDeathHandled>() && IsReplacementCorpse(__instance, out vile)) Death(__instance, null, vile);
            }
        }
    }

    [DefaultExecutionOrder(10000)]
    public sealed class LoraxVisual : MonoBehaviour
    {
        internal static readonly HashSet<LoraxVisual> Active = new HashSet<LoraxVisual>();
        private readonly Dictionary<Renderer, bool> originals = new Dictionary<Renderer, bool>();
        private Character character;
        private Plugin plugin;
        private Transform modelRoot;
        private LoxAnimation animation;
        private bool isVile;

        internal void Initialize(Character owner, Plugin settings, bool vile)
        {
            character = owner;
            isVile = vile;
            plugin = settings;
            // Keep the original Animator and its event-driven attacks alive.
            var originalVisual = AccessTools.Field(typeof(Character), "m_visual").GetValue(owner) as GameObject;
            if (!originalVisual) throw new InvalidOperationException("Lox visual root was not found.");
            var renderers = originalVisual.GetComponentsInChildren<Renderer>(true)
                .Where(r => (r is SkinnedMeshRenderer || r is MeshRenderer) && !IsSaddle(r.transform, originalVisual.transform)).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Lox body renderers were not found.");
            var model = vile ? plugin.VileModel : plugin.Model;
            modelRoot = model.Instantiate(transform, renderers[0].gameObject.layer);
            animation = new LoxAnimation(transform, modelRoot, originalVisual, model.AnimationAnchors, model.PawVertices, vile);
            foreach (var renderer in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.bones = animation.Bones;
                renderer.rootBone = modelRoot;
            }
            foreach (var renderer in renderers)
            {
                originals.Add(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
            Active.Add(this);
            UpdatePose();
        }

        private static bool IsSaddle(Transform node, Transform root)
        {
            for (var current = node; current && current != root; current = current.parent)
                if (current.name.IndexOf("saddle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.name.Equals("Sadel", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void LateUpdate()
        {
            if (modelRoot && character && plugin) UpdatePose();
        }

        private void UpdatePose()
        {
            modelRoot.localPosition = new Vector3(0f, plugin.VerticalOffset.Value, 0f);
            modelRoot.localRotation = Quaternion.Euler(0f, plugin.Yaw.Value, 0f);
            modelRoot.localScale = Vector3.one * (isVile ? plugin.VileHeight.Value : plugin.Height.Value);
            animation.Update(plugin.Motion.Value);
        }

        internal void PlayDeath(Ragdoll corpse)
        {
            if (modelRoot) Plugin.Death(corpse, modelRoot, isVile);
        }

        internal void Restore()
        {
            if (animation != null) { animation.Restore(); animation = null; }
            foreach (var entry in originals) if (entry.Key) entry.Key.forceRenderingOff = entry.Value;
            originals.Clear();
            if (modelRoot) { modelRoot.gameObject.SetActive(false); Destroy(modelRoot.gameObject); modelRoot = null; }
            Active.Remove(this);
        }

        private void OnDestroy() { Restore(); }
    }
}
