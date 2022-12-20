using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlacableObject))]
public class PlacableObjectDebugViewer : MonoBehaviour
{
    
    void Start()
    {
        PlacableObject placableObject = GetComponent<PlacableObject>();
        if (placableObject != null)
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.materials[0].SetTexture("Foliage00", placableObject.debugTexture2D00);
            meshRenderer.materials[0].SetTexture("Foliage01", placableObject.debugTexture2D01);
            meshRenderer.materials[0].SetTexture("Foliage02", placableObject.debugTexture2D02);
        }
    }
}
