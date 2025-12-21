using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class FluidSimulator : MonoBehaviour
{
    //Discretization parameters
    public int CellCountX = 50;
    public int CellCountY = 50;
    public float CellSize = 1;
    public FluidGrid fluidGrid;
    public float timeStepMul = 1;
    
    //---Boundary instantiation parameters---
    [SerializeField] private bool SetEdgeBoundaries = true;
    [SerializeField] private bool SetCircularBoundaries = true;
    [SerializeField] private bool SetDiamondBoundaries = true;
    [SerializeField] private Vector3Int[] X_Y_R; 

    public int solverIterations = 20;
    
    FluidVisualizer fluidVisualizer;
    
    //---Compute Shader variables---
    public ComputeShader computeShader;
    public float decay = 0.01f;
    
    //---References to textures used for "communicating" with the compute shader---
    private RenderTexture pressureRead;
    private RenderTexture pressureWrite;
    private RenderTexture smokeRead;
    private RenderTexture smokeWrite;
    private RenderTexture velocityRead;
    private RenderTexture velocityWrite;
    private RenderTexture divergenceTex;

    private RenderTexture solidCellMap;

    private int kernelAdvect;
    
    private int kernelComputeDivergence;
    private int kernelSolvePressure;
    private int kernelSubtractGradient;
    
    int threadGroupsX;
    int threadGroupsY;

    void Awake(){

        //Fluidgrid initialization
        fluidGrid = new FluidGrid(CellCountX, CellCountY, CellSize);
        fluidGrid.TimeStepMul = timeStepMul;

        //Instatiating the instance of FluidVisualizer.cs for this simulator
        fluidVisualizer = GetComponent<FluidVisualizer>();

        //Initializing fluidgrid for FluidVisualizer.cs
        fluidVisualizer.SetFluidGrid(fluidGrid);

        if (fluidVisualizer == null)
        {
            Debug.LogError("FluidVisualizer not found");
            return;
        }


        //---Setting boundaries and solid cells dependening on the instantiation settings---
        if(SetEdgeBoundaries){
            fluidGrid.SetEdgeBoundaries();
        }

        if(SetCircularBoundaries){
            foreach(Vector3Int XYZ in X_Y_R){
                fluidGrid.SetCircularBoundary(XYZ.x, XYZ.y, XYZ.z);
            }
        }

        if(SetDiamondBoundaries){
            foreach(Vector3Int XYZ in X_Y_R){
                fluidGrid.SetDiamondBoundary(XYZ.x, XYZ.y, XYZ.z);
            }
        }


        //---initialization of script <-> shader references---
        CreateRenderTexture(ref smokeRead, CellCountX, CellCountY);
        CreateRenderTexture(ref smokeWrite, CellCountX, CellCountY);

        CreateRenderTexture(ref velocityRead, CellCountX, CellCountY);
        CreateRenderTexture(ref velocityWrite, CellCountX, CellCountY);
        
        CreateRenderTexture(ref pressureRead, CellCountX, CellCountY);
        CreateRenderTexture(ref pressureWrite, CellCountX, CellCountY);
        CreateRenderTexture(ref divergenceTex, CellCountX, CellCountY);

        //creating the reference for the Solid Cell map
        CreateRenderTexture(ref solidCellMap, CellCountX, CellCountY);

        //references to the compute shader methods
        kernelAdvect = computeShader.FindKernel("Advect");
        kernelComputeDivergence = computeShader.FindKernel("ComputeDivergence");
        kernelSolvePressure = computeShader.FindKernel("SolvePressure");
        kernelSubtractGradient = computeShader.FindKernel("SubtractGradient");

        //setting compute shader values
        computeShader.SetFloat("_TimeStep", Time.deltaTime * timeStepMul);
        computeShader.SetFloat("_Decay", decay);
        computeShader.SetFloats("_TextureSize", CellCountX, CellCountY);
        threadGroupsX = Mathf.CeilToInt(CellCountX / 8.0f);
        threadGroupsY = Mathf.CeilToInt(CellCountY / 8.0f);

        //sending solid cell map into the compute shader, all kernels must access
        computeShader.SetTexture(kernelAdvect, "_SolidCellMap", solidCellMap);
        computeShader.SetTexture(kernelComputeDivergence, "_SolidCellMap", solidCellMap);
        computeShader.SetTexture(kernelSolvePressure, "_SolidCellMap", solidCellMap);
        computeShader.SetTexture(kernelSubtractGradient, "_SolidCellMap", solidCellMap);

    }

    void Start()
    {
        
        
    }
    
    
    //float timer = 1f;
    void Update()
    {
        //---Handling sources and interaction---
        //TODO: IMPLEMENT VELOCITY BRUSH ON THE SHADER
        
        fluidVisualizer.HandleSources();
        fluidVisualizer.HandleInteraction();
        UploadDataToGPU();
        
        //TODO: fix reset implementation
        bool ctrlDown = Input.GetKey(KeyCode.LeftControl);
        if (ctrlDown && Input.GetKeyDown(KeyCode.C))
        {
            ResetSimulation();
        }

        //---Advection---
        AdvectVelocities();
        AdvectDye();
        
        //---Divergence Computation & Corresponding Gradient Subtraction---
        DivergenceProjection();

        //NB! WRONG IMPLEMENTATION
        //TODO: IMPLEMENT STAGGERED GRID, NOT CENTERED GRID IMPLEMENTATION!!!
        //TODO: BYPASS GPU->CPU TEXTURE TO ARRAY CONVERSIONS, MAKE FLUID VISUALIZER USE TEXTURES FOR SMOKES, CPU ARRAYS ONLY FOR SETTING INITIAL CONDITIONS!!        

        //Updating CPU values for smoke FluidGrid.SmokeMap4Ch 
        DownloadSmokeFromGPU();
        //Updating CPU values for velocity fields FluidGrid.VelocitiesX, FluidGrid.VelocitiesY
        DownloadVelocityFromGPU();

        
    }

    void CreateRenderTexture(ref RenderTexture tex, int width, int height, RenderTextureFormat format = RenderTextureFormat.ARGBFloat)
    {
        tex = new RenderTexture(width, height, 0, format);
        tex.enableRandomWrite = true;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Create();

        // ensure the RT starts cleared (avoid uninitialized/garbage data)
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = tex;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }
    
    void UploadDataToGPU()
    {
        Texture2D vTex = new Texture2D(CellCountX, CellCountY, TextureFormat.RGFloat, false);
        Color[] vCols = new Color[CellCountX * CellCountY];
        
        Texture2D sTex = new Texture2D(CellCountX, CellCountY, TextureFormat.RGBAFloat, false);
        Color[] sCols = new Color[CellCountX * CellCountY];

        for (int y = 0; y < CellCountY; y++)
        {
            for (int x = 0; x < CellCountX; x++)
            {
                float u = fluidGrid.VelocitiesX[x, y];
                float v = fluidGrid.VelocitiesY[x, y];
                vCols[y * CellCountX + x] = new Color(u, v, 0, 0);

                float read_r = fluidGrid.SmokeMap4Ch[x, y, 0];
                float read_g = fluidGrid.SmokeMap4Ch[x, y, 1];
                float read_b = fluidGrid.SmokeMap4Ch[x, y, 2];
                float read_a = fluidGrid.SmokeMap4Ch[x, y, 3];
                sCols[y * CellCountX + x] = new Color(read_r, read_g, read_b, read_a);
            }
        }

        vTex.SetPixels(vCols); vTex.Apply();
        Graphics.Blit(vTex, velocityRead); 

        sTex.SetPixels(sCols); sTex.Apply();
        Graphics.Blit(sTex, smokeRead);
        
        Texture2D solidTex = new Texture2D(CellCountX, CellCountY, TextureFormat.RFloat, false);
        Color[] solidCols = new Color[CellCountX * CellCountY];
        for (int y = 0; y < CellCountY; y++)
        {
            for (int x = 0; x < CellCountX; x++)
            {
                solidCols[y * CellCountX + x] = fluidGrid.SolidCellMap[x, y] ? Color.white : Color.black;
            }
        }
        solidTex.SetPixels(solidCols); solidTex.Apply();
        Graphics.Blit(solidTex, solidCellMap);
        
        Destroy(vTex);
        Destroy(sTex);
        Destroy(solidTex);
        
        
    }

    void DownloadSmokeFromGPU()
    {
        RenderTexture.active = smokeRead;
        Texture2D tempTex = new Texture2D(CellCountX, CellCountY, TextureFormat.RGBAFloat, false);
        tempTex.ReadPixels(new Rect(0, 0, CellCountX, CellCountY), 0, 0);
        tempTex.Apply();
        RenderTexture.active = null;
        
        Color[] colors = tempTex.GetPixels();
        for (int y = 0; y < CellCountY; y++)
        {
            for (int x = 0; x < CellCountX; x++)
            {
                fluidGrid.SmokeMap4Ch[x, y, 0] = colors[y * CellCountX + x].r;
                fluidGrid.SmokeMap4Ch[x, y, 1] = colors[y * CellCountX + x].g;
                fluidGrid.SmokeMap4Ch[x, y, 2] = colors[y * CellCountX + x].b;
                fluidGrid.SmokeMap4Ch[x, y, 3] = colors[y * CellCountX + x].a;
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

    void ClearRenderTexture(RenderTexture tex)
    {
        if (tex == null) return;
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = tex;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }

    void ResetSimulation()
    {
        // Reset CPU data
        fluidGrid = new FluidGrid(CellCountX, CellCountY, CellSize);
        fluidVisualizer.SetFluidGrid(fluidGrid);

        // Clear GPU textures
        ClearRenderTexture(smokeRead);
        ClearRenderTexture(smokeWrite);
        ClearRenderTexture(velocityRead);
        ClearRenderTexture(velocityWrite);
        ClearRenderTexture(pressureRead);
        ClearRenderTexture(pressureWrite);
        ClearRenderTexture(divergenceTex);
        ClearRenderTexture(solidCellMap);

        // Reset shader uniforms
        computeShader.SetFloats("_TextureSize", CellCountX, CellCountY);
    }

    void AdvectVelocities()
    {
        computeShader.SetTexture(kernelAdvect, "_Read", velocityRead);
        computeShader.SetTexture(kernelAdvect, "_Write", velocityWrite);
        computeShader.SetTexture(kernelAdvect, "_Velocity", velocityRead);
        computeShader.Dispatch(kernelAdvect, threadGroupsX, threadGroupsY, 1);
    }

    void AdvectDye()
    {
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
        
    }

    void DivergenceProjection()
    {
        computeShader.SetTexture(kernelComputeDivergence, "_Velocity", velocityRead);
        computeShader.SetTexture(kernelComputeDivergence, "_WriteDivergence", divergenceTex);
        computeShader.Dispatch(kernelComputeDivergence, threadGroupsX, threadGroupsY, 1);
        
        computeShader.SetTexture(kernelSolvePressure, "_ReadDivergence", divergenceTex);
        computeShader.SetTexture(kernelSolvePressure, "_SolidCellMap", solidCellMap);
        
        for (int i = 0; i < solverIterations; i++)
        {
            computeShader.SetTexture(kernelSolvePressure, "_ReadPressure", pressureRead);
            computeShader.SetTexture(kernelSolvePressure, "_WritePressure", pressureWrite);
            computeShader.Dispatch(kernelSolvePressure, threadGroupsX, threadGroupsY, 1);

            RenderTexture tmp = pressureRead;
            pressureRead = pressureWrite;
            pressureWrite = tmp;
        }
        
        //---Gradient subtraction---
        computeShader.SetTexture(kernelSubtractGradient, "_ReadPressure", pressureRead);
        computeShader.SetTexture(kernelSubtractGradient, "_WriteVelocity", velocityRead);
        computeShader.SetTexture(kernelSubtractGradient, "_SolidCellMap", solidCellMap);
        computeShader.Dispatch(kernelSubtractGradient, threadGroupsX, threadGroupsY, 1);
    }
}