using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Foliage Data", menuName = "Scriptable Object/Foliage Data", order = int.MaxValue)]
public class FoliageData : ScriptableObject
{
    [SerializeField] 
    private GameObject foliagePrefab;
    [SerializeField]
    private float footprint;
    public Mesh FoliageMesh
    {
        get
        {
            meshFilter = foliagePrefab ? foliagePrefab.GetComponent<MeshFilter>() : null;
            if (meshFilter != null)
            {
                return meshFilter.sharedMesh;
            }

            return null;
        }
    }

    public Material[] FoliageMaterials
    {
        get
        {
            meshRenderer = foliagePrefab ? foliagePrefab.GetComponent<MeshRenderer>() : null;
            if (meshRenderer != null)
            {
                return meshRenderer.sharedMaterials;
            }
            return null;
        }
        
    }
    
    public float Footprint { get { return footprint; } }
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
}