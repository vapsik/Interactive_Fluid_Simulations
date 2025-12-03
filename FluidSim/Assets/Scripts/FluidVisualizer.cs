using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidVisualizer : MonoBehaviour
{
    private FluidGrid fluidGrid;
    public Color dyeColor = new Color(1, 0, 0, 0.5f);
    public Color dyeColor2 = new Color(0, 1, 0, 0.5f);
    public float interactionRadius = 2f;
    public float interactionStrength = 5f;
    private Vector2 mousePosOld;
    
    public bool drawVelocityField = true;
    public int velocityArrowSkip = 2; //draw every Nth arrow
    public float velocityArrowScale = 0.1f;
    public float minSpeedForColor = 0.0f;
    public float midSpeedForColor = 5.0f;

    public void SetFluidGrid(FluidGrid grid)
    {
        this.fluidGrid = grid;
    }
    
    void OnDrawGizmos()
    {
        if (fluidGrid == null)
        {
            return;
        }
        
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
        }
        
        int skip = Mathf.Max(1, velocityArrowSkip);

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

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 mouseDelta = mousePos - mousePosOld;
        
        Vector2Int centreCoord = CellCoordFromPos(mousePos);
        //brush size in cells
        int numCellsHalf = Mathf.CeilToInt(interactionRadius / fluidGrid.CellSize);

        if (Input.GetMouseButton(0))
        {
            for (int oy = -numCellsHalf; oy <= numCellsHalf; oy++)
            {
                for (int ox = -numCellsHalf; ox <= numCellsHalf; ox++)
                {
                    int x = centreCoord.x + ox;
                    int y = centreCoord.y + oy;

                    //skip cells outside grid boundaries
                    if (x < 0 || x >= fluidGrid.CellCountX || y < 0 || y >= fluidGrid.CellCountY)
                    {
                        continue;
                    }
                    
                    Vector2 cellCenterPos = new Vector2((x + 0.5f) * fluidGrid.CellSize, (y + 0.5f) * fluidGrid.CellSize);
                    float distSqr = (cellCenterPos - mousePos).sqrMagnitude;
                    float weight = 1f - Mathf.Clamp01(distSqr / Mathf.Pow(interactionRadius, 2));

                    fluidGrid.VelocitiesX[x, y] += mouseDelta.x * weight * interactionStrength;
                    fluidGrid.VelocitiesY[x, y] += mouseDelta.y * weight * interactionStrength;
                }
            }
        }
        
        if (Input.GetMouseButton(2))
        {
            for (int oy = -numCellsHalf; oy <= numCellsHalf; oy++)
            {
                for (int ox = -numCellsHalf; ox <= numCellsHalf; ox++)
                {
                    int x = centreCoord.x + ox;
                    int y = centreCoord.y + oy;

                    //skip cells outside grid boundaries
                    if (x < 0 || x >= fluidGrid.CellCountX || y < 0 || y >= fluidGrid.CellCountY)
                    {
                        continue;
                    }

                    fluidGrid.SmokeMap4Ch[x, y, 0] = dyeColor2.r;
                    fluidGrid.SmokeMap4Ch[x, y, 1] = dyeColor2.g;
                    fluidGrid.SmokeMap4Ch[x, y, 2] = dyeColor2.b;
                    fluidGrid.SmokeMap4Ch[x, y, 3] = dyeColor2.a;

                    //fluidGrid.SmokeMap[x,y] = 1;
                }
            }
        }

        if (Input.GetMouseButton(1))
        {
            for (int oy = -numCellsHalf; oy <= numCellsHalf; oy++)
            {
                for (int ox = -numCellsHalf; ox <= numCellsHalf; ox++)
                {
                    int x = centreCoord.x + ox;
                    int y = centreCoord.y + oy;

                    //skip cells outside grid boundaries
                    if (x < 0 || x >= fluidGrid.CellCountX || y < 0 || y >= fluidGrid.CellCountY)
                    {
                        continue;
                    }

                    fluidGrid.SmokeMap4Ch[x, y, 0] = dyeColor.r;
                    fluidGrid.SmokeMap4Ch[x, y, 1] = dyeColor.g;
                    fluidGrid.SmokeMap4Ch[x, y, 2] = dyeColor.b;
                    fluidGrid.SmokeMap4Ch[x, y, 3] = dyeColor.a;

                    //fluidGrid.SmokeMap[x,y] = 1;
                }
            }
        }

        mousePosOld = mousePos;
    }
}