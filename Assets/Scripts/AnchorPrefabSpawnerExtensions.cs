using System;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;
using Newtonsoft.Json;

namespace Meta.XR.MRUtilityKit.Extensions
{
    [Serializable]
    public class JsonAnchorData
    {
        public string UUID;
        public string[] SemanticClassifications;
        public JsonTransform Transform;
        public JsonBounds PlaneBounds;
        public JsonBounds VolumeBounds;
        public float[][] PlaneBoundary2D;
    }

    [Serializable]
    public class JsonTransform
    {
        public float[] Translation;
        public float[] Rotation;
        public float[] Scale;
    }

    [Serializable]
    public class JsonBounds
    {
        public float[] Min;
        public float[] Max;
    }

    public static class AnchorPrefabSpawnerExtensions
    {
        // Dictionary to store additional prefabs for JSON spawning only
        private static Dictionary<MRUKAnchor.SceneLabels, List<GameObject>> jsonOnlyPrefabs = new Dictionary<MRUKAnchor.SceneLabels, List<GameObject>>();

        /// <summary>
        /// Adds prefabs that will only be used for JSON spawning (not for regular MRUK spawning)
        /// </summary>
        /// <param name="spawner">The AnchorPrefabSpawner instance</param>
        /// <param name="label">The label these prefabs should be used for</param>
        /// <param name="prefabs">List of prefabs to use for this label</param>
        public static void AddJsonOnlyPrefabs(this AnchorPrefabSpawner spawner, MRUKAnchor.SceneLabels label, List<GameObject> prefabs)
        {
            jsonOnlyPrefabs[label] = prefabs;
            Debug.Log($"[xrlit] Added {prefabs.Count} JSON-only prefabs for {label}");
        }

