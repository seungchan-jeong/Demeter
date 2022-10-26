using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlacementRendererFeature : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private List<FoliageData> foliageDataList;
        private Dictionary<int, List<FoliageData>> foliageDataByFootprint;
        private Terrain mainTerrain;
        private Texture2D discretedDensityMap;

        private ComputeBuffer samplePointBuffer;
        private ComputeBuffer pointCloudBuffer;

        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        private int instanceCountSqrt = 100;

        private ComputeShader generateCS;
        private ComputeShader placementCS;

        private PoissonDiskSampler poissonDiskSampler;

        public CustomRenderPass(List<FoliageData> foliageDataList, ComputeShader placementCS, ComputeShader generateCS,
            Terrain mainTerrain, Texture2D discretedDensityMap)
        {
            this.foliageDataList = foliageDataList;
            this.placementCS = placementCS;
            this.generateCS = generateCS;
            this.mainTerrain = mainTerrain;
            // this.discretedDensityMap = discretedDensityMap;
            this.discretedDensityMap = mainTerrain.terrainData.GetAlphamapTexture(0);
            
            poissonDiskSampler = new PoissonDiskSampler();

            foliageDataByFootprint = new Dictionary<int, List<FoliageData>>();
            foreach (FoliageData foliageData in foliageDataList)
            {
                int footprint = (int)foliageData.Footprint;
                if (!foliageDataByFootprint.ContainsKey(footprint))
                {
                    foliageDataByFootprint.Add(footprint, new List<FoliageData>() { foliageData });
                }
                else
                {
                    foliageDataByFootprint[footprint].Add(foliageData);
                }
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            cmd.BeginSample("PlacementRenderer");
            foreach (KeyValuePair<int, List<FoliageData>> foliageDataAndFootprint in foliageDataByFootprint)
            {
                pointCloudBuffer =
                    new ComputeBuffer(instanceCountSqrt * instanceCountSqrt, sizeof(float) * 19 + sizeof(int), ComputeBufferType.Append);
                SetSamplePointBufferByFootprint(foliageDataAndFootprint.Key);
                
                RunPipelines(cmd, context, foliageDataAndFootprint.Value);
                DrawIndirect(cmd, context, foliageDataAndFootprint.Value);
            }

            cmd.EndSample("PlacementRenderer");

            context.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        private void SetSamplePointBufferByFootprint(float foliageDataFootprint)
        {
            List<SamplePoint> samplePoints = new List<SamplePoint>();
            List<Vector2> poissonUVs = null;
            if (Math.Abs(foliageDataFootprint - 1.0f) < Mathf.Epsilon)
            {
                poissonUVs = poissonDiskSampler.Get10000Points();
            }
            else if (Math.Abs(foliageDataFootprint - 2.0f) < Mathf.Epsilon)
            {
                poissonUVs = poissonDiskSampler.Get2500Points();
            }
            else if (Math.Abs(foliageDataFootprint - 4.0f) < Mathf.Epsilon)
            {
                poissonUVs = poissonDiskSampler.Get500Points();
            }

            if (poissonUVs == null)
            {
                return;
            }

            foreach (Vector2 pos in poissonUVs)
            {
                SamplePoint samplePoint = new SamplePoint
                {
                    densityMapUV = pos
                };
                samplePoint.heightMapUV = samplePoint.densityMapUV;
                samplePoint.threshold = 0.8f;
                samplePoints.Add(samplePoint);
            }
            
            samplePointBuffer = new ComputeBuffer(samplePoints.Count, sizeof(float) * 7);
            samplePointBuffer.SetData(samplePoints);
        }

        private void RunPipelines(CommandBuffer cmd, ScriptableRenderContext context, List<FoliageData> foliageData)
        {
            int generateCSMain = generateCS.FindKernel("CSMain");

            cmd.SetComputeFloatParam(generateCS, "TerrainWidth", mainTerrain.terrainData.size.x);
            cmd.SetComputeFloatParam(generateCS, "TerrainHeight", mainTerrain.terrainData.heightmapScale.y);
            cmd.SetComputeFloatParam(generateCS, "TerrainLength", mainTerrain.terrainData.size.z);
            
            cmd.SetComputeTextureParam(generateCS, generateCSMain, "TerrainHeightMap",
                new RenderTargetIdentifier(mainTerrain.terrainData.heightmapTexture));
            cmd.SetComputeIntParam(generateCS, "TerrainHeightMapResolution",
                mainTerrain.terrainData.heightmapResolution);

            ComputeBuffer foliageDataComputeBuffer = new ComputeBuffer(foliageData.Count, sizeof(int) * 1 + sizeof(float) * 6);
            List<FoliageComputeBufferData> foliageComputeBufferDataList = new List<FoliageComputeBufferData>(); 
            Texture2DArray densityMapArray = new Texture2DArray(foliageData[0].DensityMap.width,
                foliageData[0].DensityMap.height, foliageData.Count, foliageData[0].DensityMap.format, false);
            densityMapArray.filterMode = foliageData[0].DensityMap.filterMode;
            densityMapArray.wrapMode = foliageData[0].DensityMap.wrapMode;
            for(int i = 0; i < foliageData.Count; i++)
            {
                FoliageData item = foliageData[i];
                FoliageComputeBufferData fcbd = new FoliageComputeBufferData()
                {
                    densityMapResolution = item.DensityMap.width,
                    foliageScale = item.FoliageScale,
                    zitterScale = item.FoliageScale * 0.1f
                };
                foliageComputeBufferDataList.Add(fcbd);
                
                densityMapArray.SetPixels(item.DensityMap.GetPixels(0), i, 0);
            }
            foliageDataComputeBuffer.SetData(foliageComputeBufferDataList);
            cmd.SetComputeIntParam(generateCS, "FoliageDataCount", foliageDataList.Count);
            cmd.SetComputeBufferParam(generateCS, generateCSMain, "foliageData", foliageDataComputeBuffer);

            densityMapArray.Apply();
            cmd.SetComputeTextureParam(generateCS, generateCSMain, "DensityMaps",
                new RenderTargetIdentifier(densityMapArray));

            cmd.SetComputeBufferParam(generateCS, generateCSMain, "samplePoints", samplePointBuffer);
            cmd.SetComputeBufferParam(generateCS, generateCSMain, "foliagePoints", pointCloudBuffer);
            
            cmd.SetBufferCounterValue(pointCloudBuffer, 0);
            cmd.DispatchCompute(generateCS, generateCSMain,
                Mathf.CeilToInt((float)instanceCountSqrt * instanceCountSqrt / 64), 1, 1);
            
            // cmd.RequestAsyncReadback(pointCloudBuffer, (AsyncGPUReadbackRequest request) =>
            // {
            //     FoliagePoint[] points = request.GetData<FoliagePoint>(0).ToArray();
            //     int zeroCount = 0;
            //     foreach (FoliagePoint point in points)
            //     {
            //         Vector3 pos = new Vector3(point.TRSMat.m03, point.TRSMat.m13, point.TRSMat.m23);
            //         // Debug.Log("Pos : " + pos);
            //         if(Vector3.SqrMagnitude(pos - Vector3.zero) < Mathf.Epsilon)
            //         {
            //             zeroCount++;
            //         }
            //     }
            //     Debug.Log(zeroCount);
            // });
            
            // int placementCSMain = placementCS.FindKernel("CSMain");
            // cmd.SetComputeBufferParam(placementCS, placementCSMain,"foliagePoints", pointCloudBuffer);
            // cmd.DispatchCompute(placementCS, placementCSMain, pointCloudBuffer.count / 64,1,1);
        }

        private void DrawIndirect(CommandBuffer cmd, ScriptableRenderContext context, List<FoliageData> foliageData)
        {
            for(int i = 0 ; i < foliageData.Count; i++)
            {
                FoliageData item = foliageData[i];
                Material[] foliageMaterials = item.FoliageMaterials;
                for (int subMeshIndex = 0; subMeshIndex < item.FoliageMesh.subMeshCount; subMeshIndex++)
                {
                    if (item.FoliageMesh != null)
                    {
                        args[0] = (uint)item.FoliageMesh.GetIndexCount(subMeshIndex);
                        // args[1] = (uint)instanceCountSqrt * (uint)instanceCountSqrt; //to do : 여기서 count를 계산하지 않고 argsbuffer도 generateCS에 넘겨서, count를 거기서 update하도록 변경해야함. 
                        args[2] = (uint)item.FoliageMesh.GetIndexStart(subMeshIndex);
                        args[3] = (uint)item.FoliageMesh.GetBaseVertex(subMeshIndex);

                        ComputeBuffer argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint),
                            ComputeBufferType.IndirectArguments);
                        argsBuffer.SetData(args);
                        cmd.CopyCounterValue(pointCloudBuffer, argsBuffer, sizeof(uint));

                        foliageMaterials[subMeshIndex].SetBuffer("_PerInstanceData", pointCloudBuffer);
                        foliageMaterials[subMeshIndex].SetInt("_FoliageType", i);
                        cmd.DrawMeshInstancedIndirect(item.FoliageMesh, subMeshIndex, foliageMaterials[subMeshIndex],
                            0, argsBuffer);
                    }
                }
            }
        }

        private void FillDummySamplePointData()
        {
            List<SamplePoint> samplePoints = new List<SamplePoint>();
            // for (int i = 0; i < instanceCount; i++)
            // {
            //     SamplePoint samplePoint = new SamplePoint
            //     {
            //         // densityMapUV = new Vector2(1.0f / (float)instanceCount * i, 1.0f / (float)instanceCount * i)
            //         densityMapUV = new Vector2(Random.value, Random.value)
            //     };
            //     samplePoint.heightMapUV = samplePoint.densityMapUV;
            //     samplePoint.threshold = 0.8f;
            //     samplePoints.Add(samplePoint);
            //
            //     // float x = samplePoint.heightMapUV.x * mainTerrain.terrainData.size.z;
            //     // float y = samplePoint.heightMapUV.y * mainTerrain.terrainData.size.z;
            //     // float terrainHeight = mainTerrain.SampleHeight(new Vector3(x, 0.0f, y));
            //     // Debug.Log(
            //     //     $" height : {terrainHeight}" +
            //     //     $" x : {x}, y : {y}");
            // }

            for (int i = 0; i < instanceCountSqrt; i++)
            {
                for (int j = 0; j < instanceCountSqrt; j++)
                {
                    SamplePoint samplePoint = new SamplePoint
                    {
                        densityMapUV = new Vector2((float)i / instanceCountSqrt, (float)j / instanceCountSqrt)
                    };
                    samplePoint.heightMapUV = samplePoint.densityMapUV;
                    samplePoint.threshold = 0.8f;
                    samplePoints.Add(samplePoint);
                }
            }

            samplePointBuffer.SetData(samplePoints);
        }
    }

    struct FoliagePoint
    {
        public Matrix4x4 TRSMat;
        public Vector3 worldNormal;
        public int foliageType;
    }
    
    struct SamplePoint
    {
        public Vector2 bayerMatrixUV;
        public Vector2 densityMapUV;  //지금은 테스트 용으로 density Map과 height Map을 1:1로 매칭 
        public Vector2 heightMapUV;
        public float threshold;
    };
    
    struct FoliageComputeBufferData
    {
        public int densityMapResolution;
        public Vector3 foliageScale;
        public Vector3 zitterScale;
    };
    
    CustomRenderPass m_ScriptablePass;
    [SerializeField]
    private List<FoliageData> foliageDataList;
    [SerializeField]
    private Texture2D discretedDensityMap;
    [SerializeField]
    private ComputeShader placementCS;
    [SerializeField]
    private ComputeShader generateCS;
    [SerializeField] 
    // private Terrain mainTerrain;
    /// <inheritdoc/>
    public override void Create()
    {
        if (Terrain.activeTerrain != null)
        {
            m_ScriptablePass = new CustomRenderPass(foliageDataList, placementCS, generateCS, Terrain.activeTerrain, discretedDensityMap);
            m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (Terrain.activeTerrain != null)
        {
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}


