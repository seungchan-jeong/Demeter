using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlacementRendererFeature : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private Terrain _mainTerrain;
        private List<PlacableObject> _placeTargetObjects;
        
        private List<FoliageData> _foliageDataList;
        private Dictionary<int, List<FoliageData>> _foliageDataByFootprint;
        
        private Dictionary<int, List<ComputeBuffer>> _pointCloudBufferByFootprint;
        private Dictionary<int, ComputeBuffer> _samplePointBufferByFootprint;
        private Dictionary<int, ComputeBuffer> _foliageBufferByFootprint;
        private Dictionary<FoliageData, List<ComputeBuffer>> _indirectArgBufferByFoliageData;
        private Dictionary<Mesh, MeshDataComputeBuffer> _meshDataBufferByMesh;
        private const int POINT_CLOUD_BUFFER_NUM_MAX = 4;
        
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        private ComputeShader generateCS;
        private ComputeShader generateOnMeshCS;
        private ComputeShader placementCS;

        private PoissonDiskSampler _poissonDiskSampler;

        public CustomRenderPass(List<FoliageData> foliageDataList, ComputeShader placementCS, ComputeShader generateCS, ComputeShader generateOnMeshCS, Terrain mainTerrain, params PlacableObject[] placableObjects)
        {
            this.placementCS = placementCS;
            this.generateCS = generateCS;
            this.generateOnMeshCS = generateOnMeshCS;
            
            this._foliageDataList = foliageDataList;
            this._mainTerrain = mainTerrain;

            _poissonDiskSampler = new PoissonDiskSampler();
            
            _pointCloudBufferByFootprint = new Dictionary<int, List<ComputeBuffer>>();
            _samplePointBufferByFootprint = new Dictionary<int, ComputeBuffer>();
            _foliageDataByFootprint = new Dictionary<int, List<FoliageData>>();
            _foliageBufferByFootprint = new Dictionary<int, ComputeBuffer>();
            _indirectArgBufferByFoliageData = new Dictionary<FoliageData, List<ComputeBuffer>>();
            _meshDataBufferByMesh = new Dictionary<Mesh, MeshDataComputeBuffer>();

            _placeTargetObjects = new List<PlacableObject>();
            _placeTargetObjects.AddRange(placableObjects);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            int generateCSMain = generateCS.FindKernel("CSMain");
            
            cmd.SetComputeFloatParam(generateCS, "TerrainWidth", _mainTerrain.terrainData.size.x);
            cmd.SetComputeFloatParam(generateCS, "TerrainHeight", _mainTerrain.terrainData.heightmapScale.y);
            cmd.SetComputeFloatParam(generateCS, "TerrainLength", _mainTerrain.terrainData.size.z);
            
            cmd.SetComputeTextureParam(generateCS, generateCSMain, "TerrainHeightMap",
                new RenderTargetIdentifier(_mainTerrain.terrainData.heightmapTexture));
            cmd.SetComputeIntParam(generateCS, "TerrainHeightMapResolution",
                _mainTerrain.terrainData.heightmapResolution);

            if (_foliageDataByFootprint.Count == 0)
            {
                InitFoliageDictionary();
            }

            if (_samplePointBufferByFootprint.Count == 0)
            {
                InitSamplePointBuffer();
            }

            if (_foliageBufferByFootprint.Count == 0)
            {
                InitFoliageDataBuffer();
            }

            if (_indirectArgBufferByFoliageData.Count == 0)
            {
                InitIndirectArgBuffer();
            }

            if (_meshDataBufferByMesh.Count == 0)
            {
                InitMeshDataBuffer();
            }
            
            if (_pointCloudBufferByFootprint.Count == 0)
            {
                InitPointCloudBufferByFootprint();
            }
        }
        
        private void InitFoliageDictionary()
        {
            foreach (FoliageData foliageData in _foliageDataList)
            {
                int footprint = (int)foliageData.Footprint;
                if (_foliageDataByFootprint.ContainsKey(footprint))
                {
                    _foliageDataByFootprint[footprint].Add(foliageData);
                }
                else
                {
                    _foliageDataByFootprint.Add(footprint, new List<FoliageData>() { foliageData });
                }
            }
        }
        
        private void InitSamplePointBuffer()
        {
            foreach (FoliageData foliageData in _foliageDataList)
            {
                int footprint = (int)foliageData.Footprint;

                if (_samplePointBufferByFootprint.ContainsKey(footprint))
                {
                    continue;
                }
                
                List<Vector2> poissonUVs;
                switch (footprint)
                {
                    case 1:
                        poissonUVs = _poissonDiskSampler.Get10000Points();
                        break;
                    case 2:
                        poissonUVs = _poissonDiskSampler.Get2500Points();
                        break;
                    case 4:
                        poissonUVs = _poissonDiskSampler.Get500Points();
                        break;
                    default:
                        continue;
                }
                
                List<SamplePointBufferModel> samplePoints = new List<SamplePointBufferModel>();
                foreach (Vector2 pos in poissonUVs)
                {
                    SamplePointBufferModel samplePointBufferModel = new SamplePointBufferModel
                    {
                        densityMapUV = pos,
                        heightMapUV = pos,
                        threshold = 0.8f,
                        pad = 0.0f
                    };
                    samplePoints.Add(samplePointBufferModel);
                }
                
                ComputeBuffer samplePointBuffer = new ComputeBuffer(samplePoints.Count, sizeof(float) * 8); 
                samplePointBuffer.SetData(samplePoints);
                _samplePointBufferByFootprint.Add(footprint, samplePointBuffer);
            }
        }

        private void InitFoliageDataBuffer()
        {
            foreach(var foliageDataByFootprint in _foliageDataByFootprint)
            {
                List<FoliageBufferModel> foliageComputeBufferDataList = foliageDataByFootprint.Value.Select(
                        foliageData => new FoliageBufferModel() {
                            densityMapResolution = foliageData.TerrainDensityMap.width,
                            foliageScale = foliageData.FoliageScale,
                            zitterScale = foliageData.FoliageScale * 0.1f,
                            pad = 0.0f
                        }
                    ).ToList();
                
                ComputeBuffer foliageDataComputeBuffer = new ComputeBuffer(foliageDataByFootprint.Value.Count, sizeof(int) * 1 + sizeof(float) * 7);
                foliageDataComputeBuffer.SetData(foliageComputeBufferDataList);
                
                _foliageBufferByFootprint.Add(foliageDataByFootprint.Key, foliageDataComputeBuffer);
            }
        }

        private void InitIndirectArgBuffer()
        {
            foreach (FoliageData foliageData in _foliageDataList)
            {
                List<ComputeBuffer> argBufferList = Enumerable.Range(0, foliageData.FoliageMesh.subMeshCount).
                    Select(_ => new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments)).ToList();

                for (int subMeshIndex = 0; subMeshIndex < foliageData.FoliageMesh.subMeshCount; subMeshIndex++)
                {
                    args[0] = (uint)foliageData.FoliageMesh.GetIndexCount(subMeshIndex);
                    // args[1] => Append Compute Buffer decides
                    args[2] = (uint)foliageData.FoliageMesh.GetIndexStart(subMeshIndex);
                    args[3] = (uint)foliageData.FoliageMesh.GetBaseVertex(subMeshIndex);
                    
                    argBufferList[subMeshIndex].SetData(args);
                }

                _indirectArgBufferByFoliageData.Add(foliageData, argBufferList);
            }
        }

        private void InitMeshDataBuffer()
        {
            foreach (PlacableObject placableObject in _placeTargetObjects)
            {
                MeshFilter meshFilter = placableObject.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    Debug.LogWarning("PlacableObject should have mesh. name : " + placableObject.name);
                    continue;
                }

                Mesh sharedMesh = meshFilter.sharedMesh;
                
                ComputeBuffer tris = new ComputeBuffer(sharedMesh.triangles.Length, sizeof(int));
                ComputeBuffer uvs = new ComputeBuffer(sharedMesh.uv.Length, sizeof(float) * 2);
                ComputeBuffer verts = new ComputeBuffer(sharedMesh.vertices.Length, sizeof(float) * 3);
                tris.SetData(sharedMesh.triangles);
                uvs.SetData(sharedMesh.uv);
                verts.SetData(sharedMesh.vertices);

                MeshDataComputeBuffer meshComputeBuffers = new MeshDataComputeBuffer(tris, uvs, verts);
                _meshDataBufferByMesh.Add(sharedMesh, meshComputeBuffers);
            }
        }
        
        private void InitPointCloudBufferByFootprint()
        {
            foreach (KeyValuePair<int, List<FoliageData>> foliageDataByFootprint in _foliageDataByFootprint)
            {
                List<ComputeBuffer> pointCloudBuffers = Enumerable.Range(0, foliageDataByFootprint.Value.Count).
                    Select(_ => new ComputeBuffer(_samplePointBufferByFootprint[foliageDataByFootprint.Key].count, 
                        sizeof(float) * 19 + sizeof(int), ComputeBufferType.Append)).ToList();
                _pointCloudBufferByFootprint.Add(foliageDataByFootprint.Key, pointCloudBuffers);
            }
        }
        
        public void Dispose()
        {
            foreach(List<ComputeBuffer> bufferList in _pointCloudBufferByFootprint.Values)
            {
                foreach (ComputeBuffer buffer in bufferList)
                {
                    buffer?.Dispose();
                }
            }
            _pointCloudBufferByFootprint.Clear();
            
            foreach(List<ComputeBuffer> bufferList in _indirectArgBufferByFoliageData.Values)
            {
                foreach (ComputeBuffer buffer in bufferList)
                {
                    buffer?.Dispose();
                }
            }
            _indirectArgBufferByFoliageData.Clear();

            foreach(MeshDataComputeBuffer buffer in _meshDataBufferByMesh.Values)
            {
                buffer?.Dispose();
            }
            _meshDataBufferByMesh.Clear();
            
            foreach (ComputeBuffer buffer in _samplePointBufferByFootprint.Values)
            {
                buffer?.Dispose();
            }
            _samplePointBufferByFootprint.Clear();
            
            foreach (ComputeBuffer buffer in _foliageBufferByFootprint.Values)
            {
                buffer?.Dispose();
            }
            _foliageBufferByFootprint.Clear();
        }
        

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            cmd.BeginSample("PlacementRenderer");

            foreach (List<ComputeBuffer> pointCloudBuffers in _pointCloudBufferByFootprint.Values)
            {
                foreach (ComputeBuffer pointCloudBuffer in pointCloudBuffers)
                {
                    cmd.SetBufferCounterValue(pointCloudBuffer, 0);
                }
            }
            
            foreach (int footprint in _foliageDataByFootprint.Keys)
            {
                RunPipelines(generateCS, cmd, context, footprint, _pointCloudBufferByFootprint);
            }

            foreach (PlacableObject placeTargetObject in _placeTargetObjects)
            {
                SetPerMeshParameters(cmd, generateOnMeshCS, placeTargetObject);
                foreach (int footprint in _foliageDataByFootprint.Keys)
                {
                    RunPipelines(generateOnMeshCS, cmd, context, footprint, _pointCloudBufferByFootprint, placeTargetObject);
                }
            }

            foreach (List<FoliageData> foliageData in _foliageDataByFootprint.Values)
            {
                DrawIndirect(cmd, context, foliageData, _pointCloudBufferByFootprint);
            }
            
            cmd.EndSample("PlacementRenderer");

            context.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }

        private void RunPipelines(ComputeShader cs, CommandBuffer cmd, ScriptableRenderContext context, int footprint, Dictionary<int, List<ComputeBuffer>> pointCloudBufferByFootprint, PlacableObject placableObject = null)
        {
            int csMainHandle = cs.FindKernel("CSMain");
            
            //1. Set Foliage Data Info
            cmd.SetComputeIntParam(cs, "FoliageDataCount", _foliageBufferByFootprint[footprint].count);
            cmd.SetComputeBufferParam(cs, csMainHandle, "foliageData", _foliageBufferByFootprint[footprint]);

            //2. Set Density Maps
            for (int i = 0; i < POINT_CLOUD_BUFFER_NUM_MAX; i++)
            {
                if (i < _foliageDataByFootprint[footprint].Count)
                {
                    if (placableObject == null)
                    {
                        cmd.SetComputeTextureParam(cs, csMainHandle, "DensityMap0" + (i+1), new RenderTargetIdentifier(_foliageDataByFootprint[footprint][i].TerrainDensityMap));
                    }
                    else if(placableObject.densityMapForFoliageData != null)
                    {
                        cmd.SetComputeTextureParam(cs, csMainHandle, "DensityMap0" + (i+1), new RenderTargetIdentifier(placableObject.densityMapForFoliageData[_foliageDataByFootprint[footprint][i]]));
                    }
                }
                else
                {
                    cmd.SetComputeTextureParam(cs, csMainHandle, "DensityMap0" + (i+1), new RenderTargetIdentifier("Temp"));
                }
            }

            //3. Set Sample Points
            cmd.SetComputeBufferParam(cs, csMainHandle, "samplePoints", _samplePointBufferByFootprint[footprint]);
            
            //4. Set Output Buffer
            for (int i = 0; i < pointCloudBufferByFootprint[footprint].Count; i++)
            {
                cmd.SetComputeBufferParam(cs, csMainHandle, "foliagePoints0" + (i+1), pointCloudBufferByFootprint[footprint][i]);
            }
            for (int i = pointCloudBufferByFootprint[footprint].Count; i < POINT_CLOUD_BUFFER_NUM_MAX; i++)
            {
                ComputeBuffer temp = new ComputeBuffer(1, 4);
                cmd.SetComputeBufferParam(cs, csMainHandle, "foliagePoints0" + (i+1), temp);
                temp.Dispose(); //TODO : 이거 temp로 하지 않고 무조건 할당해주는게 나으려나..?
            }
            
            //5. Dispatch 
            cmd.DispatchCompute(cs, csMainHandle,
                Mathf.CeilToInt((float)_samplePointBufferByFootprint[footprint].count / 64), 1, 1);
        }
        
        private void SetPerMeshParameters(CommandBuffer cmd, ComputeShader targetShader, PlacableObject placeTargetObject)
        {
            int csMain = targetShader.FindKernel("CSMain");
            
            Mesh objectMesh = placeTargetObject.GetComponent<MeshFilter>().sharedMesh;
            if (_meshDataBufferByMesh.TryGetValue(objectMesh, out MeshDataComputeBuffer buffer))
            {
                cmd.SetComputeIntParam(targetShader, "meshTrisCount", objectMesh.triangles.Length);
                cmd.SetComputeBufferParam(targetShader, csMain, "meshTris", buffer.TrisBuffer);
                cmd.SetComputeBufferParam(targetShader, csMain, "meshUVs", buffer.UVsBuffer);
                cmd.SetComputeBufferParam(targetShader, csMain, "meshVerts", buffer.VertsBuffer);
                cmd.SetComputeMatrixParam(targetShader, "meshLocalToWorldMat", placeTargetObject.transform.localToWorldMatrix);
            }
        }

        private void DrawIndirect(CommandBuffer cmd, ScriptableRenderContext context, List<FoliageData> foliageData, Dictionary<int, List<ComputeBuffer>> pointCloudBufferByFootprint)
        {
            for(int i = 0 ; i < foliageData.Count; i++) //TODO : foliageData.Count 와 pointCloudBuffer.Count는 항상 같다. 이걸 보장할 방법 찾기. 
            {
                FoliageData item = foliageData[i];
                Material[] foliageMaterials = item.FoliageMaterials;
                for (int subMeshIndex = 0; subMeshIndex < item.FoliageMesh.subMeshCount; subMeshIndex++)
                {
                    if (_indirectArgBufferByFoliageData.TryGetValue(item, out List<ComputeBuffer> argBufferList) && item.FoliageMesh != null)
                    {
                        cmd.CopyCounterValue(pointCloudBufferByFootprint[(int)item.Footprint][i], argBufferList[subMeshIndex], sizeof(uint));
                        //TODO : pointCloudBufferByFootprint의 ComputeBuffer는, 어떤 footprint에 대한 FoliageData를 무작위로 가지고 있을 수 있음. foliageData List의 index와 ComputeBuffer의 index가 서로 맞지 않을 수 있음
                        
                        foliageMaterials[subMeshIndex].SetBuffer("_PerInstanceData", pointCloudBufferByFootprint[(int)item.Footprint][i]);
                        cmd.DrawMeshInstancedIndirect(item.FoliageMesh, subMeshIndex, foliageMaterials[subMeshIndex],
                            0, argBufferList[subMeshIndex]);
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
    
    struct SamplePointBufferModel
    {
        public Vector2 bayerMatrixUV;
        public Vector2 densityMapUV;  //지금은 테스트 용으로 density Map과 height Map을 1:1로 매칭 
        public Vector2 heightMapUV;
        public float threshold;
        public float pad;
    };
    
    struct FoliageBufferModel
    {
        public int densityMapResolution;
        public Vector3 foliageScale;
        public Vector3 zitterScale;
        public float pad;
    };

    class MeshDataComputeBuffer : IDisposable
    {
        public MeshDataComputeBuffer(ComputeBuffer trisBuffer, ComputeBuffer uvsBuffer, ComputeBuffer vertsBuffer)
        {
            this.TrisBuffer = trisBuffer;
            this.UVsBuffer = uvsBuffer;
            this.VertsBuffer = vertsBuffer;
        }

        public readonly ComputeBuffer TrisBuffer;
        public readonly ComputeBuffer UVsBuffer;
        public readonly ComputeBuffer VertsBuffer;

        public void Dispose()
        {
            TrisBuffer?.Dispose();
            UVsBuffer?.Dispose();
            VertsBuffer?.Dispose();
        }
    }
    
    CustomRenderPass m_ScriptablePass;
    [SerializeField]
    private List<FoliageData> foliageDataList;
    [SerializeField]
    private ComputeShader placementCS;
    [SerializeField]
    private ComputeShader generateCS;
    [SerializeField]
    private ComputeShader generateOnMeshCS;
    
    public override void Create()
    {
        if (Terrain.activeTerrain != null)
        {
            m_ScriptablePass = new CustomRenderPass(foliageDataList, placementCS, generateCS, generateOnMeshCS,
                Terrain.activeTerrain, Resources.FindObjectsOfTypeAll<PlacableObject>());
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

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass.Dispose();
        base.Dispose(disposing);
    }
}