        /// <summary>
        /// Adds a single prefab that will only be used for JSON spawning
        /// </summary>
        /// <param name="spawner">The AnchorPrefabSpawner instance</param>
        /// <param name="label">The label this prefab should be used for</param>
        /// <param name="prefab">The prefab to use for this label</param>
        public static void AddJsonOnlyPrefab(this AnchorPrefabSpawner spawner, MRUKAnchor.SceneLabels label, GameObject prefab)
        {
            if (!jsonOnlyPrefabs.ContainsKey(label))
            {
                jsonOnlyPrefabs[label] = new List<GameObject>();
            }
            jsonOnlyPrefabs[label].Add(prefab);
            Debug.Log($"[xrlit] Added JSON-only prefab {prefab.name} for {label}");
        }
        /// <summary>
        /// Spawns a prefab based on JSON anchor data
        /// </summary>
        /// <param name="spawner">The AnchorPrefabSpawner instance</param>
        /// <param name="jsonData">JSON string containing anchor data</param>
        /// <param name="parentTransform">Parent transform to spawn under (optional)</param>
        /// <returns>The spawned GameObject, or null if spawning failed</returns>
        public static GameObject SpawnPrefabFromJson(this AnchorPrefabSpawner spawner, string jsonData, Transform parentTransform = null)
        {
            Debug.Log("[xrlit] SpawnPrefabFromJson method called!");
            Debug.Log($"[xrlit] JSON data received: {jsonData?.Substring(0, Math.Min(100, jsonData?.Length ?? 0))}...");
            
            try
            {
                Debug.Log("[xrlit] Attempting to parse JSON...");
                var anchorData = JsonConvert.DeserializeObject<JsonAnchorData>(jsonData);
                if (anchorData == null)
                {
                    Debug.LogError("[xrlit] Parsed anchor data is null!");
                    return null;
                }
                Debug.Log("[xrlit] PARSED ANCHOR DATA SUCCESSFULLY");

                return spawner.SpawnPrefabFromData(anchorData, parentTransform);

            }
            catch (Exception e)
            {
                Debug.LogError($"[xrlit] Failed to parse JSON anchor data: {e.Message}");
                Debug.LogError($"[xrlit] Stack trace: {e.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Spawns a prefab based on parsed anchor data at the exact JSON coordinates
        /// </summary>
        /// <param name="spawner">The AnchorPrefabSpawner instance</param>
        /// <param name="anchorData">Parsed anchor data</param>
        /// <param name="parentTransform">Parent transform to spawn under (optional)</param>
        /// <returns>The spawned GameObject, or null if spawning failed</returns>
        public static GameObject SpawnPrefabFromData(this AnchorPrefabSpawner spawner, JsonAnchorData anchorData, Transform parentTransform = null)
        {
            Debug.Log($"[xrlit] Spawning furniture: {string.Join(", ", anchorData.SemanticClassifications)} at position {anchorData.Transform.Translation[0]}, {anchorData.Transform.Translation[1]}, {anchorData.Transform.Translation[2]}");
            
            // Convert semantic classifications to MRUK labels
            var labels = ConvertToSceneLabels(anchorData.SemanticClassifications);
            
            // Find matching prefab using the same logic as LabelToPrefab
            var prefabToCreate = FindPrefabForLabels(spawner, labels, out var prefabGroup);
            if (prefabToCreate == null)
            {
                Debug.LogWarning($"[xrlit] No prefab found for furniture type: {string.Join(", ", anchorData.SemanticClassifications)}");
                return null;
            }

            // Create the prefab instance at the JSON coordinates
            Vector3 position = new Vector3(
                anchorData.Transform.Translation[0],
                anchorData.Transform.Translation[1], 
                anchorData.Transform.Translation[2]
            );
            
            // Create initial prefab without rotation to match MRUK spawning pattern
            var prefab = UnityEngine.Object.Instantiate(prefabToCreate, position, Quaternion.identity, parentTransform);
            prefab.name = $"{prefabToCreate.name}(JSON_{anchorData.UUID})";
            
            // Apply rotation based on JSON data with coordinate system corrections
            if (anchorData.VolumeBounds != null)
            {
                // For volume objects like beds, use JSON rotation with minimal corrections to keep them lying flat
                
                // Extract JSON rotation values
                float jsonRotationX = anchorData.Transform.Rotation[0];
                float jsonRotationY = anchorData.Transform.Rotation[1];
                float jsonRotationZ = anchorData.Transform.Rotation[2] - 90f;
                
                // For beds, we want them to lie flat, so we need to be careful with rotations
                // Apply only the Y rotation from JSON (for horizontal orientation) and minimal corrections
                Quaternion finalRotation = Quaternion.Euler(
                    0,                              // X: Keep flat (no tilting up/down)
                    jsonRotationY,                  // Y: Use JSON Y rotation for horizontal facing direction
                    0                               // Z: Keep flat (no rolling)
                );
                
                prefab.transform.rotation = finalRotation;
                
                Debug.Log($"[xrlit] Applied volume rotation for flat placement: JSON({jsonRotationX}, {jsonRotationY}, {jsonRotationZ}) -> final(0, {jsonRotationY}, 0)");
            }
            else
            {
                // For plane objects, use simpler rotation (with Z correction for coordinate system)
                Quaternion rotation = Quaternion.Euler(
                    anchorData.Transform.Rotation[0],
                    anchorData.Transform.Rotation[1],
                    anchorData.Transform.Rotation[2]  // Apply Z correction for coordinate system alignment
                );
                prefab.transform.rotation = rotation;
                
                Debug.Log($"[xrlit] Applied simple rotation for plane object: {rotation.eulerAngles}");
            }
            
            // Scale the prefab based on the volume bounds from JSON
            if (anchorData.VolumeBounds != null)
            {
                Vector3 volumeSize = new Vector3(
                    anchorData.VolumeBounds.Max[0] - anchorData.VolumeBounds.Min[0],
                    anchorData.VolumeBounds.Max[1] - anchorData.VolumeBounds.Min[1],
                    anchorData.VolumeBounds.Max[2] - anchorData.VolumeBounds.Min[2]
                );
                
                // Get the original prefab size
                var originalBounds = Utilities.GetPrefabBounds(prefabToCreate);
                if (originalBounds.HasValue)
                {
                    var prefabSize = originalBounds.Value.size;
                    
                    // Apply MRUK's scaling approach: flipped z and y to correct orientation
                    // This matches: var scale = new Vector3(volumeSize.x / prefabSize.x, volumeSize.z / prefabSize.y, volumeSize.y / prefabSize.z);
                    Vector3 scaleFactors = new Vector3(
                        volumeSize.x / prefabSize.x,  // X remains X
                        volumeSize.z / prefabSize.y,  // Z becomes Y (flipped)
                        volumeSize.y / prefabSize.z   // Y becomes Z (flipped)
                    );
                    
                    prefab.transform.localScale = scaleFactors;
                    Debug.Log($"[xrlit] Scaled {prefab.name} to {scaleFactors} using MRUK Y/Z flip (volume: {volumeSize}, prefab: {prefabSize})");
                }
            }

            Debug.Log($"[xrlit] Successfully spawned {prefab.name} at {position}");
            return prefab;
        }

        private static MRUKAnchor.SceneLabels ConvertToSceneLabels(string[] classifications)
        {
            MRUKAnchor.SceneLabels labels = 0;
            
            foreach (var classification in classifications)
            {
                if (Enum.TryParse<MRUKAnchor.SceneLabels>(classification, true, out var label))
                {
                    labels |= label;
                }
                else
                {
                    Debug.LogWarning($"[xrlit] Unknown semantic classification: {classification}");
                }
            }
            
            Debug.Log($"[xrlit] Converted labels: {labels}");
            return labels;
        }

        /// <summary>
        /// Finds a prefab based on the provided labels (checks JSON-only prefabs first, then falls back to spawner config)
        /// </summary>
        private static GameObject FindPrefabForLabels(AnchorPrefabSpawner spawner, MRUKAnchor.SceneLabels labels, out AnchorPrefabSpawner.AnchorPrefabGroup prefabGroup)
        {
            Debug.Log($"[xrlit] Looking for prefabs with labels: {labels}");
            
            // First check JSON-only prefabs
            foreach (var kvp in jsonOnlyPrefabs)
            {
                if ((kvp.Key & labels) != 0 && kvp.Value != null && kvp.Value.Count > 0)
                {
                    Debug.Log($"[xrlit] Found JSON-only prefabs for {kvp.Key}, selecting from {kvp.Value.Count} options");
                    
                    // Create a temporary prefab group for JSON-only prefabs
                    prefabGroup = new AnchorPrefabSpawner.AnchorPrefabGroup
                    {
                        Labels = kvp.Key,
                        Prefabs = kvp.Value,
                        PrefabSelection = AnchorPrefabSpawner.SelectionMode.Random, // Default to random
                        Scaling = AnchorPrefabSpawner.ScalingMode.Stretch,
                        Alignment = AnchorPrefabSpawner.AlignMode.Automatic
                    };
                    
                    // Randomly select from JSON-only prefabs
                    var randomIndex = UnityEngine.Random.Range(0, kvp.Value.Count);
                    Debug.Log($"[xrlit] Selected JSON-only prefab: {kvp.Value[randomIndex].name}");
                    return kvp.Value[randomIndex];
                }
            }
            
            // Fall back to regular spawner configuration
            Debug.Log($"[xrlit] No JSON-only prefabs found, checking regular spawner config ({spawner.PrefabsToSpawn.Count} groups)");
            
            foreach (var item in spawner.PrefabsToSpawn)
            {
                Debug.Log($"[xrlit] Checking group with labels: {item.Labels}, has {item.Prefabs?.Count ?? 0} prefabs");

                if ((item.Labels & labels) != 0 && item.Prefabs != null && item.Prefabs.Count > 0)
                {
                    Debug.Log($"[xrlit] Found matching group! Labels match: {item.Labels & labels}");
                    prefabGroup = item;

                    // Use the same selection logic as the original method
                    if (item.PrefabSelection == AnchorPrefabSpawner.SelectionMode.Random)
                    {
                        var randomIndex = UnityEngine.Random.Range(0, item.Prefabs.Count);
                        Debug.Log($"[xrlit] Selected random prefab at index {randomIndex}: {item.Prefabs[randomIndex].name}");
                        return item.Prefabs[randomIndex];
                    }
                    else if (item.PrefabSelection == AnchorPrefabSpawner.SelectionMode.ClosestSize)
                    {
                        Debug.Log($"[xrlit] Selected closest size prefab: {item.Prefabs[0].name}");
                        return item.Prefabs[0];
                    }
                    else
                    {
                        Debug.Log($"[xrlit] Selected default prefab: {item.Prefabs[0].name}");
                        return item.Prefabs[0];
                    }
                }
            }

            Debug.LogWarning($"[xrlit] No matching prefab found for labels: {labels}");
            prefabGroup = new AnchorPrefabSpawner.AnchorPrefabGroup();
            return null;
        }
    }
}
