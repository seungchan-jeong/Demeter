using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Foliage Data", menuName = "Scriptable Object/Foliage Data", order = int.MaxValue)]
public class FoliageData : ScriptableObject
{
    [SerializeField] 
    private GameObject foliagePrefab;
    [SerializeField]
    private Vector3 foliageScale;
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
    public Vector3 FoliageScale
    {
        get { return foliageScale; }
    }

    public Texture2D DensityMap
    {
        get
        {
            //Temp Code below
            if (Terrain.activeTerrain == null)
            {
                return null;
            }
            return Terrain.activeTerrain.terrainData.GetAlphamapTexture(0);
        }
    }

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
}