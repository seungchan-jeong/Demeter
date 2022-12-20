using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.TerrainTools;
using Object = UnityEngine.Object;

public class DensityMapTool : EditorWindow
{
    [FormerlySerializedAs("settingsArr")] 
    [SerializeField]
    private List<FoliageData> foliageDataList;
    private Editor[] editorArr;
    private Editor brushTextureEditor;
    private static GUIStyle _soContainer;
    private static GUIStyle soContainer
    {
        get
        {
            if (_soContainer == null)
                _soContainer = new GUIStyle(GUI.skin.box);

            return _soContainer;
        }
    }

    private Terrain mainTerrain;

    private int targetTexture2DIndex;
    // private Texture2D targetTexture2D;
    private RenderTexture renderTexture;
    private Material brushMateral;

    private Texture2D brushTex;
    private Texture2D BrushTex
    {
        get { return brushTex; }
        set
        {
            brushTex = value;
            brushMateral.SetTexture("_BrushTex", value);
        }
    }

    private float brushScale = 1.0f;
    
    IBrushUIGroup m_commonUI;
    private IBrushUIGroup commonUI {
        get
        {
            if (m_commonUI == null)
            {
                m_commonUI = new PlacementBrushUIGroup(
                    "PaintHoles",
                    null,
                    PlacementBrushUIGroup.Feature.All,
                    new PlacementBrushUIGroup.FeatureDefaults { Strength = 0.99f }
                );
                m_commonUI.OnEnterToolMode();
            }
    
            return m_commonUI;
        }
    }
    
    [MenuItem("Tools/DensityMapTool")]
    public static void ShowWindow() {
        //Show existing window instance. If one doesn't exist, make one.
        DontDestroyOnLoad(EditorWindow.GetWindow<DensityMapTool>("DensityMapTool"));
    }

    private bool[] buttonSelections;
    void OnGUI()
    {
        GUILayout.Label("Brush Size");
        brushScale = EditorGUILayout.Slider(brushScale, 0, 5);
        
        BrushTex = TextureField("Brush", BrushTex);

        GUILayout.Label("Foliage Data List");
        for (int i = 0; i < foliageDataList.Count; i++)
        {
            if (foliageDataList[i] != null)
            {
                GUILayout.BeginHorizontal();
                if(GUILayout.Button(foliageDataList[i].name, buttonSelections[i] ? "OL SelectedRow" : "Button"))
                {
                    for (int j = 0; j < buttonSelections.Length; j++)
                    {
                        buttonSelections[j] = false;
                    }
                    buttonSelections[i] = true;
                    targetTexture2DIndex = i;
                }

                if (GUILayout.Button("Clear"))
                {
                    RenderTexture.active = renderTexture;

                    Texture2D clearTarget = foliageDataList[i].TerrainDensityMap;
                    LocalKeyword clearKeyword = new LocalKeyword(brushMateral.shader, "CLEAR");
                    brushMateral.SetKeyword(clearKeyword, true);
                    Graphics.Blit(clearTarget, renderTexture, brushMateral); 
                    brushMateral.SetKeyword(clearKeyword, false);
                    
                    clearTarget.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    clearTarget.Apply();
                }
                GUILayout.EndHorizontal();
                
                GUILayout.BeginVertical(soContainer);

                if (editorArr[i] == null)
                    editorArr[i] = Editor.CreateEditor(foliageDataList[i]);
                    
                editorArr[i].OnInspectorGUI();
                GUILayout.EndVertical();
            }
        }
    }
    
    private void OnEnable()
    {
        foliageDataList = FindAssetsByType<FoliageData>();
        editorArr = new Editor[foliageDataList.Count];
        buttonSelections = new bool[foliageDataList.Count];

        brushMateral = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BrushMaterial.mat");
        brushTex = (Texture2D)brushMateral.GetTexture("_BrushTex");

        SceneView.duringSceneGui += OnSceneGUI;
        
        mainTerrain = Terrain.activeTerrain;

        // targetTexture2D = foliageDataList[0].TerrainDensityMap;
        targetTexture2DIndex = 0;
        
        renderTexture = new RenderTexture(foliageDataList[0].TerrainDensityMap.width, foliageDataList[0].TerrainDensityMap.height, 0,  foliageDataList[0].TerrainDensityMap.graphicsFormat);
        renderTexture.depthStencilFormat = GraphicsFormat.None;
        renderTexture.Create();
    }

    void OnDestroy() {
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
    }
    
