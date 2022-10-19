using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowHeightmap : MonoBehaviour
{
    public RawImage rawImage;
    public Terrain terrain;

    private void OnEnable()
    {
        rawImage.texture = terrain.terrainData.heightmapTexture;
    }
    
}
