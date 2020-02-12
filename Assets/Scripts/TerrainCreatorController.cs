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
		if (tag == "Terrain") return;

		Debug.Log("Awake debug enabled!");
		var granularity = 2;

		var size = granularity + 1;
		var targetVals = new float[size * size * size];

		using (var terr = new Terrain())
		{
			terr.GenerateEmpty();
			terr.SetTargets(granularity);
			terr.UpdateTerrain(new Vector3(5.0f, 5.0f, 5.0f), 10f, +25f);
			terr.Calculate();
			terr.GetTargetValues(targetVals);
		}

		System.IO.File.WriteAllLines
		(
			System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "log.txt"),
			System.Linq.Enumerable.Select(targetVals, v => v.ToString("F2"))
		);
	}

	private void GenerateTerrain(int granularity, bool empty = true)
	{
		if (granularity <= 0) throw new ArgumentException(nameof(granularity));

		if (empty) ObservedTerrain.GenerateEmpty(TerrainStep, TerrainScale);
		else ObservedTerrain.GenerateRandom(TerrainStep, TerrainScale);

		TerrainGranularity = granularity;
		ObservedTerrain.SetTargets(granularity);
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
			ObservedTerrain.SetTargets(TerrainGranularity);
		}

		// If mouse is inside terrain bounds
		if (TerrainCollider.bounds.Contains(center))
		{
			// Display brush with some alpha
			alpha = 0.3f;

			// Adjust brush
			if (Input.GetKey(KeyCode.KeypadPlus)) BrushRadius += 0.01f;
			if (Input.GetKey(KeyCode.KeypadMinus)) BrushRadius -= 0.01f;
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
				if (build) ObservedTerrain.UpdateTerrain(center, BrushRadius, +1f);
				if (clear) ObservedTerrain.UpdateTerrain(center, BrushRadius, -1f);
				ObservedTerrain.Calculate();
				ObservedTerrain.Triangulate(TerrainGranularity);
				ObservedTerrain.GetTerrainMeshData(out var vertices, out var indices, out var normals);
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
