using System.Collections.Generic;
using UnityEngine;

using Terrain = TerrainGenerator.Terrain;

public class TerrainCreatorController : MonoBehaviour
{
	[SerializeField] private float Granularity = 0.1f;
	[SerializeField] private float BrushRadius = 1.0f;

	private BoxCollider Collider { get; set; }

	private Terrain MainTerrain { get; set; }
	private float[] OutputValues { get; set; }

	private void Start()
	{
		Collider = GetComponent<BoxCollider>();
		MainTerrain = new Terrain();

		// Create dynamically
		MainTerrain.GenerateRandom();
		MainTerrain.SetTargets
		(
			new List<Vector3>()
			{
				new Vector3(-10.0f, 10.0f, 15.0f),
				new Vector3(-11.0f, 11.0f, 14.0f),
				new Vector3(-12.0f, 12.0f, 13.0f),
				new Vector3(-13.0f, 13.0f, 12.0f)
			}
		);
		OutputValues = new float[4];
	}

	private void Update()
	{
		var build = Input.GetMouseButton(0);
		var clear = Input.GetMouseButton(1);

		/*
		if (build ^ clear)
		{
			var center = Camera.main.ScreenToWorldPoint(Input.mousePosition);

			if (Collider.bounds.Contains(center))
			{
				Debug.Log(center);
			}
		}
		*/

		if (Input.GetMouseButtonDown(0))
		{
			MainTerrain.Calculate(OutputValues);

			for (var i = 0; i < OutputValues.Length; ++i)
				Debug.Log(OutputValues[i]);
		}
	}

	private void OnDestroy()
	{
		MainTerrain.Dispose();
		MainTerrain = null;
	}
}
