using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlacementRendererFeature : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private Terrain mainTerrain;
        private Mesh cube;

        private ComputeBuffer samplePointBuffer;
        private ComputeBuffer pointCloudBuffer;
        
        private ComputeBuffer argsBuffer;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        private int instanceCount = 64;
        private int subMeshIndex = 0;
        
        private ComputeShader generateCS;
        private ComputeShader placementCS;
        private Material indirectMaterial;

        public CustomRenderPass(Mesh cube, Material indirectMaterial, ComputeShader placementCS, ComputeShader generateCS, Terrain mainTerrain)
        {
            this.cube = cube;
            this.indirectMaterial = indirectMaterial;
            this.placementCS = placementCS;
            this.generateCS = generateCS;
            this.mainTerrain = mainTerrain;
            
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            //이거 크기를 그냥 크게 만들어두는건가? Compute Shader에서 실제 채워지는 양이 다르면 어떻게 처리? 
            pointCloudBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 3 * 2 + sizeof(int));
            samplePointBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 7);
            
            FillDummySamplePointData();
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            
            cmd.BeginSample("PlacementRenderer");
            RunPipelines(cmd, context);
            DrawIndirect(cmd, context);
            cmd.EndSample("PlacementRenderer");
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }
        
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        private void RunPipelines(CommandBuffer cmd, ScriptableRenderContext context)
        {
            int generateCSMain = generateCS.FindKernel("CSMain");
            cmd.SetComputeFloatParam(generateCS, "TerrainWidth", mainTerrain.terrainData.size.x);
            cmd.SetComputeFloatParam(generateCS, "TerrainHeight", mainTerrain.terrainData.heightmapScale.y);
            cmd.SetComputeFloatParam(generateCS, "TerrainLength", mainTerrain.terrainData.size.z);
            cmd.SetComputeIntParam(generateCS, "HeightMapResolution", mainTerrain.terrainData.heightmapResolution);

            // cmd.SetComputeTextureParam(generateCS, generateCSMain, "DensityMap",);
            cmd.SetComputeTextureParam(generateCS, generateCSMain, "TerrainHeightMap", new RenderTargetIdentifier(mainTerrain.terrainData.heightmapTexture));

            cmd.SetComputeBufferParam(generateCS, generateCSMain, "samplePoints", samplePointBuffer);
            cmd.SetComputeBufferParam(generateCS, generateCSMain, "foliagePoints", pointCloudBuffer);
            
            cmd.DispatchCompute(generateCS, generateCSMain, samplePointBuffer.count / 64, 1, 1);
            // cmd.RequestAsyncReadback(pointCloudBuffer, (AsyncGPUReadbackRequest request) =>
            // {
            //     FoliagePoint[] points = request.GetData<FoliagePoint>(0).ToArray();
            //     foreach (FoliagePoint point in points)
            //     {
            //         Debug.Log("Pos : " + point.worldPosition);
            //     }
            // });
            
            int placementCSMain = placementCS.FindKernel("CSMain");
            // cmd.SetComputeBufferParam(placementCS, placementCSMain,"foliagePoints", pointCloudBuffer);
            // cmd.DispatchCompute(placementCS, placementCSMain, pointCloudBuffer.count / 64,1,1);
            
            // cmd.RequestAsyncReadback(pointCloudBuffer, (AsyncGPUReadbackRequest request) =>
            // {
            //     FoliagePoint[] points = request.GetData<FoliagePoint>(0).ToArray();
            //     foreach (FoliagePoint point in points)
            //     {
            //         Debug.Log("Pos : " + point.worldPosition);
            //     }
            // });
        }

        private void DrawIndirect(CommandBuffer cmd, ScriptableRenderContext context)
        {
            if (cube != null) {
                args[0] = (uint)cube.GetIndexCount(subMeshIndex);
                args[1] = (uint)instanceCount;
                args[2] = (uint)cube.GetIndexStart(subMeshIndex);
                args[3] = (uint)cube.GetBaseVertex(subMeshIndex);
            }
            argsBuffer.SetData(args);

            if (indirectMaterial != null)
            {
                indirectMaterial.SetBuffer("_PointCloudBuffer", pointCloudBuffer);
                cmd.DrawMeshInstancedIndirect(cube, 0, indirectMaterial, 0, argsBuffer);
            }
            
        }

        private void FillDummySamplePointData()
        {
            List<SamplePoint> samplePoints = new List<SamplePoint>();
            for (int i = 0; i < instanceCount; i++)
            {
                SamplePoint samplePoint = new SamplePoint
                {
                    // densityMapUV = new Vector2(1.0f / (float)instanceCount * i, 1.0f / (float)instanceCount * i)
                    densityMapUV = new Vector2(Random.value, Random.value)
                };
                samplePoint.heightMapUV = samplePoint.densityMapUV;
                samplePoint.threshold = -1.0f;
                samplePoints.Add(samplePoint);

                // float x = samplePoint.heightMapUV.x * mainTerrain.terrainData.size.z;
                // float y = samplePoint.heightMapUV.y * mainTerrain.terrainData.size.z;
                // float terrainHeight = mainTerrain.SampleHeight(new Vector3(x, 0.0f, y));
                // Debug.Log(
                //     $" height : {terrainHeight}" +
                //     $" x : {x}, y : {y}");
            }

            samplePointBuffer.SetData(samplePoints);
        }

        private void ToHeightMapUVFromDensityMapUV(Vector2 densityMapUV)
        {
            /*
             * 1. DensityMap은 항상 64x64이다.
             * 2. bayerMatrix의 samplePoint 사이의 거리 w 는 footprint와 같다.
             * 2-1. samplePoint사이의 거리 w는 pixel 단위로 N pixel이다.
             * 2-2. 
             */
            
            /*
             * 임시로
             */
        }

    }

    struct FoliagePoint
    {
        public Vector3 worldPosition;
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

    CustomRenderPass m_ScriptablePass;
    [SerializeField]
    private Mesh cube;
    [SerializeField]
    private Material indirectMaterial;
    [SerializeField]
    private ComputeShader placementCS;
    [SerializeField]
    private ComputeShader generateCS;
    [SerializeField] 
    // private Terrain mainTerrain;
    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass(cube, indirectMaterial, placementCS, generateCS, Terrain.activeTerrain);
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
}


