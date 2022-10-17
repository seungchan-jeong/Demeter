using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlacementRendererFeature : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private ComputeBuffer argsBuffer;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        private int subMeshIndex = 0;
        private ComputeBuffer pointCloudBuffer;
        private int instanceCount = 64;
        private Mesh cube;
        
        private ComputeShader placementCS;
        private Material indirectMaterial;

        public CustomRenderPass(Mesh cube, Material indirectMaterial, ComputeShader placementCS)
        {
            this.cube = cube;
            this.indirectMaterial = indirectMaterial;
            this.placementCS = placementCS;
            
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            //이거 크기를 그냥 크게 만들어두는건가? Compute Shader에서 실제 채워지는 양이 다르면 어떻게 처리? 
            pointCloudBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 3 * 2 + sizeof(int));
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
            int kernelMain = placementCS.FindKernel("CSMain");
            cmd.SetComputeBufferParam(placementCS, kernelMain,"foliagePoints", pointCloudBuffer);
            cmd.DispatchCompute(placementCS, kernelMain, pointCloudBuffer.count / 64,1,1);
            
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
    }

    struct FoliagePoint
    {
        public Vector3 worldPosition;
        public Vector3 worldNormal;
        public int foliageType;
    }

    CustomRenderPass m_ScriptablePass;
    [SerializeField]
    private Mesh cube;
    [SerializeField]
    private Material indirectMaterial;
    [SerializeField]
    private ComputeShader placementCS;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass(cube, indirectMaterial, placementCS);
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
}


