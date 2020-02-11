using System.Collections.Generic;
using UnityEngine;

using Terrain = TerrainGenerator.Terrain;

public class TerrainCreatorController : MonoBehaviour
{
	[SerializeField] private float Granularity = 0.1f;

	private BoxCollider TerrainCollider { get; set; }

	private Terrain ObservedTerrain { get; set; }
	private float[] OutputValues { get; set; }

	private GameObject Brush { get; set; }
	private Material BrushMaterial { get; set; }
	private float BrushRadius { get; set; } = 1.0f;
	private float BrushZCorretion { get; set; } = 0.0f;

	private void Start()
	{
		Brush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		Brush.name = "CursorSphere";
		Destroy(Brush.GetComponent<SphereCollider>());
		BrushMaterial = Brush.GetComponent<MeshRenderer>().material;
		BrushMaterial.shader = Shader.Find("Unlit/ColorBlend");

		TerrainCollider = GetComponent<BoxCollider>();
		ObservedTerrain = new Terrain();

		// Create dynamically
		ObservedTerrain.GenerateFlat();
		ObservedTerrain.SetTargets
		(
			new List<Vector3>()
			{
				new Vector3(-10.0f, 10.0f, 15.0f),
				new Vector3(11.0f, -11.0f, 14.0f),
				new Vector3(12.0f, -12.0f, 13.0f),
				new Vector3(13.0f, -13.0f, 12.0f)
			}
		);
		OutputValues = new float[4];

		ObservedTerrain.UpdateTerrain(new Vector3(-10.0f, 10.0f, 15.0f), 0.01f, +25.0f);
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
				//ObservedTerrain.Calculate(OutputValues);
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
