using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlacableObject : MonoBehaviour
{
    public List<FoliageData> foliageDatas;
    public Texture2D debugTexture2D00;
    public Texture2D debugTexture2D01;
    public Texture2D debugTexture2D02;
    public Texture2D debugTexture2D03;
    public Dictionary<FoliageData, Texture2D> densityMapForFoliageData;

    private void Start()
    {
        if (densityMapForFoliageData == null)
        {
            if (foliageDatas.Count > 3)
            {
                densityMapForFoliageData = new Dictionary<FoliageData, Texture2D>();
                densityMapForFoliageData.Add(foliageDatas[0], debugTexture2D00);
                densityMapForFoliageData.Add(foliageDatas[1], debugTexture2D01);
                densityMapForFoliageData.Add(foliageDatas[2], debugTexture2D02);
                densityMapForFoliageData.Add(foliageDatas[3], debugTexture2D03);
            }
        }
        
    }

    private void OnEnable()
    {
        if (foliageDatas.Count > 3)
        {
            densityMapForFoliageData = new Dictionary<FoliageData, Texture2D>();
            densityMapForFoliageData.Add(foliageDatas[0], debugTexture2D00);
            densityMapForFoliageData.Add(foliageDatas[1], debugTexture2D01);
            densityMapForFoliageData.Add(foliageDatas[2], debugTexture2D02);
            densityMapForFoliageData.Add(foliageDatas[3], debugTexture2D03);
        }
    }
    //or FoliageData에 OnMeshDensityMap 라는 List를 넣고, PlacableObject가 Enable되었을 때 자기가 등록하게 만들기
}
