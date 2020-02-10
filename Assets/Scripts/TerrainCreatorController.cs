using UnityEngine;

public class TerrainCreatorController : MonoBehaviour
{
	[SerializeField] private float Granularity = 0.1f;
	[SerializeField] private float BrushRadius = 1.0f;

	private BoxCollider Collider { get; set; }

	private void Start()
	{
		Collider = GetComponent<BoxCollider>();
	}

	private void Update()
	{
		var build = Input.GetMouseButton(0);
		var clear = Input.GetMouseButton(1);

		if (build ^ clear)
		{
			var center = Camera.main.ScreenToWorldPoint(Input.mousePosition);

			if (Collider.bounds.Contains(center))
			{
				Debug.Log(center);
			}
		}
	}
}
