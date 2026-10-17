// Test-only integration plugin: creates its own LOCAL profile/world in -savedir.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

[BepInPlugin("com.loxlorax.tests.world", "Lox Lorax world regression", "1.0.0")]
[BepInDependency(LoxLorax.Plugin.Id)]
public sealed class WorldRegression : BaseUnityPlugin
{
    private string resultDirectory;
    private IEnumerator Start()
    {
        resultDirectory=Environment.GetEnvironmentVariable("LOXLORAX_TEST_DIR");
        if (String.IsNullOrEmpty(resultDirectory)) yield break;
        yield return new WaitForSeconds(12f);
        try
        {
            foreach(string name in new[]{"world-pass.txt","world-fail.txt","world-lox.png","animation-pass.txt"})
            {
                string marker=Path.Combine(resultDirectory,name);
                if(File.Exists(marker)) File.Delete(marker);
            }
            var startup=UnityEngine.Object.FindFirstObjectByType<FejdStartup>();
            if(!startup) throw new Exception("Main menu did not load.");
            var profile=new PlayerProfile("LoxLoraxRegression",FileHelpers.FileSource.Local);
            profile.SetName("LoxLoraxRegression");
            profile.Save();
            Game.SetProfile("LoxLoraxRegression",FileHelpers.FileSource.Local);
            var world=new World("LoxLoraxRegression","LoraxTest1");
            world.m_fileSource=FileHelpers.FileSource.Local;
            ZNet.SetServer(true,false,false,"LoxLoraxRegression","",world);
            ZNet.ResetServerHost();
            Logger.LogInfo("LOXLORAX_WORLD_STAGE loading isolated local world");
            AccessTools.Method(typeof(FejdStartup),"LoadMainScene").Invoke(startup,null);
        }
        catch(Exception error) { Fail(error); yield break; }
        float deadline=Time.realtimeSinceStartup+120f;
        while((!ZNetScene.instance || !ZNetScene.instance.GetPrefab("Lox") || !Player.m_localPlayer) && Time.realtimeSinceStartup<deadline)
            yield return new WaitForSeconds(1f);
        GameObject lox=null, boar=null;
        try
        {
            if(!ZNetScene.instance) throw new Exception("World did not initialize.");
            bool animationTest=Environment.GetEnvironmentVariable("LOXLORAX_ANIMATION_TEST")=="1";
            var prefab=ZNetScene.instance.GetPrefab(animationTest ? "Unbjorn" : "Lox");
            if(!prefab) throw new Exception("Vanilla Lox prefab was not loaded.");
            Logger.LogInfo("LOXLORAX_WORLD_STAGE prefab type="+prefab.GetComponent<Character>().GetType().Name);
            if(!Player.m_localPlayer) throw new Exception("Local test player did not spawn.");
            Vector3 position=Player.m_localPlayer.transform.position+new Vector3(5,10,0);
            lox=Spawn(prefab,position);
            boar=Spawn(ZNetScene.instance.GetPrefab("Boar"),position+Vector3.right*20);
        }
        catch(Exception error) { Fail(error); yield break; }
        yield return new WaitForSeconds(2f);
        try
        {
            if(Environment.GetEnvironmentVariable("LOXLORAX_ANIMATION_TEST")=="1")
            {
                Capture(lox,false);
                string result=AnimationRegression.Run(lox,resultDirectory);
                Logger.LogInfo("LOXLORAX_ANIMATION_PASS "+result);
                Application.Quit(0);yield break;
            }
            CheckLox(lox,"newly spawned lox");
            if(boar.GetComponent<LoxLorax.LoraxVisual>()) throw new Exception("Non-lox creature was modified.");
            Capture(lox);
            lox.GetComponent<Character>().SetTamed(true);
        }
        catch(Exception error) { Fail(error); yield break; }
        yield return new WaitForSeconds(1f);
        try
        {
            CheckLox(lox,"tamed lox");
            // The live hotkey uses this path: disable must restore vanilla now.
            var plugin=BepInEx.Bootstrap.Chainloader.PluginInfos[LoxLorax.Plugin.Id].Instance;
            AccessTools.Method(plugin.GetType(),"SetEnabled").Invoke(plugin,new object[]{false,false});
        }
        catch(Exception error) { Fail(error); yield break; }
        yield return null;
        try
        {
            var original=lox.transform.Find("Visual");
            if(original.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r=>r.forceRenderingOff))
                throw new Exception("Live disable did not restore the vanilla lox.");
            var plugin=BepInEx.Bootstrap.Chainloader.PluginInfos[LoxLorax.Plugin.Id].Instance;
            AccessTools.Method(plugin.GetType(),"SetEnabled").Invoke(plugin,new object[]{true,false});
            CheckLox(lox,"live re-enabled lox");
            string result="PASS: spawned real Lox (Humanoid) in isolated world; replacement attached automatically; vanilla body hidden; original Animator and collider retained; tamed lox retained replacement; boar untouched; live disable restored vanilla body; live enable reattached replacement; actual lox render captured.";
            File.WriteAllText(Path.Combine(resultDirectory,"world-pass.txt"),result);
            Logger.LogInfo("LOXLORAX_WORLD_PASS "+result);
            Application.Quit(0);
        }
        catch(Exception error) { Fail(error); }
    }

    private static GameObject Spawn(GameObject prefab,Vector3 position)
    {
        var obj=Instantiate(prefab,position,Quaternion.identity);
        var ai=obj.GetComponent<BaseAI>(); if(ai) ai.enabled=false;
        var body=obj.GetComponent<Rigidbody>(); if(body) { body.useGravity=false; body.constraints=RigidbodyConstraints.FreezeAll; }
        return obj;
    }
    private static void CheckLox(GameObject obj,string label)
    {
        if(!obj) throw new Exception(label+": test actor was unloaded before the assertion.");
        if(!obj.GetComponent<LoxLorax.LoraxVisual>()) throw new Exception(label+": LoraxVisual missing; vanilla lox remains.");
        var replacement=obj.transform.Find("Lorax replacement");
        if(!replacement || replacement.GetComponentsInChildren<SkinnedMeshRenderer>().Length!=22) throw new Exception(label+": replacement geometry missing.");
        foreach(string limb in new[]{"Front left leg","Front right leg","Rear left leg","Rear right leg"})
            if(!replacement.Find(limb)) throw new Exception(label+": missing quadruped limb "+limb);
        foreach(var renderer in replacement.GetComponentsInChildren<SkinnedMeshRenderer>())
            if(renderer.bones.Length!=17 || !renderer.rootBone)
                throw new Exception(label+": skin is missing animation bones.");
        var original=obj.transform.Find("Visual");
        var bodies=original.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var visibleBodies=bodies.Where(r=>!r.forceRenderingOff && r.name!="Sadel").ToArray();
        if(bodies.Length==0 || visibleBodies.Length!=0)
            throw new Exception(label+": vanilla body still visible: "+String.Join(", ",visibleBodies.Select(r=>r.name).ToArray()));
        if(original.GetComponentInChildren<Animator>().cullingMode!=AnimatorCullingMode.AlwaysAnimate)
            throw new Exception(label+": hidden original skeleton can stop animating.");
        if(!original.GetComponentInChildren<Animator>().enabled || !obj.GetComponent<CapsuleCollider>().enabled) throw new Exception(label+": gameplay components disabled.");
    }
    private void Capture(GameObject obj, bool orange=true)
    {
        foreach(var renderer in obj.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer=30;
        if(!orange)
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name="Test ground at creature origin";floor.layer=30;
            floor.transform.position=obj.transform.position-Vector3.up*.04f;
            floor.transform.localScale=new Vector3(9,.08f,9);
            var material=new Material(obj.transform.Find("Lorax replacement").GetComponentInChildren<Renderer>().sharedMaterial);material.mainTexture=null;material.color=new Color(.16f,.22f,.19f);
            floor.GetComponent<Renderer>().sharedMaterial=material;
        }
        var camera=new GameObject("Regression camera").AddComponent<Camera>();
        camera.enabled=false; camera.cullingMask=1<<30;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.06f,.08f,.10f);
        camera.transform.position=obj.transform.position+new Vector3(-10,6.5f,12);
        camera.transform.LookAt(obj.transform.position+Vector3.up*2f);
        camera.orthographic=true; camera.orthographicSize=3.8f; camera.farClipPlane=40;
        var light=new GameObject("Regression light").AddComponent<Light>();
        light.type=LightType.Directional; light.intensity=1.2f;light.cullingMask=1<<30;
        light.transform.rotation=Quaternion.Euler(35,145,0);
        var target=new RenderTexture(900,900,24);camera.targetTexture=target;
        camera.Render();camera.Render();RenderTexture.active=target;
        var image=new Texture2D(900,900,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,900,900),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(resultDirectory,"world-lox.png"),ImageConversion.EncodeToPNG(image));
        int pixels=image.GetPixels32().Count(p=>p.r>80 && p.r>p.b*1.4f);
        if(orange && pixels<40000) throw new Exception("Actual lox replacement render is blank: "+pixels);
    }
    private void Fail(Exception error)
    {
        Logger.LogError("LOXLORAX_WORLD_FAIL "+error);
        File.WriteAllText(Path.Combine(resultDirectory,"world-fail.txt"),error.ToString());
        Application.Quit(1);
    }
}
