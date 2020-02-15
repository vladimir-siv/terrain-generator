using System;
using UnityEngine;
using Terrain = TerrainGenerator.Terrain;

public class TerrainCreatorController : MonoBehaviour
{
	[SerializeField] private int TerrainGranularity = 50;
	[SerializeField] private float TerrainStep = 0.1f;
	[SerializeField] private float TerrainScale = 10.0f;
	[SerializeField] private float BrushRadius = 1.0f;
	[SerializeField] private float BrushZCorretion = 0.0f;
	[SerializeField] private float BrushDelta = 0.1f;

	private Terrain ObservedTerrain { get; set; }
	private Mesh TerrainMesh { get; set; }
	private BoxCollider TerrainCollider { get; set; }

	private GameObject Brush { get; set; }
	private Material BrushMaterial { get; set; }

	private void GenerateTerrain(int randomlayers = 0, int terrainGranularity = 0, float terrainStep = 0.0f, float terrainScale = 0.0f)
	{
		if (terrainGranularity <= 0) terrainGranularity = TerrainGranularity;
		if (terrainStep <= 0.0f) terrainStep = TerrainStep;
		if (terrainScale <= 0.0f) terrainScale = TerrainScale;

		if (randomlayers <= 0) ObservedTerrain.GenerateEmpty(terrainStep, terrainScale);
		else ObservedTerrain.GenerateRandom(terrainStep, terrainScale, randomlayers);

		TerrainGranularity = terrainGranularity;
		TerrainStep = terrainStep;
		TerrainScale = terrainScale;

		ObservedTerrain.Gridify(terrainGranularity);
		TerrainMesh.Clear();

		if (randomlayers <= 0) return;

		ObservedTerrain.Calculate();
		ObservedTerrain.Triangulate();
		ObservedTerrain.GetMeshData(out var vertices, out var indices, out var normals);
		TerrainMesh.vertices = vertices;
		TerrainMesh.normals = normals;
		TerrainMesh.triangles = indices;
	}

	private void Start()
	{
		if (TerrainGranularity <= 0) throw new ArgumentException($"Invalid '{nameof(TerrainGranularity)}'");
		if (TerrainStep <= 0.0f) throw new ArgumentException($"Invalid '{nameof(TerrainStep)}'");
		if (TerrainScale <= 0.0f) throw new ArgumentException($"Invalid '{nameof(TerrainScale)}'");

		ObservedTerrain = new Terrain();
		TerrainMesh = GetComponent<MeshFilter>().mesh;
		TerrainCollider = GetComponent<BoxCollider>();
		GetComponent<MeshRenderer>().material.SetFloat("_Scale", ObservedTerrain.Scale);
		GenerateTerrain();

		transform.position = new Vector3(ObservedTerrain.Scale / 2.0f, ObservedTerrain.Scale / 2.0f, ObservedTerrain.Scale / 2.0f);
		transform.localScale = new Vector3(ObservedTerrain.Scale, ObservedTerrain.Scale, ObservedTerrain.Scale);

		Brush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		Brush.name = "CursorSphere";
		Destroy(Brush.GetComponent<SphereCollider>());
		BrushMaterial = Brush.GetComponent<MeshRenderer>().material;
		BrushMaterial.shader = Shader.Find("Unlit/ColorBlend");
	}

	private void Update()
	{
		// Precache information
		var mouse = Input.mousePosition;
		var cam = Camera.main;
		var center = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, Vector3.Distance(cam.transform.position, transform.position) + BrushZCorretion));
		var alpha = 0.0f;

		// Clear terrain
		if (Input.GetKeyDown(KeyCode.C))
		{
			ObservedTerrain.Clear();
			ObservedTerrain.Calculate();
			TerrainMesh.Clear();
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
			ObservedTerrain.Calculate();
			ObservedTerrain.Triangulate();
			ObservedTerrain.GetMeshData(out var vertices, out var indices, out var normals);
			TerrainMesh.Clear();
			TerrainMesh.vertices = vertices;
			TerrainMesh.normals = normals;
			TerrainMesh.triangles = indices;
		}

		// If mouse is inside terrain bounds
		if (TerrainCollider.bounds.Contains(center))
		{
			// Display brush with some alpha
			alpha = 0.3f;

			// Adjust brush
			if (Input.GetKey(KeyCode.Q)) BrushRadius += 0.01f;
			if (Input.GetKey(KeyCode.E)) BrushRadius -= 0.01f;
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
				if (build) ObservedTerrain.Update(new Vector3(center.z, center.y, center.x), BrushRadius, +BrushDelta);
				if (clear) ObservedTerrain.Update(new Vector3(center.z, center.y, center.x), BrushRadius, -BrushDelta);
				ObservedTerrain.Calculate();
				ObservedTerrain.Triangulate();
				ObservedTerrain.GetMeshData(out var vertices, out var indices, out var normals);
				TerrainMesh.Clear();
				TerrainMesh.vertices = vertices;
				TerrainMesh.normals = normals;
				TerrainMesh.triangles = indices;
			}
		}

		// Apply brush adjustment
		Brush.transform.position = center;
		Brush.transform.localScale = new Vector3(BrushRadius, BrushRadius, BrushRadius);
		BrushMaterial.SetColor("_Color", new Color(0.75f, 0.75f, 0.75f, alpha));
	}

	private void OnDestroy()
	{
		ObservedTerrain.Dispose();
		ObservedTerrain = null;
	}
}
