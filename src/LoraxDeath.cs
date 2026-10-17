using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoxLorax
{
    // Cosmetic only: the real ragdoll, its physics and its loot stay at the kill.
    public sealed class LoraxDeath : MonoBehaviour
    {
        internal static readonly HashSet<LoraxDeath> Active = new HashSet<LoraxDeath>();
        private readonly List<Mesh> bakedMeshes = new List<Mesh>();
        private Transform body, head;
        private Vector3 bodyStart, headStart, forward, right;
        private Quaternion headRotation;
        private Renderer[] headRenderers;
        private float age, groundY;
        private bool landed;

        internal static void Create(Transform source, Ragdoll corpse)
        {
            if (corpse.GetComponent<LoraxDeathHandled>()) return;
            var effectObject = new GameObject("Lorax head-off sky launch");
            var effect = effectObject.AddComponent<LoraxDeath>();
            try
            {
                effect.Build(source);
                // Mark only after the complete effect exists, so failure leaves a corpse visible.
                corpse.gameObject.AddComponent<LoraxDeathHandled>();
                foreach (var renderer in corpse.GetComponentsInChildren<Renderer>(true))
                    if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) renderer.forceRenderingOff = true;
                Active.Add(effect);
            }
            catch { Destroy(effectObject); throw; }
        }

        private void Build(Transform source)
        {
            body = new GameObject("Headless Lorax body").transform;
            head = new GameObject("Detached Lorax head").transform;
            body.SetParent(transform, false);
            head.SetParent(transform, false);
            body.position = source.position;
            var headGroup = source.Find("Head");
            if (!headGroup) throw new InvalidOperationException("Lorax head group missing from model.");
            head.position = headGroup.position;
            int bodies = 0, heads = 0;
            foreach (var original in source.GetComponentsInChildren<Renderer>())
            {
                bool isHead = original.transform.IsChildOf(headGroup);
                Mesh mesh;
                var skin = original as SkinnedMeshRenderer;
                if (skin)
                {
                    mesh = new Mesh { name = "Lorax death snapshot", indexFormat = skin.sharedMesh.indexFormat };
                    bakedMeshes.Add(mesh);
                    // Compensate for the renderer scale: the snapshot object below
                    // retains the same transform, so scale must not be baked twice.
                    skin.BakeMesh(mesh, true);
                }
                else
                {
                    var filter = original.GetComponent<MeshFilter>();
                    if (!filter || !filter.sharedMesh) continue;
                    mesh = filter.sharedMesh;
                }
                var part = new GameObject(original.name) { layer = original.gameObject.layer };
                part.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                part.transform.localScale = original.transform.lossyScale;
                part.transform.SetParent(isHead ? head : body, true);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = original.sharedMaterials;
                renderer.shadowCastingMode = original.shadowCastingMode;
                if (isHead) heads++; else bodies++;
            }
            if (heads == 0 || bodies == 0) throw new InvalidOperationException("Incomplete Lorax death snapshot.");
            bodyStart = body.position;
            headStart = head.position;
            headRotation = head.rotation;
            forward = source.forward;
            right = source.right;
            groundY = source.position.y;
            int groundMask = LayerMask.GetMask("terrain", "static_solid", "piece");
            RaycastHit hit;
            if (Physics.Raycast(source.position + Vector3.up * 2f, Vector3.down, out hit, 100f, groundMask, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;
            headRenderers = head.GetComponentsInChildren<Renderer>();
        }

        private void Update()
        {
            age += Time.deltaTime;
            float launchTime = Mathf.Max(0f, age - .12f);
            body.position = bodyStart + (Vector3.up * 80f + forward * 14f + right * 8f) * launchTime
                - Vector3.up * (1.5f * launchTime * launchTime);
            body.rotation = Quaternion.Euler(launchTime * 155f, launchTime * 45f, launchTime * 85f);
            if (age > 8f) body.gameObject.SetActive(false);
            if (!landed)
            {
                head.position = headStart + (Vector3.up * 7f + forward * 4f - right * 2f) * age - Vector3.up * (7f * age * age);
                head.rotation = Quaternion.Euler(age * 130f, age * 60f, age * 75f) * headRotation;
                float bottom = float.PositiveInfinity;
                foreach (var renderer in headRenderers) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
                if (age > .35f && bottom <= groundY)
                {
                    head.position += Vector3.up * (groundY - bottom + .03f);
                    landed = true;
                }
            }
            if (age > 11f) head.localScale = Vector3.one * Mathf.Clamp01(12f - age);
            if (age >= 12f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Active.Remove(this);
            foreach (var mesh in bakedMeshes) if (mesh) Destroy(mesh);
        }
    }

    public sealed class LoraxDeathHandled : MonoBehaviour { }
}
