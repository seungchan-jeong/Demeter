using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class CirclePackingGenerator : MonoBehaviour
{
    public int minRadius = 5;
    public int maxRadius = 10;
    public int totalCircleCounts = 1024;
    
    private List<Circle> circles;
    
    public RawImage rawImage;
    public ComputeShader computeShader;
    
    private ComputeBuffer circleBuffer;
    private RenderTexture circleRT;
    private void OnEnable()
    {
        circles = new List<Circle>();
        circleRT = new RenderTexture(Screen.width, Screen.height, 0);
        circleRT.enableRandomWrite = true;
        circleRT.Create();

        if (rawImage != null)
        {
            rawImage.texture = circleRT;
        }

        circleBuffer = new ComputeBuffer(totalCircleCounts, (sizeof(int) * 2 + sizeof(float) * 2));
    }

    private void OnDisable()
    {
        circleRT.Release();
        circleBuffer.Release();
    }

    private void Start()
    {
        GenerateCirclePacking();
    }

    public void GenerateCirclePacking()
    {
        for (int i = 0; i < totalCircleCounts; i++)
        {
            Circle circle;
            for (int j = 0; j < 500; j++)
            {
                circle = new Circle() { x = (int)(Random.value * circleRT.width), y = (int)(Random.value * circleRT.height), 
                    radius = minRadius, threshold = Random.value };

                if (DoesCircleCollideWithOther(circle))
                {
                    continue;
                }

                for (float radius = minRadius; radius < maxRadius; radius++)
                {
                    if (DoesCircleCollideWithOther(circle))
                    {
                        break;
                    }
                    circle.radius = radius;
                }
                circles.Add(circle);
            }
        }
        
        circleBuffer.SetData(circles);
        
        int kernelID = computeShader.FindKernel("CSMain");
        computeShader.SetTexture(kernelID, "Result", circleRT);
        computeShader.SetBuffer(kernelID, "CircleBuffer", circleBuffer);
        computeShader.GetKernelThreadGroupSizes(kernelID, out uint x, out uint _, out uint _);
        computeShader.Dispatch(kernelID, Mathf.Max(totalCircleCounts / (int)x, 1), 1, 1);
    }

    private bool DoesCircleCollideWithOther(Circle circle)
    {
        foreach(Circle other in circles) 
        {
            var a = circle.radius + other.radius;
            var x = circle.x - other.x;
            var y = circle.y - other.y;

            if (a * a >= (x*x) + (y*y)) {
                return true;
            }
        }

        return false;
    }

    private struct Circle
    {
        public int x;
        public int y;
        public float radius;
        public float threshold;
    }
}
