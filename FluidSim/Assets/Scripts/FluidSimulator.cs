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
    public float sor = 1.7f;
    
    FluidVisualizer fluidVisualizer;
    
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
    }
    
    void Update()
    {
        if (fluidGrid == null) return;

        fluidGrid.TimeStepMul = timeStepMul;
        fluidGrid.SOR = sor;

        fluidGrid.AdvectVelocity(); 

        fluidVisualizer.HandleInteraction(); 

        fluidGrid.RunPressureSolver(solverIterations); 
        fluidGrid.UpdateVelocities();

        fluidGrid.AdvectDye();
    }
}