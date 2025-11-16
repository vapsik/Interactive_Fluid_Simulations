using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FluidGrid
{
    public readonly float[,] VelocitiesX;
    public readonly float[,] VelocitiesY;

    private readonly float[,] VelocitiesXTemp;
    private readonly float[,] VelocitiesYTemp;
    
    public readonly float[,] SmokeMap;
    public readonly float[,] SmokeMapTemp;
    public readonly bool[,] SolidCellMap;
    
    public readonly float[,] PressureMap;
    readonly PressureSolveData[,] PressureSolveDataMap;
    
    public readonly int CellCountX;
    public readonly int CellCountY;
    public float CellSize;
    
    public float TimeStepMul = 1;
    float TimeStep => 1 / 60f * TimeStepMul;
    
    //successive over-relaxation factor
    public float SOR = 1.7f;
    
    private const float Density = 1;

    public FluidGrid(int cellCountX, int cellCountY, float cellSize)
    {
        this.CellCountX = cellCountX;
        this.CellCountY = cellCountY;
        this.CellSize = cellSize;

        this.VelocitiesX = new float[cellCountX + 1, cellCountY];
        this.VelocitiesY = new float[cellCountX, cellCountY+1];

        this.VelocitiesXTemp = new float[cellCountX + 1, cellCountY];
        this.VelocitiesYTemp = new float[cellCountX, cellCountY+1];
        
        this.SmokeMap = new float[cellCountX, cellCountY];
        this.SmokeMapTemp = new float[cellCountX, cellCountY];
        this.SolidCellMap = new bool[cellCountX, cellCountY];
        
        this.PressureMap = new float[cellCountX, cellCountY];
        this.PressureSolveDataMap = new PressureSolveData[cellCountX, cellCountY];
        
        for (int x = 0; x < cellCountX; x++)
        {
            SolidCellMap[x, 0] = true;
            SolidCellMap[x, cellCountY - 1] = true;
        }

        for (int y = 0; y < cellCountY; y++)
        {
            SolidCellMap[0, y] = true;
            SolidCellMap[cellCountX - 1, y] = true;
        }
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

    public void AdvectDye()
    {
        for (int x = 0; x < CellCountX; x++)
        {
            for (int y = 0; y < CellCountY; y++)
            {
                Vector2 pos = CellCentre(x, y);
                Vector2 vel = GetVelocityAtWorldPos(pos);
                Vector2 posPrev = pos - vel * TimeStep;
                float amount = SampleBilinear(SmokeMap, posPrev - new Vector2(0.5f * CellSize, 0.5f * CellSize));
                SmokeMapTemp[x, y] = amount;
            }
        }
        for (int x = 0; x < CellCountX; x++)
        {
            for (int y = 0; y < CellCountY; y++)
            {
                SmokeMap[x, y] = SmokeMapTemp[x, y];
            }
        }
    }
    
    public void AdvectVelocity()
    {
        for (int x = 0; x < VelocitiesX.GetLength(0); x++)
        {
            for (int y = 0; y < VelocitiesX.GetLength(1); y++)
            {
                Vector2 pos = new Vector2(x * CellSize, (y + 0.5f) * CellSize);
                Vector2 vel = GetVelocityAtWorldPos(pos);
                Vector2 posPrev = pos - vel * TimeStep;
                float amount = SampleBilinear(VelocitiesX, new Vector2(posPrev.x, posPrev.y - 0.5f * CellSize));
                VelocitiesXTemp[x, y] = amount;
            }
        }
        
        for (int x = 0; x < VelocitiesY.GetLength(0); x++)
        {
            for (int y = 0; y < VelocitiesY.GetLength(1); y++)
            {
                Vector2 pos = new Vector2((x + 0.5f) * CellSize, y * CellSize);
                Vector2 vel = GetVelocityAtWorldPos(pos);
                Vector2 posPrev = pos - vel * TimeStep;
                float amount = SampleBilinear(VelocitiesY, new Vector2(posPrev.x - 0.5f * CellSize, posPrev.y));
                VelocitiesYTemp[x, y] = amount;
            }
        }
        
        for (int x = 0; x < VelocitiesX.GetLength(0); x++)
        {
            for (int y = 0; y < VelocitiesX.GetLength(1); y++)
            {
                VelocitiesX[x, y] = VelocitiesXTemp[x, y];
            }
        }
        for (int x = 0; x < VelocitiesY.GetLength(0); x++)
        {
            for (int y = 0; y < VelocitiesY.GetLength(1); y++)
            {
                VelocitiesY[x, y] = VelocitiesYTemp[x, y];
            }
        }
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

    float GetPressure(int x, int y)
    {
        x = Mathf.Clamp(x, 0, CellCountX - 1);
        y = Mathf.Clamp(y, 0, CellCountY - 1);
        return PressureMap[x, y];
    }

    void PreparePressureSolver()
    {
        for (int x = 0; x < CellCountX; x++)
        {
            for (int y = 0; y < CellCountY; y++)
            {
                int flowTop = IsSolid(x + 0, y + 1) ? 0 : 1;
                int flowLeft = IsSolid(x - 1, y + 0) ? 0 : 1;
                int flowRight = IsSolid(x + 1, y + 0) ? 0 : 1;
                int flowBottom = IsSolid(x + 0, y - 1) ? 0 : 1;
                int fluidEdgeCount = flowLeft + flowRight + flowTop + flowBottom;
                bool isSolid = IsSolid(x, y);
                
                float velocityTop = VelocitiesY[x + 0, y + 1];
                float velocityLeft = VelocitiesX[x + 0, y + 0];
                float velocityRight = VelocitiesX[x + 1, y + 0];
                float velocityBottom = VelocitiesY[x + 0, y + 0];
                
                float velTerm = (velocityRight - velocityLeft + velocityTop - velocityBottom) / TimeStep;
                
                PressureSolveDataMap[x, y] = new PressureSolveData()
                {
                    flowLeft = flowLeft,
                    flowRight = flowRight,
                    flowTop = flowTop,
                    flowBottom = flowBottom,
                    isSolid = isSolid,
                    flowEdgeCount = fluidEdgeCount,
                    velocityTerm = velTerm
                };
            }
        }
    }

    void PressureSolve()
    {
        for (int x = 0; x < CellCountX; x++)
        {
            for (int y = 0; y < CellCountY; y++)
            {
                PressureSolveData info = PressureSolveDataMap[x, y];

                if (info.isSolid || info.flowEdgeCount == 0)
                {
                    PressureMap[x, y] = 0;
                    continue;
                }
                
                float pressureTop = GetPressure(x, y + 1) * info.flowTop;
                float pressureLeft = GetPressure(x - 1, y) * info.flowLeft;
                float pressureRight = GetPressure(x + 1, y) * info.flowRight;
                float pressureBottom = GetPressure(x, y - 1) * info.flowBottom;
                
                float pressureSum = pressureRight + pressureLeft + pressureTop + pressureBottom;
                float newPressure = (pressureSum - Density * CellSize * info.velocityTerm) / info.flowEdgeCount;
                float oldPressure = PressureMap[x, y];
                PressureMap[x, y] = oldPressure + (newPressure - oldPressure) * SOR;
            }
        }
    }
    
    public void RunPressureSolver(int iterations)
    {
        PreparePressureSolver();
        
        for (int i = 0; i < iterations; i++)
        {
            PressureSolve();
        }
    }

    public void UpdateVelocities()
    {
        float K = TimeStep / (Density * CellSize);

        for (int x = 0; x < VelocitiesX.GetLength(0); x++)
        {
            for (int y = 0; y < VelocitiesX.GetLength(1); y++)
            {
                if (IsSolid(x, y) || IsSolid(x - 1, y))
                {
                    VelocitiesX[x, y] = 0;
                    continue;
                }

                float pressureRight = GetPressure(x, y);
                float pressureLeft = GetPressure(x - 1, y);

                VelocitiesX[x, y] -= K * (pressureRight - pressureLeft);
            }
        }

        for (int x = 0; x < VelocitiesY.GetLength(0); x++)
        {
            for (int y = 0; y < VelocitiesY.GetLength(1); y++)
            {
                if (IsSolid(x, y) || IsSolid(x, y - 1))
                {
                    VelocitiesY[x, y] = 0;
                    continue;
                }

                float pressureTop = GetPressure(x, y);
                float pressureBottom = GetPressure(x, y - 1);

                VelocitiesY[x, y] -= K * (pressureTop - pressureBottom);
            }
        }
    }
}