using UnityEngine;
using System.Collections;
using EzySlice;

public class SimpleSlicer : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource cutSound;
    public AudioClip cutClip;

    [Header("Slice Settings")]
    public Material materialAfterSlice;
    public LayerMask sliceMask;
    public float sliceForce = 5f;

    [Header("Blade Settings")]
    public Vector3 bladeSize = new Vector3(0.1f, 0.1f, 0.5f);
    public Vector3 bladeOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Trajectory Settings")]
    public int trajectorySamples = 5; // Количество samples для усреднения траектории
    public float minSliceDistance = 0.1f; // Минимальное расстояние для определения траектории

    private Vector3[] previousPositions;
    private int currentSampleIndex = 0;
    private Vector3 currentDirection;

    void Start()
    {
        // Инициализируем массив предыдущих позиций
        previousPositions = new Vector3[trajectorySamples];
        Vector3 startPosition = GetBladeWorldPosition();
        for (int i = 0; i < trajectorySamples; i++)
        {
            previousPositions[i] = startPosition;
        }
    }

    void Update()
    {
        UpdateTrajectory();
    }

    public void PerformSlice()
    {
        Vector3 bladeWorldPos = GetBladeWorldPosition();
        Quaternion bladeWorldRot = GetBladeWorldRotation();
        Vector3 scaledBladeSize = GetScaledBladeSize();

        Collider[] hits = Physics.OverlapBox(
            bladeWorldPos,
            scaledBladeSize / 2f,
            bladeWorldRot,
            sliceMask
        );

        Debug.Log($"Found {hits.Length} objects to slice");

        foreach (Collider hit in hits)
        {
            if (hit != null && hit.gameObject != null)
            {
                SliceObject(hit.gameObject, bladeWorldPos);
            }
        }
    }

    void UpdateTrajectory()
    {
        // Сохраняем текущую позицию
        Vector3 currentPosition = GetBladeWorldPosition();
        previousPositions[currentSampleIndex] = currentPosition;
        currentSampleIndex = (currentSampleIndex + 1) % trajectorySamples;

        // Вычисляем направление на основе нескольких предыдущих позиций
        CalculateAverageDirection();
    }

    void CalculateAverageDirection()
    {
        Vector3 averageDirection = Vector3.zero;
        int validSamples = 0;

        // Вычисляем среднее направление из нескольких samples
        for (int i = 0; i < trajectorySamples - 1; i++)
        {
            int currentIndex = (currentSampleIndex + i) % trajectorySamples;
            int nextIndex = (currentSampleIndex + i + 1) % trajectorySamples;

            Vector3 segmentStart = previousPositions[currentIndex];
            Vector3 segmentEnd = previousPositions[nextIndex];

            float distance = Vector3.Distance(segmentStart, segmentEnd);

            if (distance > minSliceDistance)
            {
                Vector3 segmentDirection = (segmentEnd - segmentStart).normalized;
                averageDirection += segmentDirection;
                validSamples++;
            }
        }

        if (validSamples > 0)
        {
            currentDirection = (averageDirection / validSamples).normalized;
        }
        else
        {
            // Если нет валидных samples, используем текущее направление лезвия
            currentDirection = GetBladeWorldRotation() * Vector3.forward;
        }
    }

    void SliceObject(GameObject target, Vector3 slicePosition)
    {
        if (!IsMeshReadable(target)) return;

        Vector3 sliceNormal = CalculateSliceNormal();

        Debug.Log($"Slicing {target.name} with normal {sliceNormal} (direction: {currentDirection})");

        SlicedHull slicedObject = target.Slice(slicePosition, sliceNormal, materialAfterSlice);

        if (slicedObject != null)
        {
            CreateSlicedParts(slicedObject, target, slicePosition);
        }
        else
        {
            Debug.LogWarning($"Slice failed for {target.name}");
        }
    }

    Vector3 CalculateSliceNormal()
    {
        // Улучшенный расчет нормали на основе траектории движения
        if (currentDirection.magnitude > 0.1f)
        {
            // Основная логика: нормаль перпендикулярна направлению движения
            Vector3 sliceNormal = Vector3.Cross(currentDirection, Vector3.up).normalized;

            // Если результат почти нулевой (движение вертикальное), используем альтернативный расчет
            if (sliceNormal.magnitude < 0.1f)
            {
                // Для вертикального движения используем нормаль, перпендикулярную лезвию
                sliceNormal = Vector3.Cross(currentDirection, GetBladeWorldRotation() * Vector3.right).normalized;
            }

            // Если все еще нулевой, используем глобальное правое направление
            if (sliceNormal.magnitude < 0.1f)
            {
                sliceNormal = Vector3.right;
            }

            return sliceNormal.normalized;
        }

        // Fallback: используем направление лезвия
        return GetBladeWorldRotation() * Vector3.right;
    }

    void CreateSlicedParts(SlicedHull slicedObject, GameObject original, Vector3 slicePoint)
    {
        GameObject upperHull = slicedObject.CreateUpperHull(original, materialAfterSlice);
        GameObject lowerHull = slicedObject.CreateLowerHull(original, materialAfterSlice);

        if (upperHull != null && lowerHull != null)
        {
            upperHull.transform.position = slicePoint;
            lowerHull.transform.position = slicePoint;

            SetupSlicedPart(upperHull);
            SetupSlicedPart(lowerHull);

            AddSliceForce(upperHull, lowerHull);
            PlaySliceSound();
            Destroy(original);

            Debug.Log($"Successfully sliced {original.name}");
        }
    }

    IEnumerator DestroyAfterPlay(AudioSource src)
    {
        yield return new WaitForSeconds(src.clip.length + 0.1f);
        Destroy(src);
    }

    void PlaySliceSound()
    {
        cutSound.PlayOneShot(cutClip);
    }

    void SetupSlicedPart(GameObject slicedPart)
    {
        Rigidbody rb = slicedPart.AddComponent<Rigidbody>();
        MeshCollider collider = slicedPart.AddComponent<MeshCollider>();
        collider.convex = true;
        Destroy(slicedPart, 5f);
    }

    void AddSliceForce(GameObject upperHull, GameObject lowerHull)
    {
        Rigidbody upperRb = upperHull.GetComponent<Rigidbody>();
        Rigidbody lowerRb = lowerHull.GetComponent<Rigidbody>();

        if (upperRb != null)
        {
            upperRb.AddForce(currentDirection * sliceForce + Vector3.up * sliceForce * 0.5f, ForceMode.Impulse);
            upperRb.AddTorque(Random.insideUnitSphere * sliceForce, ForceMode.Impulse);
        }

        if (lowerRb != null)
        {
            lowerRb.AddForce(-currentDirection * sliceForce + Vector3.down * sliceForce * 0.5f, ForceMode.Impulse);
            lowerRb.AddTorque(Random.insideUnitSphere * sliceForce, ForceMode.Impulse);
        }
    }

    bool IsMeshReadable(GameObject target)
    {
        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            try
            {
                Vector3[] vertices = meshFilter.sharedMesh.vertices;
                return true;
            }
            catch
            {
                Debug.LogWarning($"Mesh {target.name} is not readable. Enable Read/Write in import settings.");
                return false;
            }
        }
        return false;
    }

    Vector3 GetBladeWorldPosition()
    {
        Vector3 worldOffset = transform.rotation * Vector3.Scale(bladeOffset, transform.lossyScale);
        return transform.position + worldOffset;
    }

    Quaternion GetBladeWorldRotation()
    {
        return transform.rotation;
    }

    Vector3 GetScaledBladeSize()
    {
        return Vector3.Scale(bladeSize, transform.lossyScale);
    }

    // Визуализация в редакторе
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Vector3 worldPos = GetBladeWorldPosition();
        Quaternion worldRot = GetBladeWorldRotation();
        Vector3 scaledSize = GetScaledBladeSize();

        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(worldPos, worldRot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, scaledSize);
        Gizmos.matrix = originalMatrix;

        // Показываем смещение
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, worldPos);
        Gizmos.DrawWireSphere(transform.position, 0.05f);

        // Показываем траекторию и направление
        if (Application.isPlaying)
        {
            // Траектория из предыдущих позиций
            Gizmos.color = Color.green;
            for (int i = 0; i < trajectorySamples - 1; i++)
            {
                int currentIndex = (currentSampleIndex + i) % trajectorySamples;
                int nextIndex = (currentSampleIndex + i + 1) % trajectorySamples;
                Gizmos.DrawLine(previousPositions[currentIndex], previousPositions[nextIndex]);
            }

            // Текущее направление
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(worldPos, currentDirection * 1f);

            // Нормаль реза
            Gizmos.color = Color.magenta;
            Vector3 sliceNormal = CalculateSliceNormal();
            Gizmos.DrawRay(worldPos, sliceNormal * 0.5f);
        }
    }
}