using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    public GameObject baseCarPrefab;
    public GameObject[] carModels;
    public Transform[] spawnPoints;
    public Vector3[] moveDirections;
    public float spawnInterval = 4f;
    public int maxCarCount = 10;

    float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;

            int currentCount = GameObject.FindObjectsOfType<CarController>().Length;

            if (currentCount < maxCarCount && spawnPoints.Length > 0 && baseCarPrefab != null && carModels.Length > 0)
            {
                SpawnCar();
            }
        }
    }

    void SpawnCar()
    {
        int index = Random.Range(0, spawnPoints.Length);
        Transform spawnPoint = spawnPoints[index];

        GameObject newCar = Instantiate(baseCarPrefab, spawnPoint.position, spawnPoint.rotation);

        GameObject model = carModels[Random.Range(0, carModels.Length)];
        GameObject visual = Instantiate(model, newCar.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        CarController controller = newCar.GetComponent<CarController>();
        if (controller != null && index < moveDirections.Length)
        {
            controller.SetDirection(moveDirections[index]);
        }
    }
}
