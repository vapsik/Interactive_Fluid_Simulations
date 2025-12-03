using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraPositioning : MonoBehaviour
{

    public Transform fluidManager;

    FluidSimulator fluidGrid;

    public Camera Camera;

    public bool SetSizeAccordingToGrid = true;

    // Start is called before the first frame update
    void Start()
    {
        if (SetSizeAccordingToGrid){
            fluidGrid = fluidManager.GetComponent<FluidSimulator>();
            Camera.orthographicSize = fluidGrid.CellCountX * fluidGrid.CellSize/4.0f + 2.0f;

            //centering
            transform.position = new Vector3(fluidGrid.CellCountX * fluidGrid.CellSize*0.5f, fluidGrid.CellCountY * fluidGrid.CellSize*0.5f, transform.position.z);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