    void OnSceneGUI(SceneView sceneView)
    {
        if (!EditorWindow.HasOpenInstances<DensityMapTool>())
        {
            return;
        }
        
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            Texture2D targetTexture2D;
            if (hit.transform.gameObject.GetComponent<Terrain>() != null)
            {
                targetTexture2D = foliageDataList[targetTexture2DIndex].TerrainDensityMap;
            }
            else if(hit.transform.gameObject.GetComponent<PlacableObject>() != null)
            {
                PlacableObject placableObject = hit.transform.gameObject.GetComponent<PlacableObject>();
                targetTexture2D = placableObject.densityMapForFoliageData[foliageDataList[targetTexture2DIndex]];
            }
            else
            {
                return;
            }
            
            Color color = hit.transform.gameObject.GetComponent<Terrain>() != null ? Color.cyan : Color.yellow;
            color.a = 0.25f;
            Handles.color = color;
            Handles.DrawSolidArc(hit.point, hit.normal, Vector3.Cross(hit.normal, ray.direction), 360, (float)brushTex.width / targetTexture2D.width * mainTerrain.terrainData.size.x * 0.25f * brushScale);
            Handles.color = Color.white;
            Handles.DrawLine(hit.point, hit.point + hit.normal * 5);

            // using (IBrushRenderPreviewUnderCursor brushRender =
            //        new DensityMapToolBrushRenderPreviewUIGroupUnderCursor(commonUI, "PaintDensity", brushTex))
            // {
            //     if (brushRender.CalculateBrushTransform(out BrushTransform brushXform))
            //     {
            //         PaintContext paintContext = brushRender.AcquireHeightmap(false, brushXform.GetBrushXYBounds(), 1);
            //         Material previewMaterial = Utility.GetDefaultPreviewMaterial(false);
            //         TerrainPaintUtilityEditor.DrawBrushPreview(paintContext, TerrainBrushPreviewMode.SourceRenderTexture,
            //             brushTex, brushXform, previewMaterial, 0);
            //     }
            // }

            bool bShouldUpdateTexture = Event.current.rawType is EventType.MouseDown or EventType.MouseDrag && Event.current.button == 0;
            
            if (bShouldUpdateTexture)
            {
                RenderTexture.active = renderTexture;
                
                if (hit.transform.gameObject.GetComponent<Terrain>() != null)
                {
                    Vector2 brushPos = WorldPosToTerrainPos(hit.point);
                    brushMateral.SetVector("_BrushPos",  new Vector4(brushPos.x, brushPos.y, 0.0f, 0.0f));
                }
                else
                {
                    brushMateral.SetVector("_BrushPos", hit.textureCoord);
                }

                brushMateral.SetTexture("_TestTex", targetTexture2D);
                brushMateral.SetFloat("_BrushScale", brushScale);
                brushMateral.SetVector("_BrushTint", Event.current.alt? Color.black : Color.white);
                brushMateral.SetVector("_Brush_TexelSize", new Vector4(brushTex.width, brushTex.height, 0.0f, 0.0f));
                brushMateral.SetVector("_TestTex_TexelSize", new Vector4(targetTexture2D.width, targetTexture2D.height, 0.0f, 0.0f));
                Graphics.Blit(targetTexture2D, renderTexture, brushMateral);
                
                targetTexture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                targetTexture2D.Apply();

                Event.current.Use();
            }
            else
            {
                SceneView.RepaintAll();   
            }
        }
    }

    private Vector2 WorldPosToTerrainPos(Vector3 worldPos)
    {
        if (mainTerrain == null)
        {
            return Vector2.zero;
        }

        return new Vector2(worldPos.x / mainTerrain.terrainData.size.x, worldPos.z / mainTerrain.terrainData.size.z);
    }
    
    public static List<T> FindAssetsByType<T>() where T : UnityEngine.Object
    {
        List<T> assets = new List<T>();
        string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(T)));
        for( int i = 0; i < guids.Length; i++ )
        {
            string assetPath = AssetDatabase.GUIDToAssetPath( guids[i] );
            T asset = AssetDatabase.LoadAssetAtPath<T>( assetPath );
            if( asset != null )
            {
                assets.Add(asset);
            }
        }
        return assets;
    }
    
    public static Texture2D TextureField(string name, Texture2D texture)
    {
        GUILayout.BeginVertical();
        var style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.UpperCenter;
        style.fixedWidth = 70;
        GUILayout.Label(name, style);
        var result = (Texture2D)EditorGUILayout.ObjectField(texture, typeof(Texture2D), false, GUILayout.Width(70), GUILayout.Height(70));
        GUILayout.EndVertical();
        return result;
    }
}
