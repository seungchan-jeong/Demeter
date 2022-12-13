using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Foliage Data", menuName = "Scriptable Object/Foliage Data", order = int.MaxValue)]
public class FoliageData : ScriptableObject
{
    [SerializeField]
    private Mesh foliageMesh;
    public Mesh FoliageMesh { get { return foliageMesh; } }
    [SerializeField]
    private float footprint;
    public float Footprint { get { return footprint; } }
}