using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LoxLorax
{
    // The game owns animation timing and transitions. These bones retarget that
    // evaluated pose onto the replacement's proportions without changing gameplay.
    internal sealed class LoxAnimation
    {
        internal static readonly string[] BoneNames = {
            "Hip", "spine1", "Spine2", "Neck", "Head",
            "Frontupperleg.l", "Frontlowerleg.l", "Frontfoot.l",
            "Frontupperleg.r", "Frontlowerleg.r", "Frontfoot.r",
            "Backupperleg.l", "Backlowerleg.l", "Backfoot.l",
            "Backupperleg.r", "Backlowerleg.r", "Backfoot.r"
        };
        // Verified against the installed Unbjorn prefab; it uses the bear rig.
        private static readonly string[] VileBoneNames = {
            "Hip", "Spine_01", "Chest", "Neck", "Head",
            "Front_UpperLeg.L", "Front_LowerLeg.L", "Front_Foot.L",
            "Front_UpperLeg.R", "Front_LowerLeg.R", "Front_Foot.R",
            "Back_UpperLeg.L", "Back_LowerLeg.L", "Back_Foot.L",
            "Back_UpperLeg.R", "Back_LowerLeg.R", "Back_Foot.R"
        };
        internal readonly Transform[] Bones;
        private readonly Transform owner;
        private readonly Transform[] source;
        private readonly Vector3[] restPositions, anchors;
        private readonly Quaternion[] inverseRestRotations;
        private readonly Matrix4x4[] inverseRestMatrices;
        private readonly bool followNativePose;
        private readonly Transform[] nativeToes;
        private readonly Vector3[][] pawVertices;
        private readonly Animator animator;
        private readonly AnimatorCullingMode previousCulling;
        private readonly List<AnimatorClipInfo> clips = new List<AnimatorClipInfo>();
        private float biteImpact = .4f;
        private readonly string biteClip;

        internal LoxAnimation(Transform owner, Transform root, GameObject original, Vector3[] anchors, Vector3[][] pawVertices, bool vile = false)
        {
            this.owner = owner;
            this.anchors = anchors;
            followNativePose = vile;
            this.pawVertices = pawVertices;
            string[] sourceNames = vile ? VileBoneNames : BoneNames;
            biteClip = vile ? "Attack Bite" : "Attack";
            // Use the actual body mesh bind pose, never a random frame of an idle
            // or attack animation. Saddle meshes can have different bind offsets.
            var skin = original.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r.sharedMesh && sourceNames.All(name => r.bones.Any(b => b && b.name == name)))
                .OrderByDescending(r => r.sharedMesh.vertexCount).FirstOrDefault();
            if (!skin) throw new InvalidOperationException("Lox animation skeleton was not found.");
            var bindings = skin.sharedMesh.bindposes;
            Matrix4x4 bindToOwner = owner.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            if (vile)
            {
                // The Vile mesh object has an additional import scale. Its live
                // transform is not the coordinate frame used to author bindposes.
                // Reconstruct that frame from the rig's unanimated Root binding.
                int rootIndex = Array.FindIndex(skin.bones, b => b && b.name == "Root");
                if (rootIndex < 0 || rootIndex >= bindings.Length)
                    throw new InvalidOperationException("Vile root binding was not found.");
                bindToOwner = owner.worldToLocalMatrix * skin.bones[rootIndex].localToWorldMatrix * bindings[rootIndex];
                var toeNames = new[] { "Front_Toe.L", "Front_Toe.R", "Back_Toe.L", "Back_Toe.R" };
                nativeToes = toeNames.Select(name => skin.bones.FirstOrDefault(b => b && b.name == name)).ToArray();
                if (nativeToes.Any(toe => !toe) || pawVertices.Length != 4 || pawVertices.Any(paw => paw.Length == 0))
                    throw new InvalidOperationException("Vile paw contact references were not found.");
            }
            source = new Transform[BoneNames.Length];
            restPositions = new Vector3[BoneNames.Length];
            inverseRestRotations = new Quaternion[BoneNames.Length];
            inverseRestMatrices = new Matrix4x4[BoneNames.Length];
            Bones = new Transform[BoneNames.Length];
            // Validate every binding before modifying the original animator.
            for (int i = 0; i < Bones.Length; i++)
            {
                int index = Array.FindIndex(skin.bones, b => b && b.name == sourceNames[i]);
                if (index < 0 || index >= bindings.Length)
                    throw new InvalidOperationException("Creature animation bone missing: " + sourceNames[i]);
                source[i] = skin.bones[index];
                Matrix4x4 rest = bindToOwner * bindings[index].inverse;
                restPositions[i] = rest.GetColumn(3);
                inverseRestRotations[i] = Quaternion.Inverse(rest.rotation);
                inverseRestMatrices[i] = rest.inverse;
            }
            for (int i = 0; i < Bones.Length; i++)
            {
                Bones[i] = new GameObject("Lorax bone " + BoneNames[i]).transform;
                Bones[i].SetParent(root, false);
                Bones[i].localPosition = anchors[i];
            }
            animator = original.GetComponentInChildren<Animator>();
            if (!animator) throw new InvalidOperationException("Lox animator was not found.");
            previousCulling = animator.cullingMode;
            var bite = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == biteClip);
            if (bite && bite.length > 0f)
            {
                var hit = bite.events.FirstOrDefault(e => e.functionName == "Hit" || e.functionName == "OnAttackTrigger");
                if (hit != null) biteImpact = Mathf.Clamp(hit.time / bite.length, .15f, .8f);
            }
            // Hidden vanilla renderers must not cause Unity to stop updating bones.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        internal void Update(bool enabled)
        {
            for (int i = 0; i < Bones.Length; i++)
            {
                if (!source[i]) continue;
                if (followNativePose && enabled)
                {
                    // Apply the complete bind-to-current transform, including scale
                    // and rotation of the Lorax's offset from the native joint.
                    // Separate position/rotation copying leaves offsets behind when
                    // the Vile rears up, and drops animated skeletal scaling.
                    Matrix4x4 deltaPose = owner.worldToLocalMatrix * source[i].localToWorldMatrix * inverseRestMatrices[i];
                    Bones[i].localPosition = deltaPose.MultiplyPoint3x4(anchors[i] * 4.2f) / 4.2f;
                    Bones[i].localRotation = deltaPose.rotation;
                    Bones[i].localScale = deltaPose.lossyScale;
                    continue;
                }
                Bones[i].localScale = Vector3.one;
                Vector3 delta = (owner.InverseTransformPoint(source[i].position) - restPositions[i]) / 4.2f;
                // Match lateral steps to the narrower Lorax stance.
                if (i >= 5) delta.x *= .65f;
                Bones[i].localPosition = enabled ? anchors[i] + delta : anchors[i];
                Bones[i].localRotation = enabled
                    ? Quaternion.Inverse(owner.rotation) * source[i].rotation * inverseRestRotations[i]
                    : Quaternion.identity;
            }
            if (enabled && followNativePose) MatchPawContact();
            if (enabled && !followNativePose) EmphasizeBite();
        }

        private void MatchPawContact()
        {
            // Different ankle heights and longer paws cannot share the bear's
            // raw joint offsets without intersecting the floor during foot rolls.
            // Match each sole to its native toe's vertical trajectory, including
            // lifted feet, swimming and attacks. No raycast or artificial gait.
            for (int leg = 0; leg < 4; leg++)
            {
                Transform foot = Bones[7 + leg * 3];
                Matrix4x4 pose = Matrix4x4.TRS(foot.localPosition, foot.localRotation, foot.localScale);
                float bottom = float.PositiveInfinity;
                foreach (Vector3 vertex in pawVertices[leg])
                    bottom = Mathf.Min(bottom, pose.MultiplyPoint3x4(vertex).y);
                float contact = owner.InverseTransformPoint(nativeToes[leg].position).y / 4.2f;
                Vector3 correction = Vector3.up * (contact - bottom);
                foot.localPosition += correction;
                // Distribute the small proportion correction through the shin.
                Bones[6 + leg * 3].localPosition += correction * .5f;
            }
        }

        private void EmphasizeBite()
        {
            float transition = animator.IsInTransition(0) ? Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime) : 0f;
            animator.GetCurrentAnimatorClipInfo(0, clips);
            AddBitePose(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f - transition);
            if (animator.IsInTransition(0))
            {
                animator.GetNextAnimatorClipInfo(0, clips);
                AddBitePose(animator.GetNextAnimatorStateInfo(0).normalizedTime, transition);
            }
        }

        private void AddBitePose(float time, float transitionWeight)
        {
            float weight = 0f;
            foreach (var clip in clips)
                if (clip.clip && clip.clip.name == biteClip) weight += clip.weight;
            weight *= transitionWeight;
            if (weight <= 0f || time < 0f || time > 1f) return;
            float anticipation = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Mathf.Max(0, biteImpact - .25f), biteImpact - .1f, time))
                * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(biteImpact - .1f, biteImpact, time)));
            float snap = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(biteImpact - .09f, biteImpact, time))
                * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(biteImpact + .04f, Mathf.Min(1f, biteImpact + .32f), time)));
            Vector3 offset = new Vector3(0, .055f * anticipation - .075f * snap, -.10f * anticipation + .23f * snap) * weight;
            float pitch = (-18f * anticipation + 26f * snap) * weight;
            Bones[4].localPosition += offset;
            Bones[4].localRotation = Quaternion.Euler(pitch, 0, 0) * Bones[4].localRotation;
            Bones[3].localPosition += offset * .65f;
            Bones[3].localRotation = Quaternion.Euler(pitch * .45f, 0, 0) * Bones[3].localRotation;
            Bones[2].localPosition += offset * .20f;
        }

        internal void Restore()
        {
            if (animator) animator.cullingMode = previousCulling;
        }

        internal static Vector3[] CreateAnchors(IList<string> groups, IList<Vector3> pivots)
        {
            if (groups.Count != 6 || groups[5] != "Head")
                throw new InvalidOperationException("Animation requires the 1.0.3 model asset. Copy both mod files.");
            float unit = pivots[1].y / .48f;
            var result = new Vector3[BoneNames.Length];
            result[0] = new Vector3(0, .53f, -.40f) * unit;
            result[1] = new Vector3(0, .54f, -.12f) * unit;
            result[2] = new Vector3(0, .60f, .27f) * unit;
            result[3] = new Vector3(0, .64f, .43f) * unit;
            result[4] = pivots[5];
            for (int group = 1; group <= 4; group++)
            {
                int bone = 5 + (group - 1) * 3;
                float side = group % 2 == 1 ? -1f : 1f;
                Vector3 hip = pivots[group];
                result[bone] = hip;
                result[bone + 1] = new Vector3(hip.x + side * .016f * unit, .22f * unit, hip.z);
                result[bone + 2] = new Vector3(hip.x + side * .02f * unit, .057f * unit, hip.z + .075f * unit);
            }
            return result;
        }

        internal static BoneWeight Weight(Vector3 vertex, int group, Vector3[] anchors)
        {
            if (group == 5) return Blend(4, 4, 0f);
            if (group != 0)
            {
                int bone = 5 + (group - 1) * 3;
                float knee = anchors[bone + 1].y;
                float ankle = anchors[bone + 2].y;
                // Blend around the joints, leaving toes rigid on the foot bone.
                float ankleBlend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ankle + .075f, ankle + .015f, vertex.y));
                if (ankleBlend > 0f) return Blend(bone + 1, bone + 2, ankleBlend);
                float kneeBlend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(knee + .09f, knee - .06f, vertex.y));
                return Blend(bone, bone + 1, kneeBlend);
            }
            if (vertex.z < anchors[1].z)
                return Blend(0, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(anchors[0].z, anchors[1].z, vertex.z)));
            if (vertex.z < anchors[2].z)
                return Blend(1, 2, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(anchors[1].z, anchors[2].z, vertex.z)));
            return Blend(2, 3, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(anchors[2].z, anchors[3].z, vertex.z)));
        }

        private static BoneWeight Blend(int a, int b, float blend)
        {
            return new BoneWeight { boneIndex0 = a, weight0 = 1f - blend, boneIndex1 = b, weight1 = blend };
        }
    }
}
