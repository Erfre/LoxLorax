// Test-only: sample the installed creature's actual clips and compare native skin transforms.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

internal static class AnimationRegression
{
    internal static string Run(GameObject creature, string directory)
    {
        var visual=creature.GetComponent<LoxLorax.LoraxVisual>();
        if(!visual) throw new Exception("Vile replacement did not attach.");
        var root=creature.transform.Find("Lorax replacement");
        var original=creature.transform.Find("Visual");
        var animator=original.GetComponentInChildren<Animator>();
        var controller=animator.runtimeAnimatorController;
        var names=new[]{"Hip","Spine_01","Chest","Neck","Head","Front_UpperLeg.L","Front_LowerLeg.L","Front_Foot.L","Front_UpperLeg.R","Front_LowerLeg.R","Front_Foot.R","Back_UpperLeg.L","Back_LowerLeg.L","Back_Foot.L","Back_UpperLeg.R","Back_LowerLeg.R","Back_Foot.R"};
        var aliases=new[]{"Hip","spine1","Spine2","Neck","Head","Frontupperleg.l","Frontlowerleg.l","Frontfoot.l","Frontupperleg.r","Frontlowerleg.r","Frontfoot.r","Backupperleg.l","Backlowerleg.l","Backfoot.l","Backupperleg.r","Backlowerleg.r","Backfoot.r"};
        var skin=original.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(r=>r.sharedMesh && names.All(n=>r.bones.Any(b=>b && b.name==n)))
            .OrderByDescending(r=>r.sharedMesh.vertexCount).First();
        var source=new Transform[17];var target=new Transform[17];var rest=new Matrix4x4[17];
        for(int i=0;i<17;i++)
        {
            int index=Array.FindIndex(skin.bones,b=>b && b.name==names[i]);source[i]=skin.bones[index];
            target[i]=root.Find("Lorax bone "+aliases[i]);
            int rootIndex=Array.FindIndex(skin.bones,b=>b && b.name=="Root");
            var bindRelativeToRoot=skin.sharedMesh.bindposes[rootIndex]*skin.sharedMesh.bindposes[index].inverse;
            rest[i]=creature.transform.worldToLocalMatrix*skin.bones[rootIndex].localToWorldMatrix*bindRelativeToRoot;
            if(!target[i]) throw new Exception("Missing mapped bone: "+names[i]);
        }
        Bounds liveBounds=RenderedBounds(root.GetComponentsInChildren<SkinnedMeshRenderer>(),creature.transform);
        float liveHeight=liveBounds.size.y;
        if(liveBounds.min.y < -.2f || liveBounds.min.y > .35f)
            throw new Exception("Vile rendered feet miss the creature ground origin: minY="+liveBounds.min.y);
        if(liveHeight<3.2f || liveHeight>5.5f)
            throw new Exception("Vile rig unit conversion changed the intended 4.2m model height: "+liveHeight);
        // Freeze only this test actor, then read the replacement's unanimated anchors.
        var plugin=BepInEx.Bootstrap.Chainloader.PluginInfos[LoxLorax.Plugin.Id].Instance;
        var motion=(BepInEx.Configuration.ConfigEntry<bool>)AccessTools.Field(plugin.GetType(),"Motion").GetValue(plugin);
        bool oldMotion=motion.Value;bool oldEvents=animator.fireEvents;bool oldEnabled=animator.enabled;
        var update=AccessTools.Method(visual.GetType(),"LateUpdate");
        var lines=new List<string>();float maxPosition=0,maxAngle=0;int samples=0,changingClips=0;
        int idleState=animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        if(!animator.HasState(0,idleState)) throw new Exception("Native animator has not entered a valid state.");
        var testController=new AnimatorOverrideController(controller);
        var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();
        testController.GetOverrides(overrides);
        try
        {
            animator.enabled=false;animator.fireEvents=false;
            motion.Value=false;update.Invoke(visual,null);
            Bounds restBounds=RenderedBounds(root.GetComponentsInChildren<SkinnedMeshRenderer>(),creature.transform);
            if(Mathf.Abs(restBounds.min.y)>.01f || Mathf.Abs(restBounds.max.y-4.2f)>.01f)
                throw new Exception("Rest mesh measurement does not match authored height/ground: "+restBounds);
            var anchors=target.Select(b=>b.localPosition*4.2f).ToArray();
            motion.Value=true;
            update.Invoke(visual,null);
            CheckDeathSnapshot(root,creature.transform);
            var legs=root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.transform.parent.name.EndsWith(" leg")).ToArray();
            var toes=skin.bones.Where(b=>b && (b.name.StartsWith("Front_Toe.") || b.name.StartsWith("Back_Toe."))).ToArray();
            if(legs.Length!=4 || toes.Length!=4) throw new Exception("Missing foot geometry or native toe reference.");
            int groundedPoses=0;float lowestFoot=float.PositiveInfinity;
            var clips=controller.animationClips.Distinct().ToArray();
            foreach(var clip in clips)
            {
                // Evaluate through the native Animator/avatar, rather than raw
                // SampleAnimation, which can bypass imported rig/root handling.
                for(int n=0;n<overrides.Count;n++) overrides[n]=new KeyValuePair<AnimationClip,AnimationClip>(overrides[n].Key,clip);
                testController.ApplyOverrides(overrides);
                animator.runtimeAnimatorController=testController;
                animator.Rebind();
                Vector3[] firstPositions=null;Quaternion[] firstRotations=null;bool changed=false;
                foreach(float phase in new[]{0f,.2f,.45f,.7f,.95f})
                {
                    animator.Play(idleState,0,phase);
                    animator.Update(0f);
                    var poses=source.Select(b=>creature.transform.worldToLocalMatrix*b.localToWorldMatrix).ToArray();
                    if(firstPositions==null) {firstPositions=poses.Select(m=>(Vector3)m.GetColumn(3)).ToArray();firstRotations=poses.Select(m=>m.rotation).ToArray();}
                    else for(int i=0;i<17;i++)
                        changed |= Vector3.Distance(firstPositions[i],poses[i].GetColumn(3))>.0001f || Quaternion.Angle(firstRotations[i],poses[i].rotation)>.02f;
                    update.Invoke(visual,null);
                    if(animator.runtimeAnimatorController!=testController) throw new Exception("Replacement altered the native animation controller.");
                    for(int i=0;i<17;i++)
                    {
                        // The native delta moves any point from bind pose into this clip pose.
                        Matrix4x4 nativeDelta=poses[i]*rest[i].inverse;
                        Vector3 expected=nativeDelta.MultiplyPoint3x4(anchors[i])/4.2f;
                        Vector3 error=target[i].localPosition-expected;
                        // Shin/paw height is adapted to the Lorax's paw dimensions.
                        // Their horizontal paths and all native rotations/scales must match.
                        if(i>=5 && (i-5)%3!=0) error.y=0;
                        float distance=error.magnitude;
                        float angle=Quaternion.Angle(target[i].localRotation,nativeDelta.rotation);
                        float scaleError=Vector3.Distance(target[i].localScale,nativeDelta.lossyScale);
                        maxPosition=Mathf.Max(maxPosition,distance);maxAngle=Mathf.Max(maxAngle,angle);
                        if(distance>.001f || angle>.1f || scaleError>.001f)
                            throw new Exception("Native animation mismatch: "+clip.name+" phase="+phase+" bone="+names[i]+" position="+distance+" rotation="+angle+" scale="+scaleError);
                        if(Vector3.Distance(source[i].position,creature.transform.TransformPoint(poses[i].GetColumn(3)))>.001f)
                            throw new Exception("Replacement modified the original skeleton.");
                    }
                    if(clip.name=="Idle" || clip.name.StartsWith("Walk") || clip.name.StartsWith("Jog") || clip.name.StartsWith("Run") || clip.name.StartsWith("Turn"))
                    {
                        float feetY=RenderedBounds(legs,creature.transform).min.y;
                        float nativeY=toes.Min(t=>creature.transform.InverseTransformPoint(t.position).y);
                        if(feetY < nativeY-.2f)
                            throw new Exception("Paws penetrate below native ground contact: "+clip.name+" phase="+phase+" paws="+feetY+" native toes="+nativeY);
                        lowestFoot=Mathf.Min(lowestFoot,feetY);groundedPoses++;
                    }
                    samples++;
                }
                if(changed) changingClips++;
                lines.Add("PASS "+clip.name+" (5 sampled poses; native animation changes="+changed+")");
            }
            if(changingClips<20) throw new Exception("Clip sampling did not exercise enough real motion: "+changingClips);
            if(root.GetComponentsInChildren<Animator>().Length!=0)
                throw new Exception("Replacement changed the original animation controller.");
            var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>();
            if(renderers.Length!=24 || renderers.Any(r=>r.bones.Length!=17 || !r.sharedMesh))
                throw new Exception("Undead model skin is incomplete.");
            var baked=new Mesh();renderers.First(r=>r.sharedMesh.vertexCount>1000).BakeMesh(baked,true);
            if(baked.vertexCount<1000 || baked.bounds.size.sqrMagnitude<.001f) throw new Exception("Animated skin is empty.");
            UnityEngine.Object.Destroy(baked);
            string summary="PASS: Vile's "+clips.Length+" original clips, "+samples+" sampled poses, 17 mapped bones evaluated through the native Animator/avatar with paw-height adaptation; max position error="+maxPosition+"m normalized, max rotation error="+maxAngle+" degrees; original controller retained; live model height="+liveHeight+"m; live sole height="+liveBounds.min.y+"m; "+groundedPoses+" locomotion poses checked for ground penetration (lowest sole="+lowestFoot+"m); death snapshot matches live geometry.";
            lines.Insert(0,summary);File.WriteAllLines(Path.Combine(directory,"animation-pass.txt"),lines.ToArray());return summary;
        }
        finally {motion.Value=oldMotion;animator.runtimeAnimatorController=controller;animator.Rebind();animator.fireEvents=oldEvents;animator.enabled=oldEnabled;UnityEngine.Object.Destroy(testController);}
    }

    // Unity 6 BakeMesh(true) compensates for renderer scale. The resulting
    // vertices can then use the renderer's full transform exactly once.
    private static Bounds RenderedBounds(IEnumerable<SkinnedMeshRenderer> skins, Transform owner)
    {
        Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
        var mesh=new Mesh();
        try
        {
            foreach(var skin in skins)
            {
                skin.BakeMesh(mesh,true);
                foreach(var v in mesh.vertices)
                {
                    Vector3 point=owner.InverseTransformPoint(skin.transform.TransformPoint(v));
                    min=Vector3.Min(min,point);max=Vector3.Max(max,point);
                }
                mesh.Clear();
            }
        }
        finally {UnityEngine.Object.Destroy(mesh);}
        var result=new Bounds();result.SetMinMax(min,max);return result;
    }

    private static void CheckDeathSnapshot(Transform root, Transform owner)
    {
        Bounds live=RenderedBounds(root.GetComponentsInChildren<SkinnedMeshRenderer>(),owner);
        var obj=new GameObject("Test death snapshot");
        try
        {
            var effect=obj.AddComponent<LoxLorax.LoraxDeath>();
            AccessTools.Method(effect.GetType(),"Build").Invoke(effect,new object[]{root});
            Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
            foreach(var part in obj.GetComponentsInChildren<MeshFilter>())
                foreach(var vertex in part.sharedMesh.vertices)
                {
                    Vector3 point=owner.InverseTransformPoint(part.transform.TransformPoint(vertex));
                    min=Vector3.Min(min,point);max=Vector3.Max(max,point);
                }
            if(Vector3.Distance(min,live.min)>.005f || Vector3.Distance(max,live.max)>.005f)
                throw new Exception("Death snapshot changes the live model bounds: "+min+" to "+max+" expected="+live);
        }
        finally {UnityEngine.Object.Destroy(obj);}
    }

}
