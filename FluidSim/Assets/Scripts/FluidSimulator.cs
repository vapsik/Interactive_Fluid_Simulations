using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidSimulator : MonoBehaviour
{
    public int CellCountX = 50;
    public int CellCountY = 50;
    public float CellSize = 1;
    
    public FluidGrid fluidGrid;
    public float timeStepMul = 1;
    
    public int solverIterations = 20;
    
    FluidVisualizer fluidVisualizer;
    
    public ComputeShader computeShader;
    public float decay = 0.01f;
        
    private RenderTexture smokeRead;
    private RenderTexture smokeWrite;
    private RenderTexture velocityRead;
    private RenderTexture velocityWrite;
    
    private RenderTexture pressureRead;
    private RenderTexture pressureWrite;
    private RenderTexture divergenceTex;

    private int kernelAdvect;
    
    private int kernelComputeDivergence;
    private int kernelSolvePressure;
    private int kernelSubtractGradient;
    
    void Start()
    {
        fluidVisualizer = GetComponent<FluidVisualizer>();
        if (fluidVisualizer == null)
        {
            Debug.LogError("FluidVisualizer not found");
            return;
        }
        
        fluidGrid = new FluidGrid(CellCountX, CellCountY, CellSize);
        fluidVisualizer.SetFluidGrid(fluidGrid);
        
        CreateRenderTexture(ref smokeRead, CellCountX, CellCountY);
        CreateRenderTexture(ref smokeWrite, CellCountX, CellCountY);

        CreateRenderTexture(ref velocityRead, CellCountX, CellCountY);
        CreateRenderTexture(ref velocityWrite, CellCountX, CellCountY);
        
        CreateRenderTexture(ref pressureRead, CellCountX, CellCountY);
        CreateRenderTexture(ref pressureWrite, CellCountX, CellCountY);
        CreateRenderTexture(ref divergenceTex, CellCountX, CellCountY);
        
        kernelAdvect = computeShader.FindKernel("Advect");
        
        kernelComputeDivergence = computeShader.FindKernel("ComputeDivergence");
        kernelSolvePressure = computeShader.FindKernel("SolvePressure");
        kernelSubtractGradient = computeShader.FindKernel("SubtractGradient");
    }
    
    void Update()
    {
        fluidVisualizer.HandleInteraction();

        if (Input.GetMouseButton(0) || Input.GetMouseButton(2))
        {
            UploadDataToGPU();
        }

        fluidGrid.TimeStepMul = timeStepMul;
        
        computeShader.SetFloat("_TimeStep", Time.deltaTime * timeStepMul);
        computeShader.SetFloat("_Decay", decay);
        computeShader.SetFloats("_TextureSize", CellCountX, CellCountY);
        
        int threadGroupsX = Mathf.CeilToInt(CellCountX / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(CellCountY / 8.0f);
        
        computeShader.SetTexture(kernelAdvect, "_Read",velocityRead);
        computeShader.SetTexture(kernelAdvect, "_Write",velocityWrite);
        computeShader.SetTexture(kernelAdvect, "_Velocity",velocityRead);
        computeShader.Dispatch(kernelAdvect, threadGroupsX, threadGroupsY, 1);
        
        RenderTexture temp = velocityRead;
        velocityRead = velocityWrite;
        velocityWrite = temp;
        
        computeShader.SetTexture(kernelAdvect, "_Read", smokeRead);
        computeShader.SetTexture(kernelAdvect, "_Write", smokeWrite);
        computeShader.SetTexture(kernelAdvect, "_Velocity", velocityRead);
        computeShader.Dispatch(kernelAdvect, threadGroupsX, threadGroupsY, 1);
        
        temp = smokeRead;
        smokeRead = smokeWrite;
        smokeWrite = temp;
        
        computeShader.SetTexture(kernelComputeDivergence, "_Velocity", velocityRead);
        computeShader.SetTexture(kernelComputeDivergence, "_WriteDivergence", divergenceTex);
        computeShader.Dispatch(kernelComputeDivergence, threadGroupsX, threadGroupsY, 1);
        
        computeShader.SetTexture(kernelSolvePressure, "_ReadDivergence", divergenceTex);
        
        for (int i = 0; i < solverIterations; i++)
        {
            computeShader.SetTexture(kernelSolvePressure, "_ReadPressure", pressureRead);
            computeShader.SetTexture(kernelSolvePressure, "_WritePressure", pressureWrite);
            computeShader.Dispatch(kernelSolvePressure, threadGroupsX, threadGroupsY, 1);

            RenderTexture tmp = pressureRead;
            pressureRead = pressureWrite;
            pressureWrite = tmp;
        }
        
        computeShader.SetTexture(kernelSubtractGradient, "_ReadPressure", pressureRead);
        computeShader.SetTexture(kernelSubtractGradient, "_WriteVelocity", velocityRead);
        computeShader.Dispatch(kernelSubtractGradient, threadGroupsX, threadGroupsY, 1);

        DownloadSmokeFromGPU();
        DownloadVelocityFromGPU();
    }

    void CreateRenderTexture(ref RenderTexture tex, int width, int height)
    {
        tex = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
        tex.enableRandomWrite = true;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Create();
    }
    
    void UploadDataToGPU()
    {
        Texture2D vTex = new Texture2D(CellCountX, CellCountY, TextureFormat.RGFloat, false);
        Color[] vCols = new Color[CellCountX * CellCountY];
        
        Texture2D sTex = new Texture2D(CellCountX, CellCountY, TextureFormat.RFloat, false);
        Color[] sCols = new Color[CellCountX * CellCountY];

        for (int y = 0; y < CellCountY; y++)
        {
            for (int x = 0; x < CellCountX; x++)
            {
                float u = fluidGrid.VelocitiesX[x, y];
                float v = fluidGrid.VelocitiesY[x, y];
                vCols[y * CellCountX + x] = new Color(u, v, 0, 0);

                float s = fluidGrid.SmokeMap[x, y];
                sCols[y * CellCountX + x] = new Color(s, 0, 0, 0);
            }
        }

        vTex.SetPixels(vCols); vTex.Apply();
        Graphics.Blit(vTex, velocityRead); 

        sTex.SetPixels(sCols); sTex.Apply();
        Graphics.Blit(sTex, smokeRead);
        
        Destroy(vTex);
        Destroy(sTex);
        
        fluidGrid.ClearVelocities(); 
        fluidGrid.ClearDye();
    }

    void DownloadSmokeFromGPU()
    {
        RenderTexture.active = smokeRead;
        Texture2D tempTex = new Texture2D(CellCountX, CellCountY, TextureFormat.RFloat, false);
        tempTex.ReadPixels(new Rect(0, 0, CellCountX, CellCountY), 0, 0);
        tempTex.Apply();
        RenderTexture.active = null;
        
        Color[] colors = tempTex.GetPixels();
        for (int y = 0; y < CellCountY; y++)
        {
            for (int x = 0; x < CellCountX; x++)
            {
                fluidGrid.SmokeMap[x, y] = colors[y * CellCountX + x].r;
            }
        }
        
        Destroy(tempTex);
    }
    
    void DownloadVelocityFromGPU()
    {
        RenderTexture.active = velocityRead;
        Texture2D tempTex = new Texture2D(CellCountX, CellCountY, TextureFormat.RGFloat, false);
        tempTex.ReadPixels(new Rect(0, 0, CellCountX, CellCountY), 0, 0);
        tempTex.Apply();
        RenderTexture.active = null;

        Color[] colors = tempTex.GetPixels();
        
        for (int y = 0; y < CellCountY; y++)
        {
            for (int x = 0; x < CellCountX; x++)
            {
                Color col = colors[y * CellCountX + x];
                fluidGrid.VelocitiesX[x, y] = col.r;
                fluidGrid.VelocitiesY[x, y] = col.g;
            }
        }
        Destroy(tempTex);
    }
}