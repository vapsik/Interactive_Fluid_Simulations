using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class FluidVisualizerFast : MonoBehaviour
{
    private FluidGrid fluidGrid;
    public Color dyeColor = new Color(1, 0, 0, 0.5f);
    public Color dyeColor2 = new Color(0, 1, 0, 0.5f);
    private Vector2 mousePosOld;
    
    public bool drawVelocityField = true;
    public int velocityArrowSkip = 2; //draw every Nth arrow
    public float velocityArrowScale = 10.0f;
    public float minSpeedForColor = 0.0f;
    public float midSpeedForColor = 5.0f;

    public float interactionRadius = 2f;
    public float interactionStrength = 5f;
    public float minInteractionRadius = 0.1f;
    public float interactionScrollSpeed = 0.1f;

    // Texture-based smoke rendering
    private Texture2D smokeTexture;
    private Color[] textureData;
    private Material smokeMaterial;
    private Mesh fullScreenQuad;
    private Renderer meshRenderer;

    // Velocity mesh rendering
    private Mesh velocityMesh;
    private Material velocityMaterial;
    private int velocityLineCount = 0;
    private List<Vector3> velocityVertices = new List<Vector3>();
    private List<int> velocityIndices = new List<int>();
    private Renderer velocityRenderer;

    public Color solidCellColor =  new Color(0f,0f,1f,1f);
    
    public bool isInteractive = true;

    public bool simulateSources = true;

    public FlowSource[] dyeSources;

    [System.Serializable]
    public struct FlowSource
    {
        public Vector2Int lowerLeftOrigin;
        public Vector2Int sizeXY;
        public Vector2 velXY;
        public bool addsDye;
        public Color sourceColor;
    }

    private void Start()
    {
        // setup renderer for smoke
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.mesh = fullScreenQuad;
        meshRenderer.material = smokeMaterial;

        // setup renderer for velocity
        GameObject velocityObject = new GameObject("VelocityVisualization");
        velocityObject.transform.SetParent(transform);
        velocityRenderer = velocityObject.AddComponent<MeshRenderer>();
        MeshFilter velMeshFilter = velocityObject.AddComponent<MeshFilter>();
        velMeshFilter.mesh = velocityMesh;
        velocityRenderer.material = velocityMaterial;
        DrawSolidCell();
    }

    public void SetFluidGrid(FluidGrid grid)
    {
        this.fluidGrid = grid;
        InitializeRendering();
    }

    public void InitializeRendering()
    {
        // create smoke texture
        smokeTexture = new Texture2D(fluidGrid.CellCountX, fluidGrid.CellCountY, TextureFormat.RGBA32, false);
        smokeTexture.filterMode = FilterMode.Point;
        textureData = new Color[fluidGrid.CellCountX * fluidGrid.CellCountY];
        
        // create material for smoke rendering
        smokeMaterial = new Material(Shader.Find("Sprites/Default"));
        smokeMaterial.mainTexture = smokeTexture;
        
        // create fullscreen quad
        fullScreenQuad = CreateQuadMesh();
        
        // initialize velocity mesh
        velocityMesh = new Mesh();
        velocityMaterial = new Material(Shader.Find("Sprites/Default"));
        velocityMaterial.color = Color.red;
        
        BuildVelocityMesh();
    }

    private Mesh CreateQuadMesh()
    {
        //standard procedure for drawing a quad mesh in Unity
        Mesh quad = new Mesh();
        Vector3[] verts = new Vector3[4];
        int[] tris = new int[6];
        Vector2[] uv = new Vector2[4];
        
        float width = fluidGrid.CellCountX * fluidGrid.CellSize;
        float height = fluidGrid.CellCountY * fluidGrid.CellSize;
        
        //positions of the vertices of full quad
        verts[0] = new Vector3(0, 0, 0);
        verts[1] = new Vector3(width, 0, 0);
        verts[2] = new Vector3(0, height, 0);
        verts[3] = new Vector3(width, height, 0);
        
        //uv coords of the vertices
        uv[0] = new Vector2(0, 0);
        uv[1] = new Vector2(1, 0);
        uv[2] = new Vector2(0, 1);
        uv[3] = new Vector2(1, 1);
        
        //triangle vertices
        //unity uses left-handed coordinate system
        tris[0] = 0; tris[1] = 2; tris[2] = 1;
        tris[3] = 1; tris[4] = 2; tris[5] = 3;
        
        quad.vertices = verts;
        quad.triangles = tris;
        quad.uv = uv;
        quad.RecalculateNormals();
        
        return quad;
    }
    
    private void DrawSolidCell()
    {
        bool[,] solidMap = fluidGrid.SolidCellMap;

        for (int x = 0; x < fluidGrid.CellCountX; x++)
        {
            for (int y = 0; y < fluidGrid.CellCountY; y++)
            {
                if(solidMap[x,y] == true)
                {   
                    float r = solidCellColor.r;
                    float g = solidCellColor.g;
                    float b = solidCellColor.b;
                    float a = solidCellColor.a;
                    textureData[y * fluidGrid.CellCountX + x] = new Color(r, g, b, a);
                    
                }

                smokeTexture.SetPixels(textureData);
                smokeTexture.Apply(false);
            }
        }
    }

    public void BuildVelocityMesh()
    {
        if (!drawVelocityField) return;

        velocityVertices.Clear();
        velocityIndices.Clear();
        int skip = Mathf.Max(1, velocityArrowSkip);
        
        for (int x = 0; x < fluidGrid.CellCountX; x += skip)
        {
            for (int y = 0; y < fluidGrid.CellCountY; y += skip)
            {
                Vector2 pos = fluidGrid.CellCentre(x, y);
                Vector2 vel = fluidGrid.GetVelocityAtWorldPos(pos);
                float speed = vel.magnitude;
                
                if (speed < 0.01f) continue;
                
                Vector3 start = new Vector3(pos.x, pos.y, 0);
                Vector3 end = start + new Vector3(vel.x, vel.y, 0) * velocityArrowScale;
                
                int idx = velocityVertices.Count;
                velocityVertices.Add(start);
                velocityVertices.Add(end);
                velocityIndices.Add(idx);
                velocityIndices.Add(idx + 1);
            }
        }
        
        velocityLineCount = velocityIndices.Count / 2;
        velocityMesh.Clear();
        velocityMesh.vertices = velocityVertices.ToArray();
        velocityMesh.SetIndices(velocityIndices.ToArray(), MeshTopology.Lines, 0);
    }
    
    public void UpdateSmokeTexture(RenderTexture readDye)
    {
        /*for (int x = 0; x < fluidGrid.CellCountX; x++)
        {
            for (int y = 0; y < fluidGrid.CellCountY; y++)
            {   
                if(!fluidGrid.SolidCellMap[x,y]){
                    float r = fluidGrid.SmokeMap4Ch[x, y, 0];
                    float g = fluidGrid.SmokeMap4Ch[x, y, 1];
                    float b = fluidGrid.SmokeMap4Ch[x, y, 2];
                    float a = fluidGrid.SmokeMap4Ch[x, y, 3];
                
                    textureData[y * fluidGrid.CellCountX + x] = new Color(r, g, b, a);
                }
                else
                {
                    float r = solidCellColor.r;
                    float g = solidCellColor.g;
                    float b = solidCellColor.b;
                    float a = solidCellColor.a;

                    textureData[y * fluidGrid.CellCountX + x] = new Color(r, g, b, a);
                    textureData[y * fluidGrid.CellCountX + x] = new Color(r, g, b, a);
                }
                
            }
        }*/
        RenderTexture.active = readDye;
        smokeTexture.ReadPixels(new Rect(0,0,readDye.width, readDye.height), 0, 0);
        smokeTexture.Apply(false);
        RenderTexture.active = null;
    }

    
    
    private Vector2Int CellCoordFromPosNew(Vector2 worldPos)
    {
        float size = fluidGrid.CellSize;
        int x = Mathf.FloorToInt(worldPos.x / size);
        int y = Mathf.FloorToInt(worldPos.y / size);
        return new Vector2Int(Mathf.Max(x, 1), Mathf.Max(x, 1));
    }

    private Vector2Int CellCoordFromPos(Vector2 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / fluidGrid.CellSize);
        int y = Mathf.FloorToInt(worldPos.y / fluidGrid.CellSize);
        return new Vector2Int(x, y);
    }

    /*
    void OnDrawGizmos()
    {
        /*if (fluidGrid == null){return;}
        
        for (int x = 0; x < fluidGrid.CellCountX; x++)
        {
            for (int y = 0; y < fluidGrid.CellCountY; y++)
            {
                Vector4 smokeValue = new Vector4(fluidGrid.SmokeMap4Ch[x, y, 0], fluidGrid.SmokeMap4Ch[x, y, 1], fluidGrid.SmokeMap4Ch[x, y, 2], fluidGrid.SmokeMap4Ch[x, y, 3] );

                if (smokeValue.w > 0.01f)
                {
                    //dyeColor.a = smokeValue * 0.5f;
                    Gizmos.color = smokeValue;

                    float worldX = (x + 0.5f) * fluidGrid.CellSize;
                    float worldY = (y + 0.5f) * fluidGrid.CellSize;
                    Vector3 cellCenter = new Vector3(worldX, worldY, 0);

                    Vector3 cellSize = Vector3.one * fluidGrid.CellSize;
                    Gizmos.DrawCube(cellCenter, cellSize);
                }
            }
        }
        
        if (!drawVelocityField)
        {
            return;
        }*/

        /*int skip = Mathf.Max(1, velocityArrowSkip);

        for (int x = 0; x < fluidGrid.CellCountX; x += skip)
        {
            for (int y = 0; y < fluidGrid.CellCountY; y += skip)
            {
                Vector2 pos = fluidGrid.CellCentre(x, y);
                Vector2 vel = fluidGrid.GetVelocityAtWorldPos(pos);
                float speed = vel.magnitude;

                float speedT = Mathf.InverseLerp(minSpeedForColor, midSpeedForColor, speed);
  
                float colorT = Mathf.PingPong(speedT, 1.0f);
  
                Gizmos.color = Color.Lerp(Color.red, Color.green, colorT);

                Vector3 start = new Vector3(pos.x, pos.y, 0);
                Vector3 end = start + new Vector3(vel.x, vel.y, 0) * velocityArrowScale;
                
                if (Vector3.Distance(start, end) > 0.01f)
                {
                    Gizmos.DrawLine(start, end);
                    Vector3 v = (end - start).normalized;
                    Vector3 right = Quaternion.Euler(0, 0, -30) * (-v * 0.1f * fluidGrid.CellSize);
                    Vector3 left = Quaternion.Euler(0, 0, 30) * (-v * 0.1f * fluidGrid.CellSize);
                    Gizmos.DrawLine(end, end + right);
                    Gizmos.DrawLine(end, end + left);
                }
            }
        }

        if (isInteractive)
        {         
            Gizmos.color = Color.yellow;
            DrawCircleXZ(mouseWorldPosPrev, interactionRadius, 32);   
        }
    }

    void DrawCircleXZ(Vector3 centre, float radius, int segments)
    {
        if (segments < 3) segments = 3;
        float angleStep = Mathf.PI * 2f / segments;

        Vector3 prevPoint = centre + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep;
            Vector3 nextPoint = centre + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }*/


    Vector3 mouseWorldPos;
    Vector3 mouseWorldPosPrev;

    public void HandleSources(FluidSimulatorFast fluidSimulatorFast){
        if(!simulateSources)
            return;
        
        foreach (FlowSource source in dyeSources)
            {
                int x0 = source.lowerLeftOrigin.x;
                int xf = x0 + source.sizeXY.x;
                int y0 = source.lowerLeftOrigin.y;
                int yf = y0 + source.sizeXY.y;
                for(int x = x0; x <= xf; x++)
                {
                    for (int y = y0; y <= yf; y++)
                    {
                        if(!fluidGrid.SolidCellMap[x, y] && source.addsDye){
                            fluidGrid.SmokeMap4Ch[x, y, 0] = source.sourceColor.r;
                            fluidGrid.SmokeMap4Ch[x, y, 1] = source.sourceColor.g;
                            fluidGrid.SmokeMap4Ch[x, y, 2] = source.sourceColor.b;
                            fluidGrid.SmokeMap4Ch[x, y, 3] = source.sourceColor.a;
                        }
                        //Debug.Log(source.sourceColor);

                        //add velocities to corresponding edges:
                        fluidGrid.VelocitiesX[x, y] = source.velXY.x;
                        fluidGrid.VelocitiesY[x, y] = source.velXY.y;

                        //to correctly deal with staggered grids:
                        if(x == xf){
                            fluidGrid.VelocitiesX[x+1, y] = source.velXY.x;
                        }

                        if(y == yf){
                            fluidGrid.VelocitiesY[x, y+1] = source.velXY.y;
                        }
                    }
                }
            }
    }

    public void HandleInteraction(FluidSimulatorFast fluidSimulatorFast)
    {

        if(isInteractive){
            if (fluidGrid == null) return;

            var cam = Camera.main;
            var mp = Input.mousePosition;
            mp.z = -cam.transform.position.z;
            mouseWorldPos = cam.ScreenToWorldPoint(mp);

            Vector2 scroll = Input.mouseScrollDelta;
            interactionRadius += scroll.y * interactionScrollSpeed;
            interactionRadius = Mathf.Max(minInteractionRadius, interactionRadius);

            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mouseDelta = mousePos - mousePosOld;
            
            Vector2Int centreCoord = CellCoordFromPos(mousePos);
            //brush size in cells
            int numCellsHalf = Mathf.CeilToInt(interactionRadius / fluidGrid.CellSize);

            
            // Left mouse button: add velocity
            if (Input.GetMouseButton(0))
            {
                //ApplyVelocityBrush(centreCoord, numCellsHalf, mouseDelta, interactionStrength);
                fluidSimulatorFast.ApplyVelocityBrushGPU(centreCoord, numCellsHalf, mouseDelta, interactionStrength);
            }

            // Middle mouse button: add dye(Green)
            if (Input.GetMouseButton(2))
            {
                //ApplyDyeBrush(dyeColor2, mousePos, numCellsHalf);
                fluidSimulatorFast.ApplyDyeBrushGPU(centreCoord, dyeColor2, mousePos, numCellsHalf);
            }

            // Right mouse button: add dye(Red)
            if (Input.GetMouseButton(1))
            {
                //ApplyDyeBrush(centreCoord, dyeColor, mousePos, numCellsHalf);
                fluidSimulatorFast.ApplyDyeBrushGPU(centreCoord, dyeColor, mousePos, numCellsHalf);
            }

            mouseWorldPosPrev = mouseWorldPos;
            mousePosOld = mousePos;
        }
    }

    public void ApplyVelocityBrush(Vector2Int centreCoord, int numCellsHalf, Vector2 mouseDelta, float strength)
    {
        for (int oy = -numCellsHalf; oy <= numCellsHalf; oy++)
        {
            for (int ox = -numCellsHalf; ox <= numCellsHalf; ox++)
            {
                int x = centreCoord.x + ox;
                int y = centreCoord.y + oy;

                //skip cells outside grid
                if (x < 0 || x >= fluidGrid.CellCountX || y < 0 || y >= fluidGrid.CellCountY)
                {
                    continue;
                }
                
                float distSqr = ox*ox + oy*oy;
                float weight = 1f - Mathf.Clamp01(distSqr / Mathf.Pow(interactionRadius, 2));

                fluidGrid.VelocitiesX[x, y] += mouseDelta.x * weight * strength;
                fluidGrid.VelocitiesY[x, y] += mouseDelta.y * weight * strength;
            }
        }
    }

    public void ApplyDyeBrush(Vector2Int centreCoord, Color dyeColor, Vector2 mousePos, int numCellsHalf)
    {
        float radiusSq = numCellsHalf*numCellsHalf;
        for (int oy = -numCellsHalf; oy <= numCellsHalf; oy++)
        {
            for (int ox = -numCellsHalf; ox <= numCellsHalf; ox++)
            {
                if(ox*ox + oy*oy >= radiusSq){
                    continue;
                }
                int x = centreCoord.x + ox;
                int y = centreCoord.y + oy;

                if(!(x >= 0 && x < fluidGrid.CellCountX &&  y >= 0 && y < fluidGrid.CellCountY)){
                    continue;
                }

                if(!fluidGrid.SolidCellMap[x, y]){
                    fluidGrid.SmokeMap4Ch[x, y, 0] = dyeColor.r;
                    fluidGrid.SmokeMap4Ch[x, y, 1] = dyeColor.g;
                    fluidGrid.SmokeMap4Ch[x, y, 2] = dyeColor.b;
                    fluidGrid.SmokeMap4Ch[x, y, 3] = dyeColor.a;
                }
            }
        }
    }
}