using UnityEngine;
using Meta.XR.MRUtilityKit;
using Meta.XR.MRUtilityKit.Extensions;
using System.Collections.Generic;
using static Meta.XR.MRUtilityKit.AnchorPrefabSpawner;

public class JsonAnchorSpawnerExample : MonoBehaviour
{
    [SerializeField] private AnchorPrefabSpawner anchorPrefabSpawner;
    
    [Header("JSON-Only Prefab Groups (configured in Inspector)")]
    [Tooltip("Prefab groups that will ONLY be used for JSON spawning, not regular MRUK spawning")]
    public List<AnchorPrefabGroup> JsonOnlyPrefabsToSpawn = new List<AnchorPrefabGroup>();
    // [Header("JSON-Only Prefabs (not in regular spawner config)")]
    // [SerializeField] private GameObject[] tablePrefabs;
    // [SerializeField] private GameObject[] chairPrefabs;
    
    [TextArea(10, 20)]
    [SerializeField] private string jsonAnchorData = @"{
        ""UUID"": ""8958A163567E64993533F0056087368F"",
        ""SemanticClassifications"": [
            ""TABLE""
        ],
        ""Transform"": {
            ""Translation"": [0.07175077, 0.737635255, 0.4540899],
            ""Rotation"": [270.0, 162.821869, 0.0],
            ""Scale"": [1.0, 1.0, 1.0]
        },
        ""PlaneBounds"": {
            ""Min"": [-0.410511017, -0.312011361],
            ""Max"": [0.4105109, 0.312011361]
        },
        ""VolumeBounds"": {
            ""Min"": [-0.410511, -0.312011361, -0.774893165],
            ""Max"": [0.410510927, 0.312011361, -5.96046448E-08]
        },
        ""PlaneBoundary2D"": [
            [0.4105109, 0.3120114],
            [-0.410511017, 0.312011331],
            [-0.4105109, -0.312011361],
            [0.4105109, -0.312011272]
        ]
    }";

    private void Start()
    {
        // Register JSON-only prefabs (these won't be used by regular MRUK spawning)
        SetupJsonOnlyPrefabs();

        // Example usage of the extension
        SpawnFromJsonExample();
    }

    private void SetupJsonOnlyPrefabs()
    {
        if (anchorPrefabSpawner == null)
        {
            Debug.LogError("[xrlit] AnchorPrefabSpawner reference is missing!");
            return;
        }

        // Register all JSON-only prefab groups from the Inspector configuration
        foreach (var prefabGroup in JsonOnlyPrefabsToSpawn)
        {
            if (prefabGroup.Prefabs != null && prefabGroup.Prefabs.Count > 0)
            {
                anchorPrefabSpawner.AddJsonOnlyPrefabs(prefabGroup.Labels, prefabGroup.Prefabs);
                Debug.Log($"[xrlit] Registered {prefabGroup.Prefabs.Count} JSON-only prefabs for {prefabGroup.Labels}");
            }
            else
            {
                Debug.LogWarning($"[xrlit] Prefab group for {prefabGroup.Labels} has no prefabs assigned!");
            }
        }
        
        Debug.Log($"[xrlit] Finished registering {JsonOnlyPrefabsToSpawn.Count} JSON-only prefab groups");
    }

    public void SpawnFromJsonExample()
    {
        if (anchorPrefabSpawner == null)
        {
            Debug.LogError("[xrlit] AnchorPrefabSpawner reference is missing!");
            return;
        }
        Debug.Log("[xrlit] AnchorPrefabSpawner reference OKAY");
        // Spawn a prefab from JSON data
        GameObject spawnedPrefab = anchorPrefabSpawner.SpawnPrefabFromJson(jsonAnchorData, this.transform);
        
        if (spawnedPrefab != null)
        {
            Debug.Log($"[xrlit] Successfully spawned prefab: {spawnedPrefab.name}");
        }
        else
        {
            Debug.LogWarning("[xrlit] Failed to spawn prefab from JSON data");
        }
    }

    // Method to spawn from multiple JSON entries
    public void SpawnFromJsonArray(string[] jsonEntries)
    {
        foreach (string jsonData in jsonEntries)
        {
            anchorPrefabSpawner.SpawnPrefabFromJson(jsonData, this.transform);
        }
    }

    // Method to spawn from a JSON file
    public void SpawnFromJsonFile(string filePath)
    {
        try
        {
            string fileContent = System.IO.File.ReadAllText(filePath);
            anchorPrefabSpawner.SpawnPrefabFromJson(fileContent, this.transform);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to read JSON file: {e.Message}");
        }
    }
}
