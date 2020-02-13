using System;
using UnityEngine;
using Terrain = TerrainGenerator.Terrain;

public class TerrainCreatorController : MonoBehaviour
{
	[SerializeField] private float TerrainStep = 0.1f;
	[SerializeField] private float TerrainScale = 10.0f;

	private Terrain ObservedTerrain { get; set; }
	private Mesh TerrainMesh { get; set; }
	private BoxCollider TerrainCollider { get; set; }
	private int TerrainGranularity { get; set; }

	private GameObject Brush { get; set; }
	private Material BrushMaterial { get; set; }
	private float BrushRadius { get; set; }
	private float BrushZCorretion { get; set; }

	private void Awake()
	{
		//if (tag == "Terrain") return;
		Debug.Log("Awake debug enabled!");

		var granularity = 2;
		var x = 1; var y = 1; var z = 1;
		var radius = 1f;
		var delta = +5.0f;

		using (var terrain = new Terrain())
		{
			terrain.GenerateEmpty(0.5f, 1f);
			terrain.Gridify(granularity);
			terrain.Update(new Vector3(terrain.Step * x, terrain.Step * y, terrain.Step * z), radius, delta);
			terrain.Calculate();

			var values_size = terrain.Size;
			var target_size = terrain.Granularity + 1;
			values_size = values_size * values_size * values_size;
			target_size = target_size * target_size * target_size;

			var values = new float[values_size];
			var targets = new Vector3[target_size];
			var target_values = new float[target_size];

			terrain.GetValues(values);
			terrain.GetTargets(targets);
			terrain.GetTargetValues(target_values);

			System.IO.File.WriteAllLines
			(
				System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), @"logs\values.txt"),
				System.Linq.Enumerable.Select(values, v => v.ToString("F2"))
			);
			System.IO.File.WriteAllLines
			(
				System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), @"logs\targets.txt"),
				System.Linq.Enumerable.Select(targets, v => v.ToString())
			);
			System.IO.File.WriteAllLines
			(
				System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), @"logs\target_values.txt"),
				System.Linq.Enumerable.Select(target_values, v => v.ToString("F2"))
			);
		}
	}

	private void GenerateTerrain(int granularity, bool empty = true)
	{
		if (granularity <= 0) throw new ArgumentException(nameof(granularity));

		if (empty) ObservedTerrain.GenerateEmpty(TerrainStep, TerrainScale);
		else ObservedTerrain.GenerateRandom(TerrainStep, TerrainScale);

		TerrainGranularity = granularity;
		ObservedTerrain.Gridify(granularity);
	}

	private void Start()
	{
		ObservedTerrain = new Terrain();
		TerrainMesh = GetComponent<MeshFilter>().mesh;
		TerrainCollider = GetComponent<BoxCollider>();
		GetComponent<MeshRenderer>().material.SetFloat("_Scale", ObservedTerrain.Scale);
		GenerateTerrain(50);

		transform.position = new Vector3(ObservedTerrain.Scale / 2.0f, ObservedTerrain.Scale / 2.0f, ObservedTerrain.Scale / 2.0f);
		transform.localScale = new Vector3(ObservedTerrain.Scale, ObservedTerrain.Scale, ObservedTerrain.Scale);

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
		// Precache information
		var mouse = Input.mousePosition;
		var cam = Camera.main;
		var center = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, Vector3.Distance(cam.transform.position, transform.position) + BrushZCorretion));
		var alpha = 0.0f;

		// Generate/Clear complete terrain
		var gen = Input.GetKeyDown(KeyCode.G);
		var clr = Input.GetKeyDown(KeyCode.C);
		if (gen ^ clr)
		{
			if (gen) GenerateTerrain(TerrainGranularity, false);
			if (clr) GenerateTerrain(TerrainGranularity, true);
		}

		// Terrain granularity adjustment
		var incgran = Input.GetKeyDown(KeyCode.UpArrow);
		var decgran = Input.GetKeyDown(KeyCode.DownArrow);
		if (incgran ^ decgran)
		{
			if (incgran) ++TerrainGranularity;
			if (decgran) --TerrainGranularity;
			if (TerrainGranularity < 1) TerrainGranularity = 1;
			ObservedTerrain.Gridify(TerrainGranularity);
		}

		// If mouse is inside terrain bounds
		if (TerrainCollider.bounds.Contains(center))
		{
			// Display brush with some alpha
			alpha = 0.3f;

			// Adjust brush
			if (Input.GetKey(KeyCode.KeypadPlus)) BrushRadius += 0.01f;
			if (Input.GetKey(KeyCode.KeypadMinus)) BrushRadius -= 0.01f;
			if (BrushRadius <= 0.1f) BrushRadius = 0.1f;
			BrushZCorretion += Input.mouseScrollDelta.y / 10.0f;

			// Reset brush
			if (Input.GetKeyDown(KeyCode.R))
			{
				BrushRadius = 1.0f;
				BrushZCorretion = 0.0f;
			}

			// Build/Clear terrain
			var build = Input.GetMouseButton(0);
			var clear = Input.GetMouseButton(1);
			if (build ^ clear)
			{
				if (build) ObservedTerrain.Update(center, BrushRadius, +1f);
				if (clear) ObservedTerrain.Update(center, BrushRadius, -1f);
				ObservedTerrain.Calculate();
				ObservedTerrain.Triangulate();
				ObservedTerrain.GetMeshData(out var vertices, out var indices, out var normals);
				TerrainMesh.vertices = vertices;
				TerrainMesh.triangles = indices;
				TerrainMesh.normals = normals;
			}
		}

		// Apply brush adjustment
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
