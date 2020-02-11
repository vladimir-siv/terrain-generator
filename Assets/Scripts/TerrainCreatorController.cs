using System.Collections.Generic;
using UnityEngine;

using Terrain = TerrainGenerator.Terrain;

public class TerrainCreatorController : MonoBehaviour
{
	private BoxCollider TerrainCollider { get; set; }
	private float TerrainGranularity { get; set; }

	private Terrain ObservedTerrain { get; set; }
	private float[] TargetValues { get; set; }

	private GameObject Brush { get; set; }
	private Material BrushMaterial { get; set; }
	private float BrushRadius { get; set; }
	private float BrushZCorretion { get; set; }

	private void Start()
	{
		TerrainCollider = GetComponent<BoxCollider>();
		TerrainGranularity = 100.0f;
		ObservedTerrain = new Terrain();
		
		// Create dynamically
		ObservedTerrain.GenerateFlat();
		ObservedTerrain.SetTargets
		(
			new List<Vector3>()
			{
				new Vector3(1.0f, 3.0f, 2.0f),
				new Vector3(7.0f, 9.0f, 5.0f),
				new Vector3(8.0f, 8.0f, 5.0f),
				new Vector3(9.0f, 7.0f, 5.0f),
			}
		);
		TargetValues = new float[4];
		ObservedTerrain.UpdateTerrain(new Vector3(1.0f, 3.0f, 2.0f), 0.01f, +25.0f);
		ObservedTerrain.Calculate();
		ObservedTerrain.GetTargetValues(TargetValues);
		foreach (var val in TargetValues) Debug.Log(val);

		Brush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		Brush.name = "CursorSphere";
		Destroy(Brush.GetComponent<SphereCollider>());
		BrushMaterial = Brush.GetComponent<MeshRenderer>().material;
		BrushMaterial.shader = Shader.Find("Unlit/ColorBlend");
		BrushRadius = 1.0f;
		BrushZCorretion = 0.0f;
	}

	private void Update()
	{
		var mouse = Input.mousePosition;
		var cam = Camera.main;

		var z = Vector3.Distance(cam.transform.position, transform.position);
		var center = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, z + BrushZCorretion));
		var alpha = 0.0f;

		if (TerrainCollider.bounds.Contains(center))
		{
			alpha = 0.3f;

			if (Input.GetKey(KeyCode.UpArrow)) BrushRadius += 0.01f;
			if (Input.GetKey(KeyCode.DownArrow)) BrushRadius -= 0.01f;
			BrushZCorretion += Input.mouseScrollDelta.y / 10.0f;

			if (Input.GetKeyDown(KeyCode.R))
			{
				BrushRadius = 1.0f;
				BrushZCorretion = 0.0f;
			}

			var build = Input.GetMouseButton(0);
			var clear = Input.GetMouseButton(1);

			if (build ^ clear)
			{
				if (build) ObservedTerrain.UpdateTerrain(center, BrushRadius, +0.01f);
				if (clear) ObservedTerrain.UpdateTerrain(center, BrushRadius, -0.01f);
			}
		}

		Brush.transform.position = center;
		Brush.transform.localScale = new Vector3(BrushRadius, BrushRadius, BrushRadius);
		BrushMaterial.SetColor("_Color", new Color(1.0f, 0.0f, 0.0f, alpha));
	}

	private void OnDestroy()
	{
		ObservedTerrain.Dispose();
		ObservedTerrain = null;
	}
}
