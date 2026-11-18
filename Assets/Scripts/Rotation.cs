using UnityEngine;

public class Rotation : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(
           Vector3.up,
           30f * Time.deltaTime,
           Space.World
       );
    }
}
