using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class FluidGrid
{
    public readonly float[,] VelocitiesX;
    public readonly float[,] VelocitiesY;
    
    public readonly float[,] SmokeMap;
    
    public readonly float[,,] SmokeMap4Ch;

    public readonly bool[,] SolidCellMap;
    
    public readonly float[,] PressureMap;
    
    public readonly int CellCountX;
    public readonly int CellCountY;
    public float CellSize;
    
    public float TimeStepMul = 1;
    


    public FluidGrid(int cellCountX, int cellCountY, float cellSize)
    {
        this.CellCountX = cellCountX;
        this.CellCountY = cellCountY;
        this.CellSize = cellSize;

        this.VelocitiesX = new float[cellCountX + 1, cellCountY];
        this.VelocitiesY = new float[cellCountX, cellCountY+1];
        
        this.SmokeMap = new float[cellCountX, cellCountY];
        this.SmokeMap4Ch = new float[cellCountX, cellCountY, 4];

        this.SolidCellMap = new bool[cellCountX, cellCountY];
        
        this.PressureMap = new float[cellCountX, cellCountY];
        
    }
    
    public Vector2 CellCentre(int x, int y)
    {
        return new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);
    }

    public float SampleBilinear(float[,] values, Vector2 worldPos)
    {
        float px = worldPos.x / CellSize;
        float py = worldPos.y / CellSize;

        int edgeCountX = values.GetLength(0);
        int edgeCountY = values.GetLength(1);

        int left = Mathf.Clamp((int)px, 0, edgeCountX - 2);
        int bottom = Mathf.Clamp((int)py, 0, edgeCountY - 2);
        int right = left + 1;
        int top = bottom + 1;

        float xFrac = Mathf.Clamp01(px - left);
        float yFrac = Mathf.Clamp01(py - bottom);

        float valueTop = Mathf.Lerp(values[left, top], values[right, top], xFrac);
        float valueBottom = Mathf.Lerp(values[left, bottom], values[right, bottom], xFrac);
        return Mathf.Lerp(valueBottom, valueTop, yFrac);
    }
    
    public Vector2 GetVelocityAtWorldPos(Vector2 worldPos)
    {
        float velX = SampleBilinear(VelocitiesX, new Vector2(worldPos.x, worldPos.y - 0.5f * CellSize));
        float velY = SampleBilinear(VelocitiesY, new Vector2(worldPos.x - 0.5f * CellSize, worldPos.y));
        
        return new Vector2(velX, velY);
    }
    
    struct PressureSolveData
    {
        public float flowLeft;
        public float flowRight;
        public float flowTop;
        public float flowBottom;
        public int flowEdgeCount;
        public bool isSolid;
        public float velocityTerm;
    }
    
    public bool IsSolid(int x, int y)
    {
        x = Mathf.Clamp(x, 0, CellCountX - 1);
        y = Mathf.Clamp(y, 0, CellCountY - 1);
        return SolidCellMap[x, y];
    }
    
    public void ClearVelocities()
    {
        for (int x = 0; x < VelocitiesX.GetLength(0); x++)
        {
            for (int y = 0; y < VelocitiesX.GetLength(1); y++)
            {
                VelocitiesX[x, y] = 0;
            }
        }
        
        for (int x = 0; x < VelocitiesY.GetLength(0); x++)
        {
            for (int y = 0; y < VelocitiesY.GetLength(1); y++)
            {
                VelocitiesY[x, y] = 0;
            }
        }
        
        for (int x = 0; x < CellCountX; x++)
        {
            for (int y = 0; y < CellCountY; y++)
            {
                PressureMap[x, y] = 0;
            }
        }
    }

    public void ClearDye()
    {
        for (int x = 0; x < CellCountX; x++)
        {
            for (int y = 0; y < CellCountY; y++)
            {
                SmokeMap[x, y] = 0;
                SmokeMap4Ch[x, y, 0] = SmokeMap4Ch[x, y, 1] = SmokeMap4Ch[x, y, 2] = SmokeMap4Ch[x, y, 3] = 0;
            }
        }
    }

    public void SetEdgeBoundaries()
    {
        for (int x = 0; x < CellCountX; x++)
        {
            SolidCellMap[x, 0] = true;
            SolidCellMap[x, CellCountY - 1] = true;
        }

        for (int y = 0; y < CellCountY; y++)
        {
            SolidCellMap[0, y] = true;
            SolidCellMap[CellCountX - 1, y] = true;
        }
    }

    public void SetCircularBoundary(int x_pos, int y_pos, int radius)
    {
        for (int x = math.max(0,x_pos-radius); x < math.max(CellCountX,x_pos+radius); x++)
        {
            for (int y = math.max(0,y_pos-radius); y < math.max(CellCountY,y_pos+radius); y++)
            {   
                //if()
                if ((x-x_pos)*(x-x_pos) + (y-y_pos)*(y-y_pos) <= radius*radius)//Mathf.Abs(x-x_pos)+Mathf.Abs(y-y_pos) <= radius)
                {
                    SolidCellMap[x, y] = true;
                }
            }
        }
    }

    public void SetDiamondBoundary(int x_pos, int y_pos, int radius)
    {
        for (int x = math.max(0,x_pos-radius); x < math.max(CellCountX,x_pos+radius); x++)
        {
            for (int y = math.max(0,y_pos-radius); y < math.max(CellCountY,y_pos+radius); y++)
            {   
                //if()
                if (Mathf.Abs(x-x_pos)+Mathf.Abs(y-y_pos) <= radius)
                {
                    SolidCellMap[x, y] = true;
                }
            }
        }
    }
}