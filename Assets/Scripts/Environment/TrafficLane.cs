using UnityEngine;

// Pista com carros andando em fila ao longo de uma linha com vários pontos (só visual).
// Desenha todos os carros por instancing, sem um GameObject por carro:
// uma chamada de desenho por modelo de carro.
public class TrafficLane : MonoBehaviour
{
    [SerializeField] private Vector3[] points;
    [SerializeField] private Mesh[] carMeshes;
    [SerializeField] private Material material;
    [SerializeField] private int cars = 14;
    [SerializeField] private float speed = 11f;
    [SerializeField] private int seed = 1;

    private float[] offsets;
    private int[] models;
    private Matrix4x4[][] batches;
    private int[] counts;
    private float[] cumulative;
    private float length;

    private void Awake()
    {
        if (carMeshes == null || carMeshes.Length == 0 || material == null || points == null || points.Length < 2)
        {
            enabled = false;
            return;
        }
        cumulative = new float[points.Length];
        for (int i = 1; i < points.Length; i++) cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        length = cumulative[points.Length - 1];

        var rng = new System.Random(seed);
        offsets = new float[cars];
        models = new int[cars];
        float spacing = length / cars;
        for (int i = 0; i < cars; i++)
        {
            offsets[i] = i * spacing + (float)rng.NextDouble() * spacing * 0.6f;
            models[i] = rng.Next(carMeshes.Length);
        }
        batches = new Matrix4x4[carMeshes.Length][];
        counts = new int[carMeshes.Length];
        for (int m = 0; m < carMeshes.Length; m++) batches[m] = new Matrix4x4[cars];
    }

    private Matrix4x4 At(float d)
    {
        int i = 1;
        while (i < points.Length - 1 && cumulative[i] < d) i++;
        float seg = cumulative[i] - cumulative[i - 1];
        float k = seg > 0f ? (d - cumulative[i - 1]) / seg : 0f;
        var pos = Vector3.Lerp(points[i - 1], points[i], k);
        var dir = points[i] - points[i - 1];
        return Matrix4x4.TRS(pos, dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir, Vector3.up) : Quaternion.identity, Vector3.one);
    }

    private void Update()
    {
        for (int m = 0; m < counts.Length; m++) counts[m] = 0;
        float shift = Time.time * speed;
        for (int i = 0; i < cars; i++)
        {
            int m = models[i];
            batches[m][counts[m]++] = At(Mathf.Repeat(offsets[i] + shift, length));
        }
        var rp = new RenderParams(material) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = true };
        for (int m = 0; m < carMeshes.Length; m++)
            if (counts[m] > 0)
                Graphics.RenderMeshInstanced(rp, carMeshes[m], 0, batches[m], counts[m]);
    }
}
