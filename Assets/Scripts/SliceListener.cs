using UnityEngine;

public class SimpleSliceListener : MonoBehaviour
{
    public SimpleSlicer slicer;

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Trigger entered with: {other.name}");

        if (slicer != null)
        {
            slicer.PerformSlice();
        }
    }
}