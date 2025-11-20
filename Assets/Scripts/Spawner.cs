using UnityEngine;
using System.Collections;

public class Spawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject[] prefabs;       // Список предметов
    public float spawnInterval = 2f;   // Интервал между спавнами
    public Vector2 areaSize = new Vector2(5f, 5f); // Размер плоскости

    [Header("Spawn Height")]
    public float spawnY = 0.5f; // Высота над плоскостью

    private void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnObject();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnObject()
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("Spawner: Prefabs list is empty");
            return;
        }

        // Рандомная точка внутри плоскости
        float x = Random.Range(-areaSize.x / 2f, areaSize.x / 2f);
        float z = Random.Range(-areaSize.y / 2f, areaSize.y / 2f);
        Vector3 pos = new Vector3(x, spawnY, z) + transform.position;

        // Рандомный префаб
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];

        Instantiate(prefab, pos, Quaternion.identity);
    }

    // Визуализация плоскости
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.01f, new Vector3(areaSize.x, 0.02f, areaSize.y));
    }
}
