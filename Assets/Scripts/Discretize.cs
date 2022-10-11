using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Discretize : MonoBehaviour
{
    public Texture densityMap;
    public Texture thresholdMap;
    
    public RawImage rawImage;
    public ComputeShader computeShader;
    
    private RenderTexture discretizeResultRT;
    private static int DensityMapWidth = 64;
    private static int DensityMapHeight = 64;
    private void OnEnable()
    {
        discretizeResultRT = new RenderTexture(DensityMapWidth, DensityMapHeight, 0);
        discretizeResultRT.enableRandomWrite = true;
        discretizeResultRT.Create();
    
        rawImage.texture = discretizeResultRT;
    }

    private void OnDisable()
    {
        discretizeResultRT.Release();
    }

    private void Start()
    {
        RunDiscretization();
    }

    public void RunDiscretization()
    {
        if (thresholdMap == null)
        {
            Texture2D bayerMatrix = new Texture2D(4, 4);
            bayerMatrix.wrapMode = TextureWrapMode.Repeat;
            
            float[] bayerCoeff = 
            {
                0.0f, 8.0f, 2.0f, 10.0f,
                12.0f, 4.0f, 14.0f, 6.0f,
                3.0f, 11.0f, 1.0f, 9.0f,
                15.0f, 7.0f, 13.0f, 5.0f
            };

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    float value = bayerCoeff[i * 4 + j] / 16.0f;
                    bayerMatrix.SetPixel(j, i, new Color(value, value, value, 1.0f));
                }
            }
            bayerMatrix.Apply();
            thresholdMap = bayerMatrix;
        }
        
        int kernelID = computeShader.FindKernel("CSMain");
        computeShader.SetTexture(kernelID, "DensityMap", densityMap);
        computeShader.SetTexture(kernelID, "ThresholdMap", thresholdMap);
        computeShader.SetTexture(kernelID, "Result", discretizeResultRT);
        
        computeShader.SetInt("thresholdWidth", thresholdMap.width);
        computeShader.SetInt("thresholdHeight", thresholdMap.height);
        
        computeShader.GetKernelThreadGroupSizes(kernelID, out uint x, out uint y, out uint _);
        computeShader.Dispatch(kernelID, Mathf.Max(DensityMapWidth / (int)x, 1), 
            Mathf.Max(DensityMapHeight/ (int)y , 1), 1);
    }
}
