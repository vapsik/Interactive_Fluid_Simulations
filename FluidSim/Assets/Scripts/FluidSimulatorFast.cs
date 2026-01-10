using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class FluidSimulatorFast : MonoBehaviour
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
    
    FluidVisualizerFast fluidVisualizer;
    
    //---Compute Shader variables---
    public ComputeShader computeShader; //FluidFast.compute
    public float decay = 0.01f;
    
    //---References to textures used for "communicating" with the compute shader---
    private RenderTexture readDye;
    private RenderTexture writeDye;
    private RenderTexture solidCellMap;
    private RenderTexture writeVelocityX;
    private RenderTexture writeVelocityY;
    private RenderTexture readVelocityX;
    private RenderTexture readVelocityY;

    private RenderTexture readPressure;
    
    private RenderTexture writePressure;

    private RenderTexture readDivergence;

    private RenderTexture writeDivergence;

    
    private int kernelDyeBrush;
    private int kernelVelocityBrush;

    private int kernelAdvectVelocities;
    private int kernelAdvectDye;
    private int kernelComputeDivergence;
    private int kernelSolvePressure;
    private int kernelSubtractGradient;


    void Awake(){

        //Fluidgrid initialization
        fluidGrid = new FluidGrid(CellCountX, CellCountY, CellSize);
        fluidGrid.TimeStepMul = timeStepMul;

        //Instatiating the instance of FluidVisualizer.cs for this simulator
        fluidVisualizer = GetComponent<FluidVisualizerFast>();

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

        //setting compute shader floats
        computeShader.SetFloat("_CountX", CellCountX);
        computeShader.SetFloat("_CountY", CellCountY);
        computeShader.SetFloat("_CellSize", fluidGrid.CellSize);
        computeShader.SetFloat("_TimeStep", fluidGrid.TimeStepMul);
        computeShader.SetFloat("_Decay", decay);


        //creating the textures for the shader
        CreateRenderTexture(ref readDye, CellCountX, CellCountY);
        CreateRenderTexture(ref writeDye, CellCountX, CellCountY);
        CreateRenderTexture(ref solidCellMap, CellCountX, CellCountY);
        CreateRenderTexture(ref writeVelocityX, CellCountX+1, CellCountY);
        CreateRenderTexture(ref writeVelocityY, CellCountX, CellCountY+1);
        CreateRenderTexture(ref readVelocityX, CellCountX+1, CellCountY);
        CreateRenderTexture(ref readVelocityY, CellCountX, CellCountY+1);
        CreateRenderTexture(ref readDivergence, CellCountX, CellCountY);
        CreateRenderTexture(ref writeDivergence, CellCountX, CellCountY);
        CreateRenderTexture(ref readPressure, CellCountX, CellCountY);
        CreateRenderTexture(ref writePressure, CellCountX, CellCountY);

        //instantiating kernel references
        kernelDyeBrush = computeShader.FindKernel("DyeBrush");
        kernelVelocityBrush = computeShader.FindKernel("VelocityBrush");
        kernelAdvectVelocities = computeShader.FindKernel("AdvectVelocities");
        kernelAdvectDye = computeShader.FindKernel("AdvectDye");
        kernelComputeDivergence = computeShader.FindKernel("ComputeDivergence");
        kernelSolvePressure = computeShader.FindKernel("SolvePressure");
        kernelSubtractGradient = computeShader.FindKernel("SubtractGradient");

        //passing solid cell maps reference into kernels
        computeShader.SetTexture(kernelDyeBrush, "_SolidCellMap", solidCellMap);
        computeShader.SetTexture(kernelVelocityBrush, "_SolidCellMap", solidCellMap);
        computeShader.SetTexture(kernelAdvectVelocities, "_SolidCellMap", solidCellMap);
        computeShader.SetTexture(kernelAdvectDye, "_SolidCellMap", solidCellMap);
        computeShader.SetTexture(kernelComputeDivergence, "_SolidCellMap", solidCellMap);
        computeShader.SetTexture(kernelSolvePressure, "_SolidCellMap", solidCellMap);
        computeShader.SetTexture(kernelSubtractGradient, "_SolidCellMap", solidCellMap);

        //uploading solid cell map onto GPU
        UploadSolidCellsToGPU();

        //if one has to initialize with velocities:
        //UploadVelocitiesToGPU();

    }
    
    //float timer = 1f;
    void Update()
    {
        //---Handling sources and interaction---
        //BASICALLY ADD FORCES STEP
        //TODO: IMPLEMENT VELOCITY BRUSH ON THE SHADER

        //fluidVisualizer.HandleSources(this);
        fluidVisualizer.HandleInteraction(this); //calls this.ApplyDyeBrushGPU() and this.ApplyVelocityBrushGPU()
        

        //TODO: fix reset implementation
        bool ctrlDown = Input.GetKey(KeyCode.LeftControl);
        if (ctrlDown && Input.GetKeyDown(KeyCode.C))
        {
            ResetSimulation();
        }

        //---Advection of Velocities---
        AdvectVelocities();
        
        
        //---Divergence Computation & Corresponding Gradient Subtraction---
        DivergenceProjection();

        //NB! WRONG IMPLEMENTATION
        //TODO: IMPLEMENT STAGGERED GRID, NOT CENTERED GRID IMPLEMENTATION!!!
        //TODO: BYPASS GPU->CPU TEXTURE TO ARRAY CONVERSIONS, MAKE FLUID VISUALIZER USE TEXTURES FOR SMOKES, CPU ARRAYS ONLY FOR SETTING INITIAL CONDITIONS!!        

        AdvectDye();
        
        //Updating CPU values for smoke FluidGrid.SmokeMap4Ch 
        DownloadSmokeFromGPU();
        //Updating CPU values for velocity fields FluidGrid.VelocitiesX, FluidGrid.VelocitiesY
        DownloadVelocityFromGPU();

        
    }


    private void LateUpdate()
    {
        if (fluidGrid == null) return;
        // update smoke texture every frame
        fluidVisualizer.UpdateSmokeTexture(readDye);
        
        // update velocity mesh every frame
        fluidVisualizer.BuildVelocityMesh();
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
    
    void UploadSolidCellsToGPU()
    {
        //Solid Cells
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
        
        Destroy(solidTex);
    }

    void DownloadSmokeFromGPU()
    {
        
        /*RenderTexture.active = readDye;
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
        
        Destroy(tempTex);*/
    }
    
    void DownloadVelocityFromGPU()
    {
        RenderTexture.active = readVelocityX;
        Texture2D tempTex = new Texture2D(CellCountX+1, CellCountY, TextureFormat.RFloat, false);
        tempTex.ReadPixels(new Rect(0, 0, CellCountX+1, CellCountY), 0, 0);
        tempTex.Apply();
        RenderTexture.active = null;

        Color[] colors = tempTex.GetPixels();
        
        for (int y = 0; y < CellCountY; y++)
        {
            for (int x = 0; x < CellCountX+1; x++)
            {
                Color col = colors[y * (CellCountX + 1) + x];  // Fixed: Use correct width for indexing
                fluidGrid.VelocitiesX[x, y] = col.r;
            }
        }
        Destroy(tempTex);

        RenderTexture.active = readVelocityY;
        tempTex = new Texture2D(CellCountX, CellCountY+1, TextureFormat.RFloat, false);
        tempTex.ReadPixels(new Rect(0, 0, CellCountX, CellCountY+1), 0, 0);
        tempTex.Apply();
        RenderTexture.active = null;

        colors = tempTex.GetPixels();
        
        for (int y = 0; y < CellCountY+1; y++)
        {
            for (int x = 0; x < CellCountX; x++)
            {
                Color col = colors[y * CellCountX + x];
                fluidGrid.VelocitiesY[x, y] = col.r;
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
    }
    void AdvectVelocities()
    {
        int threadGroupsX = (int)Mathf.Ceil((CellCountX+1.0f)/8.0f);
        int threadGroupsY = (int)Mathf.Ceil((CellCountY+1.0f)/8.0f);

        computeShader.SetTexture(kernelAdvectVelocities, "_ReadVelocityX", readVelocityX);
        computeShader.SetTexture(kernelAdvectVelocities, "_ReadVelocityY", readVelocityY);
        
        computeShader.SetTexture(kernelAdvectVelocities, "_WriteVelocityX", writeVelocityX);
        computeShader.SetTexture(kernelAdvectVelocities, "_WriteVelocityY", writeVelocityY);
        
        computeShader.SetTexture(kernelAdvectVelocities, "_SolidCellMap", solidCellMap);
        
        computeShader.Dispatch(kernelAdvectVelocities, threadGroupsX, threadGroupsY, 1);

        
        WriteToRead(ref writeVelocityX, ref readVelocityX);
        WriteToRead(ref writeVelocityY, ref readVelocityY);
    }

    void AdvectDye()
    {
        int threadGroupsX = Mathf.CeilToInt(CellCountX / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(CellCountY / 8.0f);
        
        computeShader.SetTexture(kernelAdvectDye, "_ReadDye", readDye);
        computeShader.SetTexture(kernelAdvectDye, "_WriteDye", writeDye);

        computeShader.SetTexture(kernelAdvectDye, "_ReadVelocityX", readVelocityX);
        computeShader.SetTexture(kernelAdvectDye, "_ReadVelocityY", readVelocityY);

        computeShader.Dispatch(kernelAdvectDye, threadGroupsX, threadGroupsY, 1);
        
        WriteToRead(ref writeDye, ref readDye);
    }

    void DivergenceProjection()
    {
        int threadGroupsX = Mathf.CeilToInt(CellCountX/8.0f);
        int threadGroupsY = Mathf.CeilToInt(CellCountY/8.0f);

        //compute divergence
        computeShader.SetTexture(kernelComputeDivergence, "_ReadVelocityX", readVelocityX);
        computeShader.SetTexture(kernelComputeDivergence, "_ReadVelocityY", readVelocityY);
        computeShader.SetTexture(kernelComputeDivergence, "_WriteDivergence", writeDivergence);
        computeShader.Dispatch(kernelComputeDivergence, threadGroupsX, threadGroupsY, 1);

        //solve pressure
        //Graphics.Blit(writeDivergence, readDivergence);
        computeShader.SetTexture(kernelSolvePressure, "_ReadDivergence", writeDivergence); //readDivergence
        
        for (int i = 0; i < solverIterations; i++)
        {
            computeShader.SetTexture(kernelSolvePressure, "_ReadPressure", readPressure);
            computeShader.SetTexture(kernelSolvePressure, "_WritePressure", writePressure);
            computeShader.Dispatch(kernelSolvePressure, threadGroupsX, threadGroupsY, 1);

            WriteToRead(ref writePressure, ref readPressure);

            //---Gradient subtraction---
            computeShader.SetTexture(kernelSubtractGradient, "_ReadPressure", readPressure);
            computeShader.SetTexture(kernelSubtractGradient, "_ReadVelocityX", readVelocityX);
            computeShader.SetTexture(kernelSubtractGradient, "_ReadVelocityY", readVelocityY);
            computeShader.SetTexture(kernelSubtractGradient, "_WriteVelocityX", writeVelocityX);
            computeShader.SetTexture(kernelSubtractGradient, "_WriteVelocityY", writeVelocityY);
            computeShader.Dispatch(kernelSubtractGradient, threadGroupsX, threadGroupsY, 1);
        }

    }

    void WriteToRead(ref RenderTexture write, ref RenderTexture read)
    {
        var tmp = read;
        read = write;
        write = tmp;
    }

    public void AddForcesGPU()
    {
        
    }

    public void ApplyVelocityBrushGPU(Vector2Int centreCoord, int numCellsHalf, Vector2 mouseDelta, float strength)
    {   
        int brushSize = numCellsHalf * 2 + 1;  // Total cells in brush (e.g., 3 for numCellsHalf=1)
        int threadGroupsX = Mathf.CeilToInt(brushSize / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(brushSize / 8.0f);

        computeShader.SetVector("_CentreCoord", new Vector4(centreCoord.x, centreCoord.y, 0, 0));
        computeShader.SetInt("_NumCellsHalf", numCellsHalf);
        computeShader.SetVector("_MouseDelta", new Vector4(mouseDelta.x, mouseDelta.y, 0f, 0f));
        computeShader.SetFloat("_Strength", strength);

        computeShader.SetTexture(kernelVelocityBrush, "_ReadVelocityX", readVelocityX);
        computeShader.SetTexture(kernelVelocityBrush, "_ReadVelocityY", readVelocityY);
        computeShader.SetTexture(kernelVelocityBrush, "_WriteVelocityX", writeVelocityX);
        computeShader.SetTexture(kernelVelocityBrush, "_WriteVelocityY", writeVelocityY);
        computeShader.Dispatch(kernelVelocityBrush, threadGroupsX, threadGroupsY, 1);

        

        WriteToRead(ref writeVelocityX, ref readVelocityX);
        WriteToRead(ref writeVelocityY, ref readVelocityY);

        Debug.Log(centreCoord);
    }

    public void ApplyDyeBrushGPU(Vector2Int centreCoord, Color dyeColor, Vector2 mousePos, int numCellsHalf)
    {
        int threadGroupsX = (int)Mathf.Ceil(((float)numCellsHalf*2.0f)/8.0f);
        int threadGroupsY = (int)Mathf.Ceil(((float)numCellsHalf*2.0f)/8.0f);
        
        computeShader.SetInt("_NumCellsHalf", numCellsHalf);
        computeShader.SetInts("_CentreCoord", centreCoord.x, centreCoord.y);
        computeShader.SetVector("_AppliedDyeColor", dyeColor);
        computeShader.SetTexture(kernelDyeBrush, "_WriteDye", writeDye);
        computeShader.Dispatch(kernelDyeBrush, threadGroupsX, threadGroupsY, 1);
        
        WriteToRead(ref writeDye, ref readDye);
    }
}