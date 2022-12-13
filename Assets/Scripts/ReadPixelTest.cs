using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

public class ReadPixelTest : MonoBehaviour
{
    public Material BlitMat;
    public RawImage rawImage;
    public Texture2D targetTexture2D;
    private RenderTexture renderTexture;
    
    // Start is called before the first frame update
    void Start()
    {
        renderTexture = new RenderTexture(targetTexture2D.width, targetTexture2D.height, 0,  GraphicsFormat.R32_SFloat);
        renderTexture.Create();
        
        if(rawImage != null)
            rawImage.texture = renderTexture;
        
        BlitMat.SetVector("_Brush_TexelSize", new Vector4(64.0f, 64.0f, 0.0f, 0.0f));
        BlitMat.SetVector("_SourceTex_TexelSize", new Vector4(64.0f, 64.0f, 0.0f, 0.0f));
    }                                                     

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RenderTexture.active = renderTexture;
            Graphics.Blit(targetTexture2D, renderTexture, BlitMat);
            
            targetTexture2D.ReadPixels(new Rect(0, 0, targetTexture2D.width, targetTexture2D.height), 0, 0);
            targetTexture2D.Apply();
        }
        
    }
}
