using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidVizFast : MonoBehaviour
{
    private FluidGrid fluidGrid;
    public Color dyeColor = new Color(1f, 0, 0, 0.5f);
    public Color dyeColor2 = new Color(0, 1f, 0, 0.5f);
    public float interactionRadius = 2f;
    public float interactionStrength = 5f;
    private Vector2 mousePosOld;
    
    public bool drawVelocityField = true;
    public int velocityArrowSkip = 2;
    public float velocityArrowScale = 0.1f;
    public float minSpeedForColor = 0.0f;
    public float midSpeedForColor = 5.0f;

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

    public Camera m_camera;


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
        //DrawSolidCell();
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
                    fluidGrid.SmokeMap4Ch[x,y, 0] = r;
                    fluidGrid.SmokeMap4Ch[x,y, 1] = g;
                    fluidGrid.SmokeMap4Ch[x,y, 2] = b;
                    fluidGrid.SmokeMap4Ch[x,y, 3] = a;
                    Debug.Log("tere!");
                }

                smokeTexture.SetPixels(textureData);
                smokeTexture.Apply(false);
            }
        }
    }

    private void BuildVelocityMesh()
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
    
    private void UpdateSmokeTexture()
    {
        for (int x = 0; x < fluidGrid.CellCountX; x++)
        {
            for (int y = 0; y < fluidGrid.CellCountY; y++)
            {
                float r = fluidGrid.SmokeMap4Ch[x, y, 0];
                float g = fluidGrid.SmokeMap4Ch[x, y, 1];
                float b = fluidGrid.SmokeMap4Ch[x, y, 2];
                float a = fluidGrid.SmokeMap4Ch[x, y, 3];
                
                textureData[y * fluidGrid.CellCountX + x] = new Color(r, g, b, a);
            }
        }
        
        smokeTexture.SetPixels(textureData);
        smokeTexture.Apply(false);
    }

    private void LateUpdate()
    {
        if (fluidGrid == null) return;
        
        //the following methods will be called in late update since the input must be read first during the update loop
        
        // update smoke texture every frame
        UpdateSmokeTexture();
        
        // update velocity mesh periodically (every frame or less frequently)
        BuildVelocityMesh();
    }
    
    private Vector2Int CellCoordFromPos(Vector2 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / fluidGrid.CellSize);
        int y = Mathf.FloorToInt(worldPos.y / fluidGrid.CellSize);
        return new Vector2Int(x, y);
    }

    public void HandleInteraction()
    {
        if (fluidGrid == null) return; 

        
        //read mouse's coordinate
        Vector2 mousePos = m_camera.ScreenToWorldPoint(Input.mousePosition);
        //rate at which mouse was moving, used to affect fluid
        Vector2 mouseDelta = mousePos - mousePosOld;
        Vector2Int centreCoord = CellCoordFromPos(mousePos);

        int numCellsHalf = Mathf.CeilToInt(interactionRadius / fluidGrid.CellSize);

        //affect velocities
        if (Input.GetMouseButton(2))
        {
            for (int oy = -numCellsHalf; oy <= numCellsHalf; oy++)
            {
                for (int ox = -numCellsHalf; ox <= numCellsHalf; ox++)
                {
                    int x = centreCoord.x + ox;
                    int y = centreCoord.y + oy;

                    if (x < 0 || x >= fluidGrid.CellCountX || y < 0 || y >= fluidGrid.CellCountY)
                        continue;
                    
                    Vector2 cellCenterPos = new Vector2((x + 0.5f) * fluidGrid.CellSize, (y + 0.5f) * fluidGrid.CellSize);
                    float distSqr = (cellCenterPos - mousePos).sqrMagnitude;
                    float weight = 1f - Mathf.Clamp01(distSqr / Mathf.Pow(interactionRadius, 2));

                    fluidGrid.VelocitiesX[x, y] += mouseDelta.x * weight * interactionStrength;
                    fluidGrid.VelocitiesY[x, y] += mouseDelta.y * weight * interactionStrength;
                }
            }
        }
        
        //apply first color
        if (Input.GetMouseButton(0))
        {   

            float radiusSq = numCellsHalf*numCellsHalf;
            for (int oy = -numCellsHalf; oy <= numCellsHalf; oy++)
            {
                for (int ox = -numCellsHalf; ox <= numCellsHalf; ox++)
                {
                    //if (x < 0 || x >= fluidGrid.CellCountX || y < 0 || y >= fluidGrid.CellCountY)
                    if(ox*ox + oy*oy >= radiusSq){
                        continue;
                    }

                    int x = centreCoord.x + ox;
                    int y = centreCoord.y + oy;

                    fluidGrid.SmokeMap4Ch[x, y, 0] = dyeColor.r;
                    fluidGrid.SmokeMap4Ch[x, y, 1] = dyeColor.g;
                    fluidGrid.SmokeMap4Ch[x, y, 2] = dyeColor.b;
                    fluidGrid.SmokeMap4Ch[x, y, 3] = dyeColor.a;
                }
            }
        }

        //apply second color
        if (Input.GetMouseButton(1))
        {   

            float radiusSq = numCellsHalf*numCellsHalf;
            for (int oy = -numCellsHalf; oy <= numCellsHalf; oy++)
            {
                for (int ox = -numCellsHalf; ox <= numCellsHalf; ox++)
                {
                    //if (x < 0 || x >= fluidGrid.CellCountX || y < 0 || y >= fluidGrid.CellCountY)
                    if(ox*ox + oy*oy >= radiusSq){
                        continue;
                    }

                    int x = centreCoord.x + ox;
                    int y = centreCoord.y + oy;

                    fluidGrid.SmokeMap4Ch[x, y, 0] = dyeColor2.r;
                    fluidGrid.SmokeMap4Ch[x, y, 1] = dyeColor2.g;
                    fluidGrid.SmokeMap4Ch[x, y, 2] = dyeColor2.b;
                    fluidGrid.SmokeMap4Ch[x, y, 3] = dyeColor2.a;
                }
            }
        }

        mousePosOld = mousePos;
    }
}