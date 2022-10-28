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

        private Dictionary<int, ComputeBuffer> samplePointBufferDict;
        private Dictionary<int, ComputeBuffer> foliageComputeBufferDataDict;
        private List<ComputeBuffer> pointCloudBufferList;
        private const int POINT_CLOUD_BUFFER_NUM_MAX = 4;
        
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        private int instanceCountSqrt = 100;

        private ComputeShader generateCS;
        private ComputeShader placementCS;

        private PoissonDiskSampler poissonDiskSampler;

        public CustomRenderPass(List<FoliageData> foliageDataList, ComputeShader placementCS, ComputeShader generateCS, Terrain mainTerrain)
        {
            this.foliageDataList = foliageDataList;
            this.placementCS = placementCS;
            this.generateCS = generateCS;
            this.mainTerrain = mainTerrain;

            poissonDiskSampler = new PoissonDiskSampler();
            pointCloudBufferList = new List<ComputeBuffer>();
            foliageDataByFootprint = new Dictionary<int, List<FoliageData>>();
            samplePointBufferDict = new Dictionary<int, ComputeBuffer>();
            foliageComputeBufferDataDict = new Dictionary<int, ComputeBuffer>();

            InitFoliageDictionary();
            InitSamplePointBuffer();
            InitFoliageDataBuffer();
        }

        private void InitFoliageDictionary()
        {
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
        
        private void InitSamplePointBuffer()
        {
            foreach (FoliageData foliageData in foliageDataList)
            {
                float foliageDataFootprint = foliageData.Footprint;

                if (samplePointBufferDict.ContainsKey((int)foliageDataFootprint))
                {
                    continue;
                }

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
                    continue;
                }

                foreach (Vector2 pos in poissonUVs)
                {
                    SamplePoint samplePoint = new SamplePoint
                    {
                        densityMapUV = pos,
                        heightMapUV = pos,
                        threshold = 0.8f,
                        pad = 0.0f
                    };
                    samplePoints.Add(samplePoint);
                }
                
                ComputeBuffer samplePointBuffer = new ComputeBuffer(samplePoints.Count, sizeof(float) * 8); 
                samplePointBuffer.SetData(samplePoints);
                samplePointBufferDict.Add((int)foliageDataFootprint, samplePointBuffer);
            }
        }

        private void InitFoliageDataBuffer()
        {
            foreach(var foliageDataByFootprint in foliageDataByFootprint)
            {
                List<FoliageData> currentList = foliageDataByFootprint.Value;
                List<FoliageComputeBufferData> foliageComputeBufferDataList = new List<FoliageComputeBufferData>();
                for(int i = 0; i < currentList.Count; i++)
                {
                    FoliageData item = currentList[i];
                    FoliageComputeBufferData fcbd = new FoliageComputeBufferData()
                    {
                        densityMapResolution = item.DensityMap.width,
                        foliageScale = item.FoliageScale,
                        zitterScale = item.FoliageScale * 0.1f,
                        pad = 0.0f
                    };
                    foliageComputeBufferDataList.Add(fcbd);
                }
                
                ComputeBuffer foliageDataComputeBuffer = new ComputeBuffer(currentList.Count, sizeof(int) * 1 + sizeof(float) * 7);
                foliageDataComputeBuffer.SetData(foliageComputeBufferDataList);
                foliageComputeBufferDataDict.Add(foliageDataByFootprint.Key, foliageDataComputeBuffer);
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            int generateCSMain = generateCS.FindKernel("CSMain");
            
            cmd.SetComputeFloatParam(generateCS, "TerrainWidth", mainTerrain.terrainData.size.x);
            cmd.SetComputeFloatParam(generateCS, "TerrainHeight", mainTerrain.terrainData.heightmapScale.y);
            cmd.SetComputeFloatParam(generateCS, "TerrainLength", mainTerrain.terrainData.size.z);
            
            cmd.SetComputeTextureParam(generateCS, generateCSMain, "TerrainHeightMap",
                new RenderTargetIdentifier(mainTerrain.terrainData.heightmapTexture));
            cmd.SetComputeIntParam(generateCS, "TerrainHeightMapResolution",
                mainTerrain.terrainData.heightmapResolution);
            
        }
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            cmd.BeginSample("PlacementRenderer");
            foreach (KeyValuePair<int, List<FoliageData>> foliageDataAndFootprint in foliageDataByFootprint)
            {
                InitPointCloudBufferList(foliageDataAndFootprint.Value.Count, samplePointBufferDict[foliageDataAndFootprint.Key].count);
                RunPipelines(cmd, context, foliageDataAndFootprint.Key);
                DrawIndirect(cmd, context, foliageDataAndFootprint.Value);
            }

            cmd.EndSample("PlacementRenderer");

            context.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }
        
        private void InitPointCloudBufferList(int cloudBufferCount, int instanceCount)
        {
            pointCloudBufferList.Clear(); //todo 이거 꼭 new 해야해? 재사용 불가능? 
            for (int i = 0; i < cloudBufferCount; i++)
            {
                pointCloudBufferList.Add(new ComputeBuffer(instanceCount, sizeof(float) * 19 + sizeof(int), ComputeBufferType.Append));
            }
        }

        private void RunPipelines(CommandBuffer cmd, ScriptableRenderContext context, int footprint)
        {
            int generateCSMain = generateCS.FindKernel("CSMain");
            
            //1. Set Foliage Data Info
            cmd.SetComputeIntParam(generateCS, "FoliageDataCount", foliageComputeBufferDataDict[footprint].count);
            cmd.SetComputeBufferParam(generateCS, generateCSMain, "foliageData", foliageComputeBufferDataDict[footprint]);

            //2. Set Density Maps
            // List<FoliageData> currentFoliageDataList = foliageDataByFootprint[footprint];
            // Texture2DArray densityMapArray = new Texture2DArray(currentFoliageDataList[0].DensityMap.width,
            //     currentFoliageDataList[0].DensityMap.height, currentFoliageDataList.Count,
            //     currentFoliageDataList[0].DensityMap.format, false);
            // densityMapArray.filterMode = currentFoliageDataList[0].DensityMap.filterMode;
            // densityMapArray.wrapMode = currentFoliageDataList[0].DensityMap.wrapMode;
            // for (int i = 0; i < currentFoliageDataList.Count; i++)
            // {
            //     FoliageData item = currentFoliageDataList[i];
            //     densityMapArray.SetPixels(item.DensityMap.GetPixels(0), i, 0);
            //     // Debug.Log("SetPixels");
            // }
            // densityMapArray.Apply();
            cmd.SetComputeTextureParam(generateCS, generateCSMain, "DensityMap", new RenderTargetIdentifier(mainTerrain.terrainData.GetAlphamapTexture(0)));

            //3. Set Sample Points
            cmd.SetComputeBufferParam(generateCS, generateCSMain, "samplePoints", samplePointBufferDict[footprint]);
            
            //4. Set Output Buffer
            for (int i = 0; i < pointCloudBufferList.Count; i++)
            {
                cmd.SetComputeBufferParam(generateCS, generateCSMain, "foliagePoints0" + (i+1), pointCloudBufferList[i]);
                cmd.SetBufferCounterValue(pointCloudBufferList[i], 0);
            }
            for (int i = pointCloudBufferList.Count; i < POINT_CLOUD_BUFFER_NUM_MAX; i++)
            {
                ComputeBuffer temp = new ComputeBuffer(1, 4);
                cmd.SetComputeBufferParam(generateCS, generateCSMain, "foliagePoints0" + (i+1), temp);
            }
            
            //5. Dispatch 
            cmd.DispatchCompute(generateCS, generateCSMain,
                Mathf.CeilToInt((float)samplePointBufferDict[footprint].count / 64), 1, 1);
            
            // cmd.RequestAsyncReadback(pointCloudBufferList[0], (AsyncGPUReadbackRequest request) =>
            // {
            //     FoliagePoint[] points = request.GetData<FoliagePoint>(0).ToArray();
            //     int zeroCount = 0;
            //     foreach (FoliagePoint point in points)
            //     {
            //         Vector3 pos = new Vector3(point.TRSMat.m03, point.TRSMat.m13, point.TRSMat.m23);
            //         Debug.Log("Pos : " + pos);
            //         // if(Vector3.SqrMagnitude(pos - Vector3.zero) < Mathf.Epsilon)
            //         // {
            //         //     zeroCount++;
            //         // }
            //     }
            //     // Debug.Log("zero : " + zeroCount);
            // });
            // int placementCSMain = placementCS.FindKernel("CSMain");
            // cmd.SetComputeBufferParam(placementCS, placementCSMain,"foliagePoints", pointCloudBuffer);
            // cmd.DispatchCompute(placementCS, placementCSMain, pointCloudBuffer.count / 64,1,1);
        }

        private void DrawIndirect(CommandBuffer cmd, ScriptableRenderContext context, List<FoliageData> foliageData)
        {
            for(int i = 0 ; i < foliageData.Count; i++) //todo foliageData.Count 와 pointCloudBuffer.Count는 항상 같다. 이걸 보장할 방법 찾기. 
            {
                FoliageData item = foliageData[i];
                Material[] foliageMaterials = item.FoliageMaterials;
                for (int subMeshIndex = 0; subMeshIndex < item.FoliageMesh.subMeshCount; subMeshIndex++)
                {
                    if (item.FoliageMesh != null)
                    {
                        args[0] = (uint)item.FoliageMesh.GetIndexCount(subMeshIndex);
                        // args[1] => Compute Buffer decides
                        args[2] = (uint)item.FoliageMesh.GetIndexStart(subMeshIndex);
                        args[3] = (uint)item.FoliageMesh.GetBaseVertex(subMeshIndex);

                        ComputeBuffer argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint),
                            ComputeBufferType.IndirectArguments);
                        argsBuffer.SetData(args);
                        cmd.CopyCounterValue(pointCloudBufferList[i], argsBuffer, sizeof(uint));

                        foliageMaterials[subMeshIndex].SetBuffer("_PerInstanceData", pointCloudBufferList[i]);
                        cmd.DrawMeshInstancedIndirect(item.FoliageMesh, subMeshIndex, foliageMaterials[subMeshIndex],
                            0, argsBuffer);
                    }
                }
            }
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
        public float pad;
    };
    
    struct FoliageComputeBufferData
    {
        public int densityMapResolution;
        public Vector3 foliageScale;
        public Vector3 zitterScale;
        public float pad;
    };
    
    CustomRenderPass m_ScriptablePass;
    [SerializeField]
    private List<FoliageData> foliageDataList;
    [SerializeField]
    private ComputeShader placementCS;
    [SerializeField]
    private ComputeShader generateCS;
    
    public override void Create()
    {
        if (Terrain.activeTerrain != null)
        {
            m_ScriptablePass = new CustomRenderPass(foliageDataList, placementCS, generateCS, Terrain.activeTerrain);
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


