using Dreamteck.Splines;
using UnityEngine;

public class TentacleSplineDriver : MonoBehaviour
{
    [Header("Components")]
    public SplineComputer spline;
    public Transform[] targets; // Сюда закинем 31 пустышку (Target_Bones)

    [Header("Controls")]
    [Range(0f, 1f)] 
    public float coverage = 1f; // Насколько тентакля заполняет сплайн

    private Quaternion[] rotationOffsets;

    void Start()
    {
        if (spline == null || targets == null || targets.Length == 0) return;

        rotationOffsets = new Quaternion[targets.Length];
        
        for (int i = 0; i < targets.Length; i++)
        {
            float percent = (float)i / (targets.Length - 1);
            SplineSample sample = spline.Evaluate(percent);
            
            // Use the explicit normals we carefully calculated in the controller
            Quaternion currentSplineRot = Quaternion.LookRotation(sample.forward, sample.up);
            
            // Cache the local offset of the bone relative to this spline rotation
            rotationOffsets[i] = Quaternion.Inverse(currentSplineRot) * targets[i].rotation;
        }
    }

    public void EvaluateSpline()
    {
        if (spline == null || targets == null || targets.Length == 0) return;

        if (rotationOffsets == null || rotationOffsets.Length != targets.Length)
        {
            Start();
        }

        for (int i = 0; i < targets.Length; i++)
        {
            float percent = (float)i / (targets.Length - 1);
            float targetT = Mathf.Clamp01(percent * coverage);
            SplineSample sample = spline.Evaluate(targetT);

            targets[i].position = sample.position;
            
            // Use the mathematically stable frame directly from the spline
            Quaternion currentSplineRot = Quaternion.LookRotation(sample.forward, sample.up);
            targets[i].rotation = currentSplineRot * rotationOffsets[i]; 
        }
    }
}